#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Prologue
{
    public sealed class SC01RuntimeVerification : MonoBehaviour
    {
        public bool Done{get;private set;}
        public string Failure{get;private set;}
        public readonly List<string> Checks=new();
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);var task=Run();
            while(true)
            {
                object next=null;bool more=false;
                try{more=task.MoveNext();if(more)next=task.Current;}
                catch(Exception e){Failure=e.ToString();Done=true;yield break;}
                if(!more){Done=true;yield break;}yield return next;
            }
        }
        IEnumerator Run()
        {
            SC01PlaybackController entry=null;float deadline=Time.realtimeSinceStartup+10;
            while(entry==null){entry=FindFirstObjectByType<SC01PlaybackController>();Require(Time.realtimeSinceStartup<deadline,"SC01 entry missing");yield return null;}
            var director=entry.director;int finished=0;CutsceneResult result=CutsceneResult.Failed;
            director.Finished+=(value)=>{finished++;result=value;};
            Require(entry.sequence.beginWithBlack,"Opening must start behind a black cover.");
            Require(entry.sequence.turns.FindAll(t=>t.hasDialogue).Count==25,"The original 25 dialogue lines must be retained.");
            Require(director.stage.FindActor("san").voice.blips[0]!=director.stage.FindActor("driver").voice.blips[0],"Speakers need different voice samples.");
            var visited=new HashSet<string>();bool checkedReveal=false;int projected=0;deadline=Time.realtimeSinceStartup+150;
            while(finished==0)
            {
                Require(Time.realtimeSinceStartup<deadline,"Full SC01 playback timed out");Require(director!=null&&string.IsNullOrEmpty(director.LastError),"Runtime director error");
                int index=director.CurrentTurnIndex;
                if(index>=0&&index<entry.sequence.turns.Count)
                {
                    var turn=entry.sequence.turns[index];visited.Add(turn.id);
                    if(director.view.VisibleCharacters>=3&&turn.hasDialogue)
                    {
                        Require(director.view.SpeakerName==CutsceneTextUtility.SpeakerName(turn,director.stage),"Wrong speaker name: "+turn.id);
                        if(turn.id=="sc01-d03"||turn.id=="sc01-d17")
                        {
                            var head=director.stage.FindActor("san").lookAtBone.position;var point=director.stage.output.WorldToViewportPoint(head);
                            Require(point.z>0&&point.x>.1f&&point.x<.9f&&point.y>.32f&&point.y<.98f,"San's head is outside the dialogue-safe frame: "+point);projected++;
                        }
                        if(director.IsTyping)
                        {
                            int before=director.CurrentTurnIndex;director.Advance();yield return null;
                            Require(director.CurrentTurnIndex==before,"Reveal input skipped the current line");checkedReveal=true;
                        }
                    }
                    if(director.State==CutscenePlaybackState.Waiting)director.Advance();
                }
                yield return null;
            }
            Require(result==CutsceneResult.Completed&&finished==1,"Normal completion must fire once");
            Require(SceneManager.GetActiveScene().name=="ProjectCRT_SC02_SanRoom","Normal completion must load SC02");
            Require(visited.Count==39&&checkedReveal&&projected>0,"Not every authored turn was exercised");
            Checks.Add("PASS: all 39 turns / 25 original lines; first input reveals; speaker names; San face framing; one completion and SC02 loading.");
            var op=SceneManager.LoadSceneAsync("Assets/Scenes/ProjectCRT_SC01_TaxiArrival.unity",LoadSceneMode.Single);while(!op.isDone)yield return null;
            entry=null;deadline=Time.realtimeSinceStartup+15;
            while(entry==null||!entry.director.IsTyping){entry=FindFirstObjectByType<SC01PlaybackController>();Require(Time.realtimeSinceStartup<deadline,"Replay did not start");yield return null;}
            director=entry.director;finished=0;director.Finished+=value=>{finished++;result=value;};director.Cancel();deadline=Time.realtimeSinceStartup+8;
            while(finished==0){Require(Time.realtimeSinceStartup<deadline,"Cancel did not finish");yield return null;}
            Require(result==CutsceneResult.Cancelled&&SceneManager.GetActiveScene().name=="ProjectCRT_SC01_TaxiArrival","Cancel changed the scene");
            Require(entry.viewer.enabled&&entry.inspection.enabled,"Cancel did not restore inspection controls");
            Require(!entry.rain.isPlaying&&!entry.engine.isPlaying&&!entry.jazz.isPlaying,"Cancel left background audio playing");
            Checks.Add("PASS: cancel restores inspection and stops audio without advancing to SC02.");
            finished=0;Require(entry.Play(),"F6 replay entry refused after cancellation");yield return null;director.Skip();deadline=Time.realtimeSinceStartup+12;
            while(finished==0){Require(Time.realtimeSinceStartup<deadline,"Skip did not finish");yield return null;}
            Require(result==CutsceneResult.Skipped&&finished==1&&SceneManager.GetActiveScene().name=="ProjectCRT_SC02_SanRoom","Skip did not commit once and load SC02");
            Require(!CutsceneDirector.IsActive,"Playback gate remained held after loading SC02");
            Checks.Add("PASS: replay and whole-scene skip commit exactly once, load SC02, and release the cutscene gate.");
            var compatibility=ScriptableObject.CreateInstance<CutsceneSequence>();Require(!compatibility.beginWithBlack,"Existing sequences changed their default loading fade");Destroy(compatibility);
            Checks.Add("PASS: existing sequence loading fade remains the default.");
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
