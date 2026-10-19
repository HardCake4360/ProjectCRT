using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    public sealed class CombatRunner : MonoBehaviour
    {
        GameSession session;
        IEventPresentation presentation;
        public bool IsRunning { get; private set; }

        public void Configure(GameSession gameSession, IEventPresentation eventPresentation)
        {
            if (IsRunning) throw new InvalidOperationException("Cannot configure combat during an encounter.");
            session = gameSession;
            presentation = eventPresentation;
        }

        // The event interpreter owns this iterator. Disposing its nested iterator stack on
        // cancellation runs this finally block, even while dialogue waits for input.
        public IEnumerator Run(EnemyDefinition enemy, Action<CombatResult> onCompleted)
        {
            if (IsRunning) throw new InvalidOperationException("Another encounter is already running.");
            if (session == null || session.State == null || session.SurvivalSettings == null || presentation == null)
                throw new InvalidOperationException("Combat requires a session, survival settings and presentation.");
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            IsRunning = true;
            CombatResult result = CombatResult.Cancelled;
            bool completed = false;
            try
            {
                var rules = new CombatRules(session.State, enemy, session.SurvivalSettings, session.Stimulant);
                yield return presentation.ShowDialogue(enemy.displayName, "앞을 막는 상대와 접촉했다.");
                int selections = 0;
                while (!rules.IsFinished)
                {
                    if (++selections > CombatRules.MaximumTurns * 2) { rules.Cancel(); break; }
                    var actions = rules.GetAvailableActions();
                    if (actions.Count == 0) { rules.Cancel(); break; }
                    var choices = new List<string>();
                    foreach (var action in actions) choices.Add(ActionName(action));
                    int selected = -1;
                    string prompt = $"{enemy.displayName}  체력 {rules.EnemyHealth}/{rules.EnemyMaxHealth}\n" +
                        $"{session.State.player.name}  체력 {session.State.player.health}/{session.State.player.maxHealth}";
                    yield return presentation.ShowChoices(prompt, choices, index => selected = index);
                    if (selected < 0 || selected >= actions.Count)
                        throw new InvalidOperationException("Combat choice finished without a valid selection.");
                    CombatTurnResult turn = rules.Resolve(actions[selected]);
                    yield return presentation.ShowDialogue("", turn.Message);
                }
                result = rules.Result ?? CombatResult.Cancelled;
                yield return presentation.ShowDialogue("", ResultMessage(result));
                completed = true;
            }
            finally
            {
                IsRunning = false;
                onCompleted?.Invoke(completed ? result : CombatResult.Cancelled);
            }
        }

        string ActionName(CombatAction action)
        {
            switch (action)
            {
                case CombatAction.Strike: return "공격";
                case CombatAction.Guard: return "방어";
                case CombatAction.Flee: return "물러나기";
                case CombatAction.Aggressive: return "과격한 공격 (체력 8)";
                case CombatAction.Observe: return "관찰";
                case CombatAction.UseStimulant: return session.Stimulant.displayName + " 사용";
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        static string ResultMessage(CombatResult result)
        {
            switch (result)
            {
                case CombatResult.Victory: return "이번 접촉에서 살아남았다.";
                case CombatResult.Escaped: return "상대와 거리를 벌리고 빠져나왔다.";
                case CombatResult.Defeat: return "더 버틸 체력이 남아 있지 않다.";
                default: return "접촉이 길어져 이번 전투를 중단했다.";
            }
        }
    }
}
