using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public sealed class EventDebuggerWindow : EditorWindow
    {
        GameSession session;
        Vector2 scroll;
        double lastRefresh;
        bool showTrace = true;
        bool showRecent = true;
        bool showConditions, showState, showNpcs;
        string conditionFilter = "", stateFilter = "";

        [MenuItem("No Sleep/Event Debugger")]
        public static void Open() => GetWindow<EventDebuggerWindow>("No Sleep Events");

        void OnEnable() => EditorApplication.update += Refresh;
        void OnDisable() => EditorApplication.update -= Refresh;
        void Refresh()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - lastRefresh < 0.1) return;
            lastRefresh = EditorApplication.timeSinceStartup; Repaint();
        }

        void OnGUI()
        {
            if (session == null && EditorApplication.isPlaying) session = Object.FindFirstObjectByType<GameSession>();
            session = (GameSession)EditorGUILayout.ObjectField("게임 세션", session, typeof(GameSession), true);
            if (!EditorApplication.isPlaying || session == null)
            {
                EditorGUILayout.HelpBox("Play 상태에서 실행 중인 세션을 선택하세요. 전경·병렬 실행, 요청 큐와 저장 차단 이유를 조회할 수 있습니다.", MessageType.Info);
                return;
            }
            var scheduler = session.Scheduler;
            if (scheduler == null) { EditorGUILayout.HelpBox("이 세션에는 이벤트 스케줄러가 없습니다.", MessageType.Info); return; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(scheduler.Paused ? "실행 재개" : "일시 정지")) scheduler.SetPaused(!scheduler.Paused);
                using (new EditorGUI.DisabledScope(!scheduler.Paused))
                    if (GUILayout.Button("명령 한 단계")) scheduler.Step();
                if (GUILayout.Button("전경 취소")) session.Events?.Cancel();
                if (GUILayout.Button("전체 취소")) { session.Events?.Cancel(); scheduler.CancelAll(); }
            }
            EditorGUILayout.LabelField("스케줄러", scheduler.Status);
            EditorGUILayout.LabelField("실행 소유권", session.ExecutionStatus);
            EditorGUILayout.LabelField("입력", session.IsInputBlocked ? session.InputBlockReason : "이동·상호작용 가능");
            EditorGUILayout.LabelField("저장", string.IsNullOrEmpty(session.SaveBlockReason) ? "안전한 저장 경계" : session.SaveBlockReason);
            EditorGUILayout.LabelField("저장 요청", session.SaveStatus + (session.HasPendingSave ? " · 슬롯 " + (session.PendingSaveSlot + 1) : ""));
            showTrace = EditorGUILayout.Toggle("최근 실행 기록 표시", showTrace);
            showRecent = EditorGUILayout.Toggle("종료된 실행 표시", showRecent);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("전경 실행", EditorStyles.boldLabel);
            DrawContext(session.Events?.CurrentContext);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("병렬 실행", EditorStyles.boldLabel);
            if (scheduler.BackgroundContexts.Count == 0) EditorGUILayout.LabelField("실행 중인 배경 작업 없음");
            foreach (var context in scheduler.BackgroundContexts) DrawContext(context);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("전경 요청 큐 · " + scheduler.QueuedCount, EditorStyles.boldLabel);
            foreach (var request in scheduler.PendingRequests)
                EditorGUILayout.LabelField(request.DefinitionName + " · " + request.EventKey + " · " + request.PageId, EditorStyles.wordWrappedLabel);
            if (showRecent)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("최근 완료·실패·취소 실행", EditorStyles.boldLabel);
                foreach (var context in scheduler.RecentContexts) DrawContext(context);
            }
            showConditions = EditorGUILayout.Foldout(showConditions, "현재 맵의 활성 페이지·조건", true);
            if (showConditions) DrawConditions();
            showNpcs = EditorGUILayout.Foldout(showNpcs, "현재 맵 NPC 이동·예약·일과", true);
            if (showNpcs) DrawNpcs();
            showState = EditorGUILayout.Foldout(showState, "스위치·변수 조회", true);
            if (showState) DrawState();
            EditorGUILayout.EndScrollView();
        }

        void DrawNpcs()
        {
            if (session.Map == null || session.Npcs == null) return;
            EditorGUILayout.LabelField("이동 경계", session.Npcs.IsAtCellBoundary ? "모두 칸 경계" : "이동 보간 중");
            int count = 0;
            foreach (var actor in session.Map.GetComponentsInChildren<NpcActor>(true))
            {
                if (++count > 64) { EditorGUILayout.LabelField("최대 64개 NPC를 표시합니다."); break; }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(actor.Key, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("셀/예약", actor.Cell + (actor.HasSegment ? " → " + actor.ReservedDestination : ""));
                    EditorGUILayout.LabelField("이동", actor.Status + (actor.PersistentOverride ? " · 영구 위치 우선" : ""));
                    EditorGUILayout.LabelField("경로/지점", actor.RouteId + " · " + actor.WaypointIndex);
                    EditorGUILayout.LabelField("일과", actor.ScheduleId);
                    if (!string.IsNullOrEmpty(actor.Error)) EditorGUILayout.HelpBox(actor.Error, MessageType.Warning);
                    if (actor.Definition != null && GUILayout.Button("NPC 정의")) { Selection.activeObject = actor.Definition; EditorGUIUtility.PingObject(actor.Definition); }
                }
            }
        }

        void DrawConditions()
        {
            conditionFilter = EditorGUILayout.TextField("배치·페이지·키 검색", conditionFilter);
            if (session.Map == null) return;
            int owners = 0, rows = 0;
            foreach (var owner in session.Map.EventObjects)
            {
                if (owner == null) continue;
                var page = owner.ActivePage;
                bool matches = Matches(owner.EventKey, conditionFilter) || Matches(owner.DisplayName, conditionFilter) ||
                    (page != null && Matches(page.name, conditionFilter));
                if (!matches && page?.conditions != null)
                    foreach (var condition in page.conditions)
                        if (condition != null && Matches(condition.key, conditionFilter)) { matches = true; break; }
                if (!matches) continue;
                if (++owners > 64 || rows >= 100)
                { EditorGUILayout.LabelField("최대 64개 배치·100개 조건만 표시합니다. 검색을 좁히세요."); break; }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(owner.EventKey, EditorStyles.miniBoldLabel);
                    if (page == null) { EditorGUILayout.LabelField("조건에 맞는 활성 페이지 없음"); continue; }
                    EditorGUILayout.LabelField(page.name + " · " + page.trigger + " · " + page.pageId, EditorStyles.wordWrappedMiniLabel);
                    if (page.conditions == null || page.conditions.Count == 0) EditorGUILayout.LabelField("무조건");
                    else foreach (var condition in page.conditions)
                    {
                        if (++rows > 100) break;
                        if (condition == null) { EditorGUILayout.HelpBox("빈 조건 데이터", MessageType.Error); continue; }
                        try
                        {
                            bool result = condition.Evaluate(session.State, owner.EventKey);
                            EditorGUILayout.LabelField((result ? "참 · " : "거짓 · ") + condition.kind + " · " + condition.key +
                                " · " + condition.comparison + " " + condition.value + " · expected=" + condition.expected,
                                EditorStyles.wordWrappedMiniLabel);
                        }
                        catch (System.Exception error) { EditorGUILayout.HelpBox("조건 판정 오류: " + error.Message, MessageType.Error); }
                    }
                }
            }
        }

        void DrawState()
        {
            stateFilter = EditorGUILayout.TextField("상태 키 검색", stateFilter);
            DrawBools("전역 스위치", session.State.switches);
            DrawInts("정수 변수", session.State.variables);
            DrawBools("배치 셀프 스위치", session.State.selfSwitches);
            EditorGUILayout.HelpBox("읽기 전용 조회입니다. 호출별 지역 매개변수 값은 현재 조회 API에 포함되지 않습니다.", MessageType.Info);
        }
        void DrawBools(string label, List<BoolEntry> values)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel); int count = 0;
            if (values != null) foreach (var entry in values)
            {
                if (entry == null || !Matches(entry.key, stateFilter)) continue;
                if (++count > 100) { EditorGUILayout.LabelField("최대 100행만 표시합니다."); break; }
                EditorGUILayout.LabelField(entry.key + " = " + entry.value, EditorStyles.wordWrappedMiniLabel);
            }
        }
        void DrawInts(string label, List<IntEntry> values)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel); int count = 0;
            if (values != null) foreach (var entry in values)
            {
                if (entry == null || !Matches(entry.key, stateFilter)) continue;
                if (++count > 100) { EditorGUILayout.LabelField("최대 100행만 표시합니다."); break; }
                EditorGUILayout.LabelField(entry.key + " = " + entry.value, EditorStyles.wordWrappedMiniLabel);
            }
        }
        static bool Matches(string text, string filter) => string.IsNullOrEmpty(filter) ||
            (!string.IsNullOrEmpty(text) && text.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0);

        void DrawContext(EventExecutionContext context)
        {
            if (context == null) { EditorGUILayout.LabelField("실행 없음"); return; }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(context.EventKey, EditorStyles.boldLabel);
                    if (context.Definition != null && GUILayout.Button("에셋", GUILayout.Width(48))) EditorGUIUtility.PingObject(context.Definition);
                }
                EditorGUILayout.LabelField("페이지 ID", context.PageId ?? "");
                EditorGUILayout.LabelField("프로그램 ID", context.ProgramId ?? "");
                EditorGUILayout.LabelField("명령", (context.CommandIndex + 1) + " · " + (context.CommandId ?? ""));
                EditorGUILayout.LabelField("실행 수", context.ExecutedInstructions.ToString());
                EditorGUILayout.LabelField("상태", context.IsCancelled ? "취소" : context.IsComplete ? context.Succeeded ? "완료" : "실패" : "실행 중");
                if (!string.IsNullOrEmpty(context.WaitReason)) EditorGUILayout.LabelField("대기 이유", context.WaitReason, EditorStyles.wordWrappedLabel);
                if (context.Error != null) EditorGUILayout.HelpBox(context.Error.Message, MessageType.Error);
                if (context.CurrentCommonEvent != null)
                {
                    EditorGUILayout.ObjectField("현재 공통 이벤트", context.CurrentCommonEvent, typeof(CommonEventDefinition), false);
                }
                EditorGUILayout.LabelField("호출 스택", EditorStyles.miniBoldLabel);
                foreach (string frame in context.CallStack) EditorGUILayout.LabelField(frame, EditorStyles.wordWrappedMiniLabel);
                if (showTrace)
                {
                    EditorGUILayout.LabelField("최근 명령 기록", EditorStyles.miniBoldLabel);
                    foreach (string trace in context.Trace) EditorGUILayout.LabelField(trace, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }
}
