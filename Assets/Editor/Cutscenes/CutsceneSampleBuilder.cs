using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ProjectCRT.Cutscenes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Cutscenes.Editor
{
    public static class CutsceneSampleBuilder
    {
        public const string ScenePath="Assets/Scenes/Shangrila_CutsceneTest.unity";
        public const string SequencePath="Assets/Settings/Cutscenes/Shangrila_Introduction.asset";
        const string Folder="Assets/Settings/Cutscenes";
        [MenuItem("Tools/ProjectCRT/Create Shangrila Cutscene Sample")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before creating the sample.");
            if(File.Exists(ScenePath) || File.Exists(SequencePath))throw new InvalidOperationException("The cutscene sample already exists. Open Shangrila_CutsceneTest or Cutscene Studio to edit it; existing authored files will not be overwritten.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save or discard the current scene changes before creating the sample. The builder does not save the original scene.");
            EnsureFolder(Folder);EnsureFolder(Folder+"/Voices");
            if(!AssetDatabase.CopyAsset("Assets/Scenes/Outdoor_BarStreetTest.unity",ScenePath))throw new InvalidOperationException("Could not copy the existing outdoor scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var context=UnityEngine.Object.FindFirstObjectByType<StreetWalkContext>();
            if(context==null || context.player==null)throw new InvalidOperationException("The outdoor scene needs its existing street-walking player.");
            var root=new GameObject("Cutscene System");var stage=root.AddComponent<CutsceneStage>();
            stage.output=context.player.GetComponentInChildren<Camera>();stage.player=context.player;
            var view=root.AddComponent<CutsceneView>();view.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/resources/font/NeoDunggeunmoPro-Regular 1 SDF.asset");
            root.AddComponent<CutsceneVoicePlayer>();var director=root.AddComponent<CutsceneDirector>();director.stage=stage;director.view=view;
            var npcs=UnityEngine.Object.FindObjectsByType<NPC_script>(FindObjectsSortMode.None).Where(n=>n.GetComponent<NpcInvestigationController>()!=null).ToArray();
            var rebeccaSource=npcs.FirstOrDefault(n=>n.name.IndexOf("reb",StringComparison.OrdinalIgnoreCase)>=0);
            var jeremySource=npcs.FirstOrDefault(n=>n.name.IndexOf("jer",StringComparison.OrdinalIgnoreCase)>=0);
            if(rebeccaSource==null || jeremySource==null)throw new InvalidOperationException("The scene must contain Rebecca and Jeremy.");
            var rebecca=Actor(stage,rebeccaSource,"rebecca","레베카",MakeVoice("Rebecca",205,1.05f));
            var jeremy=Actor(stage,jeremySource,"jeremy","제레미",MakeVoice("Jeremy",155,0.88f));
            FaceShot(stage,rebecca,rebeccaSource,"rebecca_close");FaceShot(stage,jeremy,jeremySource,"jeremy_close");
            var center=Marker(root.transform,"Look - Room center",new Vector3(-3.4f,1.25f,10.6f));
            stage.spawns.Add(new CutsceneSpawn{id="room_center",target=center});
            stage.spawns.Add(new CutsceneSpawn{id="rebecca_mark",target=Marker(root.transform,"Actor mark - Rebecca",rebecca.transform.position,rebecca.transform.rotation)});
            stage.spawns.Add(new CutsceneSpawn{id="jeremy_mark",target=Marker(root.transform,"Actor mark - Jeremy",jeremy.transform.position,jeremy.transform.rotation)});
            stage.spawns.Add(new CutsceneSpawn{id="player_return",target=Marker(root.transform,"Return - Original street spawn",context.streetSpawn.position,context.streetSpawn.rotation)});
            var overview=Marker(root.transform,"Shot - Room overview",new Vector3(-3.9f,2.02f,7.3f));overview.LookAt(center);
            var wide=overview.gameObject.AddComponent<CutsceneCameraPreset>();wide.id="room_wide";wide.fieldOfView=61;wide.lookAt=center;stage.shots.Add(wide);
            var pathRoot=Marker(root.transform,"Path - Entrance approach",Vector3.zero);var path=pathRoot.gameObject.AddComponent<CutscenePath>();path.id="entrance_approach";path.fieldOfView=56;path.lookAt=center;
            path.knots=new[]{Marker(pathRoot,"Knot 0",new Vector3(-5.44f,1.7f,7.35f)),Marker(pathRoot,"Knot 1",new Vector3(-4.9f,1.72f,8.1f)),Marker(pathRoot,"Knot 2",new Vector3(-4.2f,1.68f,8.8f)),Marker(pathRoot,"Knot 3",new Vector3(-3.55f,1.65f,9.1f))};stage.paths.Add(path);
            var sequence=ScriptableObject.CreateInstance<CutsceneSequence>();sequence.sequenceId="shangrila-introduction";sequence.fadeDuration=.3f;
            sequence.turns=SampleTurns(rebecca,jeremy);AssetDatabase.CreateAsset(sequence,SequencePath);director.defaultSequence=sequence;
            var controls=root.AddComponent<CutsceneDemoControls>();controls.director=director;controls.sequence=sequence;
            var errors=stage.Validate(sequence);if(errors.Count>0)throw new InvalidOperationException("Sample validation failed: "+string.Join("; ",errors));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var scenes=EditorBuildSettings.scenes.ToList();AddBuildScene(scenes,ScenePath);AddBuildScene(scenes,"Assets/Scenes/Outdoor_BarStreetTest.unity");EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/Cutscenes");
            File.WriteAllText("Logs/Cutscenes/setup.txt","Created Shangrila_CutsceneTest from the outdoor scene without editing the original.\nF6: play sample; E/Enter/Space: reveal/advance; Escape: skip; F8: compare blips and synthesized vowel-like placeholders.\n8 turns: silent establishing cut and route, Rebecca dialogue, hidden-name thought, Jeremy dialogue, pose/look-at change, zoom while speaking, automatic closing shot.\nVoice files are original synthesized placeholders for comparing rhythm/timbre, not intelligible Korean speech.\nActual model BlendShapes were mapped when present; no unavailable facial expressions were invented.\nCutscene Studio supports full/selected-turn Edit Mode preview.\n");
            Selection.activeObject=sequence;
        }
        static CutsceneActor Actor(CutsceneStage stage,NPC_script source,string id,string name,CutsceneVoiceProfile voice)
        {
            var actor=source.gameObject.AddComponent<CutsceneActor>();actor.id=id;actor.displayName=name;actor.voice=voice;actor.animator=source.GetComponentInChildren<Animator>();
            if(actor.animator!=null && actor.animator.isHuman)actor.lookAtBone=actor.animator.GetBoneTransform(HumanBodyBones.Head);
            var so=new SerializedObject(source);
            foreach(var entry in new[]{("idle","idleClip"),("greeting","onInteractionClip"),("conversation_idle","onInteractionIdleClip"),("thinking_start","startThinkingClip"),("thinking","thinkingClip"),("talking","onAnswerClip")})
            {
                var clip=so.FindProperty(entry.Item2)?.objectReferenceValue as AnimationClip;
                if(clip!=null)actor.poses.Add(new CutscenePosePreset{id=entry.Item1,clip=clip});
            }
            // Expressions describe only capabilities that exist on this model.
            var neutral=new CutsceneExpressionPreset{id="neutral"};
            foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(skin.sharedMesh==null)continue;
                for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                {
                    string shape=skin.sharedMesh.GetBlendShapeName(i);neutral.shapes.Add(new CutsceneBlendShapeValue{renderer=skin,shape=shape,weight=0});
                    actor.expressions.Add(new CutsceneExpressionPreset{id=skin.name+"/"+shape,shapes=new List<CutsceneBlendShapeValue>{new CutsceneBlendShapeValue{renderer=skin,shape=shape,weight=100}}});
                }
            }
            if(neutral.shapes.Count>0)actor.expressions.Insert(0,neutral);
            stage.actors.Add(actor);return actor;
        }
        static void FaceShot(CutsceneStage stage,CutsceneActor actor,NPC_script npc,string id)
        {
            var original=npc.GetComponentInChildren<ShangrilaConversationShot>(true);
            if(original==null)throw new InvalidOperationException("Configure the existing conversation shot for "+npc.name+" first.");
            var marker=Marker(stage.transform,"Shot - "+actor.displayName,original.transform.position,original.transform.rotation);
            var preset=marker.gameObject.AddComponent<CutsceneCameraPreset>();preset.id=id;preset.fieldOfView=34;
            preset.lookAt=actor.lookAtBone;
            if(preset.lookAt!=null){preset.lookAtOffset=original.focusOffset;preset.framingOffset=new Vector2(.12f,0);}
            var fill=new GameObject("Sample face fill",typeof(Light));fill.transform.SetParent(marker,false);var light=fill.GetComponent<Light>();
            light.type=LightType.Spot;light.intensity=1.5f;light.range=3;light.spotAngle=75;light.innerSpotAngle=40;light.color=new Color(1,.86f,.73f);light.shadows=LightShadows.None;
            stage.shots.Add(preset);
        }
        static List<CutsceneTurn> SampleTurns(CutsceneActor rebecca,CutsceneActor jeremy)
        {
            var greeting=rebecca.FindPose("greeting")?.clip;var thinking=rebecca.FindPose("thinking")?.clip;var talking=rebecca.FindPose("talking")?.clip;
            var result=new List<CutsceneTurn>
            {
                new CutsceneTurn{id="establish",label="01 · 샹그리아 전경",hasDialogue=false,advance=CutsceneAdvanceMode.Auto,minimumDuration=.5f,autoDelay=0,camera=new CutsceneCameraCue{mode=CutsceneCameraMode.Cut,shotId="room_wide"}},
                new CutsceneTurn{id="approach",label="02 · 입구에서 실내로",hasDialogue=false,advance=CutsceneAdvanceMode.Auto,autoDelay=0,waitForCamera=true,camera=new CutsceneCameraCue{mode=CutsceneCameraMode.Path,pathId="entrance_approach",joinDuration=.35f,duration=1.8f}},
                new CutsceneTurn{id="rebecca-greeting",label="03 · 레베카 인사",speakerId="rebecca",text="샹그리아에 오신 걸 환영해요. 오늘은 조금 조용하네요.",camera=new CutsceneCameraCue{mode=CutsceneCameraMode.Blend,shotId="rebecca_close",duration=.7f},actors=new List<CutsceneActorCue>{new CutsceneActorCue{actorId="rebecca",clip=greeting,loop=false,changeLookAt=true,lookAtId="camera"}}},
                new CutsceneTurn{id="protagonist-thought",label="04 · 주인공 속마음",dialogueKind=CutsceneDialogueKind.Thought,nameMode=CutsceneNameMode.Hidden,voiceMode=CutsceneVoiceMode.Silent,text="조용하다… 그 말이 묘하게 마음에 걸린다."},
                new CutsceneTurn{id="jeremy-answer",label="05 · 제레미 대답",speakerId="jeremy",text="아…! 그… 어서 오세요. 무슨 일로 오셨나요?",camera=new CutsceneCameraCue{mode=CutsceneCameraMode.Cut,shotId="jeremy_close"}},
                new CutsceneTurn{id="rebecca-thinking",label="06 · 레베카 포즈와 응시",speakerId="rebecca",text="천천히 이야기해요. 기억나는 것부터 말해 드릴게요.",camera=new CutsceneCameraCue{mode=CutsceneCameraMode.Rough,shotId="rebecca_close",duration=.25f},actors=new List<CutsceneActorCue>{new CutsceneActorCue{actorId="rebecca",clip=thinking,loop=true,changeLookAt=true,lookAtId="camera",expressionId=rebecca.HasExpression("neutral")?"neutral":""}}},
                new CutsceneTurn{id="rebecca-zoom",label="07 · 말하는 동안 줌인",speakerId="rebecca",text="하지만… 그날 밤의 일은 아직도 또렷하게 기억나요.",dialogueAfterCamera=false,waitForCamera=true,camera=new CutsceneCameraCue{mode=CutsceneCameraMode.CutThenZoom,shotId="rebecca_close",duration=1.8f,useEndFieldOfView=true,endFieldOfView=28},actors=new List<CutsceneActorCue>{new CutsceneActorCue{actorId="rebecca",clip=talking,loop=true}}},
                new CutsceneTurn{id="closing",label="08 · 전경으로 마무리",hasDialogue=false,advance=CutsceneAdvanceMode.Auto,autoDelay=.2f,minimumDuration=.8f,camera=new CutsceneCameraCue{mode=CutsceneCameraMode.Blend,shotId="room_wide",duration=.7f}}
            };
            return result;
        }
        static CutsceneVoiceProfile MakeVoice(string name,float fundamental,float pitch)
        {
            string assetPath=Folder+"/Voices/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<CutsceneVoiceProfile>(assetPath);if(existing!=null)return existing;
            var voice=ScriptableObject.CreateInstance<CutsceneVoiceProfile>();voice.displayName=name;voice.mode=CutsceneVoiceMode.Blip;voice.pitch=pitch;voice.volume=.3f;voice.minimumInterval=.04f;
            voice.blips=new AudioClip[3];
            for(int i=0;i<3;i++)voice.blips[i]=Tone(name+"_blip_"+i,fundamental*(1+i*.075f),.065f,0,false);
            var vowels=new AudioClip[7];for(int i=0;i<vowels.Length;i++)vowels[i]=Tone(name+"_vowel_"+i,fundamental,.1f,i,true);
            int[] map={0,0,0,0,1,1,1,1,2,2,2,2,2,3,3,3,3,3,4,4,5};
            for(int i=0;i<21;i++)voice.syllables.Add(new CutsceneSyllableSample{key="v"+i,clip=vowels[map[i]]});
            voice.syllables.Add(new CutsceneSyllableSample{key="*",clip=vowels[6]});AssetDatabase.CreateAsset(voice,assetPath);return voice;
        }
        static AudioClip Tone(string name,float frequency,float seconds,int vowel,bool formant)
        {
            string path=Folder+"/Voices/"+name+".wav";if(File.Exists(path))return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            const int rate=22050;int count=Mathf.RoundToInt(rate*seconds);var pcm=new short[count];
            float[] f1={730,530,570,440,300,270,480},f2={1090,1840,840,1020,870,2290,1400};
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate,u=i/(float)(count-1);double sample=0;
                if(formant)
                    for(int h=1;h<=16;h++){float hz=frequency*h;double a=.32/h+Math.Exp(-Math.Pow((hz-f1[vowel])/180,2))*.4+Math.Exp(-Math.Pow((hz-f2[vowel])/260,2))*.2;sample+=Math.Sin(2*Math.PI*hz*t)*a;}
                else sample=Math.Sin(2*Math.PI*frequency*t)+.3*Math.Sin(2*Math.PI*frequency*2*t);
                float envelope=Mathf.Min(1,u/.1f)*Mathf.Min(1,(1-u)/.25f);pcm[i]=(short)(Math.Max(-1,Math.Min(1,sample*.24*envelope))*short.MaxValue);
            }
            using(var stream=File.Create(path))using(var writer=new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);foreach(short sample in pcm)writer.Write(sample);
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static Transform Marker(Transform parent,string name,Vector3 position,Quaternion? rotation=null)
        {
            var marker=new GameObject(name).transform;marker.SetParent(parent);marker.SetPositionAndRotation(position,rotation??Quaternion.identity);return marker;
        }
        static void AddBuildScene(List<EditorBuildSettingsScene> scenes,string path){if(!scenes.Any(s=>s.path==path))scenes.Add(new EditorBuildSettingsScene(path,true));}
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');var parent=path.Substring(0,slash);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(slash+1));
        }
    }
}
