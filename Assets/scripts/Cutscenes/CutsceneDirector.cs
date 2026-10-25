using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Cutscenes
{
    public enum CutscenePlaybackState { Idle, Preparing, Entering, Typing, Waiting, Finishing, Loading }
    public enum CutsceneResult { Completed, Skipped, Cancelled, Failed }

    /// <summary>Owns the entire cutscene session, including input, loading, and exactly-once cleanup.</summary>
    public sealed class CutsceneDirector : MonoBehaviour
    {
        public static CutsceneDirector Current {get;private set;}
        public static bool IsActive=>Current!=null&&Current.IsPlaying;
        public CutsceneStage stage;
        public CutsceneView view;
        public CutsceneSequence defaultSequence;
        public KeyCode advanceKey=KeyCode.E;
        public KeyCode skipKey=KeyCode.Escape;
        public KeyCode voiceComparisonKey=KeyCode.F8;
        public CutscenePlaybackState State{get;private set;}
        public bool IsPlaying=>State!=CutscenePlaybackState.Idle;
        public bool IsTyping=>State==CutscenePlaybackState.Typing;
        public int CurrentTurnIndex{get;private set;}=-1;
        public CutsceneSequence CurrentSequence{get;private set;}
        public event Action<CutsceneResult> Finished;
        public event Action<CutsceneResult> Committed;
        public string LastError{get;private set;}
        CutsceneVoicePlayer voice;
        readonly CutsceneTimelineSampler timeline=new CutsceneTimelineSampler();
        CutsceneStage initialStage;
        PlayerControler sourcePlayer;
        bool playerEnabled,inputGate,cursorVisible,ownsLoadedStage,cleaning,sourceStageCaptured;
        CursorLockMode cursorLock;
        MainState savedMainState;
        Camera sourceCamera;
        Vector3 sourceCameraPosition;
        Quaternion sourceCameraRotation;
        float sourceFov;
        Scene sourceScene,loadedStage;
        readonly List<GameObject> hiddenRoots=new List<GameObject>();
        readonly List<Behaviour> cameraOwners=new List<Behaviour>();
        readonly List<bool> cameraOwnerStates=new List<bool>();
        readonly Dictionary<string,Scene> retainedStages=new Dictionary<string,Scene>();
        bool stopRequested,revealRequested,advanceRequested;
        CutsceneResult requestedResult;
        CutsceneVoiceMode comparison=CutsceneVoiceMode.Inherit;

        public bool Play(CutsceneSequence sequence,int startTurn=0)
        {
            if(IsPlaying||IsActive||sequence==null||sequence.turns==null||sequence.turns.Count==0||startTurn<0||startTurn>=sequence.turns.Count)return false;
            if(NpcInvestigationController.CurrentInteraction!=null || (DialogueManager.Instance!=null&&DialogueManager.Instance.IsDialogueRunning))return Reject("Finish the current conversation before starting a cutscene.");
            var activePlayer=MainLoop.Instance!=null?MainLoop.Instance.PC:(stage!=null?stage.player:null);
            if(activePlayer!=null&&activePlayer.cameraTransitionInProgress)return Reject("Wait for the current camera transition to finish.");
            if(string.IsNullOrEmpty(sequence.stageScene))
            {
                if(stage==null)stage=FindFirstObjectByType<CutsceneStage>();
                if(stage==null)return Reject("No cutscene stage is assigned.");
                var errors=stage.Validate(sequence);if(errors.Count>0)return Reject(string.Join("\n",errors));
            }
            else if(!Application.CanStreamedLevelBeLoaded(sequence.stageScene))return Reject("Cutscene stage is not in the build scene list: "+sequence.stageScene);
            if(sequence.exitMode==CutsceneExitMode.LoadScene && (string.IsNullOrEmpty(sequence.destinationScene)||!Application.CanStreamedLevelBeLoaded(sequence.destinationScene)))return Reject("Destination scene is not in the build scene list: "+sequence.destinationScene);
            if(transform.parent!=null)return Reject("Place CutsceneDirector on a root GameObject.");
            if(view==null)view=GetComponent<CutsceneView>()??gameObject.AddComponent<CutsceneView>();
            voice=GetComponent<CutsceneVoicePlayer>()??gameObject.AddComponent<CutsceneVoicePlayer>();
            Current=this;CurrentSequence=sequence;CurrentTurnIndex=-1;State=CutscenePlaybackState.Preparing;LastError=null;
            stopRequested=revealRequested=advanceRequested=false;cleaning=false;requestedResult=CutsceneResult.Completed;comparison=CutsceneVoiceMode.Inherit;
            SaveSession();
            if(!string.IsNullOrEmpty(sequence.stageScene)||sequence.exitMode==CutsceneExitMode.LoadScene)DontDestroyOnLoad(gameObject);
            StartCoroutine(Guarded(Playback(startTurn)));return true;
        }
        bool Reject(string error){LastError=error;Debug.LogWarning("[Cutscene] "+error,this);return false;}
        void SaveSession()
        {
            sourceScene=SceneManager.GetActiveScene();initialStage=stage;loadedStage=default;ownsLoadedStage=false;sourceStageCaptured=false;hiddenRoots.Clear();
            sourcePlayer=MainLoop.Instance!=null?MainLoop.Instance.PC:(stage!=null?stage.player:FindFirstObjectByType<PlayerControler>());
            sourceCamera=sourcePlayer!=null?sourcePlayer.GetComponentInChildren<Camera>():Camera.main;
            if(sourceCamera!=null){sourceCameraPosition=sourceCamera.transform.position;sourceCameraRotation=sourceCamera.transform.rotation;sourceFov=sourceCamera.fieldOfView;}
            playerEnabled=sourcePlayer!=null&&sourcePlayer.enabled;inputGate=sourcePlayer!=null&&sourcePlayer.cameraTransitionInProgress;
            savedMainState=MainLoop.Instance!=null?MainLoop.Instance.MainLoopState:MainState.Main;
            if(savedMainState==MainState.Interact)savedMainState=MainState.Main;
            cursorLock=Cursor.lockState;cursorVisible=Cursor.visible;
            if(sourcePlayer!=null){sourcePlayer.cameraTransitionInProgress=true;sourcePlayer.HideHint();sourcePlayer.enabled=false;}
            if(MainLoop.Instance!=null)MainLoop.Instance.SetMainLoopState(MainState.Freeze);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;view.Begin();
        }
        void Update()
        {
            if(!IsPlaying)return;
            if(Input.GetKeyDown(advanceKey)||Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))Advance();
            if(Input.GetKeyDown(skipKey))Skip();
            if(Input.GetKeyDown(voiceComparisonKey))
            {
                comparison=comparison==CutsceneVoiceMode.Blip?CutsceneVoiceMode.Syllable:CutsceneVoiceMode.Blip;
                voice.Stop();
            }
        }
        public void Advance(){if(IsTyping)revealRequested=true;else if(State==CutscenePlaybackState.Waiting)advanceRequested=true;}
        public void Skip(){if(IsPlaying&&CurrentSequence.allowSkip&&!cleaning){stopRequested=true;requestedResult=CutsceneResult.Skipped;}}
        public void Cancel(){if(IsPlaying&&!cleaning){stopRequested=true;requestedResult=CutsceneResult.Cancelled;}}

        IEnumerator Playback(int startTurn)
        {
            if(CurrentSequence.beginWithBlack)view.SetBlack(1);
            else yield return FadeBlack(0,1,CurrentSequence.fadeDuration);
            if(!stopRequested&&!string.IsNullOrEmpty(CurrentSequence.stageScene))
            {
                State=CutscenePlaybackState.Loading;view.SetLoading("컷씬 준비 중…");
                if(initialStage!=null&&sourceCamera!=null){initialStage.Capture(sourceCamera,false);sourceStageCaptured=true;}
                var existingScenes=new HashSet<int>();for(int s=0;s<SceneManager.sceneCount;s++)existingScenes.Add(SceneManager.GetSceneAt(s).handle);
                if(retainedStages.TryGetValue(CurrentSequence.stageScene,out var cached)&&cached.IsValid()&&cached.isLoaded)
                {loadedStage=cached;foreach(var root in loadedStage.GetRootGameObjects())root.SetActive(true);retainedStages.Remove(CurrentSequence.stageScene);}
                else
                {
                    var op=SceneManager.LoadSceneAsync(CurrentSequence.stageScene,LoadSceneMode.Additive);
                    if(op==null)throw new InvalidOperationException("Unable to load cutscene stage.");
                    while(!op.isDone){view.SetLoading("컷씬 준비 중… "+Mathf.RoundToInt(op.progress*100)+"%");yield return null;}
                    for(int s=0;s<SceneManager.sceneCount;s++){var candidate=SceneManager.GetSceneAt(s);if(!existingScenes.Contains(candidate.handle)){loadedStage=candidate;break;}}
                }
                if(!loadedStage.IsValid())loadedStage=SceneManager.GetSceneByPath(CurrentSequence.stageScene);
                if(!loadedStage.IsValid())loadedStage=SceneManager.GetSceneByName(System.IO.Path.GetFileNameWithoutExtension(CurrentSequence.stageScene));
                ownsLoadedStage=true;
                stage=loadedStage.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CutsceneStage>(true)).FirstOrDefault();
                if(stage==null||stage.output==null)throw new InvalidOperationException("Loaded cutscene scene needs a CutsceneStage and output camera.");
                foreach(var root in sourceScene.GetRootGameObjects())if(root!=gameObject&&root.activeSelf){hiddenRoots.Add(root);root.SetActive(false);}
            }
            if(!stopRequested)
            {
                var errors=stage.Validate(CurrentSequence);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
                foreach(var clip in CurrentSequence.preloadAudio)if(clip!=null)clip.LoadAudioData();
                float audioDeadline=Time.realtimeSinceStartup+10;
                while(CurrentSequence.preloadAudio.Any(c=>c!=null&&c.loadState==AudioDataLoadState.Loading)&&!stopRequested)
                {if(Time.realtimeSinceStartup>audioDeadline)throw new InvalidOperationException("Cutscene audio preparation timed out.");yield return null;}
                if(CurrentSequence.preloadAudio.Any(c=>c!=null&&c.loadState==AudioDataLoadState.Failed))throw new InvalidOperationException("A cutscene audio clip failed to load.");
                stage.Capture(stage.output,false);SuspendCameraOwners(stage.output);
                if(startTurn>0)stage.Reconstruct(CurrentSequence,startTurn,0);
                yield return null; // Resources and first camera pose settle behind the loading cover.
                view.SetLoading("");
                yield return FadeBlack(1,0,CurrentSequence.fadeDuration);
                for(int i=startTurn;i<CurrentSequence.turns.Count&&!stopRequested;i++)yield return PlayTurn(i);
            }
            yield return Finish(stopRequested?requestedResult:CutsceneResult.Completed);
        }
        IEnumerator PlayTurn(int index)
        {
            CurrentTurnIndex=index;var turn=CurrentSequence.turns[index];
            view.Hide();voice.ResetLine();revealRequested=advanceRequested=false;
            stage.BeginTurn(turn);timeline.Open(stage,turn);
            float elapsed=0,textStart=-1,completedAt=-1;bool opened=false,complete=!turn.hasDialogue;int previousCount=0;
            int total=CutsceneTextUtility.CharacterCount(turn.text);
            float cameraDuration=stage.CameraDuration(turn);
            State=CutscenePlaybackState.Entering;
            while(!stopRequested)
            {
                stage.EvaluateTurn(turn,elapsed);timeline.Evaluate(elapsed);stage.RefreshCamera(elapsed);
                bool cameraReady=elapsed>=cameraDuration;
                if(turn.hasDialogue&&!opened&&(!turn.dialogueAfterCamera||cameraReady))
                {
                    opened=true;view.SetLine(CutsceneTextUtility.SpeakerName(turn,stage),turn.text,turn.dialogueKind);view.Show();
                }
                if(opened&&textStart<0&&view.FadeComplete){textStart=elapsed;State=CutscenePlaybackState.Typing;}
                if(textStart>=0&&!complete)
                {
                    int count=revealRequested?total:CutsceneTextUtility.RevealCount(turn.text,elapsed-textStart,turn.charsPerSecond,turn.punctuationPause);
                    view.Reveal(count);
                    if(count>previousCount&&!revealRequested&&total>0)
                    {
                        var actor=stage.FindActor(turn.speakerId);var mode=turn.voiceMode;
                        if(mode==CutsceneVoiceMode.Inherit&&turn.dialogueKind!=CutsceneDialogueKind.Spoken)mode=CutsceneVoiceMode.Silent;
                        if(comparison!=CutsceneVoiceMode.Inherit&&mode!=CutsceneVoiceMode.Silent)mode=comparison;
                        voice.Reveal(CutsceneTextUtility.CharacterAt(turn.text,Mathf.Min(count,total)-1),count-1,actor!=null?actor.voice:null,mode);
                    }
                    previousCount=count;
                    if(count>=total){complete=true;completedAt=elapsed;if(revealRequested)voice.Stop();}
                    revealRequested=false;
                }
                stage.SetDialogueProgress(previousCount,complete);stage.EvaluateTurn(turn,elapsed);timeline.Evaluate(elapsed);stage.RefreshCamera(elapsed);
                bool actionsReady=elapsed>=turn.minimumDuration&&(!turn.waitForCamera||cameraReady)&&(!turn.waitForActors||elapsed>=stage.ActorCompletionTime(turn))&&(!turn.waitForTimeline||elapsed>=timeline.Duration);
                if(complete)
                {
                    if(completedAt<0)completedAt=elapsed;
                    State=actionsReady?CutscenePlaybackState.Waiting:CutscenePlaybackState.Entering;
                    view.SetHint((actionsReady?"E / Enter 다음":"연출 진행 중…")+(CurrentSequence.allowSkip?"   ·   ESC 전체 건너뛰기":"")+"   ·   F8 음성 비교: "+(comparison==CutsceneVoiceMode.Syllable?"음절형":comparison==CutsceneVoiceMode.Blip?"반복음":"캐릭터 설정"));
                    if(actionsReady&&((turn.advance==CutsceneAdvanceMode.Auto&&elapsed-completedAt>=turn.autoDelay)||advanceRequested))break;
                }
                else if(textStart>=0)view.SetHint("E / Enter 대사 전체 표시"+(CurrentSequence.allowSkip?"   ·   ESC 전체 건너뛰기":""));
                yield return null;elapsed+=Time.unscaledDeltaTime;
            }
            if(!stopRequested)
            {
                stage.SetDialogueProgress(previousCount,complete,true);State=CutscenePlaybackState.Entering;
                float tail=stage.AdvanceDuration(turn),end=elapsed+tail;
                do
                {
                    stage.EvaluateTurn(turn,elapsed);timeline.Evaluate(elapsed);stage.RefreshCamera(elapsed);
                    if(elapsed>=end||stopRequested)break;
                    yield return null;elapsed+=Time.unscaledDeltaTime;
                }while(true);
            }
            voice.Stop();view.Hide();timeline.Dispose();
        }
        IEnumerator Finish(CutsceneResult result)
        {
            if(cleaning)yield break;cleaning=true;State=CutscenePlaybackState.Finishing;voice.Stop();view.Hide();timeline.Dispose();
            yield return FadeBlack(view.BlackOpacity,1,CurrentSequence.fadeDuration);
            if(stage!=null)stage.Restore();RestoreCameraOwners();
            bool committed=result==CutsceneResult.Completed||result==CutsceneResult.Skipped;
            if(committed)InvokeSafely(Committed,result);
            if(committed&&CurrentSequence.exitMode==CutsceneExitMode.LoadScene)
            {
                State=CutscenePlaybackState.Loading;view.SetLoading("다음 장소 준비 중…");
                var op=SceneManager.LoadSceneAsync(CurrentSequence.destinationScene,LoadSceneMode.Single);
                while(op!=null&&!op.isDone)yield return null;
                hiddenRoots.Clear();ownsLoadedStage=false;yield return null;
                var destination=SceneManager.GetActiveScene().GetRootGameObjects().Where(r=>r!=gameObject).SelectMany(r=>r.GetComponentsInChildren<CutsceneStage>(true)).FirstOrDefault(s=>s.output!=null);
                if(sourceStageCaptured&&initialStage!=null){initialStage.Restore();sourceStageCaptured=false;}
                var player=MainLoop.Instance!=null?MainLoop.Instance.PC:FindFirstObjectByType<PlayerControler>();
                if(player==null)player=FindFirstObjectByType<PlayerControler>();
                var spawn=destination!=null?destination.FindSpawn(CurrentSequence.destinationSpawnId):null;
                if(!string.IsNullOrEmpty(CurrentSequence.destinationSpawnId)&&spawn==null)throw new InvalidOperationException("Destination spawn is missing: "+CurrentSequence.destinationSpawnId);
                if(player!=null)
                {
                    if(spawn!=null){var cc=player.GetComponent<CharacterController>();bool enabled=cc!=null&&cc.enabled;if(cc!=null)cc.enabled=false;player.transform.SetPositionAndRotation(spawn.position,spawn.rotation);if(cc!=null)cc.enabled=enabled;}
                    player.cameraTransitionInProgress=false;player.enabled=true;
                    if(MainLoop.Instance!=null){MainLoop.Instance.PC=player;MainLoop.Instance.Cam=player.GetComponentInChildren<Camera>();}
                }
                sourcePlayer=null;sourceCamera=null;savedMainState=MainState.Main;cursorLock=CursorLockMode.Locked;cursorVisible=false;
                stage=destination;
            }
            else
            {
                if(ownsLoadedStage&&loadedStage.IsValid()&&loadedStage.isLoaded&&CurrentSequence.unloadStageOnFinish)
                {var op=SceneManager.UnloadSceneAsync(loadedStage);while(op!=null&&!op.isDone)yield return null;}
                else if(ownsLoadedStage&&loadedStage.IsValid())
                {foreach(var root in loadedStage.GetRootGameObjects())root.SetActive(false);retainedStages[CurrentSequence.stageScene]=loadedStage;}
                foreach(var root in hiddenRoots)if(root!=null)root.SetActive(true);hiddenRoots.Clear();
                if(sourceStageCaptured&&initialStage!=null){initialStage.Restore();sourceStageCaptured=false;}
                if(sourceScene.IsValid()&&sourceScene.isLoaded)SceneManager.SetActiveScene(sourceScene);
                stage=initialStage;
            }
            if(sourceCamera!=null){sourceCamera.transform.SetPositionAndRotation(sourceCameraPosition,sourceCameraRotation);sourceCamera.fieldOfView=sourceFov;}
            view.SetLoading("");
            yield return FadeBlack(1,0,CurrentSequence.fadeDuration);
            view.End();State=CutscenePlaybackState.Idle;Current=null;cleaning=false;CurrentTurnIndex=-1;
            RestoreInput();
            if(gameObject.scene.name=="DontDestroyOnLoad"&&SceneManager.GetActiveScene().IsValid())SceneManager.MoveGameObjectToScene(gameObject,SceneManager.GetActiveScene());
            InvokeSafely(Finished,result);
        }
        IEnumerator FadeBlack(float from,float to,float duration)
        {
            float elapsed=0;view.SetBlack(from);
            while(elapsed<duration){elapsed+=Time.unscaledDeltaTime;float t=duration<=0?1:Mathf.Clamp01(elapsed/duration);view.SetBlack(Mathf.Lerp(from,to,t*t*(3-2*t)));yield return null;}
            view.SetBlack(to);
        }
        void SuspendCameraOwners(Camera camera)
        {
            cameraOwners.Clear();cameraOwnerStates.Clear();
            foreach(var behaviour in camera.GetComponents<Behaviour>())
                if(behaviour.GetType().FullName=="Unity.Cinemachine.CinemachineBrain")
                {cameraOwners.Add(behaviour);cameraOwnerStates.Add(behaviour.enabled);behaviour.enabled=false;}
        }
        void RestoreCameraOwners(){for(int i=0;i<cameraOwners.Count;i++)if(cameraOwners[i]!=null)cameraOwners[i].enabled=cameraOwnerStates[i];cameraOwners.Clear();cameraOwnerStates.Clear();}
        void RestoreInput()
        {
            if(sourceCamera!=null){sourceCamera.transform.SetPositionAndRotation(sourceCameraPosition,sourceCameraRotation);sourceCamera.fieldOfView=sourceFov;}
            if(sourcePlayer!=null)
            {
                sourcePlayer.cameraTransitionInProgress=inputGate;sourcePlayer.enabled=playerEnabled;
                if(MainLoop.Instance!=null){MainLoop.Instance.PC=sourcePlayer;MainLoop.Instance.Cam=sourceCamera;}
            }
            if(MainLoop.Instance!=null)MainLoop.Instance.SetMainLoopState(savedMainState);
            Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;
        }
        IEnumerator Guarded(IEnumerator task)
        {
            var stack=new Stack<IEnumerator>();stack.Push(task);Exception failure=null;
            while(stack.Count>0)
            {
                bool more=false;object next=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
                catch(Exception e){failure=e;break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(next is IEnumerator nested){stack.Push(nested);continue;}
                yield return next;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            if(failure!=null)
            {
                LastError=failure.ToString();Debug.LogException(failure,this);
                EmergencyRestore();view.End();State=CutscenePlaybackState.Idle;Current=null;cleaning=false;
                InvokeSafely(Finished,CutsceneResult.Failed);
            }
        }
        void EmergencyRestore()
        {
            timeline.Dispose();if(stage!=null)stage.Restore();RestoreCameraOwners();
            foreach(var root in hiddenRoots)if(root!=null)root.SetActive(true);hiddenRoots.Clear();
            if(sourceStageCaptured&&initialStage!=null){initialStage.Restore();sourceStageCaptured=false;}
            if(ownsLoadedStage&&loadedStage.IsValid()&&loadedStage.isLoaded)SceneManager.UnloadSceneAsync(loadedStage);
            stage=initialStage;RestoreInput();if(voice!=null)voice.Stop();
        }
        void OnDisable(){if(IsPlaying){StopAllCoroutines();EmergencyRestore();if(view!=null)view.End();State=CutscenePlaybackState.Idle;if(Current==this)Current=null;}}
        static void InvokeSafely(Action<CutsceneResult> action,CutsceneResult result)
        {
            if(action==null)return;foreach(Action<CutsceneResult> callback in action.GetInvocationList())try{callback(result);}catch(Exception e){Debug.LogException(e);}
        }
    }
}
