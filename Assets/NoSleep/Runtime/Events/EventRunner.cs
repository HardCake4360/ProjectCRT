using System;
using System.Collections;
using UnityEngine;

namespace NoSleep
{
    // Existing interaction code uses this foreground facade. Parallel execution
    // uses independent contexts owned by EventScheduler.
    public sealed class EventRunner : MonoBehaviour
    {
        [SerializeField, Min(1)] int instructionBudget = 10000;
        GameSession session;
        IEventPresentation presentation;
        Coroutine routine;
        int generation;
        public bool IsRunning { get; private set; }
        public string CurrentEventKey { get; private set; }
        public int CurrentPageIndex { get; private set; } = -1;
        public EventExecutionContext CurrentContext { get; private set; }
        public event Action Completed;
        public event Action<Exception> Failed;

        public void Configure(GameSession gameSession, IEventPresentation eventPresentation)
        {
            Cancel(); session = gameSession; presentation = eventPresentation;
        }
        public bool TryRun(EventDefinition definition, string eventKey)
        {
            if (session == null || definition == null) return false;
            return TryRun(definition, eventKey, definition.ResolvePageIndex(session.State, eventKey));
        }
        public bool TryRun(EventDefinition definition, string eventKey, int pageIndex)
        {
            if (!isActiveAndEnabled || IsRunning || session == null || session.HasPendingSave || session.State == null || definition == null ||
                string.IsNullOrWhiteSpace(eventKey) || pageIndex < 0 || definition.pages == null || pageIndex >= definition.pages.Count ||
                definition.ResolvePageIndex(session.State, eventKey) != pageIndex ||
                definition.pages[pageIndex].trigger == EventTrigger.Parallel || definition.IsPageCompleted(session.State, eventKey, pageIndex)) return false;
            // Failed validation belongs to this caller too. Touch/autorun objects
            // use these fields to stop retrying a broken page every frame.
            CurrentEventKey = eventKey.Trim(); CurrentPageIndex = pageIndex; CurrentContext = null;
            EventExecutionContext context;
            try { context = CreateExecution(definition, eventKey, pageIndex); }
            catch (Exception error) { ReportFailure(error); return false; }
            CurrentContext = context; CurrentEventKey = context.EventKey; CurrentPageIndex = pageIndex;
            IsRunning = true;
            int runGeneration = ++generation;
            Coroutine started = StartCoroutine(Drive(context, runGeneration));
            // A pure instruction stream can finish inside StartCoroutine.
            if (IsRunning && generation == runGeneration) routine = started;
            return true;
        }

        public EventExecutionContext CreateExecution(EventDefinition definition, string eventKey, int pageIndex = -1, bool background = false)
        {
            if (session == null || session.State == null) throw new InvalidOperationException("Event session is not configured.");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrWhiteSpace(eventKey)) throw new ArgumentException("Event key is missing.", nameof(eventKey));
            if (pageIndex < 0) pageIndex = definition.ResolvePageIndex(session.State, eventKey);
            if (pageIndex < 0 || definition.pages == null || pageIndex >= definition.pages.Count || definition.pages[pageIndex] == null)
                throw new ArgumentException("Event page is missing.");
            if (definition.ResolvePageIndex(session.State, eventKey) != pageIndex) throw new ArgumentException("Event page is no longer active.");
            definition.EnsureStableIds();
            if (definition.IsPageCompleted(session.State, eventKey, pageIndex)) throw new ArgumentException("Event page has already completed.");
            var program = EventCompiler.Compile(definition, pageIndex, background);
            if (!background && definition.pages[pageIndex].trigger == EventTrigger.Parallel) throw new ArgumentException("Parallel pages require background execution.");
            if (program.needsPresentation && presentation == null) throw new InvalidOperationException("Event dialogue presentation is not configured.");
            return new EventExecutionContext(session, presentation, definition, eventKey, pageIndex, background, program, instructionBudget);
        }

        public IEnumerator ExecuteContext(EventExecutionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            try { while (context.MoveNext(out object yielded)) yield return yielded; }
            finally { context.Dispose(); }
        }

        public void Cancel()
        {
            if (!IsRunning && routine == null) return;
            ++generation;
            // Mark cancellation before disposal so nested combat/presentation
            // finally blocks cannot accidentally complete the page.
            CurrentContext?.Cancel();
            if (routine != null) StopCoroutine(routine);
            routine = null; CurrentContext?.Dispose(); IsRunning = false;
        }
        void OnDisable() => Cancel();
        void OnDestroy() => Cancel();

        IEnumerator Drive(EventExecutionContext context, int runGeneration)
        {
            try
            {
                while (generation == runGeneration && context.MoveNext(out object yielded)) yield return yielded;
            }
            finally
            {
                context.Dispose();
                if (generation == runGeneration) { routine = null; IsRunning = false; }
            }
            if (generation != runGeneration) yield break;
            if (context.Error != null) { ReportFailure(context.Error); yield break; }
            if (context.Succeeded) Completed?.Invoke();
        }
        void ReportFailure(Exception error)
        {
            Debug.LogException(error, this);
            if (session != null) session.ShowNotice("이벤트 실행 오류: " + error.Message);
            Failed?.Invoke(error);
        }
    }
}
