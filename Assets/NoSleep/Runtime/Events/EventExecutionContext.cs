using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    // A context owns every iterator and local value. The scheduler never shares a
    // coroutine stack between a foreground conversation and a parallel page.
    public sealed class EventExecutionContext : IDisposable
    {
        readonly GameSession session;
        readonly IEventPresentation presentation;
        readonly CompiledEventProgram root;
        readonly int instructionBudget;
        readonly bool runOnce;
        readonly string completionKey;
        readonly Stack<IEnumerator> iterators = new Stack<IEnumerator>();
        readonly List<string> callStack = new List<string>();
        readonly List<string> trace = new List<string>();
        readonly List<string> diagnosticStack = new List<string>();
        readonly IReadOnlyList<string> callStackView;
        readonly IReadOnlyList<string> traceView;
        bool pumping, paused, operationActive, steppingOperation, incomplete;
        int stepTokens;
        readonly int startInstruction;

        public string ExecutionId { get; } = Guid.NewGuid().ToString("N");
        public GameState State { get; }
        public EventDefinition Definition { get; }
        public string EventKey { get; }
        public string PageId { get; }
        public string Scope { get; }
        public int PageIndex { get; }
        public int ExecutionEpoch { get; }
        public bool IsBackground { get; }
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool Succeeded { get; private set; }
        public Exception Error { get; private set; }
        public int MaxInstructionsPerPump { get; set; } = 128;
        public int InstructionsLastPump { get; private set; }
        public int ExecutedInstructions { get; private set; }
        public string ProgramId { get; private set; }
        public string CommandId { get; private set; }
        public int CommandIndex { get; private set; } = -1;
        public string WaitReason { get; private set; } = "";
        public CommonEventDefinition CurrentCommonEvent { get; private set; }
        public IReadOnlyList<string> CallStack => callStackView;
        public IReadOnlyList<string> Trace => traceView;
        public bool Paused { get => paused; set { paused = value; if (!value) { stepTokens = 0; steppingOperation = false; } } }
        public bool IsSteppingOperation => paused && steppingOperation && operationActive && !IsComplete;

        internal EventExecutionContext(GameSession gameSession, IEventPresentation eventPresentation,
            EventDefinition definition, string eventKey, int pageIndex, bool background,
            CompiledEventProgram program, int budget)
        {
            session = gameSession; presentation = eventPresentation;
            State = session.State; ExecutionEpoch = session.ExecutionEpoch;
            Definition = definition; EventKey = eventKey.Trim(); PageIndex = pageIndex;
            PageId = definition.pages[pageIndex].pageId;
            Scope = EventKey + "/" + definition.definitionId + "/" + PageId;
            IsBackground = background; root = program; instructionBudget = Math.Max(1, budget);
            ProgramId = program.id;
            runOnce = definition.pages[pageIndex].runOnce;
            completionKey = definition.StableCompletionKey(PageId);
            callStackView = callStack.AsReadOnly(); traceView = trace.AsReadOnly();
            Paused = session.Scheduler != null && session.Scheduler.Paused;
            if (background)
            {
                var checkpoint = State.GetEventCheckpoint(Scope);
                if (checkpoint != null)
                {
                    int index = program.commands.FindIndex(command => command.commandId == checkpoint.checkpointId);
                    if (index < 0 || (program.commands[index].type != EventCommandType.Checkpoint &&
                        program.commands[index].type != EventCommandType.WaitGameMinutes))
                        throw new ArgumentException("Saved checkpoint no longer exists in the root parallel page: " + checkpoint.checkpointId);
                    startInstruction = program.commands[index].type == EventCommandType.Checkpoint ? index + 1 : index;
                    Record("Resume " + checkpoint.checkpointId + " due " + checkpoint.dueElapsedMinutes);
                }
            }
            iterators.Push(RunRoot());
        }

        public void Step()
        {
            if (IsComplete) return;
            paused = true;
            // A presentation or wait already in progress belongs to its command.
            if (operationActive) steppingOperation = true;
            else stepTokens = 1;
        }

        public bool MoveNext(out object yielded)
        {
            yielded = null; InstructionsLastPump = 0;
            if (IsComplete) return false;
            if (pumping) throw new InvalidOperationException("An event context cannot be pumped recursively.");
            if (!IsCurrentSession()) { Cancel(); return false; }
            if (paused && stepTokens == 0 && !steppingOperation) { WaitReason = "Debugger paused"; return true; }
            pumping = true;
            try
            {
                while (iterators.Count > 0 && !IsComplete)
                {
                    if (!IsCurrentSession()) { Cancel(); break; }
                    if (paused && stepTokens == 0 && !steppingOperation && operationActive) { WaitReason = "Debugger paused"; return true; }
                    IEnumerator iterator = iterators.Peek();
                    bool next = iterator.MoveNext();
                    if (IsComplete) break;
                    if (!next)
                    {
                        iterators.Pop(); (iterator as IDisposable)?.Dispose();
                        continue;
                    }
                    if (iterator.Current is IEnumerator nested) { iterators.Push(nested); continue; }
                    yielded = iterator.Current;
                    return true;
                }
                if (!IsComplete) IsComplete = true;
                return false;
            }
            catch (Exception error)
            {
                Error = error; Succeeded = false; IsComplete = true;
                callStack.Clear(); callStack.AddRange(diagnosticStack);
                Record("ERROR " + error.Message);
                return false;
            }
            finally
            {
                pumping = false;
                if (IsComplete) DisposeIterators();
            }
        }

        public void Cancel()
        {
            if (IsComplete) return;
            IsCancelled = true; IsComplete = true; Succeeded = false;
            Record("Cancelled"); WaitReason = "Cancelled";
            // State callbacks may cancel the current context during MoveNext.
            // Dispose it only after that MoveNext has unwound.
            if (!pumping) DisposeIterators();
        }
        public void Dispose() { if (!IsComplete) Cancel(); if (!pumping) DisposeIterators(); }

        bool IsCurrentSession() => session != null && session.State == State && session.ExecutionEpoch == ExecutionEpoch;
        bool BeginCommand(CompiledEventProgram program, int index)
        {
            if (IsComplete) return false;
            if (paused && stepTokens == 0) { WaitReason = "Debugger paused"; return false; }
            if (InstructionsLastPump >= Math.Max(0, MaxInstructionsPerPump)) { WaitReason = "Scheduler budget"; return false; }
            if (ExecutedInstructions >= instructionBudget)
                throw new InvalidOperationException("Event exceeded its instruction budget; check for a jump loop.");
            ExecutedInstructions++;
            InstructionsLastPump++;
            if (paused) { stepTokens--; steppingOperation = true; }
            operationActive = true; WaitReason = "";
            ProgramId = program.id; CurrentCommonEvent = program.common;
            CommandIndex = index; CommandId = program.commands[index].commandId;
            diagnosticStack.Clear(); diagnosticStack.AddRange(callStack);
            Record(program.id + " [" + index + "] " + program.commands[index].type + " (" + CommandId + ")");
            return true;
        }
        void FinishCommand() { operationActive = false; steppingOperation = false; }
        void Record(string message)
        {
            const int maximum = 256;
            if (trace.Count == maximum) trace.RemoveAt(0);
            trace.Add(message);
        }
        void DisposeIterators()
        {
            while (iterators.Count > 0)
            {
                try { (iterators.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { if (Error == null) Error = error; Succeeded = false; Record("Dispose error: " + error.Message); }
            }
        }

        IEnumerator RunRoot()
        {
            yield return RunProgram(root, new Dictionary<string, EventValue>(StringComparer.Ordinal), "", startInstruction);
            if (incomplete || IsCancelled || !IsCurrentSession()) yield break;
            // Set the outcome before notifications: a completed page may unregister
            // itself from the scheduler in a Changed callback.
            Succeeded = true; IsComplete = true; WaitReason = "Completed";
            if (runOnce) State.SetSelfSwitch(EventKey, completionKey, true);
            if (IsBackground) State.ClearEventCheckpoint(Scope);
            Record("Completed");
        }

        IEnumerator RunProgram(CompiledEventProgram program, Dictionary<string, EventValue> locals, string callPath, int start = 0)
        {
            if (callStack.Count >= 16) throw new InvalidOperationException("Common event call depth exceeds 16.");
            callStack.Add(program.id + (string.IsNullOrEmpty(callPath) ? "" : " @ " + callPath));
            try
            {
                for (int index = start; index < program.commands.Count; index++)
                {
                    while (!BeginCommand(program, index)) { if (IsComplete) yield break; yield return null; }
                    var command = program.commands[index];
                    string Text(string value) => EventParameters.Resolve(value, locals);
                    int Number() => string.IsNullOrEmpty(command.intParameter) ? command.intValue : locals[command.intParameter].Int;
                    bool Flag() => string.IsNullOrEmpty(command.boolParameter) ? command.boolValue : locals[command.boolParameter].Bool;
                    try
                    {
                        switch (command.type)
                        {
                            case EventCommandType.ShowDialogue:
                                WaitReason = "Dialogue";
                                yield return presentation.ShowDialogue(Text(command.speaker), Text(command.text)) ?? throw new InvalidOperationException("Dialogue presentation returned no iterator.");
                                break;
                            case EventCommandType.ShowChoices:
                            {
                                var choices = new List<string>(); foreach (var choice in command.choices) choices.Add(Text(choice.text));
                                int selected = -1; WaitReason = "Choices";
                                yield return presentation.ShowChoices(Text(command.text), choices, value => selected = value) ?? throw new InvalidOperationException("Choice presentation returned no iterator.");
                                if (selected < 0 || selected >= command.choices.Count) throw new InvalidOperationException("Choice presentation finished without a valid selection.");
                                string target = command.choices[selected].targetLabel;
                                if (!string.IsNullOrWhiteSpace(target)) index = program.labels[target.Trim()];
                                break;
                            }
                            case EventCommandType.SetSwitch: State.SetSwitch(Text(command.key), Flag()); break;
                            case EventCommandType.SetVariable: State.SetVariable(Text(command.key), Number()); break;
                            case EventCommandType.AddVariable: State.AddVariable(Text(command.key), Number()); break;
                            case EventCommandType.SetSelfSwitch: State.SetSelfSwitch(EventKey, SelfKey(Text(command.key)), Flag()); break;
                            case EventCommandType.AddItem: State.AddItem(Text(command.key), Number()); break;
                            case EventCommandType.AddGold: State.AddGold(Number()); break;
                            case EventCommandType.Wait:
                            {
                                WaitReason = "Wait seconds";
                                float remaining = command.seconds;
                                do { yield return null; remaining -= Time.deltaTime; } while (remaining > 0);
                                break;
                            }
                            case EventCommandType.TransferPlayer:
                                if (!session.TransferPlayer(command.map, command.cell)) throw new InvalidOperationException("Player transfer failed; verify the destination map and cell.");
                                yield return null;
                                break;
                            case EventCommandType.MoveNpc:
                            case EventCommandType.StopNpc:
                            {
                                if (session.Npcs == null) throw new InvalidOperationException("NPC movement service is not configured.");
                                string target = string.IsNullOrWhiteSpace(command.key) ? EventKey : Text(command.key);
                                NpcMoveResult result = NpcMoveResult.Cancelled; WaitReason = "NPC movement: " + target;
                                yield return command.type == EventCommandType.MoveNpc
                                    ? session.Npcs.MoveTo(target, command.cell, Flag(), value => result = value)
                                    : session.Npcs.StopAtBoundary(target, Flag(), value => result = value);
                                Record(command.type + " " + target + ": " + result);
                                if (result != NpcMoveResult.Succeeded) throw new InvalidOperationException("NPC movement failed: " + target + ": " + result);
                                break;
                            }
                            case EventCommandType.FaceNpc:
                            {
                                if (session.Npcs == null) throw new InvalidOperationException("NPC movement service is not configured.");
                                string target = string.IsNullOrWhiteSpace(command.key) ? EventKey : Text(command.key);
                                NpcMoveResult result = NpcMoveResult.Cancelled; WaitReason = "NPC face boundary: " + target;
                                yield return session.Npcs.FaceAtBoundary(target, Number(), Flag(), value => result = value);
                                if (result != NpcMoveResult.Succeeded)
                                    throw new InvalidOperationException("NPC face failed: " + target + ": " + result);
                                break;
                            }
                            case EventCommandType.ResumeNpc:
                                if (session.Npcs == null || !session.Npcs.Resume(string.IsNullOrWhiteSpace(command.key) ? EventKey : Text(command.key)))
                                    throw new InvalidOperationException("NPC resume failed: " + (string.IsNullOrWhiteSpace(command.key) ? EventKey : Text(command.key)));
                                break;
                            case EventCommandType.IfCondition:
                            {
                                bool match = command.useLocalCondition ? command.localCondition.Evaluate(locals) : EvaluateCondition(command.condition, locals);
                                Record("Condition " + match);
                                if (!match) index = program.labels[command.targetLabel.Trim()];
                                break;
                            }
                            case EventCommandType.Jump: index = program.labels[command.targetLabel.Trim()]; break;
                            case EventCommandType.Label: break;
                            case EventCommandType.AdvanceTime: SurvivalRules.AdvanceTime(State, Number(), session.SurvivalSettings); break;
                            case EventCommandType.SetObjective: SurvivalRules.SetObjective(State, Text(command.key), Text(command.text), Number()); break;
                            case EventCommandType.RecordClue: SurvivalRules.RecordClue(State, Text(command.key), Text(command.speaker), Text(command.text)); break;
                            case EventCommandType.JoinCompanion: SurvivalRules.JoinCompanion(State, Text(command.key)); break;
                            case EventCommandType.LeaveCompanion: SurvivalRules.LeaveCompanion(State, Text(command.key)); break;
                            case EventCommandType.ClearParty: SurvivalRules.ClearParty(State); break;
                            case EventCommandType.UnlockAction: SurvivalRules.UnlockAction(State, Text(command.key)); break;
                            case EventCommandType.UseItem:
                                if (!SurvivalRules.UseItem(State, command.item, session.SurvivalSettings))
                                { session.ShowNotice("아이템을 사용할 수 없습니다. 소지품과 체력을 확인하세요."); incomplete = true; yield break; }
                                break;
                            case EventCommandType.StayAwake:
                                if (!SurvivalRules.CompleteNightActivity(State, command.nightActivity, session.SurvivalSettings))
                                { session.ShowNotice("밤을 지샐 동료와 진행 시간을 확인하세요."); incomplete = true; yield break; }
                                break;
                            case EventCommandType.ReachDestination: State.journey.reachedDestination = true; State.NotifyChanged(); break;
                            case EventCommandType.StartEncounter:
                            {
                                if (session.Combat == null) throw new InvalidOperationException("Combat runner is not configured.");
                                CombatResult result = CombatResult.Cancelled; WaitReason = "Encounter";
                                yield return session.Combat.Run(command.enemy, value => result = value);
                                if (result == CombatResult.Victory) State.SetSelfSwitch(EventKey, string.IsNullOrWhiteSpace(command.key) ? "defeated" : SelfKey(Text(command.key)), true);
                                if (result == CombatResult.Defeat) session.UI?.ShowGameOver();
                                if (result != CombatResult.Victory) { incomplete = true; yield break; }
                                break;
                            }
                            case EventCommandType.CallCommon:
                            {
                                var child = program.children[command.commonEvent];
                                var parameters = EventParameters.Bind(child.parameters, command.arguments, locals);
                                string path = (string.IsNullOrEmpty(callPath) ? "" : callPath + "/") + command.commandId + "/" + child.id;
                                // Calling is one debug instruction. The child's first
                                // instruction requires its own step token.
                                FinishCommand();
                                yield return RunProgram(child, parameters, path);
                                if (incomplete || IsCancelled) yield break;
                                break;
                            }
                            case EventCommandType.RequestForeground:
                                if (session.Scheduler == null || !session.Scheduler.EnqueueForeground(command.foregroundEvent, EventKey))
                                { Record("Foreground request rejected"); incomplete = true; yield break; }
                                Record("Foreground request queued");
                                break;
                            case EventCommandType.GrantOnce:
                            {
                                var effects = new List<StateMutation>();
                                foreach (var mutation in command.mutations) effects.Add(new StateMutation { kind = mutation.kind,
                                    key = mutation.kind == StateMutationKind.SetSelfSwitch ? SelfKey(Text(mutation.key)) : Text(mutation.key),
                                    intValue = mutation.intValue, boolValue = mutation.boolValue });
                                string transaction = Scope + "/" + (string.IsNullOrEmpty(callPath) ? "" : callPath + "/") + command.commandId;
                                Record(State.TryCommitGrant(transaction, effects, EventKey) ? "Grant committed " + transaction : "Grant already committed " + transaction);
                                break;
                            }
                            case EventCommandType.WaitGameMinutes:
                            {
                                int minutes = Number(); if (minutes < 0) throw new ArgumentException("Game-minute wait cannot be negative.");
                                var saved = State.GetEventCheckpoint(Scope);
                                int due = saved != null && saved.checkpointId == command.commandId ? saved.dueElapsedMinutes : checked(State.survival.elapsedMinutes + minutes);
                                State.SetEventCheckpoint(Scope, command.commandId, due);
                                WaitReason = "Game minutes until " + due;
                                while (State.survival.elapsedMinutes < due) yield return null;
                                break;
                            }
                            case EventCommandType.Checkpoint:
                                State.SetEventCheckpoint(Scope, command.commandId, State.survival.elapsedMinutes);
                                Record("Checkpoint " + command.commandId); break;
                            case EventCommandType.End: yield break;
                            default: throw new InvalidOperationException("Unknown event instruction.");
                        }
                    }
                    finally { FinishCommand(); }
                    if (!IsCurrentSession()) Cancel();
                    if (IsCancelled || incomplete) yield break;
                }
            }
            finally
            {
                // Retain a failed/cancelled stack for the debugger's final trace.
                if (Error == null && !IsCancelled && callStack.Count > 0) callStack.RemoveAt(callStack.Count - 1);
            }
        }

        bool EvaluateCondition(StateCondition source, Dictionary<string, EventValue> locals)
        {
            var condition = EventCompiler.CloneCondition(source);
            condition.key = EventParameters.Resolve(condition.key, locals);
            return condition.Evaluate(State, EventKey);
        }
        static string SelfKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Trim().StartsWith("__nosleep_", StringComparison.Ordinal))
                throw new ArgumentException("Self-switch key is missing or reserved.");
            return key;
        }
    }

    internal sealed class CompiledEventProgram
    {
        internal string id;
        internal CommonEventDefinition common;
        internal List<EventParameter> parameters;
        internal List<EventCommand> commands;
        internal Dictionary<string, int> labels;
        internal readonly Dictionary<CommonEventDefinition, CompiledEventProgram> children = new Dictionary<CommonEventDefinition, CompiledEventProgram>();
        internal bool needsPresentation;
    }

    public static partial class EventProgram
    {
        public static void ValidateForExecution(EventDefinition definition, int pageIndex, bool background = false)
            => EventCompiler.Compile(definition, pageIndex, background);
    }

    internal sealed class EventCompiler
    {
        readonly bool background;
        readonly Dictionary<string, CommonEventDefinition> commonIds = new Dictionary<string, CommonEventDefinition>(StringComparer.Ordinal);
        readonly HashSet<CommonEventDefinition> ancestors = new HashSet<CommonEventDefinition>();
        int visits;
        EventCompiler(bool isBackground) { background = isBackground; }

        internal static CompiledEventProgram Compile(EventDefinition definition, int pageIndex, bool background)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.EnsureStableIds();
            if (!definition.ValidateIdentity(out string error)) throw new ArgumentException(error);
            if (pageIndex < 0 || pageIndex >= definition.pages.Count) throw new ArgumentOutOfRangeException(nameof(pageIndex));
            if (background && definition.pages[pageIndex].trigger != EventTrigger.Parallel) throw new ArgumentException("Only Parallel pages can run in the background.");
            var compiler = new EventCompiler(background);
            return compiler.Build(definition.definitionId + "/" + definition.pages[pageIndex].pageId,
                null, new List<EventParameter>(), definition.pages[pageIndex].commands, 1);
        }

        CompiledEventProgram Build(string id, CommonEventDefinition common, List<EventParameter> parameters, List<EventCommand> commands, int depth)
        {
            if (depth > 16) throw new ArgumentException("Common event call depth exceeds 16.");
            if (++visits > 1024) throw new ArgumentException("Common event call graph exceeds its validation budget.");
            var program = new CompiledEventProgram { id = id, common = common, parameters = new List<EventParameter>(), commands = new List<EventCommand>() };
            var types = new Dictionary<string, EventParameterType>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                program.parameters.Add(new EventParameter { name = parameter.name, type = parameter.type, intValue = parameter.intValue, boolValue = parameter.boolValue, stringValue = parameter.stringValue });
                types.Add(parameter.name, parameter.type);
            }
            foreach (var command in commands) program.commands.Add(CloneCommand(command));
            program.labels = EventProgram.ValidateAndIndex(program.commands);
            foreach (var command in program.commands)
            {
                if (background && !BackgroundAllowed(command.type)) throw new ArgumentException("Parallel/common background command is not safe: " + command.type);
                if ((command.type == EventCommandType.WaitGameMinutes || command.type == EventCommandType.Checkpoint) && (!background || common != null))
                    throw new ArgumentException("Durable game-minute waits and checkpoints are supported only on a root Parallel page.");
                ValidateParameter(command.intParameter, EventParameterType.Int, types);
                ValidateParameter(command.boolParameter, EventParameterType.Bool, types);
                ValidateText(command.speaker, types); ValidateText(command.text, types); ValidateText(command.key, types);
                if (command.condition != null) ValidateText(command.condition.key, types);
                if (command.choices != null) foreach (var choice in command.choices) if (choice != null) ValidateText(choice.text, types);
                if (command.type == EventCommandType.IfCondition && command.useLocalCondition)
                {
                    if (command.localCondition == null) throw new ArgumentException("Local condition is missing.");
                    RequireParameter(command.localCondition.parameter, command.localCondition.type, types);
                    if (!Enum.IsDefined(typeof(NumberComparison), command.localCondition.comparison)) throw new ArgumentException("Local comparison is invalid.");
                }
                if (command.type == EventCommandType.GrantOnce)
                    foreach (var mutation in command.mutations)
                    {
                        if (mutation == null || !Enum.IsDefined(typeof(StateMutationKind), mutation.kind)) throw new ArgumentException("Grant effect is invalid.");
                        if (mutation.kind != StateMutationKind.AddGold && string.IsNullOrWhiteSpace(mutation.key)) throw new ArgumentException("Grant effect key is missing.");
                        if (mutation.kind == StateMutationKind.SetSelfSwitch && mutation.key.Trim().StartsWith("__nosleep_", StringComparison.Ordinal)) throw new ArgumentException("Grant self-switch key is reserved.");
                        ValidateText(mutation.key, types);
                    }
                if (command.type == EventCommandType.ShowDialogue || command.type == EventCommandType.ShowChoices) program.needsPresentation = true;
                if (command.type != EventCommandType.CallCommon) continue;
                var child = command.commonEvent; child.EnsureStableIds();
                if (!child.ValidateIdentity(out string error)) throw new ArgumentException(error);
                if (commonIds.TryGetValue(child.commonId, out var existing) && existing != child) throw new ArgumentException("Different common events share ID: " + child.commonId);
                commonIds[child.commonId] = child;
                if (!ancestors.Add(child)) throw new ArgumentException("Recursive common event call: " + child.commonId);
                try
                {
                    var childTypes = new Dictionary<string, EventParameterType>(StringComparer.Ordinal);
                    foreach (var parameter in child.parameters) childTypes.Add(parameter.name, parameter.type);
                    var provided = new HashSet<string>(StringComparer.Ordinal);
                    if (command.arguments != null) foreach (var argument in command.arguments)
                    {
                        if (argument == null || !provided.Add(argument.name ?? "") || !childTypes.TryGetValue(argument.name ?? "", out var type)) throw new ArgumentException("Unknown or duplicate common argument.");
                        if (!string.IsNullOrEmpty(argument.fromParameter)) RequireParameter(argument.fromParameter, type, types);
                        else
                        {
                            if (argument.type != type) throw new ArgumentException("Common argument type mismatch: " + argument.name);
                            if (type == EventParameterType.String) ValidateText(argument.stringValue, types);
                        }
                    }
                    var compiled = Build(child.commonId, child, child.parameters, child.commands, depth + 1);
                    program.children[child] = compiled;
                    if (compiled.needsPresentation) program.needsPresentation = true;
                }
                finally { ancestors.Remove(child); }
            }
            return program;
        }

        static bool BackgroundAllowed(EventCommandType type)
        {
            switch (type)
            {
                case EventCommandType.SetSwitch: case EventCommandType.SetVariable: case EventCommandType.SetSelfSwitch:
                case EventCommandType.IfCondition: case EventCommandType.Jump: case EventCommandType.Label: case EventCommandType.End:
                case EventCommandType.SetObjective: case EventCommandType.RecordClue: case EventCommandType.JoinCompanion:
                case EventCommandType.LeaveCompanion: case EventCommandType.ClearParty: case EventCommandType.UnlockAction:
                case EventCommandType.ReachDestination: case EventCommandType.CallCommon: case EventCommandType.RequestForeground:
                case EventCommandType.GrantOnce: case EventCommandType.WaitGameMinutes: case EventCommandType.Checkpoint: return true;
                default: return false;
            }
        }
        static void ValidateText(string text, Dictionary<string, EventParameterType> types)
        { foreach (string parameter in EventParameters.Referenced(text)) if (!types.ContainsKey(parameter)) throw new ArgumentException("Text references an unknown parameter: " + parameter); }
        static void ValidateParameter(string parameter, EventParameterType type, Dictionary<string, EventParameterType> types)
        { if (!string.IsNullOrEmpty(parameter)) RequireParameter(parameter, type, types); }
        static void RequireParameter(string parameter, EventParameterType type, Dictionary<string, EventParameterType> types)
        { if (!types.TryGetValue(parameter ?? "", out var actual) || actual != type) throw new ArgumentException("Parameter is missing or has another type: " + parameter); }

        internal static StateCondition CloneCondition(StateCondition value) => value == null ? null : new StateCondition
        { kind = value.kind, key = value.key, expected = value.expected, comparison = value.comparison, value = value.value };
        static EventCommand CloneCommand(EventCommand value)
        {
            if (value == null) return null;
            var copy = new EventCommand
            {
                commandId = value.commandId, type = value.type, speaker = value.speaker, text = value.text, key = value.key,
                label = value.label, targetLabel = value.targetLabel, intValue = value.intValue, boolValue = value.boolValue,
                seconds = value.seconds, condition = CloneCondition(value.condition), map = value.map, cell = value.cell,
                item = value.item, nightActivity = value.nightActivity, enemy = value.enemy, commonEvent = value.commonEvent,
                foregroundEvent = value.foregroundEvent, intParameter = value.intParameter, boolParameter = value.boolParameter,
                useLocalCondition = value.useLocalCondition,
                localCondition = value.localCondition == null ? null : new EventLocalCondition { parameter = value.localCondition.parameter,
                    type = value.localCondition.type, comparison = value.localCondition.comparison, intValue = value.localCondition.intValue,
                    boolValue = value.localCondition.boolValue, stringValue = value.localCondition.stringValue }
            };
            copy.choices = value.choices == null ? null : new List<EventChoice>();
            if (value.choices != null) foreach (var choice in value.choices) copy.choices.Add(choice == null ? null : new EventChoice { text = choice.text, targetLabel = choice.targetLabel });
            copy.arguments = value.arguments == null ? null : new List<EventArgument>();
            if (value.arguments != null) foreach (var argument in value.arguments) copy.arguments.Add(argument == null ? null : new EventArgument
            { name = argument.name, type = argument.type, intValue = argument.intValue, boolValue = argument.boolValue, stringValue = argument.stringValue, fromParameter = argument.fromParameter });
            copy.mutations = value.mutations == null ? null : new List<StateMutation>();
            if (value.mutations != null) foreach (var mutation in value.mutations) copy.mutations.Add(mutation == null ? null : new StateMutation
            { kind = mutation.kind, key = mutation.key, intValue = mutation.intValue, boolValue = mutation.boolValue });
            return copy;
        }
    }
}
