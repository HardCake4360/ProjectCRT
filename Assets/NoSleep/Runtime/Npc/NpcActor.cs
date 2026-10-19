using UnityEngine;

namespace NoSleep
{
    public sealed class NpcActor : MonoBehaviour
    {
        public NpcDefinition Definition { get; private set; }
        public MapEventPlacement Placement { get; private set; }
        public EventObject Event { get; private set; }
        public string Key { get; private set; }
        public Vector2Int Cell { get; internal set; }
        public Vector2Int ReservedDestination { get; internal set; }
        public bool HasSegment { get; internal set; }
        public float Yaw { get; internal set; }
        public string RouteId => CurrentRoute == null ? "" : CurrentRoute.routeId;
        public int WaypointIndex { get; internal set; }
        public string ScheduleId { get; internal set; } = "";
        public int LastEvaluatedMinutes { get; internal set; }
        public bool PersistentOverride { get; internal set; }
        public bool IsActive { get; internal set; }
        public string Status { get; internal set; } = "Stationary";
        public string Error { get; internal set; } = "";
        public bool BlocksMovement => IsActive && Definition != null && Definition.blocksMovement && (Event == null || Event.BlocksMovement);

        internal NpcRouteDefinition CurrentRoute;
        internal Vector2Int SegmentSource;
        internal float SegmentProgress;
        internal float WaitRemaining;
        internal int BlockedRetries;
        internal float RetryRemaining;
        internal bool Stopped;
        internal bool RouteFinished;
        internal bool NeedsSchedulePlacement;
        internal Vector2Int ScheduleDestination;
        internal string PendingScheduleId = "";
        internal NpcRouteDefinition PendingRoute;
        internal float PendingYaw;
        internal Vector2Int HomeCell;
        internal bool Pursuing;
        internal bool Returning;
        internal NpcOperation Operation;

        public void Configure(MapDefinition map, MapEventPlacement placement, EventObject eventObject)
        {
            Definition = placement.npc;
            Placement = placement;
            Event = eventObject;
            Key = map.mapId + "/" + placement.id;
            Cell = ReservedDestination = SegmentSource = placement.cell;
            Yaw = placement.yaw;
            HomeCell = placement.cell;
        }
    }

    internal sealed class NpcOperation
    {
        internal int Generation;
        internal Vector2Int Destination;
        internal bool Persistent;
        internal bool Foreground;
        internal string OwnerExecutionId;
        internal bool Stop;
        internal bool Complete;
        internal NpcMoveResult Result;
        internal NpcRouteDefinition PreviousRoute;
        internal int PreviousWaypoint;
        internal bool PreviousStopped;
    }
}
