using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectCRT.Cutscenes.Editor
{
    /// <summary>Read-only playback uses the same absolute stage sampler as runtime.</summary>
    public sealed class CutsceneStudioWindow : EditorWindow
    {
        [SerializeField] CutsceneSequence sequence;
        [SerializeField] CutsceneStage stage;
        [SerializeField] int selectedTurn;
        [SerializeField] float playbackSpeed=1;
        [SerializeField] bool autoAdvance=true;
        [SerializeField] float readingHold=1.5f;
        [SerializeField] bool showDialogue=true;
        [SerializeField] bool editTurn;
        Vector2 listScroll, inspectorScroll;
        Camera previewCamera;
        RenderTexture previewTexture;
        bool captured, playing, selectedOnly, needsRender;
        float turnTime;
        double lastUpdate;
        string message;
        List<string> validation=new List<string>();
        const float WindowFade=0.28f;

        [MenuItem("Tools/ProjectCRT/Cutscene Studio")]
        public static void Open()
        {
            var window=GetWindow<CutsceneStudioWindow>("Cutscene Studio");
            if(window.sequence==null)window.sequence=Selection.activeObject as CutsceneSequence??AssetDatabase.LoadAssetAtPath<CutsceneSequence>("Assets/Settings/Cutscenes/Shangrila_Introduction.asset");
            Rect host=EditorGUIUtility.GetMainWindowPosition();
            float width=Mathf.Clamp(host.width-80,960,1200),height=Mathf.Clamp(host.height-100,620,800);
            window.position=new Rect(host.center.x-width/2,host.center.y-height/2,width,height);
            if(window.stage==null)window.FindStage();window.Show();
        }

        void OnEnable()
        {
            minSize=new Vector2(960,620);
            EditorApplication.update+=EditorUpdate;
            EditorApplication.playModeStateChanged+=PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload+=StopPreview;
            EditorSceneManager.sceneClosing+=SceneClosing;
            lastUpdate=EditorApplication.timeSinceStartup;
        }
        void OnDisable()
        {
            StopPreview();
            EditorApplication.update-=EditorUpdate;
            EditorApplication.playModeStateChanged-=PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload-=StopPreview;
            EditorSceneManager.sceneClosing-=SceneClosing;
        }
        void SceneClosing(UnityEngine.SceneManagement.Scene scene,bool removingScene)
        {
            if(stage!=null && stage.gameObject.scene==scene)StopPreview();
        }
        void PlayModeChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode)StopPreview();}
        void OnSelectionChange()
        {
            StopPreview();
            if(Selection.activeObject is CutsceneSequence selected){sequence=selected;selectedTurn=0;}
            Repaint();
        }

        void OnGUI()
        {
            if(Count>0)selectedTurn=Mathf.Clamp(selectedTurn,0,Count-1);
            DrawHeader();
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(320)))DrawTurnList();
                using(new EditorGUILayout.VerticalScope())
                {
                    DrawPlaybackControls();
                    DrawPreview();
                    DrawTurnDetails();
                }
            }
            HandleKeys();
        }

        void DrawHeader()
        {
            EditorGUILayout.LabelField("컷씬 대본과 무대 미리보기",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(captured))
            {
                EditorGUI.BeginChangeCheck();
                sequence=(CutsceneSequence)EditorGUILayout.ObjectField("대본",sequence,typeof(CutsceneSequence),false);
                stage=(CutsceneStage)EditorGUILayout.ObjectField("현재 씬의 무대",stage,typeof(CutsceneStage),true);
                if(EditorGUI.EndChangeCheck()){selectedTurn=0;turnTime=0;validation.Clear();}
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUI.DisabledScope(captured))
                {
                    if(GUILayout.Button("씬에서 무대 찾기",GUILayout.Width(150)))FindStage();
                    if(GUILayout.Button("대본 검사",GUILayout.Width(110)))Validate();
                }
                if(sequence!=null && GUILayout.Button("대본 Inspector",GUILayout.Width(135))){StopPreview();Selection.activeObject=sequence;}
                GUILayout.FlexibleSpace();
                if(captured)GUILayout.Label("미리보기 중 · 씬/대본 편집 잠금",EditorStyles.boldLabel);
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("에디터 미리보기는 Play 모드를 종료한 다음 사용하세요.",MessageType.Info);
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
            foreach(string error in validation)EditorGUILayout.HelpBox(error,MessageType.Warning);
        }

        void FindStage()
        {
            var stages=UnityEngine.Object.FindObjectsByType<CutsceneStage>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            if(stages.Length==1){stage=stages[0];message=null;}
            else message=stages.Length==0?"현재 씬에 CutsceneStage가 없습니다. 테스트 컷씬 씬을 여세요.":"무대가 여러 개입니다. 사용할 CutsceneStage를 직접 선택하세요.";
        }
        void Validate()
        {
            validation.Clear();
            if(sequence==null){validation.Add("대본을 선택하세요.");return;}
            if(stage==null){validation.Add("현재 열린 씬의 무대를 연결하세요.");return;}
            validation=stage.Validate(sequence);
            if(validation.Count==0)message="검사 통과: 등장인물·카메라·경로 연결을 확인했습니다.";
        }

        int Count => sequence!=null && sequence.turns!=null?sequence.turns.Count:0;
        CutsceneTurn Current => Count>0?sequence.turns[Mathf.Clamp(selectedTurn,0,Count-1)]:null;
        void DrawTurnList()
        {
            EditorGUILayout.LabelField("턴 목록",EditorStyles.boldLabel);
            listScroll=EditorGUILayout.BeginScrollView(listScroll,GUILayout.MinHeight(140));
            for(int i=0;i<Count;i++)
            {
                var turn=sequence.turns[i];
                string label=turn==null?"누락된 턴":string.IsNullOrWhiteSpace(turn.label)?turn.hasDialogue?turn.text:"연출":turn.label;
                if(label!=null && label.Length>26)label=label.Substring(0,26)+"…";
                var style=new GUIStyle(EditorStyles.miniButton){alignment=TextAnchor.MiddleLeft,fontStyle=i==selectedTurn?FontStyle.Bold:FontStyle.Normal};
                Color old=GUI.backgroundColor;if(i==selectedTurn)GUI.backgroundColor=new Color(0.55f,0.8f,1);
                if(GUILayout.Button($"{i+1:00}  {label}",style,GUILayout.Height(30)))SelectTurn(i);
                GUI.backgroundColor=old;
            }
            EditorGUILayout.EndScrollView();
            using(new EditorGUI.DisabledScope(captured||sequence==null))
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("턴 추가"))AddTurn();
                    using(new EditorGUI.DisabledScope(Current==null))if(GUILayout.Button("선택 턴 복제"))DuplicateTurn();
                }
                if(GUILayout.Button("Scene View → 선택 카메라 프리셋"))CaptureSceneView();
            }
            EditorGUILayout.HelpBox("원하는 턴을 선택해 구도를 확인하세요. 앞선 턴의 포즈와 카메라 상태도 재구성합니다. 미리보기는 씬 이동·진행 저장·이벤트를 실행하지 않습니다.",MessageType.None);
            DrawVoiceAudition();
            EditorGUILayout.HelpBox("영상 미리보기는 무음입니다. 선택한 대사는 아래 음성 미리듣기로 별도 비교할 수 있습니다. 미리듣기는 최대 30초이며 음성 프로필의 볼륨을 사용합니다.",MessageType.None);
        }

        void DrawVoiceAudition()
        {
            EditorGUILayout.LabelField("선택 대사 음성 비교",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(Current==null||!Current.hasDialogue||Current.dialogueKind!=CutsceneDialogueKind.Spoken||stage==null||EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if(GUILayout.Button("반복음 미리듣기"))Audition(CutsceneVoiceMode.Blip);
                if(GUILayout.Button("음절형 미리듣기"))Audition(CutsceneVoiceMode.Syllable);
            }
            using(new EditorGUI.DisabledScope(!CutsceneVoiceAudition.IsPlaying))
                if(GUILayout.Button("소리 중지"))CutsceneVoiceAudition.Stop();
        }
        void Audition(CutsceneVoiceMode mode)
        {
            string error=CutsceneVoiceAudition.Play(Current,stage,mode);
            message=error??((mode==CutsceneVoiceMode.Blip?"반복음":"음절형")+" 미리듣기 · "+CutsceneVoiceAudition.LastDuration.ToString("0.0")+"초"+(CutsceneVoiceAudition.WasTruncated?" (최대 30초까지 재생)":""));
        }

        void DrawPlaybackControls()
        {
            using(new EditorGUI.DisabledScope(Current==null||stage==null||EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("전체 재생")){selectedTurn=0;StartPreview(false);}
                    if(GUILayout.Button("선택 턴 재생"))StartPreview(true);
                    if(GUILayout.Button("여기서부터 재생"))StartPreview(false);
                    using(new EditorGUI.DisabledScope(!captured))
                    {
                        if(GUILayout.Button(playing?"일시정지":"계속")){playing=!playing;lastUpdate=EditorApplication.timeSinceStartup;}
                        if(GUILayout.Button("중지 · 복원"))StopPreview();
                    }
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("◀ 이전",GUILayout.Width(80)))SelectTurn(selectedTurn-1);
                    if(GUILayout.Button("다음 ▶",GUILayout.Width(80)))AdvancePreview();
                    autoAdvance=GUILayout.Toggle(autoAdvance,"자동 넘김",GUILayout.Width(95));
                    GUILayout.Label("속도",GUILayout.Width(30));
                    playbackSpeed=EditorGUILayout.Slider(playbackSpeed,0.1f,2,GUILayout.MinWidth(110));
                    showDialogue=GUILayout.Toggle(showDialogue,"대사창",GUILayout.Width(65));
                }
                readingHold=EditorGUILayout.Slider("자동 넘김 읽기 여유(초)",readingHold,0,6);
                float duration=Current!=null?Duration(Current):1;
                EditorGUI.BeginChangeCheck();
                float sampled=EditorGUILayout.Slider($"{selectedTurn+1}/{Count} 턴 · 시간",turnTime,0,Mathf.Max(0.1f,duration));
                if(EditorGUI.EndChangeCheck())
                {
                    if(!captured)CapturePreview();
                    playing=false;turnTime=sampled;Evaluate();
                }
            }
        }

        void StartPreview(bool onlySelected)
        {
            if(Current==null)return;
            if(!captured && !CapturePreview())return;
            selectedOnly=onlySelected;turnTime=0;playing=true;lastUpdate=EditorApplication.timeSinceStartup;Evaluate();
            message="Space/Enter: 대사 전체 표시 또는 다음 턴 · 중지하면 원래 씬 상태로 복원됩니다.";
        }
        bool CapturePreview()
        {
            if(sequence==null||stage==null||EditorApplication.isPlayingOrWillChangePlaymode)return false;
            Validate();if(validation.Count>0)return false;
            try
            {
                var source=stage.output!=null?stage.output:Camera.main;
                if(source==null)throw new InvalidOperationException("무대의 출력 카메라가 없습니다.");
                var go=new GameObject("Cutscene Studio Preview Camera"){hideFlags=HideFlags.HideAndDontSave};
                previewCamera=go.AddComponent<Camera>();previewCamera.CopyFrom(source);previewCamera.enabled=false;
                previewCamera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                var sourceData=source.GetComponent<UniversalAdditionalCameraData>();
                if(sourceData!=null)
                {
                    var copied=go.AddComponent<UniversalAdditionalCameraData>();EditorUtility.CopySerialized(sourceData,copied);
                    copied.renderType=CameraRenderType.Base;copied.cameraStack.Clear();
                }
                captured=true;stage.Capture(previewCamera,true);needsRender=true;return true;
            }
            catch(Exception e){message="미리보기를 시작하지 못했습니다: "+e.Message;StopPreview();return false;}
        }
        void SelectTurn(int index)
        {
            if(Count==0)return;
            CutsceneVoiceAudition.Stop();
            selectedTurn=Mathf.Clamp(index,0,Count-1);turnTime=0;playing=false;
            if(captured)Evaluate();
        }
        void AdvancePreview()
        {
            if(Current==null)return;
            if(!captured && !CapturePreview())return;
            float textEnd=TextStart(Current)+CutsceneTextUtility.TextDuration(Current.text,Current.charsPerSecond,Current.punctuationPause);
            if(Current.hasDialogue && turnTime<textEnd){turnTime=textEnd;Evaluate();return;}
            if(selectedTurn+1>=Count){playing=false;return;}
            selectedTurn++;turnTime=0;Evaluate();
        }
        void EditorUpdate()
        {
            double now=EditorApplication.timeSinceStartup;
            float delta=(float)(now-lastUpdate);
            if(!captured || !playing || Current==null){lastUpdate=now;return;}
            if(delta<1f/30f)return;
            lastUpdate=now;
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode){StopPreview();return;}
            turnTime+=Mathf.Min(0.25f,delta)*playbackSpeed;
            if(autoAdvance && turnTime>=Duration(Current))
            {
                if(!selectedOnly && selectedTurn+1<Count){selectedTurn++;turnTime=0;}
                else {turnTime=Duration(Current);playing=false;}
            }
            Evaluate();Repaint();
        }
        void Evaluate()
        {
            if(!captured||stage==null||sequence==null)return;
            try{stage.Reconstruct(sequence,selectedTurn,turnTime);needsRender=true;}
            catch(Exception e){message="미리보기 중단: "+e.Message;StopPreview();}
            SceneView.RepaintAll();Repaint();
        }
        public void StopPreview()
        {
            CutsceneVoiceAudition.Stop();
            playing=false;
            try{if(captured&&stage!=null)stage.Restore();}
            finally
            {
                captured=false;
                if(previewCamera!=null)DestroyImmediate(previewCamera.gameObject);
                if(previewTexture!=null){previewTexture.Release();DestroyImmediate(previewTexture);}
                previewCamera=null;previewTexture=null;needsRender=false;Repaint();SceneView.RepaintAll();
            }
        }
        float TextStart(CutsceneTurn turn)=>turn.dialogueAfterCamera&&stage!=null?stage.CameraDuration(turn)+WindowFade:WindowFade;
        float Duration(CutsceneTurn turn)
        {
            if(turn==null)return 1;
            float duration=stage!=null?stage.PreviewDuration(turn):Mathf.Max(turn.minimumDuration,turn.hasDialogue?TextStart(turn)+CutsceneTextUtility.TextDuration(turn.text,turn.charsPerSecond,turn.punctuationPause):0)+turn.autoDelay;
            return duration+(turn.advance==CutsceneAdvanceMode.Manual?readingHold:0);
        }

        void DrawPreview()
        {
            Rect available=GUILayoutUtility.GetRect(320,340,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            float width=Mathf.Min(available.width,available.height*16f/9f);float height=width*9f/16f;
            Rect frame=new Rect(available.center.x-width/2,available.center.y-height/2,width,height);
            EditorGUI.DrawRect(frame,new Color(0.045f,0.05f,0.06f));
            if(captured && previewCamera!=null && Event.current.type==EventType.Repaint)
            {
                EnsureTexture(Mathf.Clamp(Mathf.RoundToInt(width*EditorGUIUtility.pixelsPerPoint),640,1920));
                if(needsRender)
                {
                    try
                    {
                        previewCamera.aspect=16f/9f;
                        if(GraphicsSettings.currentRenderPipeline!=null)
                            previewCamera.SubmitRenderRequest(new UniversalRenderPipeline.SingleCameraRequest{destination=previewTexture});
                        else {previewCamera.targetTexture=previewTexture;previewCamera.Render();previewCamera.targetTexture=null;}
                        needsRender=false;
                    }
                    catch(Exception e){message="미리보기 렌더링: "+e.Message;playing=false;}
                }
                if(previewTexture!=null)GUI.DrawTexture(frame,previewTexture,ScaleMode.StretchToFill,false);
            }
            else if(!captured)GUI.Label(frame,"대본과 무대를 연결한 다음 선택 턴을 재생하세요.",CenteredStyle());
            if(captured&&showDialogue&&Current!=null&&Current.hasDialogue)DrawDialogue(frame,Current);
        }
        void EnsureTexture(int width)
        {
            int height=Mathf.RoundToInt(width*9f/16f);
            if(previewTexture!=null&&previewTexture.width==width)return;
            if(previewTexture!=null){previewTexture.Release();DestroyImmediate(previewTexture);}
            previewTexture=new RenderTexture(width,height,24){hideFlags=HideFlags.HideAndDontSave,name="Cutscene Studio Frame"};
            previewTexture.Create();needsRender=true;
        }
        void DrawDialogue(Rect frame,CutsceneTurn turn)
        {
            float start=turn.dialogueAfterCamera?stage.CameraDuration(turn):0;
            float fade=Mathf.Clamp01((turnTime-start)/WindowFade);
            if(fade<=0)return;
            fade=fade*fade*(3-2*fade);
            Color old=GUI.color;GUI.color=new Color(1,1,1,fade);
            Rect panel=new Rect(frame.x+frame.width*0.055f,frame.y+frame.height*0.69f,frame.width*0.89f,frame.height*0.265f);
            EditorGUI.DrawRect(panel,new Color(0.035f,0.045f,0.065f,0.96f*fade));
            float scale=frame.width/1280;
            string speaker=CutsceneTextUtility.SpeakerName(turn,stage);
            var speakerStyle=new GUIStyle(EditorStyles.label){fontSize=Mathf.Max(12,Mathf.RoundToInt(22*scale)),normal={textColor=new Color(0.94f,0.78f,0.48f)}};
            var textStyle=new GUIStyle(EditorStyles.label){fontSize=Mathf.Max(12,Mathf.RoundToInt(25*scale)),fontStyle=turn.dialogueKind==CutsceneDialogueKind.Thought?FontStyle.Italic:FontStyle.Normal,wordWrap=true,richText=false,normal={textColor=new Color(0.92f,0.92f,0.85f)}};
            if(!string.IsNullOrEmpty(speaker))
            {
                var namePlate=new Rect(panel.x,panel.y-panel.height*0.21f,panel.width*0.28f,panel.height*0.21f);
                EditorGUI.DrawRect(namePlate,new Color(0.035f,0.045f,0.065f,0.96f*fade));
                GUI.Label(new Rect(namePlate.x+16*scale,namePlate.y,namePlate.width-24*scale,namePlate.height),speaker,speakerStyle);
            }
            int visible=turnTime<TextStart(turn)?0:CutsceneTextUtility.RevealCount(turn.text,Mathf.Max(0,turnTime-TextStart(turn)),turn.charsPerSecond,turn.punctuationPause);
            string text=CutsceneTextUtility.Prefix(turn.text,visible);
            GUI.Label(new Rect(panel.x+panel.width*0.035f,panel.y+panel.height*0.13f,panel.width*0.93f,panel.height*0.63f),text,textStyle);
            var hintStyle=new GUIStyle(EditorStyles.miniLabel){fontSize=Mathf.Max(9,Mathf.RoundToInt(14*scale)),alignment=TextAnchor.MiddleRight,normal={textColor=new Color(0.92f,0.92f,0.85f)}};
            GUI.Label(new Rect(panel.x+panel.width*0.035f,panel.y+panel.height*0.79f,panel.width*0.93f,panel.height*0.16f),"Enter / Space  ·  다음",hintStyle);
            GUI.color=old;
        }
        static GUIStyle CenteredStyle()=>new GUIStyle(EditorStyles.centeredGreyMiniLabel){wordWrap=true,alignment=TextAnchor.MiddleCenter};

        void DrawTurnDetails()
        {
            using(new EditorGUI.DisabledScope(captured))editTurn=EditorGUILayout.Foldout(editTurn,"선택 턴 편집 (미리보기 중 잠김)",true);
            if(!editTurn||Current==null)return;
            using(new EditorGUI.DisabledScope(captured))
            {
                inspectorScroll=EditorGUILayout.BeginScrollView(inspectorScroll,GUILayout.Height(230));
                var so=new SerializedObject(sequence);so.Update();
                var element=so.FindProperty("turns").GetArrayElementAtIndex(selectedTurn);
                EditorGUILayout.PropertyField(element,new GUIContent($"턴 {selectedTurn+1}"),true);
                so.ApplyModifiedProperties();EditorGUILayout.EndScrollView();
            }
        }
        void AddTurn()
        {
            Undo.RecordObject(sequence,"Add cutscene turn");
            sequence.turns.Add(new CutsceneTurn{id=Guid.NewGuid().ToString("N"),label="새 턴"});
            selectedTurn=sequence.turns.Count-1;EditorUtility.SetDirty(sequence);editTurn=true;
        }
        void DuplicateTurn()
        {
            Undo.RecordObject(sequence,"Duplicate cutscene turn");
            var duplicate=JsonUtility.FromJson<CutsceneTurn>(JsonUtility.ToJson(Current));duplicate.id=Guid.NewGuid().ToString("N");
            sequence.turns.Insert(selectedTurn+1,duplicate);selectedTurn++;EditorUtility.SetDirty(sequence);
        }
        void CaptureSceneView()
        {
            if(!(Selection.activeGameObject!=null && Selection.activeGameObject.TryGetComponent<CutsceneCameraPreset>(out var shot)))
            {message="Hierarchy에서 CutsceneCameraPreset 오브젝트를 선택한 다음 실행하세요.";return;}
            var view=SceneView.lastActiveSceneView;
            if(view==null||view.camera==null){message="Scene View를 먼저 여세요.";return;}
            Undo.RecordObjects(new UnityEngine.Object[]{shot.transform,shot},"Capture cutscene camera preset");
            shot.transform.SetPositionAndRotation(view.camera.transform.position,view.camera.transform.rotation);shot.fieldOfView=view.camera.fieldOfView;
            shot.lookAt=null;shot.lookAtOffset=Vector3.zero;shot.framingOffset=Vector2.zero;
            PrefabUtility.RecordPrefabInstancePropertyModifications(shot.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(shot);
            EditorUtility.SetDirty(shot);EditorSceneManager.MarkSceneDirty(shot.gameObject.scene);
            message="Scene View의 위치·각도·화각을 저장했습니다. 구도 유지를 위해 프리셋 자동 응시는 해제했습니다. 필요한 경우 Look At을 다시 연결하고 씬을 저장하세요.";
        }
        void HandleKeys()
        {
            if(!captured||EditorGUIUtility.editingTextField)return;
            var e=Event.current;
            if(e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Space||e.keyCode==KeyCode.Return)){AdvancePreview();e.Use();}
            else if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){StopPreview();e.Use();}
        }
    }
}
