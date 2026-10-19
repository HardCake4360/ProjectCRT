using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoSleep
{
    /// <summary>One owner for NPC grid reservations, interpolation and operation cancellation.</summary>
    public sealed class NpcMovementService : MonoBehaviour
    {
        GameSession session;
        GameState boundState;
        TileMapRuntime map;
        readonly List<NpcActor> actors = new List<NpcActor>();
        int generation;
        int epoch;
        bool saveDrain;
        public IReadOnlyList<NpcActor> Actors => actors;
        public bool HasActiveSegments => actors.Exists(actor => actor != null && actor.HasSegment);
        public bool IsAtCellBoundary => !HasActiveSegments;
        public bool IsSaveDrainRequested => saveDrain;

        public void Configure(GameSession gameSession)
        {
            if (session != gameSession) CancelAll();
            session = gameSession;
        }

        public static bool IsPlacementActive(GameState state, MapDefinition definition, MapEventPlacement placement)
        {
            if (placement == null || placement.npc == null) return false;
            if (placement.definition == null) return true;
            EventPage page = placement.definition.ResolvePage(state, definition.mapId + "/" + placement.id);
            return page != null && (page.visible || page.blocksMovement);
        }

        public static NpcState ResolveBindingState(GameState state, MapDefinition definition, MapEventPlacement placement,
            SurvivalSettings settings, Func<Vector2Int, bool> occupied = null)
        {
            if (state == null || definition == null || placement?.npc == null) throw new ArgumentException("An NPC binding needs state, map and NPC placement.");
            NpcDefinition npc = placement.npc;
            int elapsed = Mathf.Max(0, state.survival?.elapsedMinutes ?? 0);
            NpcState saved = state.GetNpcState(definition.mapId, placement.id);
            if (saved != null && saved.npcId != npc.npcId) throw new InvalidDataException("NPC definition identity changed: " + definition.mapId + "/" + placement.id);
            var result = saved?.Clone() ?? new NpcState { mapId = definition.mapId, placementId = placement.id, npcId = npc.npcId,
                cell = placement.cell, yaw = placement.yaw, lastEvaluatedMinutes = elapsed };
            if (saved != null && saved.persistentOverride) return result;
            if (!IsPlacementActive(state, definition, placement)) return result;
            NpcScheduleEntry schedule = npc.ResolveSchedule(state, definition.mapId + "/" + placement.id, settings);
            NpcRouteDefinition route = schedule == null ? npc.route : schedule.route;
            string scheduleId = schedule?.scheduleId ?? "";
            string routeId = route?.routeId ?? "";
            Vector2Int station = schedule?.stationCell ?? placement.cell;
            bool stationaryReturn = route == null && result.cell != station;
            bool reset = saved == null || stationaryReturn || result.scheduleId != scheduleId || result.routeId != routeId || (long)elapsed - result.lastEvaluatedMinutes > 1 || elapsed < result.lastEvaluatedMinutes;
            if (reset)
            {
                Vector2Int desired = route?.points != null && route.points.Count > 0 && route.points[0] != null ? route.points[0].cell : schedule?.stationCell ?? placement.cell;
                float yaw = route?.points != null && route.points.Count > 0 && route.points[0] != null ? route.points[0].yaw : schedule?.yaw ?? placement.yaw;
                bool blocked = occupied != null ? occupied(desired) : IsAuthoredOccupied(state, definition, placement, desired);
                // Keep the old checkpoint as well as the pose when placement fails.
                // Otherwise a save/load would disguise this failure as a completed reset.
                if (!definition.IsWalkable(desired) || blocked) return result;
                result.cell = desired; result.yaw = yaw;
                result.routeId = routeId;
                result.scheduleId = scheduleId;
                result.waypointIndex = 0;
            }
            result.lastEvaluatedMinutes = elapsed;
            return result;
        }

        static bool IsAuthoredOccupied(GameState state, MapDefinition definition, MapEventPlacement self, Vector2Int cell)
        {
            if (definition.events == null) return false;
            foreach (var other in definition.events)
            {
                if (other == null || ReferenceEquals(self, other)) continue;
                if (other.npc != null)
                {
                    if (IsPlacementActive(state, definition, other) && (state.GetNpcState(definition.mapId, other.id)?.cell ?? other.cell) == cell) return true;
                }
                else if (other.cell == cell && other.definition?.ResolvePage(state, definition.mapId + "/" + other.id)?.blocksMovement == true) return true;
            }
            return false;
        }

        public void BindMap(TileMapRuntime runtime, IReadOnlyList<NpcActor> mapActors)
        {
            CancelAll();
            map = runtime;
            boundState = session?.State;
            epoch = session?.ExecutionEpoch ?? 0;
            if (map == null || map.Map == null || boundState == null) return;
            // Save prevalidation uses this same ordered, detached resolution.
            var resolved = NpcSaveValidation.ResolveMapStates(boundState, map.Map, session.SurvivalSettings);
            if (mapActors != null) foreach (var actor in mapActors) if (actor != null) actors.Add(actor);
            foreach (var actor in actors)
            {
                var state = resolved.Find(record => record.placementId == actor.Placement.id);
                if (state == null) throw new InvalidDataException("Missing resolved NPC: " + actor.Key);
                ApplyState(actor, state);
                actor.IsActive = IsPlacementActive(boundState, map.Map, actor.Placement);
                actor.HomeCell = ResolveHomeCell(actor);
                Persist(actor);
            }
            // Tick order is stable even if authoring reorders placement rows.
            actors.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        }

        public NpcActor Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || map?.Map == null) return null;
            string full = key.Contains("/") ? key : map.Map.mapId + "/" + key;
            return actors.Find(actor => actor != null && actor.Key == full);
        }

        Vector2Int ResolveHomeCell(NpcActor actor)
        {
            if (actor.PersistentOverride) return actor.Cell;
            var schedule = actor.Definition.FindSchedule(actor.ScheduleId);
            return actor.CurrentRoute?.points != null && actor.CurrentRoute.points.Count > 0
                ? actor.CurrentRoute.points[0].cell : schedule?.stationCell ?? actor.Placement.cell;
        }

        public bool IsCellOccupied(Vector2Int cell, NpcActor exclude = null, bool forPlayer = false)
        {
            foreach (var actor in actors)
            {
                if (actor == null || actor == exclude || !IsActiveNow(actor) || (forPlayer && !actor.BlocksMovement)) continue;
                if (actor.Cell == cell || (actor.HasSegment && actor.ReservedDestination == cell)) return true;
            }
            return false;
        }

        bool IsActiveNow(NpcActor actor) => actor != null && map?.Map != null && session?.State != null &&
            IsPlacementActive(session.State, map.Map, actor.Placement);

        bool IsBlocked(Vector2Int cell, NpcActor self, bool includePlayer = true)
        {
            if (map == null || map.Map == null || !map.Map.IsWalkable(cell) || map.HasBlockingStaticEvent(cell, self?.Event) || IsCellOccupied(cell, self)) return true;
            return includePlayer && PlayerOccupies(cell);
        }

        bool PlayerOccupies(Vector2Int cell)
        {
            if (session?.Player == null || map?.Map == null) return false;
            Vector3 local = map.transform.InverseTransformPoint(session.Player.transform.position);
            float scale = Mathf.Min(Mathf.Abs(map.transform.lossyScale.x), Mathf.Abs(map.transform.lossyScale.z));
            float radius = session.Player.Radius / Mathf.Max(0.0001f, scale);
            float size = map.Map.cellSize;
            float dx = local.x - Mathf.Clamp(local.x, (cell.x - .5f) * size, (cell.x + .5f) * size);
            float dz = local.z - Mathf.Clamp(local.z, (cell.y - .5f) * size, (cell.y + .5f) * size);
            return dx * dx + dz * dz <= radius * radius + .000001f;
        }

        public void BeginSaveDrain() => saveDrain = true;
        public void EndSaveDrain() => saveDrain = false;
        public void RequestSaveBoundary() => BeginSaveDrain();
        public void ReleaseSaveBoundary() => EndSaveDrain();

        void Update() => Tick(Time.deltaTime);

        public void Tick(float delta)
        {
            if (session == null || map?.Map == null || !float.IsFinite(delta) || delta < 0) return;
            if (!ReferenceEquals(boundState, session.State) || epoch != session.ExecutionEpoch) { CancelAll(); return; }
            // A slow frame cannot replay an entire route; each actor commits at most one cell per pump.
            foreach (var actor in actors)
            {
                if (actor == null) continue;
                bool active = IsActiveNow(actor);
                actor.IsActive = active;
                if (!active)
                {
                    RollbackSegment(actor);
                    if (actor.Operation != null) FinishOperation(actor, NpcMoveResult.Cancelled);
                    actor.Event?.ObserveNpcContact(false);
                    actor.Status = "Dormant";
                    continue;
                }
                UpdateEnemyContact(actor);
                bool advance = CanAdvance(actor);
                if (actor.HasSegment)
                {
                    if (saveDrain || advance) AdvanceSegment(actor, delta);
                    continue;
                }
                if (saveDrain || !advance) continue;
                if (actor.Operation != null)
                {
                    AdvanceOperation(actor, delta);
                    continue;
                }
                EvaluateSchedule(actor);
                if (actor.NeedsSchedulePlacement)
                {
                    RetrySchedulePlacement(actor, delta);
                    continue;
                }
                if (session.IsGameplayBusy || (session.UI != null && session.UI.IsModalOpen)) continue;
                if (actor.Stopped) continue;
                if (AdvanceEnemy(actor, delta)) continue;
                AdvanceRoute(actor, delta);
            }
        }

        bool CanAdvance(NpcActor actor)
        {
            bool pausedStepOwner = actor.Operation?.Foreground == true && !string.IsNullOrEmpty(actor.Operation.OwnerExecutionId) &&
                session.Events?.CurrentContext?.ExecutionId == actor.Operation.OwnerExecutionId && session.Events.CurrentContext.IsSteppingOperation;
            if (session.State.player.health <= 0 || session.HasPendingSave || (session.Scheduler?.Paused == true && !pausedStepOwner) || session.Combat?.IsRunning == true || session.UI?.IsModalOpen == true) return false;
            return !session.IsGameplayBusy || (actor.Operation != null && actor.Operation.Foreground);
        }

        void AdvanceSegment(NpcActor actor, float delta)
        {
            // A parallel switch can change a static event page during a segment.
            // The destination reservation protects against other movers, but the
            // authored page must still be checked before committing the cell.
            if (IsBlocked(actor.ReservedDestination, actor))
            {
                RollbackSegment(actor);
                actor.Status = "Blocked";
                actor.Error = "Reserved destination became occupied during movement.";
                if (actor.Operation != null) FinishOperation(actor, NpcMoveResult.Blocked);
                else { actor.BlockedRetries++; actor.RetryRemaining = actor.Definition.retryInterval; }
                Persist(actor);
                return;
            }
            actor.SegmentProgress = Mathf.Min(1, actor.SegmentProgress + actor.Definition.speed * delta);
            actor.transform.position = Vector3.Lerp(map.CellToWorld(actor.SegmentSource), map.CellToWorld(actor.ReservedDestination), actor.SegmentProgress);
            if (actor.SegmentProgress < 1) return;
            actor.Cell = actor.ReservedDestination;
            actor.HasSegment = false;
            actor.SegmentSource = actor.Cell;
            Persist(actor);
            if (actor.Operation != null && actor.Operation.Stop) CompleteStop(actor);
            else if (actor.Operation != null && actor.Cell == actor.Operation.Destination) CompleteMove(actor);
        }

        bool StartSegment(NpcActor actor, Vector2Int destination)
        {
            if (saveDrain || actor.HasSegment || Math.Abs(destination.x - actor.Cell.x) + Math.Abs(destination.y - actor.Cell.y) != 1 || IsBlocked(destination, actor)) return false;
            actor.SegmentSource = actor.Cell;
            actor.ReservedDestination = destination;
            actor.HasSegment = true;
            actor.SegmentProgress = 0;
            Vector2Int direction = destination - actor.Cell;
            SetYaw(actor, Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg);
            actor.Status = actor.Operation == null ? "Moving" : "Explicit move";
            actor.Error = "";
            actor.BlockedRetries = 0;
            actor.RetryRemaining = 0;
            return true;
        }

        bool TryStepToward(NpcActor actor, Vector2Int goal, float delta)
        {
            if (actor.Cell == goal) { actor.BlockedRetries = 0; return true; }
            actor.RetryRemaining = Mathf.Max(0, actor.RetryRemaining - delta);
            if (actor.RetryRemaining > 0) return false;
            if (NpcPathfinding.TryFindPath(map.Map, actor.Cell, goal, cell => IsBlocked(cell, actor), actor.Definition.pathNodeBudget, out var path) && path.Count > 0 && StartSegment(actor, path[0])) return true;
            actor.BlockedRetries++;
            actor.RetryRemaining = actor.Definition.retryInterval;
            actor.Status = "Waiting for a free cell";
            if (actor.BlockedRetries > actor.Definition.maxBlockedRetries)
            {
                actor.Status = "Blocked";
                actor.Error = "No free route to " + goal + ". Retry budget exhausted.";
                if (actor.Operation != null) FinishOperation(actor, NpcMoveResult.Blocked);
                else actor.Stopped = true;
            }
            return false;
        }

        void AdvanceRoute(NpcActor actor, float delta)
        {
            var route = actor.CurrentRoute;
            if (route?.points == null || route.points.Count == 0 || actor.RouteFinished) { actor.Status = "Stationary"; return; }
            actor.WaypointIndex = Mathf.Clamp(actor.WaypointIndex, 0, route.points.Count - 1);
            var point = route.points[actor.WaypointIndex];
            if (point == null) { actor.Stopped = true; actor.Error = "Missing route point."; return; }
            if (actor.Cell != point.cell) { TryStepToward(actor, point.cell, delta); return; }
            SetYaw(actor, point.yaw);
            if (actor.WaitRemaining < 0) actor.WaitRemaining = point.waitSeconds;
            actor.WaitRemaining = Mathf.Max(0, actor.WaitRemaining - delta);
            actor.Status = "Route wait";
            if (actor.WaitRemaining > 0) return;
            if (actor.WaypointIndex + 1 < route.points.Count) actor.WaypointIndex++;
            else if (route.repeat) actor.WaypointIndex = 0;
            else actor.RouteFinished = true;
            actor.WaitRemaining = -1;
            Persist(actor);
        }

        void EvaluateSchedule(NpcActor actor)
        {
            if (actor.PersistentOverride) return;
            int elapsed = Mathf.Max(0, session.State.survival?.elapsedMinutes ?? 0);
            var selected = actor.Definition.ResolveSchedule(session.State, actor.Key, session.SurvivalSettings);
            var route = selected == null ? actor.Definition.route : selected.route;
            string id = selected?.scheduleId ?? "";
            // An ordinary stationary NPC returns to its automatic station after a
            // temporary move. Enemies use pursuit/return below while the map is live.
            bool stationaryReturn = actor.Definition.enemy == null && !actor.Stopped && route == null && actor.Cell != (selected?.stationCell ?? actor.Placement.cell);
            bool change = stationaryReturn || id != actor.ScheduleId || (route?.routeId ?? "") != actor.RouteId || (long)elapsed - actor.LastEvaluatedMinutes > 1 || elapsed < actor.LastEvaluatedMinutes;
            if (actor.NeedsSchedulePlacement && actor.PendingScheduleId == id && actor.PendingRoute == route) return;
            if (!change) { actor.LastEvaluatedMinutes = elapsed; return; }
            actor.PendingRoute = route;
            actor.PendingScheduleId = id;
            actor.PendingYaw = route?.points != null && route.points.Count > 0 ? route.points[0].yaw : selected?.yaw ?? actor.Placement.yaw;
            actor.Stopped = false;
            actor.BlockedRetries = 0;
            actor.RetryRemaining = 0;
            actor.Pursuing = actor.Returning = false;
            actor.ScheduleDestination = route?.points != null && route.points.Count > 0 ? route.points[0].cell : selected?.stationCell ?? actor.Placement.cell;
            actor.NeedsSchedulePlacement = true;
            if (actor.Cell == actor.ScheduleDestination) CommitSchedulePlacement(actor);
        }

        void RetrySchedulePlacement(NpcActor actor, float delta)
        {
            if (actor.Stopped) return;
            actor.RetryRemaining = Mathf.Max(0, actor.RetryRemaining - delta);
            if (actor.RetryRemaining > 0) return;
            if (!IsBlocked(actor.ScheduleDestination, actor))
            {
                CommitSchedulePlacement(actor);
            }
            else
            {
                actor.BlockedRetries++;
                actor.RetryRemaining = actor.Definition.retryInterval;
                actor.Status = "Schedule cell occupied";
                if (actor.BlockedRetries > actor.Definition.maxBlockedRetries)
                {
                    actor.Stopped = true;
                    actor.Status = "Blocked";
                    actor.Error = "Schedule start is occupied or invalid; position preserved.";
                }
            }
        }

        void CommitSchedulePlacement(NpcActor actor)
        {
            actor.Cell = actor.SegmentSource = actor.ReservedDestination = actor.ScheduleDestination;
            actor.transform.position = map.CellToWorld(actor.Cell);
            actor.CurrentRoute = actor.PendingRoute;
            actor.ScheduleId = actor.PendingScheduleId;
            actor.WaypointIndex = 0;
            actor.RouteFinished = false;
            actor.WaitRemaining = -1;
            actor.LastEvaluatedMinutes = Mathf.Max(0, session.State.survival?.elapsedMinutes ?? 0);
            SetYaw(actor, actor.PendingYaw);
            actor.HomeCell = actor.Cell;
            actor.NeedsSchedulePlacement = false;
            actor.BlockedRetries = 0;
            actor.Error = "";
            actor.Status = "Schedule selected";
            Persist(actor);
        }

        void UpdateEnemyContact(NpcActor actor)
        {
            bool near = actor.Definition.enemy != null && session.Player != null && actor.Event != null && actor.IsActive &&
                FlatDistance(actor.transform.position, session.Player.transform.position) <= actor.Definition.contactRadius * map.Map.cellSize;
            actor.Event?.ObserveNpcContact(near);
        }

        bool AdvanceEnemy(NpcActor actor, float delta)
        {
            if (actor.Definition.enemy == null || !actor.Definition.approachPlayer || session.Player == null || actor.PersistentOverride) return false;
            var playerCell = map.WorldToCell(session.Player.transform.position);
            float distance = Vector2Int.Distance(actor.Cell, playerCell);
            bool inLeash = Vector2Int.Distance(actor.HomeCell, playerCell) <= actor.Definition.leashRadius;
            bool detected = inLeash && distance <= actor.Definition.detectionRadius && (!actor.Definition.requireLineOfSight || HasLineOfSight(actor.Cell, playerCell, actor));
            if (detected)
            {
                actor.Pursuing = true;
                actor.Returning = false;
                actor.Status = "Approaching player";
                if (distance <= actor.Definition.contactRadius) return true;
                List<Vector2Int> shortest = null;
                foreach (var direction in NpcPathfinding.Directions)
                {
                    var goal = playerCell + direction;
                    if (Vector2Int.Distance(actor.HomeCell, goal) > actor.Definition.leashRadius || IsBlocked(goal, actor)) continue;
                    if (NpcPathfinding.TryFindPath(map.Map, actor.Cell, goal, cell => IsBlocked(cell, actor) || Vector2Int.Distance(actor.HomeCell, cell) > actor.Definition.leashRadius,
                        actor.Definition.pathNodeBudget, out var path) && (shortest == null || path.Count < shortest.Count)) shortest = path;
                }
                if (shortest != null && shortest.Count > 0) StartSegment(actor, shortest[0]);
                else if (shortest == null) TryStepToward(actor, playerCell, delta); // bounded failure without entering the player's cell
                return true;
            }
            if (actor.Pursuing) { actor.Pursuing = false; actor.Returning = true; actor.BlockedRetries = 0; }
            if (!actor.Returning) return false;
            if (actor.Cell == actor.HomeCell) { actor.Returning = false; actor.BlockedRetries = 0; return false; }
            actor.Status = "Returning to patrol";
            TryStepToward(actor, actor.HomeCell, delta);
            return true;
        }

        public bool HasLineOfSight(Vector2Int from, Vector2Int to, NpcActor self = null)
        {
            if (map?.Map == null || !map.Map.IsInBounds(from) || !map.Map.IsInBounds(to)) return false;
            // Supercover grid ray: diagonals cannot see through two touching wall corners.
            int dx = to.x - from.x, dy = to.y - from.y;
            int nx = Math.Abs(dx), ny = Math.Abs(dy), sx = Math.Sign(dx), sy = Math.Sign(dy);
            int ix = 0, iy = 0;
            var cell = from;
            while (ix < nx || iy < ny)
            {
                long decision = (long)(1 + 2 * ix) * ny - (long)(1 + 2 * iy) * nx;
                if (decision == 0)
                {
                    if (SightBlocked(cell + new Vector2Int(sx, 0), self, to) || SightBlocked(cell + new Vector2Int(0, sy), self, to)) return false;
                    cell += new Vector2Int(sx, sy); ix++; iy++;
                }
                else if (decision < 0) { cell.x += sx; ix++; }
                else { cell.y += sy; iy++; }
                if (SightBlocked(cell, self, to)) return false;
            }
            return true;
        }

        bool SightBlocked(Vector2Int cell, NpcActor self, Vector2Int target) => cell != target &&
            (!map.Map.IsWalkable(cell) || map.HasBlockingStaticEvent(cell, self?.Event));

        static float FlatDistance(Vector3 left, Vector3 right) { left.y = right.y = 0; return Vector3.Distance(left, right); }

        public IEnumerator MoveTo(string key, Vector2Int destination, bool persistent, Action<NpcMoveResult> callback = null, bool foreground = true)
        {
            NpcActor actor = Find(key);
            if (actor == null || map?.Map == null || !map.Map.IsWalkable(destination) || !IsActiveNow(actor)) { callback?.Invoke(NpcMoveResult.Invalid); yield break; }
            if (actor.Operation != null || saveDrain) { callback?.Invoke(NpcMoveResult.Busy); yield break; }
            var operation = BeginOperation(actor, destination, persistent, foreground, false);
            try
            {
                while (!operation.Complete && operation.Generation == generation && actor != null) yield return null;
                if (!operation.Complete) { operation.Complete = true; operation.Result = NpcMoveResult.Cancelled; }
                callback?.Invoke(operation.Result);
            }
            finally
            {
                if (!operation.Complete) CancelOperation(actor, operation);
            }
        }

        public IEnumerator StopAtBoundary(string key, bool persistent, Action<NpcMoveResult> callback = null)
        {
            NpcActor actor = Find(key);
            if (actor == null || !IsActiveNow(actor)) { callback?.Invoke(NpcMoveResult.Invalid); yield break; }
            if (actor.Operation != null || saveDrain) { callback?.Invoke(NpcMoveResult.Busy); yield break; }
            var operation = BeginOperation(actor, actor.HasSegment ? actor.ReservedDestination : actor.Cell, persistent, true, true);
            try
            {
                while (!operation.Complete && operation.Generation == generation && actor != null) yield return null;
                if (!operation.Complete) { operation.Complete = true; operation.Result = NpcMoveResult.Cancelled; }
                callback?.Invoke(operation.Result);
            }
            finally { if (!operation.Complete) CancelOperation(actor, operation); }
        }

        public IEnumerator FaceAtBoundary(string key, float yaw, bool persistent, Action<NpcMoveResult> callback = null)
        {
            var actor = Find(key);
            if (actor == null || !float.IsFinite(yaw)) { callback?.Invoke(NpcMoveResult.Invalid); yield break; }
            bool previousStopped = actor.Stopped;
            int operationGeneration = generation;
            bool succeeded = false;
            var result = NpcMoveResult.Cancelled;
            try
            {
                yield return StopAtBoundary(key, false, value => result = value);
                if (result != NpcMoveResult.Succeeded) { callback?.Invoke(result); yield break; }
                succeeded = TryFace(key, yaw, persistent);
                callback?.Invoke(succeeded ? NpcMoveResult.Succeeded : NpcMoveResult.Invalid);
            }
            finally
            {
                if (actor != null && operationGeneration == generation && (!persistent || !succeeded)) actor.Stopped = previousStopped;
            }
        }

        NpcOperation BeginOperation(NpcActor actor, Vector2Int destination, bool persistent, bool foreground, bool stop)
        {
            var operation = new NpcOperation { Generation = generation, Destination = destination, Persistent = persistent, Foreground = foreground,
                Stop = stop, OwnerExecutionId = foreground ? session.Events?.CurrentContext?.ExecutionId : null,
                PreviousRoute = actor.CurrentRoute, PreviousWaypoint = actor.WaypointIndex, PreviousStopped = actor.Stopped };
            actor.Operation = operation;
            actor.BlockedRetries = 0;
            actor.RetryRemaining = 0;
            actor.NeedsSchedulePlacement = false;
            actor.Error = "";
            actor.Status = stop ? "Stopping at cell boundary" : "Explicit move";
            return operation;
        }

        void AdvanceOperation(NpcActor actor, float delta)
        {
            if (actor.Operation.Stop) { CompleteStop(actor); return; }
            if (actor.Cell == actor.Operation.Destination) { CompleteMove(actor); return; }
            TryStepToward(actor, actor.Operation.Destination, delta);
        }

        void CompleteMove(NpcActor actor)
        {
            var operation = actor.Operation;
            if (operation.Persistent) SetStationaryOverride(actor);
            else RestorePreviousRoute(actor, operation);
            Persist(actor);
            FinishOperation(actor, NpcMoveResult.Succeeded);
        }

        void CompleteStop(NpcActor actor)
        {
            if (actor.Operation.Persistent) SetStationaryOverride(actor);
            else actor.Stopped = true;
            Persist(actor);
            FinishOperation(actor, NpcMoveResult.Succeeded);
        }

        void FinishOperation(NpcActor actor, NpcMoveResult result)
        {
            var operation = actor.Operation;
            if (operation == null) return;
            operation.Complete = true;
            operation.Result = result;
            if (result != NpcMoveResult.Succeeded) RestorePreviousRoute(actor, operation);
            actor.Operation = null;
        }

        void CancelOperation(NpcActor actor, NpcOperation operation)
        {
            operation.Complete = true;
            operation.Result = NpcMoveResult.Cancelled;
            if (actor == null || actor.Operation != operation) return;
            RollbackSegment(actor);
            RestorePreviousRoute(actor, operation);
            actor.Operation = null;
            actor.Status = "Cancelled";
            Persist(actor);
        }

        static void RestorePreviousRoute(NpcActor actor, NpcOperation operation)
        {
            actor.CurrentRoute = operation.PreviousRoute;
            actor.WaypointIndex = operation.PreviousWaypoint;
            actor.Stopped = operation.PreviousStopped;
            actor.BlockedRetries = 0;
            actor.RetryRemaining = 0;
        }

        public bool TryFace(string key, float yaw, bool persistent)
        {
            var actor = Find(key);
            if (actor == null || !float.IsFinite(yaw) || actor.HasSegment || actor.Operation != null || saveDrain || !IsActiveNow(actor)) return false;
            SetYaw(actor, yaw);
            if (persistent) SetStationaryOverride(actor);
            Persist(actor);
            return true;
        }

        public bool Resume(string key)
        {
            var actor = Find(key);
            if (actor == null || actor.Operation != null || saveDrain || !IsActiveNow(actor)) return false;
            RollbackSegment(actor);
            actor.PersistentOverride = false;
            actor.Stopped = false;
            actor.RouteFinished = false;
            actor.BlockedRetries = 0;
            actor.NeedsSchedulePlacement = false;
            actor.Error = "";
            var schedule = actor.Definition.ResolveSchedule(session.State, actor.Key, session.SurvivalSettings);
            var route = schedule == null ? actor.Definition.route : schedule.route;
            string scheduleId = schedule?.scheduleId ?? "";
            if (route == null && actor.Cell != (schedule?.stationCell ?? actor.Placement.cell))
            {
                // Commit the selected checkpoint only when the station is reached.
                actor.NeedsSchedulePlacement = true;
                actor.PendingScheduleId = scheduleId;
                actor.PendingRoute = null;
                actor.PendingYaw = schedule?.yaw ?? actor.Placement.yaw;
                actor.ScheduleDestination = schedule?.stationCell ?? actor.Placement.cell;
            }
            else
            {
                actor.CurrentRoute = route;
                actor.ScheduleId = scheduleId;
                actor.LastEvaluatedMinutes = Mathf.Max(0, session.State.survival?.elapsedMinutes ?? 0);
                actor.WaypointIndex = 0;
                actor.WaitRemaining = -1;
                actor.HomeCell = ResolveHomeCell(actor);
            }
            Persist(actor);
            return true;
        }

        public bool SetRoute(string key, NpcRouteDefinition route, bool persistent = true)
        {
            var actor = Find(key);
            if (actor == null || route == null || actor.Definition.FindRoute(route.routeId) != route || actor.HasSegment || actor.Operation != null || saveDrain) return false;
            actor.CurrentRoute = route; actor.WaypointIndex = 0; actor.WaitRemaining = -1;
            actor.PersistentOverride = persistent; actor.RouteFinished = false; actor.Stopped = false;
            actor.BlockedRetries = 0; actor.NeedsSchedulePlacement = false;
            Persist(actor);
            return true;
        }

        public bool ClearOverride(string key) => Resume(key);

        static void SetStationaryOverride(NpcActor actor)
        {
            actor.PersistentOverride = true;
            actor.NeedsSchedulePlacement = false;
            actor.CurrentRoute = null;
            actor.WaypointIndex = 0;
            actor.Stopped = true;
            actor.RouteFinished = false;
            actor.Pursuing = actor.Returning = false;
        }

        void SetYaw(NpcActor actor, float yaw)
        {
            actor.Yaw = Mathf.Repeat(yaw, 360);
            actor.transform.rotation = map.transform.rotation * Quaternion.Euler(0, actor.Yaw, 0);
        }

        void ApplyState(NpcActor actor, NpcState state)
        {
            actor.Cell = actor.SegmentSource = actor.ReservedDestination = state.cell;
            actor.HasSegment = false;
            actor.SegmentProgress = 0;
            actor.NeedsSchedulePlacement = false;
            actor.BlockedRetries = 0;
            actor.RetryRemaining = 0;
            actor.WaypointIndex = state.waypointIndex;
            actor.CurrentRoute = actor.Definition.FindRoute(state.routeId);
            actor.PersistentOverride = state.persistentOverride;
            actor.ScheduleId = state.scheduleId ?? "";
            actor.LastEvaluatedMinutes = state.lastEvaluatedMinutes;
            actor.Stopped = state.persistentOverride && actor.CurrentRoute == null;
            actor.RouteFinished = actor.CurrentRoute != null && !actor.CurrentRoute.repeat && actor.WaypointIndex == actor.CurrentRoute.points.Count - 1 && actor.Cell == actor.CurrentRoute.points[actor.WaypointIndex].cell;
            actor.WaitRemaining = -1;
            actor.transform.position = map.CellToWorld(actor.Cell);
            SetYaw(actor, state.yaw);
        }

        NpcState ReadState(NpcActor actor) => new NpcState { mapId = map.Map.mapId, placementId = actor.Placement.id,
            npcId = actor.Definition.npcId, cell = actor.Cell, yaw = actor.Yaw, routeId = actor.RouteId, waypointIndex = actor.WaypointIndex,
            persistentOverride = actor.PersistentOverride, scheduleId = actor.ScheduleId, lastEvaluatedMinutes = actor.LastEvaluatedMinutes };

        void Persist(NpcActor actor)
        {
            if (map?.Map != null && session?.State != null && ReferenceEquals(boundState, session.State)) session.State.SetNpcState(ReadState(actor), false);
        }

        public void SnapshotInto(GameState detached)
        {
            if (detached == null) throw new ArgumentNullException(nameof(detached));
            if (ReferenceEquals(detached, session?.State)) throw new ArgumentException("NPC save snapshot must target a detached candidate.");
            if (map?.Map == null) return;
            foreach (var actor in actors) if (actor != null) detached.SetNpcState(ReadState(actor), false);
        }

        void RollbackSegment(NpcActor actor)
        {
            if (!actor.HasSegment) return;
            actor.HasSegment = false;
            actor.ReservedDestination = actor.SegmentSource = actor.Cell;
            actor.SegmentProgress = 0;
            if (map != null) actor.transform.position = map.CellToWorld(actor.Cell);
        }

        public void OnMapChanging() => CancelAll();

        public void CancelAll()
        {
            generation++;
            foreach (var actor in actors)
            {
                if (actor == null) continue;
                RollbackSegment(actor);
                if (actor.Operation != null) FinishOperation(actor, NpcMoveResult.Cancelled);
                actor.Event?.ObserveNpcContact(false);
                Persist(actor);
            }
            actors.Clear();
            map = null;
            boundState = null;
            saveDrain = false;
        }

        sealed class ActorSnapshot
        {
            internal string Key;
            internal NpcState State;
            internal bool HasSegment;
            internal Vector2Int Source, Destination, Home, ScheduleDestination;
            internal float Progress, Wait, Retry;
            internal int Retries;
            internal bool Stopped, Finished, PendingSchedule, Pursuing, Returning;
            internal string Status, Error;
            internal string PendingScheduleId;
            internal NpcRouteDefinition PendingRoute;
            internal float PendingYaw;
        }

        sealed class RuntimeSnapshot
        {
            internal string MapId;
            internal bool Drain;
            internal List<ActorSnapshot> Actors = new List<ActorSnapshot>();
            internal List<NpcState> SavedStates = new List<NpcState>();
        }

        public object CaptureRuntime()
        {
            var snapshot = new RuntimeSnapshot { MapId = map?.Map?.mapId, Drain = saveDrain };
            if (session?.State?.npcStates != null) foreach (var state in session.State.npcStates) snapshot.SavedStates.Add(state?.Clone());
            if (map?.Map != null) foreach (var actor in actors)
                if (actor != null) snapshot.Actors.Add(new ActorSnapshot { Key = actor.Key, State = ReadState(actor), HasSegment = actor.HasSegment,
                    Source = actor.SegmentSource, Destination = actor.ReservedDestination, Progress = actor.SegmentProgress,
                    Wait = actor.WaitRemaining, Retry = actor.RetryRemaining, Retries = actor.BlockedRetries,
                    Stopped = actor.Stopped, Finished = actor.RouteFinished, PendingSchedule = actor.NeedsSchedulePlacement,
                    ScheduleDestination = actor.ScheduleDestination, Home = actor.HomeCell, Pursuing = actor.Pursuing,
                    Returning = actor.Returning, Status = actor.Status, Error = actor.Error, PendingScheduleId = actor.PendingScheduleId,
                    PendingRoute = actor.PendingRoute, PendingYaw = actor.PendingYaw });
            return snapshot;
        }

        public void RestoreRuntime(object saved)
        {
            if (!(saved is RuntimeSnapshot snapshot) || map?.Map == null || map.Map.mapId != snapshot.MapId) return;
            boundState = session.State;
            epoch = session.ExecutionEpoch;
            session.State.npcStates = snapshot.SavedStates.ConvertAll(record => record?.Clone());
            saveDrain = snapshot.Drain;
            foreach (var entry in snapshot.Actors)
            {
                var actor = Find(entry.Key);
                if (actor == null) continue;
                ApplyState(actor, entry.State);
                actor.HasSegment = entry.HasSegment;
                actor.SegmentSource = entry.Source; actor.ReservedDestination = entry.Destination; actor.SegmentProgress = entry.Progress;
                actor.WaitRemaining = entry.Wait; actor.RetryRemaining = entry.Retry; actor.BlockedRetries = entry.Retries;
                actor.Stopped = entry.Stopped; actor.RouteFinished = entry.Finished; actor.NeedsSchedulePlacement = entry.PendingSchedule;
                actor.ScheduleDestination = entry.ScheduleDestination; actor.HomeCell = entry.Home;
                actor.PendingScheduleId = entry.PendingScheduleId; actor.PendingRoute = entry.PendingRoute; actor.PendingYaw = entry.PendingYaw;
                actor.Pursuing = entry.Pursuing; actor.Returning = entry.Returning; actor.Status = entry.Status; actor.Error = entry.Error;
                actor.IsActive = IsActiveNow(actor);
                if (actor.HasSegment) actor.transform.position = Vector3.Lerp(map.CellToWorld(entry.Source), map.CellToWorld(entry.Destination), entry.Progress);
            }
        }

        void OnDisable() => CancelAll();
    }
}
