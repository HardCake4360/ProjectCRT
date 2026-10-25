using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectCRT.Prologue.Editor
{
    public static partial class SC01CutsceneAuthoring
    {
        public static void BatchFinalize()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath);stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var seq=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SequencePath);var head=stage.FindActor("san").lookAtBone;
                SetShot(2,new Vector3(90.05f,5.3f,.18f),head.position,58,head,new Vector3(0,-.08f,0));SetShot(4,new Vector3(90.0f,5.26f,.04f),head.position,60,head,new Vector3(0,-.08f,0));
                var faceFill=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Single(l=>l.name=="San cabin soft fill");faceFill.intensity=.8f;faceFill.transform.position=new Vector3(90.1f,5.4f,.25f);
                var bank=AssetDatabase.LoadAssetAtPath<CutsceneVoiceProfile>("Assets/Settings/Cutscenes/Voices/Jeremy.asset");
                if(bank!=null)foreach(var actor in new[]{stage.FindActor("san"),stage.FindActor("driver")}){actor.voice.syllables=bank.syllables.Select(s=>new CutsceneSyllableSample{key=s.key,clip=s.clip}).ToList();EditorUtility.SetDirty(actor.voice);}
                var set=stage.GetComponent<SC01PlaybackController>().filmingSets[1];
                if(!set.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Cabin window city backdrop 0"))
                {
                    const string asset="Assets/ThirdParty/Prologue/ElegantCrowSkyscrapers/Source/skyscraper_pack/models/building_04.2.fbx";
                    var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Prologue/Imported building_04.2 building_04.mat");
                    var parentPosition=set.localPosition;set.localPosition=Vector3.zero;
                    var positions=new[]{new Vector3(102,-2,-8),new Vector3(108,-2,7),new Vector3(96,-2,-24)};
                    for(int i=0;i<positions.Length;i++)
                    {
                        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(asset));PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.name="Cabin window city backdrop "+i;model.transform.SetParent(set,true);var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);model.transform.localScale*=8/Mathf.Max(bounds.size.x,bounds.size.z);bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);model.transform.position+=positions[i]-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                        foreach(var r in renderers)if(material!=null)r.sharedMaterials=r.sharedMaterials.Select(_=>material).ToArray();
                    }
                    set.localPosition=parentPosition;
                }
                stage.shots[0].Sample().Apply(stage.output);EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);AssetDatabase.SaveAssets();File.WriteAllText("Logs/SC01/finalize-status.txt","PASS");SC01CutsceneVerification.BatchPreview();
            }
            catch(Exception e){File.WriteAllText("Logs/SC01/finalize-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchLayoutCheck()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath);stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var seq=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SequencePath);
                var sample=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/fbx/San/San.fbx"));sample.transform.SetPositionAndRotation(new Vector3(300,0,0),Quaternion.identity);CalibrateSanMaterials(sample.transform);foreach(var a in sample.GetComponentsInChildren<Animator>())a.enabled=false;
                foreach(int side in new[]{-1,1}){stage.output.transform.position=new Vector3(300,1.1f,side*2.3f);stage.output.transform.LookAt(new Vector3(300,.8f,0));stage.output.fieldOfView=45;SC01CutsceneVerification.Capture(stage.output,"san-orientation-"+side);}
                UnityEngine.Object.DestroyImmediate(sample);
                stage.Capture(stage.output,true);stage.Reconstruct(seq,1,.1f);
                var cabin=Find("Interior taxi - same hero model");var filter=cabin.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Car_Base");
                string path=Folder+"/TaxiInteriorCameraClearance.asset";var derived=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(derived==null)
                {
                    derived=UnityEngine.Object.Instantiate(filter.sharedMesh);derived.name="SC01 interior filming - passenger seat clearance";var points=derived.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();var volume=new Bounds(new Vector3(90.36f,5.06f,.05f),new Vector3(.60f,.96f,1.1f));int removed=0;
                    for(int sub=0;sub<derived.subMeshCount;sub++)
                    {
                        var source=derived.GetTriangles(sub);var result=new List<int>();
                        for(int i=0;i<source.Length;i+=3){if(volume.Contains(points[source[i]])&&volume.Contains(points[source[i+1]])&&volume.Contains(points[source[i+2]])){removed++;continue;}result.Add(source[i]);result.Add(source[i+1]);result.Add(source[i+2]);}
                        derived.SetTriangles(result,sub);
                    }
                    AssetDatabase.CreateAsset(derived,path);File.WriteAllText("Logs/SC01/cabin-clearance.txt","Removed passenger-seat triangles only in a derived interior filming mesh: "+removed+". Original Car.fbx is unchanged.");
                }
                filter.sharedMesh=derived;
                var head=stage.FindActor("san").lookAtBone;var positions=new[]{new Vector3(90.02f,5.26f,.02f),new Vector3(89.92f,5.27f,-.12f),new Vector3(90.05f,5.3f,.18f)};
                for(int i=0;i<positions.Length;i++){SetShot(2,positions[i],head.position,58,head,new Vector3(0,-.08f,0));stage.Reconstruct(seq,5,2);stage.RefreshCamera(2);SC01CutsceneVerification.Capture(stage.output,"clearance-camera-"+i);}
                stage.Restore();SetShot(2,positions[0],head.position,58,head,new Vector3(0,-.08f,0));SetShot(4,positions[1],head.position,60,head,new Vector3(0,-.08f,0));
                stage.shots[0].Sample().Apply(stage.output);EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText("Logs/SC01/layout-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchSeatFit()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath);stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();
                if(File.Exists("Logs/SC01/seat-fit-applied.txt"))throw new Exception("Seat correction already applied.");
                foreach(string name in new[]{"SanSeatB","SanTalkB","SanNodB","SanSeatC","SanTalkC","SanArrive","SanPay","SanExit"})
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Animation/"+name+".anim");
                    var binding=EditorCurveBinding.FloatCurve("Model",typeof(Transform),"m_LocalPosition.z");var curve=AnimationUtility.GetEditorCurve(clip,binding);
                    var keys=curve.keys;
                    for(int i=0;i<keys.Length;i++)keys[i].value+=.44f*(name=="SanExit"?1-Mathf.SmoothStep(0,1,keys[i].time/4.2f):1);
                    curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);EditorUtility.SetDirty(clip);
                }
                var actor=stage.FindActor("san");SetShot(2,new Vector3(89.9f,5.25f,-.60f),actor.lookAtBone.position,60,actor.lookAtBone,new Vector3(0,-.06f,0));SetShot(4,new Vector3(89.9f,5.24f,-.72f),actor.lookAtBone.position,62,actor.lookAtBone,new Vector3(0,-.06f,0));
                var sequence=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SequencePath);
                foreach(var cue in sequence.turns[0].actors)if(cue.clip!=null)cue.clip.SampleAnimation(stage.FindActor(cue.actorId).gameObject,0);
                stage.shots[0].Sample().Apply(stage.output);EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);AssetDatabase.SaveAssets();
                File.WriteAllText("Logs/SC01/seat-fit-applied.txt","Rear seat hip moved forward 0.44m, clear of the rear backrest; source FBX untouched.");SC01CutsceneVerification.BatchPreview();
            }
            catch(Exception e){File.WriteAllText("Logs/SC01/seat-fit-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void CalibrateSanMaterials(Transform model)
        {
            var copied=new Dictionary<Material,Material>();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var source=slots[i];if(source==null||!source.shader.name.Contains("ToonShader"))continue;
                    if(!copied.TryGetValue(source,out var material))
                    {
                        string path=Folder+"/San palette - "+source.name+".mat";material=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(material==null)
                        {
                            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="San original palette - "+source.name};
                            material.SetColor("_BaseColor",source.HasProperty("_Color")?source.GetColor("_Color"):source.color);material.SetFloat("_Smoothness",0);material.SetFloat("_Cull",0);material.doubleSidedGI=true;AssetDatabase.CreateAsset(material,path);
                        }
                        copied[source]=material;
                    }
                    slots[i]=material;
                }
                renderer.sharedMaterials=slots;
            }
        }
        public static void BatchPolish()
        {
            try
            {
                Directory.CreateDirectory("Logs/SC01/Previews");EditorSceneManager.OpenScene(ScenePath);stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();
                var sequence=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SequencePath);CalibrateSanMaterials(stage.FindActor("san").transform);
                var fill=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Single(l=>l.name=="San cabin soft fill");fill.intensity=.8f;
                var cabin=Find("Interior taxi - same hero model");Material clear=null;
                foreach(var renderer in cabin.GetComponentsInChildren<Renderer>(true))
                {
                    var slots=renderer.sharedMaterials;
                    for(int i=0;i<slots.Length;i++)if(slots[i].name.ToLowerInvariant().Contains("carwindows"))
                    {
                        if(clear==null){string path=Folder+"/SC01 clear cabin glass.mat";clear=AssetDatabase.LoadAssetAtPath<Material>(path);if(clear==null){clear=new Material(slots[i]);clear.name="SC01 clear cabin glass";var color=clear.GetColor("_BaseColor");color.a=.015f;clear.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(clear,path);}}
                        slots[i]=clear;
                    }
                    renderer.sharedMaterials=slots;
                }
                var candidates=new[]{new Vector3(90.1f,5.30f,-.55f),new Vector3(90.15f,5.32f,-.68f),new Vector3(90.65f,5.28f,-.4f),new Vector3(90.78f,5.30f,-.8f),new Vector3(89.95f,5.42f,-.5f)};
                var head=stage.FindActor("san").lookAtBone;
                stage.Capture(stage.output,true);
                for(int i=0;i<candidates.Length;i++)
                {
                    SetShot(2,candidates[i],head.position,55,head,new Vector3(0,-.08f,0));
                    stage.Reconstruct(sequence,sequence.turns.FindIndex(t=>t.id=="sc01-d03"),2);stage.RefreshCamera(2);SC01CutsceneVerification.Capture(stage.output,"tune-d03-"+i);
                }
                stage.Restore();SetShot(2,candidates[0],head.position,55,head,new Vector3(0,-.08f,0));SetShot(4,candidates[1],head.position,58,head,new Vector3(0,-.08f,0));
                // Independently hinged CD case; the original case base and disc are retained.
                if(stage.FindActor("cd_case")==null)
                {
                    var original=Find("CD case");var container=new GameObject("Model");original.transform.SetParent(container.transform,true);var rig=MakeRig("CD case performance","cd_case","",container);
                    var hinge=new GameObject("Case lid hinge").transform;hinge.SetParent(rig.model);hinge.position=new Vector3(90.35f,4.98f,.585f);
                    Primitive(hinge,"Case lid",new Vector3(0,.0025f,-.065f),new Vector3(.14f,.005f,.13f),Mat("CD case lid",new Color(.12f,.17f,.19f)));
                    rig.CaptureRest();stage.actors.Add(rig.actor);var clip=SaveClip("CDCaseOpen",rig,4.8f,t=>hinge.localRotation=Quaternion.Euler(110*Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.9f)),0,0));
                    sequence.turns.Single(t=>t.id=="sc01-cd").actors.Add(new CutsceneActorCue{actorId="cd_case",clip=clip,loop=false});
                }
                EditorUtility.SetDirty(sequence);EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);AssetDatabase.SaveAssets();
                File.WriteAllText("Logs/SC01/polish-status.txt","PASS");SC01CutsceneVerification.BatchPreview();
            }
            catch(Exception e){File.WriteAllText("Logs/SC01/polish-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
