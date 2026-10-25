#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectCRT.Cutscenes
{
    /// <summary>Exercises the public playback API without writing dialogue saves or making network requests.</summary>
    public sealed class CutsceneVerification : MonoBehaviour
    {
        public bool Done { get; private set; }
        public string Failure { get; private set; }
        public readonly List<string> Checks = new List<string>();
        readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
        CutsceneDirector director;
        CutsceneStage stage;
        Camera output;
        float originalTimeScale;
        int finishedCount;
        CutsceneResult lastResult;
        Vector3 savedPosition;
        Quaternion savedRotation;
        float savedFov;
        bool savedPlayerEnabled;
        bool savedInputGate;

        IEnumerator Start()
        {
            originalTimeScale=Time.timeScale;
            // Flatten nested enumerators so any assertion failure still produces a report and cleanup.
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more=false;object next=null;
                try { more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current; }
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
            yield return new WaitForSecondsRealtime(0.5f);
            director=FindFirstObjectByType<CutsceneDirector>();
            Require(director!=null,"CutsceneDirector missing from sample scene.");
            stage=director.stage;Require(stage!=null,"CutsceneStage missing.");
            output=stage.output;Require(output!=null,"Stage output camera missing.");
            Require(stage.shots.Count>0 && stage.actors.Count>0,"Sample stage needs a camera preset and actor.");
            Require(!director.IsPlaying,"Sample should wait for a trigger rather than auto-play.");
            director.Finished+=OnFinished;
            SaveBaseline();

            var manual=Sequence("verification-manual");
            manual.turns.Add(Dialogue("spoken",CutsceneDialogueKind.Spoken,CutsceneNameMode.Profile));
            manual.turns.Add(Dialogue("thought",CutsceneDialogueKind.Thought,CutsceneNameMode.Hidden));
            manual.turns[1].camera.mode=CutsceneCameraMode.Maintain;
            manual.turns[1].charsPerSecond=100;
            manual.turns[1].text="이름을 숨긴 속마음입니다.";
            manual.turns.Add(new CutsceneTurn{id="silent",label="Silent automatic beat",hasDialogue=false,advance=CutsceneAdvanceMode.Auto,minimumDuration=0.2f,autoDelay=0});
            Require(director.Play(manual),"Manual fixture did not start.");
            yield return Until(()=>director.State==CutscenePlaybackState.Entering,3,"Camera entry did not start.");
            Require(!director.view.IsVisible || director.view.Opacity<0.001f,"Dialogue shown before camera arrival.");
            yield return Until(()=>director.IsTyping,3,"Typewriter did not start after camera arrival.");
            Require(Vector3.Distance(output.transform.position,stage.shots[0].transform.position)<0.02f,"Text began before camera reached its preset.");
            Require(director.view.Opacity>0.98f,"Typing began before window fade completed.");
            Require(director.view.SpeakerName==stage.actors[0].displayName,"Spoken line did not show the actor name.");
            Require(stage.player==null || stage.player.cameraTransitionInProgress || !stage.player.enabled,"Player input remained active during cutscene.");
            Checks.Add("Camera arrival precedes dialogue fade and typing; spoken line displays its character name; player input is suspended");
            int turn=director.CurrentTurnIndex;
            var voice=FindFirstObjectByType<CutsceneVoicePlayer>();int sounds=voice!=null?voice.SoundsPlayed:0;
            director.Advance();
            yield return null;
            Require(director.CurrentTurnIndex==turn,"First advance skipped a partially typed line.");
            Require(!director.IsTyping && director.view.VisibleCharacters>=director.view.FullText.Length,"First advance did not reveal the remaining text.");
            Require(voice==null || voice.SoundsPlayed<=sounds+1,"Revealing the remaining line produced a burst of voice sounds.");
            director.Advance();
            yield return Until(()=>director.CurrentTurnIndex==1 && director.IsTyping,3,"Second advance did not enter the next turn.");
            Require(string.IsNullOrEmpty(director.view.SpeakerName),"Thought line displayed a speaker name.");
            director.Advance();yield return null;
            director.Advance();
            yield return Until(()=>!director.IsPlaying,4,"Silent auto beat did not finish.");
            Require(finishedCount==1 && lastResult==CutsceneResult.Completed,"Manual fixture did not complete exactly once.");
            CheckRestored();
            Checks.Add("Advance first reveals the whole line, then advances; thought hides the name; a silent automatic turn completes without input");

            yield return null;
            int count=finishedCount;
            Require(director.Play(manual),"Could not restart fixture for early cancellation.");
            director.Cancel();director.Cancel();
            yield return Until(()=>!director.IsPlaying,4,"Cancellation during preparation did not finish.");
            Require(finishedCount==count+1 && lastResult==CutsceneResult.Cancelled,"Early cancellation produced duplicate or incorrect completion.");
            yield return new WaitForSecondsRealtime(0.8f);
            Require(!director.view.IsVisible || director.view.Opacity<0.001f,"Cancelled preparation opened a stale dialogue window.");
            CheckRestored();
            Checks.Add("Repeated cancellation during preparation restores controls/camera and never opens a delayed dialogue window");

            yield return null;
            count=finishedCount;
            Require(director.Play(manual),"Could not restart fixture for skip.");
            yield return Until(()=>director.State==CutscenePlaybackState.Entering,3,"Skip fixture did not enter camera transition.");
            director.Skip();director.Skip();
            yield return Until(()=>!director.IsPlaying,4,"Skip during camera entry did not finish.");
            Require(finishedCount==count+1 && lastResult==CutsceneResult.Skipped,"Skip completion was not applied exactly once.");
            CheckRestored();
            Checks.Add("Repeated whole-cutscene skip during camera entry produces one skipped result and restores the original camera and input state");

            yield return null;
            var protectedSequence=Sequence("verification-no-skip");protectedSequence.allowSkip=false;
            protectedSequence.turns.Add(Dialogue("protected",CutsceneDialogueKind.Narration,CutsceneNameMode.Hidden));
            Require(director.Play(protectedSequence),"Protected fixture did not start.");
            director.Skip();yield return null;
            Require(director.IsPlaying,"allowSkip=false was ignored.");
            director.Cancel();yield return Until(()=>!director.IsPlaying,4,"Protected fixture could not be cancelled.");
            CheckRestored();
            Checks.Add("A sequence that disallows skipping ignores Skip while explicit cancellation still cleans up");

            yield return null;
            var performer=stage.actors.Find(a=>a!=null && a.animator!=null && a.poses.Exists(p=>p!=null && p.clip!=null && p.clip.length>0));
            Require(performer!=null,"Sample needs a mapped character animation for waitForActors verification.");
            var animation=performer.poses.Find(p=>p!=null && p.clip!=null && p.clip.length>0).clip;
            var actorWait=Sequence("verification-actor-wait");
            actorWait.turns.Add(new CutsceneTurn{id="actor-wait",label="Wait for character action",hasDialogue=false,advance=CutsceneAdvanceMode.Auto,
                waitForActors=true,autoDelay=0,actors=new List<CutsceneActorCue>{new CutsceneActorCue{actorId=performer.id,clip=animation,loop=false,speed=animation.length/0.8f}}});
            Require(stage.ActorDuration(actorWait.turns[0])>0.7f,"Animation wait fixture has no duration.");
            Require(director.Play(actorWait),"Character wait fixture did not start.");
            yield return new WaitForSecondsRealtime(0.3f);
            Require(director.IsPlaying,"Automatic turn ignored its character-animation completion condition.");
            yield return Until(()=>!director.IsPlaying,4,"Character action completion did not release the turn.");
            CheckRestored();
            Checks.Add("An automatic turn waits for its non-looping character action before completing");

            yield return null;
            var timed=Sequence("verification-timed-cues");var actor=stage.actors[0];var baseAngles=actor.transform.eulerAngles;
            var cueTurn=Dialogue("timed",CutsceneDialogueKind.Spoken,CutsceneNameMode.Profile);cueTurn.waitForActors=true;
            cueTurn.actors.Add(new CutsceneActorCue{actorId=actor.id,timing=CutsceneCueTiming.TextCharacter,characterIndex=2,delay=.04f,rotate=true,eulerAngles=baseAngles+Vector3.up*25});
            cueTurn.actors.Add(new CutsceneActorCue{actorId=actor.id,timing=CutsceneCueTiming.TextComplete,delay=.2f,rotate=true,eulerAngles=baseAngles+Vector3.up*45});
            cueTurn.actors.Add(new CutsceneActorCue{actorId=actor.id,timing=CutsceneCueTiming.OnAdvance,delay=.12f,rotate=true,eulerAngles=baseAngles+Vector3.up*65});
            timed.turns.Add(cueTurn);timed.turns.Add(new CutsceneTurn{id="hold-cues",hasDialogue=false,advance=CutsceneAdvanceMode.Manual});
            Require(director.Play(timed),"Timed cue fixture did not start.");
            yield return Until(()=>director.IsTyping,3,"Timed cue text did not start.");
            Require(Quaternion.Angle(actor.transform.rotation,Quaternion.Euler(baseAngles))<.1f,"Character cue fired before its text index.");
            director.Advance();yield return new WaitForSecondsRealtime(.1f);
            Require(Quaternion.Angle(actor.transform.rotation,Quaternion.Euler(baseAngles+Vector3.up*25))<.2f,"Text-index cue did not fire after reveal-all.");
            yield return new WaitForSecondsRealtime(.2f);
            Require(Quaternion.Angle(actor.transform.rotation,Quaternion.Euler(baseAngles+Vector3.up*45))<.2f,"Text-complete cue did not respect its delay.");
            director.Advance();yield return Until(()=>director.CurrentTurnIndex==1,3,"Advance cue tail did not finish.");
            Require(Quaternion.Angle(actor.transform.rotation,Quaternion.Euler(baseAngles+Vector3.up*65))<.2f,"Advance cue did not persist into next turn.");
            director.Cancel();yield return Until(()=>!director.IsPlaying,3,"Timed cue cancellation failed.");CheckRestored();
            Checks.Add("Character-index, text-complete and advance cues fire at their own delayed times, including reveal-all; advance pose persists into the next turn");

            yield return null;
            var realtime=Sequence("verification-unscaled");
            realtime.turns.Add(new CutsceneTurn{id="unscaled",label="Real-time automatic turn",hasDialogue=false,advance=CutsceneAdvanceMode.Auto,minimumDuration=0.2f,autoDelay=0});
            Time.timeScale=0;
            Require(director.Play(realtime),"Unscaled fixture did not start.");
            yield return Until(()=>!director.IsPlaying,4,"Cutscene stalled while timeScale was zero.");
            Time.timeScale=originalTimeScale;CheckRestored();
            Checks.Add("Preparation, silent-turn timing, and cleanup continue at timeScale zero");

            yield return null;
            var sample=AssetDatabase.LoadAssetAtPath<CutsceneSequence>("Assets/Settings/Cutscenes/Shangrila_Introduction.asset");
            Require(sample!=null,"Authored sample sequence is missing.");
            var sampleCopy=Instantiate(sample);temporary.Add(sampleCopy);sampleCopy.stageScene="";sampleCopy.exitMode=CutsceneExitMode.Return;
            count=finishedCount;
            Require(director.Play(sampleCopy),"Authored sample was rejected.");
            var capturedTurns=new HashSet<int>();
            float deadline=Time.realtimeSinceStartup+45;
            while(director.IsPlaying)
            {
                Require(Time.realtimeSinceStartup<deadline,"Authored sample did not finish in 45 seconds.");
                if(director.IsTyping&&capturedTurns.Add(director.CurrentTurnIndex))
                {
                    director.Advance();yield return null;
                    yield return new WaitForEndOfFrame();
                    var snapshot=ScreenCapture.CaptureScreenshotAsTexture();
                    Directory.CreateDirectory("Logs/Cutscenes");File.WriteAllBytes("Logs/Cutscenes/turn-"+director.CurrentTurnIndex+".png",snapshot.EncodeToPNG());Destroy(snapshot);
                }
                if(director.IsTyping || director.State==CutscenePlaybackState.Waiting)director.Advance();
                yield return new WaitForSecondsRealtime(0.08f);
            }
            Require(finishedCount==count+1 && lastResult==CutsceneResult.Completed,"Authored sample did not complete successfully.");
            CheckRestored();
            Checks.Add("The full authored Shangrila sequence plays through its dialogue, camera and character cues and returns to walking");
            Directory.CreateDirectory("Logs/Cutscenes");
        }

        CutsceneSequence Sequence(string id)
        {
            var sequence=ScriptableObject.CreateInstance<CutsceneSequence>();temporary.Add(sequence);
            sequence.sequenceId=id;sequence.fadeDuration=0.06f;sequence.exitMode=CutsceneExitMode.Return;sequence.stageScene="";return sequence;
        }
        CutsceneTurn Dialogue(string id,CutsceneDialogueKind kind,CutsceneNameMode name)
        {
            return new CutsceneTurn{id=id,label=id,hasDialogue=true,dialogueKind=kind,nameMode=name,speakerId=stage.actors[0].id,
                text="카메라가 도착한 뒤 이 긴 문장이 한 글자씩 표시됩니다. 진행키는 먼저 나머지 문장을 보여 줍니다.",charsPerSecond=2,
                camera=new CutsceneCameraCue{mode=CutsceneCameraMode.Blend,shotId=stage.shots[0].id,duration=0.45f},
                advance=CutsceneAdvanceMode.Manual,dialogueAfterCamera=true,waitForCamera=true};
        }
        void SaveBaseline()
        {
            savedPosition=output.transform.position;savedRotation=output.transform.rotation;savedFov=output.fieldOfView;
            if(stage.player!=null){savedPlayerEnabled=stage.player.enabled;savedInputGate=stage.player.cameraTransitionInProgress;}
        }
        void CheckRestored()
        {
            Require(Vector3.Distance(output.transform.position,savedPosition)<0.02f,"Camera position was not restored.");
            Require(Quaternion.Angle(output.transform.rotation,savedRotation)<0.2f,"Camera rotation was not restored.");
            Require(Mathf.Abs(output.fieldOfView-savedFov)<0.02f,"Camera field of view was not restored.");
            if(stage.player!=null)
            {
                Require(stage.player.enabled==savedPlayerEnabled,"Player enabled state was not restored.");
                Require(stage.player.cameraTransitionInProgress==savedInputGate,"Player input gate was not restored.");
            }
            Require(!director.view.IsVisible || director.view.Opacity<0.001f,"Dialogue view remains visible after cleanup.");
            Require(NpcInvestigationController.CurrentInteraction==null,"Verification unexpectedly opened an NPC conversation.");
        }
        IEnumerator Until(Func<bool> predicate,float seconds,string failure)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!predicate()){Require(Time.realtimeSinceStartup<deadline,failure);yield return null;}
        }
        static void Require(bool pass,string message){if(!pass)throw new Exception(message);}
        void OnFinished(CutsceneResult result){finishedCount++;lastResult=result;}
        void Cleanup()
        {
            Time.timeScale=originalTimeScale;
            if(director!=null){director.Finished-=OnFinished;if(director.IsPlaying)director.Cancel();}
            foreach(var item in temporary)if(item!=null)Destroy(item);temporary.Clear();
        }
        void OnDestroy(){if(!Done)Cleanup();}
    }
}
#endif
