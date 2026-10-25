using System;
using System.IO;
using System.Linq;
using ProjectCRT.Cases;
using ProjectCRT.Cutscenes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Cases.Editor
{
    public static class ShangrilaCaseBuilder
    {
        public const string ScenePath = "Assets/Scenes/Shangrila_CaseTest.unity";
        const string Folder = "Assets/Settings/Cases";
        [MenuItem("Tools/ProjectCRT/Cases/Create Shangrila Arrival Test")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Case test already exists; authored files will not be overwritten.");
            for (int i=0; i<SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the current scene changes before creating the case test.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Settings", "Cases");
            if (!AssetDatabase.CopyAsset("Assets/Scenes/Shangrila_CutsceneTest.unity", ScenePath)) throw new Exception("Could not copy the cutscene scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var walking = UnityEngine.Object.FindFirstObjectByType<StreetWalkContext>();
            var director = UnityEngine.Object.FindFirstObjectByType<CutsceneDirector>();
            foreach (var demo in UnityEngine.Object.FindObjectsByType<CutsceneDemoControls>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(demo);
            var root = new GameObject("Case - Shangrila Arrival");
            var manager = root.AddComponent<ShangrilaCaseManager>(); manager.walking = walking; manager.director = director;
            manager.introduction = director.defaultSequence;
            var original = AssetDatabase.LoadAssetAtPath<InvestigationItemDatabase>("Assets/resources/Investigation/InvestigationItemDatabase.asset");
            var database = UnityEngine.Object.Instantiate(original); database.name = "ShangrilaArrivalItems";
            AssetDatabase.CreateAsset(database, Folder + "/ShangrilaArrivalItems.asset"); manager.itemDatabase = database;
            foreach (var npc in UnityEngine.Object.FindObjectsByType<NPC_script>(FindObjectsSortMode.None))
                if (npc.GetComponent<NpcInvestigationController>() != null) npc.gameObject.AddComponent<CaseInteractionGate>().manager = manager;
            var entrance = new GameObject("First Entry - Interior Threshold", typeof(BoxCollider), typeof(ShangrilaEntranceTrigger));
            entrance.transform.SetParent(root.transform);
            entrance.transform.position = new Vector3(walking.entranceInside.position.x, 1.1f, 7.55f);
            var trigger = entrance.GetComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(1.35f, 2.1f, 1.0f);
            entrance.GetComponent<ShangrilaEntranceTrigger>().manager = manager;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/resources/font/NeoDunggeunmoPro-Regular 1 SDF.asset");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = "Case test sign";
            material.color = new Color(.46f,.42f,.33f); material.SetFloat("_Smoothness",0);
            AssetDatabase.CreateAsset(material, Folder + "/CaseSign.mat");
            AddProp(root.transform, manager, database, font, material, "arrival_notice", "영업 안내문", "샹그리아의 영업 안내",
                "샹그리아의 영업 안내문이다. 먼저 안에 있는 사람들과 이야기를 나눠 보자.", new Vector3(-7.65f,.2f,8.8f), 270);
            AddProp(root.transform, manager, database, font, material, "arrival_menu", "메뉴판", "바에 놓인 메뉴",
                "몇 가지 음료 이름이 적혀 있다. 아직 사건의 단서인지 판단할 만한 내용은 없다.", new Vector3(-3.2f,.2f,11.6f), 0);
            var canvas = walking.locationText.canvas;
            manager.objectiveText = Hud(canvas.transform, "Case Objective", font, new Vector2(26,-65), 19);
            manager.saveText = Hud(canvas.transform, "Case Save Status", font, new Vector2(26,-94), 17);
            manager.objectiveText.text = "첫 방문 · 샹그리아 안으로 들어가자";
            manager.saveText.text = "F5 저장 · F9 불러오기 · Tab 조사 기록";
            walking.player.transform.SetPositionAndRotation(walking.streetSpawn.position, walking.streetSpawn.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(walking.player.transform);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            var scenes=EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s=>s.path==ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Directory.CreateDirectory("Logs/Cases"); File.WriteAllText("Logs/Cases/setup.txt", "Created " + ScenePath + "\nStreet start; interior-only entry trigger; two gated NPCs and two gated inspection props.\nF5 save, F9 load, Tab inventory; one case slot owns milestone, inventory and player pose.\n");
            Selection.activeGameObject = root;
        }
        static void AddProp(Transform parent, ShangrilaCaseManager manager, InvestigationItemDatabase database,
            TMP_FontAsset font, Material material, string id, string label, string summary, string detail, Vector3 position, float yaw)
        {
            var item=ScriptableObject.CreateInstance<InvestigationItemDefinition>(); item.name=id;
            var settings=new SerializedObject(item);
            settings.FindProperty("id").stringValue=id; settings.FindProperty("category").enumValueIndex=(int)InvestigationItemCategory.Information;
            settings.FindProperty("displayName").stringValue=label; settings.FindProperty("summary").stringValue=summary;
            settings.FindProperty("detail").stringValue=detail; settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(item,Folder+"/"+id+".asset");
            var db=new SerializedObject(database); var list=db.FindProperty("items"); int index=list.arraySize;
            list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue=item; db.ApplyModifiedPropertiesWithoutUndo();
            var dialogue=ScriptableObject.CreateInstance<DialogueObject>(); dialogue.name=id+"_inspection";
            dialogue.lines=new[]{new DialogueObject.DialogueLine{characterName="",text=detail,CharInterval=.035f,OnLineStart=new UnityEvent()}};
            dialogue.OnStart=new UnityEvent(); dialogue.OnEnd=new UnityEvent();
            AssetDatabase.CreateAsset(dialogue,Folder+"/"+id+"_inspection.asset");
            var root=new GameObject(label+" - 조사 테스트",typeof(BoxCollider),typeof(InspectableProp),typeof(CaseInteractionGate));
            root.transform.SetParent(parent); root.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            var collider=root.GetComponent<BoxCollider>(); collider.center=new Vector3(0,.68f,0); collider.size=new Vector3(.68f,1.35f,.22f);
            var prop=root.GetComponent<InspectableProp>(); prop.HintName=label;
            var so=new SerializedObject(prop); so.FindProperty("informationDefinition").objectReferenceValue=item;
            so.FindProperty("onInteractDialogue").objectReferenceValue=dialogue; so.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CaseInteractionGate>().manager=manager;
            Box(root.transform,"Base",new Vector3(0,.04f,0),new Vector3(.45f,.08f,.35f),material);
            Box(root.transform,"Stand",new Vector3(0,.55f,0),new Vector3(.07f,1.05f,.07f),material);
            Box(root.transform,"Board",new Vector3(0,1.12f,0),new Vector3(.65f,.43f,.07f),material);
            var text=new GameObject("Label",typeof(TextMeshPro)).GetComponent<TextMeshPro>(); text.transform.SetParent(root.transform,false);
            text.rectTransform.sizeDelta=new Vector2(.58f,.36f); text.transform.localPosition=new Vector3(0,1.12f,-.043f);
            text.font=font; text.fontSize=.8f; text.text=label; text.alignment=TextAlignmentOptions.Center;
            text.color=new Color(.91f,.88f,.74f); text.raycastTarget=false;
        }
        static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube); box.name=name; box.transform.SetParent(parent,false);
            box.transform.localPosition=position; box.transform.localScale=scale;
            box.GetComponent<Renderer>().sharedMaterial=material; UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        }
        static TMP_Text Hud(Transform parent,string name,TMP_FontAsset font,Vector2 position,float size)
        {
            var text=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent,false); text.font=font; text.fontSize=size; text.color=new Color(.85f,.9f,.94f);
            text.raycastTarget=false; text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(0,1);
            text.rectTransform.anchoredPosition=position; text.rectTransform.sizeDelta=new Vector2(1050,28); return text;
        }
    }
}
