#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace ProjectCRT.Cutscenes
{
    /// <summary>Integration checks for Timeline ownership and actual additive/single-scene loading.</summary>
    public sealed class CutsceneSceneVerification : MonoBehaviour
    {
        const string Sample="Assets/Scenes/Shangrila_CutsceneTest.unity";
        public bool Done {get;private set;}
        public string Failure {get;private set;}
        public readonly List<string> Checks=new List<string>();
        readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
        CutsceneDirector director;
        CutsceneStage initialStage;
        Scene sourceScene;
        Camera sourceCamera;
        PlayerControler sourcePlayer;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        float cameraFov;
        bool playerEnabled,inputGate;
        int finished,committed;
        CutsceneResult lastResult;

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more=false;object next=null;
                try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
                catch(Exception e){Failure=e.ToString();break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(next is IEnumerator nested){stack.Push(nested);continue;}
                yield return next;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            Cleanup();Done=true;
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(.6f);
            sourceScene=SceneManager.GetActiveScene();
            Require(sourceScene.path==Sample,"Open the authored cutscene sample before scene verification.");
            director=FindFirstObjectByType<CutsceneDirector>();Require(director!=null && director.stage!=null,"Cutscene runtime is missing.");
            initialStage=director.stage;sourceCamera=initialStage.output;sourcePlayer=initialStage.player;
            Require(sourceCamera!=null && sourcePlayer!=null,"Source stage needs its output camera and player.");
            cameraPosition=sourceCamera.transform.position;cameraRotation=sourceCamera.transform.rotation;cameraFov=sourceCamera.fieldOfView;
            playerEnabled=sourcePlayer.enabled;inputGate=sourcePlayer.cameraTransitionInProgress;
            director.Finished+=Finished;director.Committed+=Committed;
            int sceneCount=CountSampleScenes();Require(sceneCount==1,"Scene verification expects one loaded sample scene at startup.");

            var actor=initialStage.FindActor("rebecca");
            Require(actor!=null && actor.animator!=null,"Rebecca needs an Animator for the Timeline integration check.");
            var pose=actor.poses.Find(p=>p!=null && p.clip!=null);Require(pose!=null,"Map a source animation before Timeline verification.");
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>();temporary.Add(timeline);
            var track=timeline.CreateTrack<AnimationTrack>(null,"actor:rebecca");temporary.Add(track);
            var clip=track.CreateClip<AnimationPlayableAsset>();temporary.Add(clip.asset);
            ((AnimationPlayableAsset)clip.asset).clip=pose.clip;clip.start=0;clip.duration=.55;
            var timelineSequence=Sequence("verification-timeline");
            timelineSequence.turns[0].timeline=timeline;timelineSequence.turns[0].waitForTimeline=true;
            timelineSequence.turns[0].minimumDuration=0;
            Require(initialStage.Validate(timelineSequence).Count==0,"Timeline-only actor binding was rejected by stage validation.");
            int startFinished=finished,startCommitted=committed;
            Require(director.Play(timelineSequence),"Timeline fixture was rejected: "+director.LastError);
            yield return new WaitForSecondsRealtime(.25f);
            Require(director.IsPlaying,"Timeline automatic turn completed before its animation duration.");
            yield return Until(()=>!director.IsPlaying,6,"Timeline fixture did not complete.");
            Require(finished==startFinished+1 && committed==startCommitted+1 && lastResult==CutsceneResult.Completed,"Timeline completion was not delivered exactly once.");
            CheckSourceRestored();
            Checks.Add("A transient actor:rebecca Timeline binds its existing Animator, holds the automatic turn until its animation ends, and restores the camera/player without competing actor cues");

            yield return null;
            var returnTrip=Sequence("verification-additive-return");returnTrip.stageScene=Sample;returnTrip.exitMode=CutsceneExitMode.ReturnToPreviousScene;
            startFinished=finished;startCommitted=committed;
            Require(director.Play(returnTrip),"Additive return fixture was rejected: "+director.LastError);
            yield return Until(()=>director.stage!=initialStage && director.stage!=null,12,"A distinct additive cutscene stage was not bound.");
            Require(CountSampleScenes()==sceneCount+1,"The additive stage did not create exactly one scene instance.");
            yield return Until(()=>!director.IsPlaying,15,"Additive return fixture did not finish.");
            Require(finished==startFinished+1 && committed==startCommitted+1 && lastResult==CutsceneResult.Completed,"Additive return did not complete exactly once.");
            Require(CountSampleScenes()==sceneCount,"Temporary additive stage was not unloaded.");
            CheckSourceRestored();
            Checks.Add("A separate instance of the sample loads additively, then unloads and returns to the original scene handle, camera pose/FOV and player input state");

            yield return null;
            var cached=Sequence("verification-cached-stage");cached.stageScene=Sample;cached.unloadStageOnFinish=false;cached.exitMode=CutsceneExitMode.ReturnToPreviousScene;
            Require(director.Play(cached),"Cached stage fixture was rejected.");
            yield return Until(()=>!director.IsPlaying,15,"First cached stage run did not finish.");
            Require(CountSampleScenes()==sceneCount+1,"Cached cutscene stage was not retained exactly once.");
            var retained=SampleScenes().First(s=>s.handle!=sourceScene.handle);int retainedHandle=retained.handle;
            Require(retained.GetRootGameObjects().All(r=>!r.activeSelf),"Retained cutscene stage remains active after returning.");
            CheckSourceRestored();
            yield return null;
            startFinished=finished;startCommitted=committed;
            Require(director.Play(cached),"Cached cutscene could not be replayed.");
            yield return Until(()=>director.stage!=initialStage && director.stage!=null,12,"Cached stage did not become active again.");
            Require(director.stage.gameObject.scene.handle==retainedHandle && CountSampleScenes()==sceneCount+1,"Replay created another stage instead of reusing the cached scene.");
            director.Cancel();director.Cancel();
            yield return Until(()=>!director.IsPlaying,10,"Cancellation in a cached stage did not finish.");
            Require(finished==startFinished+1 && committed==startCommitted && lastResult==CutsceneResult.Cancelled,"Cancellation committed story progress or delivered more than one result.");
            Require(CountSampleScenes()==sceneCount+1 && retained.GetRootGameObjects().All(r=>!r.activeSelf),"Cancelled cached stage was left active or duplicated.");
            CheckSourceRestored();
            Checks.Add("A retained additive stage stays inactive between runs, is reused without increasing scene count, and cancellation returns without committing progress");

            yield return null;
            var target=Sequence("verification-target-load");target.exitMode=CutsceneExitMode.LoadScene;target.destinationScene=Sample;target.destinationSpawnId="player_return";
            startFinished=finished;startCommitted=committed;
            Require(director.Play(target),"Destination scene fixture was rejected: "+director.LastError);
            yield return Until(()=>!director.IsPlaying,20,"Destination scene fixture did not finish.");
            yield return null;
            var destination=SceneManager.GetActiveScene();
            Require(destination.path==Sample && destination.handle!=sourceScene.handle,"Destination was not loaded as a new active scene instance.");
            Require(!sourceScene.IsValid() || !sourceScene.isLoaded,"The original scene was not unloaded by the destination transition.");
            Require(CountSampleScenes()==1,"Single-scene destination left duplicate cached scene instances.");
            var destinationStage=destination.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CutsceneStage>(true)).FirstOrDefault(s=>s.player!=null && s.player.gameObject.scene.handle==destination.handle);
            Require(destinationStage!=null,"New destination stage was not found in its own scene.");
            Require(director.stage==destinationStage,"Persisted director still references its unloaded source-stage actors.");
            var destinationPlayer=destinationStage.player;var spawn=destinationStage.FindSpawn("player_return");
            Require(spawn!=null,"Destination spawn is missing.");
            Require(Vector3.Distance(destinationPlayer.transform.position,spawn.position)<.25f,"Destination player was not positioned at the requested spawn.");
            Require(destinationPlayer.enabled && !destinationPlayer.cameraTransitionInProgress,"Destination player controls were not released.");
            Require(MainLoop.Instance!=null && MainLoop.Instance.PC==destinationPlayer && MainLoop.Instance.Cam==destinationPlayer.GetComponentInChildren<Camera>(),"MainLoop still references the previous scene player/camera.");
            Require(finished==startFinished+1 && committed==startCommitted+1 && lastResult==CutsceneResult.Completed,"Destination transition did not finish and commit exactly once.");
            Checks.Add("The target scene loads as a single new instance, places its player at player_return, rebinds MainLoop, and releases input after the loading fade");
            yield return null;
            var destinationCamera=destinationStage.output;var destinationPose=CutsceneCameraState.From(destinationCamera);
            var replay=Sequence("verification-after-destination");startFinished=finished;startCommitted=committed;
            Require(director.Play(replay),"Persisted director could not start a cutscene after destination loading: "+director.LastError);
            yield return Until(()=>!director.IsPlaying,6,"Post-load replay did not finish.");
            Require(director.stage==destinationStage && MainLoop.Instance.PC==destinationPlayer,"Post-load replay lost destination bindings.");
            Require(destinationPlayer.enabled && !destinationPlayer.cameraTransitionInProgress,"Post-load replay left destination player locked.");
            Require(Vector3.Distance(destinationCamera.transform.position,destinationPose.position)<.02f && Quaternion.Angle(destinationCamera.transform.rotation,destinationPose.rotation)<.2f,"Post-load replay failed to restore the new scene camera.");
            Require(finished==startFinished+1 && committed==startCommitted+1 && lastResult==CutsceneResult.Completed,"Post-load replay did not complete exactly once.");
            Checks.Add("The persisted director can replay another cutscene using the newly loaded stage and returns to that scene's camera/player");
        }
        CutsceneSequence Sequence(string id)
        {
            var sequence=ScriptableObject.CreateInstance<CutsceneSequence>();temporary.Add(sequence);
            sequence.sequenceId=id;sequence.fadeDuration=.03f;sequence.turns.Add(new CutsceneTurn{id=id+"-beat",label=id,hasDialogue=false,
                advance=CutsceneAdvanceMode.Auto,minimumDuration=.18f,autoDelay=0});return sequence;
        }
        void CheckSourceRestored()
        {
            Require(sourceScene.IsValid() && sourceScene.isLoaded && SceneManager.GetActiveScene().handle==sourceScene.handle,"Original scene handle was not restored.");
            Require(sourceCamera!=null && Vector3.Distance(sourceCamera.transform.position,cameraPosition)<.02f && Quaternion.Angle(sourceCamera.transform.rotation,cameraRotation)<.2f && Mathf.Abs(sourceCamera.fieldOfView-cameraFov)<.02f,"Source camera pose/FOV was not restored.");
            Require(sourcePlayer!=null && sourcePlayer.gameObject.activeInHierarchy && sourcePlayer.enabled==playerEnabled && sourcePlayer.cameraTransitionInProgress==inputGate,"Source player/input state was not restored.");
            Require(MainLoop.Instance!=null && MainLoop.Instance.PC==sourcePlayer,"MainLoop is still bound to an additive cutscene player.");
            Require(director.stage==initialStage,"Director did not restore its source stage binding.");
        }
        static IEnumerable<Scene> SampleScenes(){for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);if(scene.path==Sample)yield return scene;}}
        static int CountSampleScenes()=>SampleScenes().Count();
        IEnumerator Until(Func<bool> condition,float seconds,string failure)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!condition()){Require(Time.realtimeSinceStartup<deadline,failure);yield return null;}
        }
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        void Finished(CutsceneResult result){finished++;lastResult=result;}
        void Committed(CutsceneResult result){committed++;}
        void Cleanup()
        {
            if(director!=null){director.Finished-=Finished;director.Committed-=Committed;if(director.IsPlaying)director.Cancel();}
            foreach(var item in temporary)if(item!=null)Destroy(item);temporary.Clear();
        }
        void OnDestroy(){if(!Done)Cleanup();}
    }
}
#endif
