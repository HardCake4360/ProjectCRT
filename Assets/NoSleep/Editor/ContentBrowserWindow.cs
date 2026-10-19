using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public sealed class ContentBrowserWindow : EditorWindow
    {
        ContentReferenceIndex index;
        Vector2 scroll;
        string search = "";
        int tab;
        StateKeyRegistry registry;
        SerializedObject registryEditor;
        ContentReferenceCategory category;
        bool filterCategory;

        [MenuItem("No Sleep/Content Browser")]
        public static void Open() => GetWindow<ContentBrowserWindow>("No Sleep Content");
        void OnEnable() { Refresh(); Undo.undoRedoPerformed += Refresh; }
        void OnDisable() => Undo.undoRedoPerformed -= Refresh;
        void Refresh() { index = ContentReferenceIndex.Build(); Repaint(); }
        void OnGUI()
        {
            if (index == null) Refresh();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("새로 고침", EditorStyles.toolbarButton, GUILayout.Width(80))) Refresh();
                search = GUILayout.TextField(search, EditorStyles.toolbarTextField);
            }
            tab = GUILayout.Toolbar(tab, new[] { "카탈로그", "사용 위치", "진단", "선언 목록" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawCatalog(); break;
                case 1: DrawReferences(); break;
                case 2: DrawDiagnostics(); break;
                case 3: DrawRegistry(); break;
            }
            EditorGUILayout.EndScrollView();
        }
        bool Matches(string value) => string.IsNullOrEmpty(search) || (value ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        void DrawCatalog()
        {
            EditorGUILayout.HelpBox("Assets/NoSleep의 실사용 키와 제작 선언을 합친 목록입니다. 선택한 키로 사용 위치를 조회할 수 있습니다.", MessageType.Info);
            DrawKeys("스위치", index.SwitchKeys, ContentReferenceCategory.Switch);
            DrawKeys("정수 변수", index.VariableKeys, ContentReferenceCategory.Variable);
            DrawKeys("아이템 ID", index.ItemIds, ContentReferenceCategory.Item);
            EditorGUILayout.LabelField("동료 정의", EditorStyles.boldLabel);
            foreach (var companion in index.Companions.Where(x => Matches(x.companionId + " " + x.displayName)).Take(250))
                AssetButton(companion, companion.companionId + " · " + companion.displayName);
            EditorGUILayout.LabelField("공통 이벤트", EditorStyles.boldLabel);
            foreach (var common in index.CommonEvents.Where(x => Matches(x.commonId + " " + x.displayName)).Take(250))
                AssetButton(common, common.displayName + " · " + common.commonId);
            EditorGUILayout.LabelField("NPC 정의", EditorStyles.boldLabel);
            foreach (var npc in index.Npcs.Where(x => Matches(x.npcId + " " + x.displayName)).Take(250))
                AssetButton(npc, npc.displayName + " · " + npc.npcId);
            EditorGUILayout.LabelField("NPC 경로", EditorStyles.boldLabel);
            foreach (var route in index.NpcRoutes.Where(x => Matches(x.routeId + " " + x.name)).Take(250))
                AssetButton(route, route.name + " · " + route.routeId);
        }
        void DrawKeys(string label, System.Collections.Generic.IReadOnlyList<string> values, ContentReferenceCategory kind)
        {
            EditorGUILayout.LabelField(label + " · " + values.Count, EditorStyles.boldLabel);
            foreach (string key in values.Where(Matches).Take(250))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(key, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("사용 위치", GUILayout.Width(72)))
                    { search = key; category = kind; filterCategory = true; tab = 1; }
                }
            }
        }
        void DrawReferences()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                filterCategory = EditorGUILayout.ToggleLeft("종류로 좁히기", filterCategory, GUILayout.Width(130));
                category = (ContentReferenceCategory)EditorGUILayout.EnumPopup(category);
            }
            int rows = 0;
            foreach (var row in index.References)
            {
                if (filterCategory && row.Category != category) continue;
                if (!Matches(row.Key + " " + row.Message + " " + row.Owner?.name)) continue;
                if (++rows > 250) { EditorGUILayout.LabelField("최대 250행입니다. 검색을 좁히세요."); break; }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(row.Category + " · " + row.Key + (row.IsWrite ? " · 쓰기" : " · 읽기/참조"), EditorStyles.boldLabel);
                    DrawLocation(row); EditorGUILayout.LabelField(row.Message, EditorStyles.wordWrappedMiniLabel);
                    if (row.Target != null) EditorGUILayout.ObjectField("대상", row.Target, typeof(UnityEngine.Object), false);
                }
            }
        }
        void DrawDiagnostics()
        {
            EditorGUILayout.HelpBox("미선언 읽기 키는 오타 가능성 경고입니다. 자유 키 작성은 허용되며 이 경고만으로 실행이나 저장을 막지 않습니다.", MessageType.Info);
            int rows = 0;
            foreach (var diagnostic in index.GetDiagnostics().Concat(NpcContentValidator.CollectDiagnostics(ContentReferenceIndex.LoadAssets())))
            {
                if (!Matches(diagnostic.ToString())) continue;
                if (++rows > 250) { EditorGUILayout.LabelField("최대 250행입니다. 검색을 좁히세요."); break; }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox(diagnostic.Message, diagnostic.Severity == ContentDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
                    DrawLocation(diagnostic.Reference);
                }
            }
        }
        void DrawRegistry()
        {
            var selected = (StateKeyRegistry)EditorGUILayout.ObjectField("선언 에셋", registry, typeof(StateKeyRegistry), false);
            if (selected != registry) { registry = selected; registryEditor = registry != null ? new SerializedObject(registry) : null; }
            bool locked = EditorApplication.isPlayingOrWillChangePlaymode;
            if (locked) EditorGUILayout.HelpBox("Play 중에는 제작 선언 편집을 잠급니다. 카탈로그와 사용 위치는 계속 조회할 수 있습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(locked))
            if (GUILayout.Button("새 선언 목록 만들기"))
            {
                string path = EditorUtility.SaveFilePanelInProject("상태 키 선언 목록", "StateKeyRegistry", "asset", "Assets/NoSleep 아래에 저장하세요.", "Assets/NoSleep/Data");
                if (!string.IsNullOrEmpty(path))
                {
                    if (!path.StartsWith("Assets/NoSleep/", StringComparison.Ordinal))
                        EditorUtility.DisplayDialog("콘텐츠 경로", "목록 조회와 검증에 포함되도록 Assets/NoSleep 아래에 저장하세요.", "확인");
                    else
                    {
                        registry = CreateInstance<StateKeyRegistry>();
                        AssetDatabase.CreateAsset(registry, AssetDatabase.GenerateUniqueAssetPath(path)); AssetDatabase.SaveAssets();
                        registryEditor = new SerializedObject(registry); Refresh();
                    }
                }
            }
            foreach (var value in index.Registries)
                if (value != registry && GUILayout.Button(value.name)) { registry = value; registryEditor = new SerializedObject(value); }
            if (registry == null) return;
            registryEditor ??= new SerializedObject(registry);
            registryEditor.Update();
            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUILayout.PropertyField(registryEditor.FindProperty("switchKeys"), true);
                EditorGUILayout.PropertyField(registryEditor.FindProperty("variableKeys"), true);
                EditorGUILayout.PropertyField(registryEditor.FindProperty("itemIds"), true);
                EditorGUILayout.PropertyField(registryEditor.FindProperty("mapLinks"), true);
                if (registryEditor.ApplyModifiedProperties()) Refresh();
                if (GUILayout.Button("에셋 저장")) { AssetDatabase.SaveAssets(); Refresh(); }
            }
        }
        static void AssetButton(ScriptableObject asset, string label)
        {
            if (GUILayout.Button(label, EditorStyles.linkLabel))
            {
                if (asset is CommonEventDefinition || asset is EventDefinition) EventEditorWindow.Open(asset);
                else { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
            }
        }
        public static void DrawLocation(ContentReference row)
        {
            if (row?.Owner == null) return;
            string location = row.Owner.name + (row.PageIndex >= 0 ? " · 페이지 " + (row.PageIndex + 1) : "") +
                (row.CommandIndex >= 0 ? " · 명령 " + (row.CommandIndex + 1) : "");
            if (GUILayout.Button(location, EditorStyles.linkLabel)) OpenReference(row);
        }
        public static void OpenReference(ContentReference row)
        {
            if (row?.Owner == null) return;
            if (row.Owner is EventDefinition || row.Owner is CommonEventDefinition)
                EventEditorWindow.Open(row.Owner, row.PageIndex, row.CommandIndex);
            else if (row.Owner is MapDefinition map) MapEditorWindow.Open(map, map.spawnCell);
            else { Selection.activeObject = row.Owner; EditorGUIUtility.PingObject(row.Owner); }
        }
    }
}
