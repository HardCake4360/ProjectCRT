using System;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class AuthoringSamplesBuilder
    {
        public const string Folder = "Assets/NoSleep/Data/Authoring";
        [MenuItem("No Sleep/Create Authoring Templates")]
        public static void BuildAndValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before creating authoring templates.");
            EnsureFolder(Folder + "/Templates");
            var refs = new EventTemplateReferences
            {
                speaker = "보관인", text = "준비한 표식을 확인했다.", rewardItemId = "lab_token", rewardItemAmount = 1, rewardGold = 5,
                condition = new StateCondition { kind = ConditionKind.Switch, key = "lab_watch_enabled", expected = true },
                destinationMap = AssetDatabase.LoadAssetAtPath<MapDefinition>(SystemsLabBuilder.Data + "Maps/Annex.asset"),
                nightActivity = AssetDatabase.LoadAssetAtPath<NightActivityDefinition>("Assets/NoSleep/Data/Horror/Rules/BandNight.asset"),
                enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/NoSleep/Data/Horror/Enemies/HallwayPresence.asset"),
                commonEvent = AssetDatabase.LoadAssetAtPath<CommonEventDefinition>(SystemsLabBuilder.Data + "Common/SharedWelcome.asset")
            };
            if (refs.destinationMap == null || refs.nightActivity == null || refs.enemy == null || refs.commonEvent == null)
                throw new InvalidOperationException("Create the Systems Lab and Horror Prototype before creating linked authoring templates.");
            refs.destinationCell = refs.destinationMap.spawnCell;
            int created = 0;
            foreach (EventTemplateKind kind in Enum.GetValues(typeof(EventTemplateKind)))
            {
                string path = Folder + "/Templates/" + kind + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<EventTemplateDefinition>(path);
                if (existing != null) continue;
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Unexpected existing asset: " + path);
                var template = EventTemplates.Create(kind, refs);
                if (template.GetDiagnostics().Count > 0) { UnityEngine.Object.DestroyImmediate(template); throw new InvalidOperationException("Invalid linked template: " + kind); }
                AssetDatabase.CreateAsset(template, path); created++;
            }
            string registryPath = Folder + "/StateKeys.asset";
            if (AssetDatabase.LoadAssetAtPath<StateKeyRegistry>(registryPath) == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(registryPath) != null) throw new InvalidOperationException("Unexpected existing asset: " + registryPath);
                var registry = ScriptableObject.CreateInstance<StateKeyRegistry>();
                registry.switchKeys.Add("lab_watch_enabled"); registry.variableKeys.Add("lab_reward_count"); registry.itemIds.Add("lab_token");
                AssetDatabase.CreateAsset(registry, registryPath);
            }
            AssetDatabase.SaveAssets(); DemoBuilder.ValidateContent();
            Debug.Log("NO_SLEEP_AUTHORING_TEMPLATES_VALID: " + created + " new templates; existing authoring assets preserved.");
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }
    }
}
