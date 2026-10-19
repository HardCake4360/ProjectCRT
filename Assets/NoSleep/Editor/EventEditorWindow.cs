using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public sealed class EventEditorWindow : EditorWindow
    {
        [SerializeField] ScriptableObject owner;
        [SerializeField] int pageIndex;
        [SerializeField] int commandIndex = -1;
        [SerializeField] string search = "";
        Vector2 pagesScroll, commandsScroll, detailScroll;
        readonly HashSet<int> selectedCommands = new HashSet<int>();
        SerializedObject serializedOwner;
        ContentReferenceIndex references;
        EventCommandType newCommand = EventCommandType.ShowDialogue;
        EventTemplateKind templateKind;
        EventTemplateDefinition templateAsset;
        [SerializeField] EventTemplateReferences templateReferences = new EventTemplateReferences();
        bool showTemplate, allowExternalTargets;
        string feedback;
        List<EventEditorDiagnostic> diagnostics = new List<EventEditorDiagnostic>();

        [MenuItem("No Sleep/Event Editor")]
        public static void OpenMenu() => GetWindow<EventEditorWindow>("No Sleep Event");
        public static void Open(ScriptableObject asset, int page = -1, int command = -1)
        {
            var window = GetWindow<EventEditorWindow>("No Sleep Event");
            window.SetOwner(asset);
            window.pageIndex = asset is CommonEventDefinition ? -1 : Mathf.Max(0, page);
            window.SelectOnly(command); window.Refresh(); window.Show();
        }
        void OnEnable()
        {
            minSize = new Vector2(900, 540); Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (owner != null) serializedOwner = new SerializedObject(owner);
        }
        void OnDisable() { Undo.undoRedoPerformed -= OnUndo; EditorApplication.playModeStateChanged -= OnPlayMode; }
        void OnUndo() { selectedCommands.Clear(); Refresh(); Repaint(); }
        void OnPlayMode(PlayModeStateChange _) => Repaint();
        void SetOwner(ScriptableObject asset)
        {
            if (asset != null && !(asset is EventDefinition) && !(asset is CommonEventDefinition))
                throw new ArgumentException("Choose an EventDefinition or CommonEventDefinition.");
            owner = asset; pageIndex = asset is CommonEventDefinition ? -1 : 0; SelectOnly(-1);
            if (asset is EventDefinition definition && !EditorApplication.isPlayingOrWillChangePlaymode) EventIdentityUpgrader.UpgradeDefinition(definition);
            if (asset is CommonEventDefinition common && !EditorApplication.isPlayingOrWillChangePlaymode) EventIdentityUpgrader.UpgradeCommon(common);
            serializedOwner = owner != null ? new SerializedObject(owner) : null;
            references = null; feedback = null; Refresh();
        }
        void Refresh()
        {
            if (owner == null) { diagnostics.Clear(); return; }
            ClampSelection();
            serializedOwner = new SerializedObject(owner);
            diagnostics = EventEditorDiagnostics.Collect(owner);
        }
        void ClampSelection()
        {
            if (owner is EventDefinition definition) pageIndex = definition.pages == null || definition.pages.Count == 0 ? -1 : Mathf.Clamp(pageIndex, 0, definition.pages.Count - 1);
            else pageIndex = -1;
            var commands = Commands;
            if (commandIndex >= (commands?.Count ?? 0)) commandIndex = -1;
            selectedCommands.RemoveWhere(index => index < 0 || index >= (commands?.Count ?? 0));
        }
        List<EventCommand> Commands => owner is CommonEventDefinition common ? common.commands :
            owner is EventDefinition definition && definition.pages != null && pageIndex >= 0 && pageIndex < definition.pages.Count ? definition.pages[pageIndex]?.commands : null;
        void SelectOnly(int index) { commandIndex = index; selectedCommands.Clear(); if (index >= 0) selectedCommands.Add(index); }
        void Change(Action action)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { action(); feedback = null; Refresh(); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { feedback = error.Message; }
            Repaint();
        }
        void OnGUI()
        {
            DrawToolbar();
            if (owner == null) { EditorGUILayout.HelpBox("이벤트 또는 공통 이벤트를 선택하세요. 페이지와 명령을 편집하고 템플릿을 추가할 수 있습니다.", MessageType.Info); return; }
            ClampSelection();
            bool locked = EditorApplication.isPlayingOrWillChangePlaymode;
            if (locked) EditorGUILayout.HelpBox("Play 중에는 제작 데이터 편집을 잠급니다. 실행 상태는 Event Debugger에서 확인하세요.", MessageType.Info);
            if (!string.IsNullOrEmpty(feedback)) EditorGUILayout.HelpBox(feedback, MessageType.Warning);
            using (new EditorGUI.DisabledScope(locked))
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(215))) DrawPages();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(300, position.width * 0.36f)))) DrawCommands();
                using (new EditorGUILayout.VerticalScope()) DrawDetails();
            }
            DrawDiagnostics();
        }
        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var value = EditorGUILayout.ObjectField(owner, typeof(ScriptableObject), false, GUILayout.MinWidth(200)) as ScriptableObject;
                if (value != owner)
                {
                    if (value == null || value is EventDefinition || value is CommonEventDefinition) SetOwner(value);
                    else feedback = "일반 또는 공통 이벤트를 선택하세요.";
                }
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("새 이벤트", EditorStyles.toolbarButton)) CreateAsset(false);
                    if (GUILayout.Button("새 공통", EditorStyles.toolbarButton)) CreateAsset(true);
                    if (GUILayout.Button("저장", EditorStyles.toolbarButton) && owner != null) { AssetDatabase.SaveAssetIfDirty(owner); Refresh(); }
                }
                if (GUILayout.Button("참조 갱신", EditorStyles.toolbarButton)) { references = ContentReferenceIndex.Build(); Refresh(); }
                if (GUILayout.Button("검사", EditorStyles.toolbarButton)) Refresh();
                if (GUILayout.Button("사용 위치", EditorStyles.toolbarButton)) ContentBrowserWindow.Open();
                if (GUILayout.Button("맵 연결", EditorStyles.toolbarButton)) MapConnectionsWindow.Open();
            }
        }
        void CreateAsset(bool common)
        {
            string path = EditorUtility.SaveFilePanelInProject(common ? "새 공통 이벤트" : "새 이벤트", common ? "CommonEvent" : "Event", "asset", "저장 위치를 선택하세요.", "Assets/NoSleep");
            if (string.IsNullOrEmpty(path)) return;
            ScriptableObject asset = common ? (ScriptableObject)CreateInstance<CommonEventDefinition>() : CreateInstance<EventDefinition>();
            if (asset is EventDefinition definition) { definition.pages.Add(new EventPage()); definition.EnsureStableIds(); }
            else ((CommonEventDefinition)asset).EnsureStableIds();
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset); SetOwner(asset); Selection.activeObject = asset;
        }
        void DrawPages()
        {
            EditorGUILayout.LabelField(owner is CommonEventDefinition ? "공통 프로그램" : "페이지 · 아래쪽 우선", EditorStyles.boldLabel);
            pagesScroll = EditorGUILayout.BeginScrollView(pagesScroll);
            if (HasMissingLists(owner))
            {
                EditorGUILayout.HelpBox("누락된 페이지/명령 목록이 있습니다. 유효한 기존 ID는 보존하고 빈 목록/페이지를 복구할 수 있습니다.", MessageType.Warning);
                if (GUILayout.Button("누락 목록 복구")) Change(() => RepairMissingLists(owner));
            }
            if (owner is EventDefinition definition && definition.pages != null)
            {
                for (int index = 0; index < definition.pages.Count; index++)
                {
                    var page = definition.pages[index];
                    string caption = (index + 1) + " · " + (page?.name ?? "빈 페이지");
                    if (GUILayout.Toggle(pageIndex == index, caption, "Button") && pageIndex != index) { pageIndex = index; SelectOnly(-1); Refresh(); }
                }
                if (GUILayout.Button("페이지 추가")) Change(() => { pageIndex = EventEditOperations.AddPage(definition); SelectOnly(-1); });
                using (new EditorGUI.DisabledScope(pageIndex < 0))
                {
                    if (GUILayout.Button("페이지 복제")) Change(() => { pageIndex = EventEditOperations.DuplicatePage(definition, pageIndex); SelectOnly(-1); });
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("위로") && pageIndex > 0) Change(() => { EventEditOperations.MovePage(definition, pageIndex, pageIndex - 1); pageIndex--; });
                        if (GUILayout.Button("아래로") && pageIndex + 1 < definition.pages.Count) Change(() => { EventEditOperations.MovePage(definition, pageIndex, pageIndex + 1); pageIndex++; });
                    }
                    if (GUILayout.Button("페이지 삭제") && EditorUtility.DisplayDialog("페이지 삭제", "이 페이지를 참조하는 기존 저장의 완료·체크포인트 연결이 사라질 수 있습니다. Undo로 되돌릴 수 있습니다.", "삭제", "취소"))
                        Change(() => { EventEditOperations.RemovePage(definition, pageIndex); SelectOnly(-1); });
                }
            }
            DrawTemplates();
            EditorGUILayout.EndScrollView();
        }
        void DrawCommands()
        {
            EditorGUILayout.LabelField("명령", EditorStyles.boldLabel);
            search = EditorGUILayout.TextField("검색", search);
            var commands = Commands;
            using (new EditorGUI.DisabledScope(commands == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    newCommand = (EventCommandType)EditorGUILayout.EnumPopup(newCommand);
                    if (GUILayout.Button("추가", GUILayout.Width(50))) Change(() => {
                        int insertion = commandIndex < 0 ? commands.Count : commandIndex + 1;
                        var indices = EventEditOperations.InsertCommands(owner, pageIndex, insertion, new[] { new EventCommand { type = newCommand } });
                        SelectOnly(indices[0]); });
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("전체 선택")) { selectedCommands.Clear(); if (commands != null) for (int i = 0; i < commands.Count; i++) selectedCommands.Add(i); }
                    if (GUILayout.Button("분기 범위")) SelectBranch();
                    if (GUILayout.Button("선택 해제")) SelectOnly(-1);
                }
            }
            commandsScroll = EditorGUILayout.BeginScrollView(commandsScroll);
            if (commands != null) for (int index = 0; index < commands.Count; index++)
            {
                string caption = (index + 1).ToString("D3") + "  " + EventEditorDiagnostics.Summary(commands[index]);
                if (!string.IsNullOrWhiteSpace(search) && caption.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                int indent = EventEditorDiagnostics.Indent(commands, index);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(Mathf.Min(4, indent) * 10);
                    if (GUILayout.Button(caption, selectedCommands.Contains(index) ? EditorStyles.miniButtonMid : EditorStyles.miniButton, GUILayout.Height(24)))
                    {
                        if (Event.current.shift && commandIndex >= 0)
                            for (int i = Math.Min(commandIndex, index); i <= Math.Max(commandIndex, index); i++) selectedCommands.Add(i);
                        else if (Event.current.control || Event.current.command) { if (!selectedCommands.Add(index)) selectedCommands.Remove(index); }
                        else SelectOnly(index);
                        commandIndex = index;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            allowExternalTargets = EditorGUILayout.ToggleLeft("같은 프로그램의 범위 밖 라벨 연결 유지", allowExternalTargets);
            using (new EditorGUI.DisabledScope(commands == null || selectedCommands.Count == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("복사")) Change(() => EditorGUIUtility.systemCopyBuffer = EventEditOperations.CopyCommands(owner, pageIndex, selectedCommands.OrderBy(i => i).ToArray()));
                    if (GUILayout.Button("복제")) Change(() => {
                        var copies = EventEditOperations.ReadClipboard(EventEditOperations.CopyCommands(owner, pageIndex, selectedCommands.OrderBy(i => i).ToArray()));
                        var inserted = EventEditOperations.InsertCommands(owner, pageIndex, selectedCommands.Max() + 1, copies, allowExternalTargets); SelectIndices(inserted); });
                    if (GUILayout.Button("삭제")) Change(() => { EventEditOperations.RemoveCommands(owner, pageIndex, selectedCommands.OrderBy(i => i).ToArray()); SelectOnly(-1); });
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("위로") && selectedCommands.Min() > 0) Change(() => {
                        int boundary = selectedCommands.Min() - 1; EventEditOperations.MoveCommands(owner, pageIndex, selectedCommands.OrderBy(i => i).ToArray(), boundary);
                        SelectIndices(Enumerable.Range(boundary, selectedCommands.Count).ToArray()); });
                    if (GUILayout.Button("아래로") && selectedCommands.Max() < commands.Count - 1) Change(() => {
                        int boundary = selectedCommands.Max() + 2, destination = boundary - selectedCommands.Count;
                        EventEditOperations.MoveCommands(owner, pageIndex, selectedCommands.OrderBy(i => i).ToArray(), boundary); SelectIndices(Enumerable.Range(destination, selectedCommands.Count).ToArray()); });
                }
            }
            using (new EditorGUI.DisabledScope(commands == null))
                if (GUILayout.Button("붙여넣기")) Change(() => {
                    var source = EventEditOperations.ReadClipboard(EditorGUIUtility.systemCopyBuffer);
                    SelectIndices(EventEditOperations.InsertCommands(owner, pageIndex, commandIndex < 0 ? commands.Count : commandIndex + 1, source, allowExternalTargets)); });
            EditorGUILayout.LabelField("Shift 범위 선택 · Ctrl 추가 선택 · Ctrl+Z Undo", EditorStyles.wordWrappedMiniLabel);
        }
        void SelectIndices(IEnumerable<int> indices)
        { selectedCommands.Clear(); foreach (int index in indices) selectedCommands.Add(index); commandIndex = selectedCommands.Count == 0 ? -1 : selectedCommands.Min(); }
        void SelectBranch()
        {
            var commands = Commands; if (commands == null || commandIndex < 0 || commandIndex >= commands.Count) return;
            var span = EventEditorDiagnostics.BranchRange(commands, commandIndex);
            SelectIndices(Enumerable.Range(span.x, span.y - span.x + 1));
        }
        void DrawDetails()
        {
            EditorGUILayout.LabelField("세부 설정", EditorStyles.boldLabel);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            serializedOwner.Update();
            EditorGUILayout.PropertyField(serializedOwner.FindProperty("displayName"), new GUIContent("표시 이름"));
            if (owner is CommonEventDefinition) EditorGUILayout.PropertyField(serializedOwner.FindProperty("parameters"), new GUIContent("지역 인자"), true);
            else if (pageIndex >= 0 && ((EventDefinition)owner).pages[pageIndex] != null)
            {
                var page = serializedOwner.FindProperty("pages").GetArrayElementAtIndex(pageIndex);
                foreach (string field in new[] { "name", "conditions", "trigger", "runOnce", "visible", "blocksMovement" }) EditorGUILayout.PropertyField(page.FindPropertyRelative(field), true);
                var conditions = page.FindPropertyRelative("conditions");
                if (references != null && conditions != null) for (int index = 0; index < conditions.arraySize; index++)
                    DrawConditionSelector(conditions.GetArrayElementAtIndex(index), "페이지 조건 " + (index + 1));
                if (page.FindPropertyRelative("trigger").intValue == (int)EventTrigger.Parallel)
                { EditorGUILayout.PropertyField(page.FindPropertyRelative("parallelRepeat")); EditorGUILayout.PropertyField(page.FindPropertyRelative("parallelIntervalSeconds")); }
                using (new EditorGUI.DisabledScope(true)) { EditorGUILayout.PropertyField(page.FindPropertyRelative("pageId")); EditorGUILayout.PropertyField(page.FindPropertyRelative("legacyPageIndex")); }
            }
            var commandsProperty = owner is CommonEventDefinition ? serializedOwner.FindProperty("commands") :
                pageIndex < 0 || ((EventDefinition)owner).pages[pageIndex] == null ? null : serializedOwner.FindProperty("pages").GetArrayElementAtIndex(pageIndex).FindPropertyRelative("commands");
            if (commandsProperty != null && commandIndex >= 0 && commandIndex < commandsProperty.arraySize)
            {
                EditorGUILayout.Space(); var command = commandsProperty.GetArrayElementAtIndex(commandIndex); command.isExpanded = true;
                EditorGUILayout.PropertyField(command, new GUIContent("선택 명령 " + (commandIndex + 1)), true);
                DrawReferenceSelectors(command);
            }
            if (serializedOwner.ApplyModifiedProperties())
            { if (owner is EventDefinition definition) EventIdentityUpgrader.UpgradeDefinition(definition); else EventIdentityUpgrader.UpgradeCommon((CommonEventDefinition)owner); Refresh(); }
            EditorGUILayout.EndScrollView();
        }
        void DrawReferenceSelectors(SerializedProperty command)
        {
            if (references == null) { if (GUILayout.Button("기존 상태 키/에셋 목록 불러오기")) references = ContentReferenceIndex.Build(); return; }
            var type = (EventCommandType)command.FindPropertyRelative("type").intValue;
            if (type == EventCommandType.MoveNpc || type == EventCommandType.FaceNpc || type == EventCommandType.StopNpc || type == EventCommandType.ResumeNpc)
                KeyPicker("기존 NPC 배치 선택", command.FindPropertyRelative("key"), references.Maps.Where(map => map != null && map.events != null)
                    .SelectMany(map => map.events.Where(placement => placement?.npc != null).Select(placement => map.mapId + "/" + placement.id)));
            IEnumerable<string> keys = null;
            if (type == EventCommandType.SetSwitch) keys = references.SwitchKeys;
            if (type == EventCommandType.SetVariable || type == EventCommandType.AddVariable) keys = references.VariableKeys;
            if (type == EventCommandType.AddItem) keys = references.ItemIds;
            if (type == EventCommandType.JoinCompanion || type == EventCommandType.LeaveCompanion) keys = references.Companions.Where(value => value != null).Select(value => value.companionId);
            if (keys != null) KeyPicker("기존 키 선택", command.FindPropertyRelative("key"), keys);
            if (type == EventCommandType.IfCondition && !command.FindPropertyRelative("useLocalCondition").boolValue)
            {
                DrawConditionSelector(command.FindPropertyRelative("condition"), "명령 조건");
            }
            if (type == EventCommandType.GrantOnce)
            {
                var mutations = command.FindPropertyRelative("mutations");
                for (int index = 0; index < mutations.arraySize; index++)
                {
                    var mutation = mutations.GetArrayElementAtIndex(index);
                    var kind = (StateMutationKind)mutation.FindPropertyRelative("kind").intValue;
                    IEnumerable<string> choices = kind == StateMutationKind.SetSwitch ? references.SwitchKeys :
                        kind == StateMutationKind.SetVariable || kind == StateMutationKind.AddVariable ? references.VariableKeys :
                        kind == StateMutationKind.AddItem ? references.ItemIds : null;
                    if (choices != null) KeyPicker("지급 " + (index + 1) + " 키", mutation.FindPropertyRelative("key"), choices);
                }
            }
            if (type == EventCommandType.CallCommon)
            {
                var choices = references.CommonEvents.Where(value => value != null).ToArray();
                int selected = EditorGUILayout.Popup("기존 공통 선택", 0, new[] { "직접 지정 / 선택" }.Concat(choices.Select(value => value.displayName + " · " + value.name)).ToArray());
                if (selected > 0) command.FindPropertyRelative("commonEvent").objectReferenceValue = choices[selected - 1];
            }
        }
        void DrawConditionSelector(SerializedProperty condition, string caption)
        {
            if (condition == null || references == null) return;
            var kind = (ConditionKind)condition.FindPropertyRelative("kind").intValue;
            IEnumerable<string> choices = kind == ConditionKind.Switch ? references.SwitchKeys : kind == ConditionKind.Variable ? references.VariableKeys :
                kind == ConditionKind.Item ? references.ItemIds : kind == ConditionKind.Companion ? references.Companions.Where(value => value != null).Select(value => value.companionId) : null;
            if (choices != null) KeyPicker(caption + " 기존 키", condition.FindPropertyRelative("key"), choices);
        }
        static void KeyPicker(string caption, SerializedProperty key, IEnumerable<string> choices)
        {
            var values = choices.Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
            int selected = EditorGUILayout.Popup(caption, 0, new[] { "직접 입력 / 선택" }.Concat(values).ToArray()); if (selected > 0) key.stringValue = values[selected - 1];
        }
        void DrawTemplates()
        {
            EditorGUILayout.Space(); showTemplate = EditorGUILayout.Foldout(showTemplate, "템플릿 추가", true); if (!showTemplate) return;
            templateAsset = EditorGUILayout.ObjectField("저장된 조합", templateAsset, typeof(EventTemplateDefinition), false) as EventTemplateDefinition;
            if (templateAsset == null)
            {
                templateKind = (EventTemplateKind)EditorGUILayout.EnumPopup("기본 조합", templateKind);
                templateReferences.speaker = EditorGUILayout.TextField("화자", templateReferences.speaker);
                templateReferences.text = EditorGUILayout.TextField("대사", templateReferences.text);
                if (templateKind == EventTemplateKind.OneTimeReward)
                {
                    templateReferences.rewardItemId = EditorGUILayout.TextField("아이템 ID", templateReferences.rewardItemId);
                    templateReferences.rewardItemAmount = EditorGUILayout.IntField("아이템 수량", templateReferences.rewardItemAmount);
                    templateReferences.rewardGold = EditorGUILayout.IntField("소지금", templateReferences.rewardGold);
                }
                if (templateKind == EventTemplateKind.ConditionalBranch)
                {
                    templateReferences.condition ??= new StateCondition();
                    templateReferences.condition.kind = (ConditionKind)EditorGUILayout.EnumPopup("조건 종류", templateReferences.condition.kind);
                    templateReferences.condition.key = EditorGUILayout.TextField("조건 키", templateReferences.condition.key);
                    templateReferences.condition.expected = EditorGUILayout.Toggle("참/거짓", templateReferences.condition.expected);
                    templateReferences.condition.value = EditorGUILayout.IntField("비교 수치", templateReferences.condition.value);
                    templateReferences.destinationMap = EditorGUILayout.ObjectField("목적지", templateReferences.destinationMap, typeof(MapDefinition), false) as MapDefinition;
                    templateReferences.destinationCell = EditorGUILayout.Vector2IntField("목적지 칸", templateReferences.destinationCell);
                }
                if (templateKind == EventTemplateKind.StayAwake) templateReferences.nightActivity = EditorGUILayout.ObjectField("밤샘 활동", templateReferences.nightActivity, typeof(NightActivityDefinition), false) as NightActivityDefinition;
                if (templateKind == EventTemplateKind.ContactCombat) templateReferences.enemy = EditorGUILayout.ObjectField("접촉 적", templateReferences.enemy, typeof(EnemyDefinition), false) as EnemyDefinition;
                if (templateKind == EventTemplateKind.CommonCall) templateReferences.commonEvent = EditorGUILayout.ObjectField("공통 이벤트", templateReferences.commonEvent, typeof(CommonEventDefinition), false) as CommonEventDefinition;
            }
            if (GUILayout.Button(owner is EventDefinition ? "새 페이지로 추가" : "공통 종료 앞에 조합 추가")) Change(() => {
                bool temporary = templateAsset == null; var template = templateAsset ?? EventTemplates.Create(templateKind, templateReferences);
                try
                {
                    if (owner is EventDefinition definition) {
                        if (definition.pages == null) throw new ArgumentException("누락 목록 복구 후 템플릿을 추가하세요.");
                        EventEditOperations.InsertPages(definition, definition.pages.Count, template.pages); pageIndex = definition.pages.Count - 1; SelectOnly(-1); }
                    else SelectIndices(InsertCommonTemplate((CommonEventDefinition)owner, template));
                }
                finally { if (temporary) DestroyImmediate(template); }
            });
            if (GUILayout.Button("기본 조합을 에셋으로 저장") && templateAsset == null)
            {
                string path = EditorUtility.SaveFilePanelInProject("템플릿 저장", templateKind.ToString(), "asset", "조합을 저장하세요.", "Assets/NoSleep");
                if (!string.IsNullOrEmpty(path)) Change(() => { templateAsset = EventTemplates.Create(templateKind, templateReferences); AssetDatabase.CreateAsset(templateAsset, path); AssetDatabase.SaveAssetIfDirty(templateAsset); });
            }
        }
        public static IReadOnlyList<int> InsertCommonTemplate(CommonEventDefinition common, EventTemplateDefinition template)
        {
            if (common == null || template?.pages == null || template.pages.Count != 1 || template.pages[0]?.commands == null)
                throw new ArgumentException("공통 이벤트에는 명령이 있는 단일 페이지 템플릿을 선택하세요.");
            var commands = common.commands;
            var additions = template.pages[0].commands.ToList();
            // A template's terminal End is for standalone execution. In a common
            // program keep the existing terminal End so sequential additions run.
            if (additions.Count > 0 && additions[additions.Count - 1]?.type == EventCommandType.End) additions.RemoveAt(additions.Count - 1);
            int insertion = commands?.Count ?? 0;
            if (insertion > 0 && commands[insertion - 1]?.type == EventCommandType.End) insertion--;
            return EventEditOperations.InsertCommands(common, -1, insertion, additions);
        }
        static bool HasMissingLists(ScriptableObject asset) => asset is EventDefinition definition ?
            definition.pages == null || definition.pages.Any(page => page == null || page.commands == null) :
            asset is CommonEventDefinition common && common.commands == null;
        public static void RepairMissingLists(ScriptableObject asset)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 중에는 제작 목록을 복구할 수 없습니다.");
            if (!(asset is EventDefinition) && !(asset is CommonEventDefinition)) throw new ArgumentException("이벤트를 선택하세요.");
            if (!HasMissingLists(asset)) return;
            Undo.RegisterCompleteObjectUndo(asset, "Repair Missing Event Lists");
            if (asset is EventDefinition definition)
            {
                definition.pages ??= new List<EventPage>();
                for (int index = 0; index < definition.pages.Count; index++)
                {
                    definition.pages[index] ??= new EventPage { pageId = Guid.NewGuid().ToString("N"), legacyPageIndex = -1 };
                    definition.pages[index].commands ??= new List<EventCommand>();
                }
                definition.EnsureStableIds();
            }
            else { var common = (CommonEventDefinition)asset; common.commands = new List<EventCommand>(); common.EnsureStableIds(); }
            EditorUtility.SetDirty(asset);
        }
        void DrawDiagnostics()
        {
            if (diagnostics.Count == 0) return;
            EditorGUILayout.LabelField("편집 진단 · " + diagnostics.Count, EditorStyles.boldLabel);
            foreach (var item in diagnostics.Take(4))
            {
                if (GUILayout.Button(item.Message, EditorStyles.wordWrappedMiniLabel))
                { if (owner is EventDefinition && item.PageIndex >= 0) pageIndex = item.PageIndex; SelectOnly(item.CommandIndex); Repaint(); }
            }
            if (diagnostics.Count > 4) EditorGUILayout.LabelField("추가 진단은 Content Browser와 Validate Content에서 확인하세요.", EditorStyles.miniLabel);
        }
    }
}
