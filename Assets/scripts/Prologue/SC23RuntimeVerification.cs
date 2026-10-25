#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ProjectCRT.Prologue
{
    public sealed class SC23RuntimeVerification : MonoBehaviour
    {
        public bool Done{get;private set;}
        public string Failure{get;private set;}
        public readonly List<string> Checks=new();
        IEnumerator Start(){DontDestroyOnLoad(gameObject);var run=Run();while(true){bool more=false;object next=null;try{more=run.MoveNext();if(more)next=run.Current;}catch(Exception e){Failure=e.ToString();Done=true;yield break;}if(!more){Done=true;yield break;}yield return next;}}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        IEnumerator Run()
        {
            yield return null;var room=FindFirstObjectByType<SC23PlaybackController>();Require(room!=null&&room.isRoom,"Room entry missing");Require(!room.MapUnlocked&&!room.director.IsPlaying,"Room must begin in gameplay with map locked");Debug.Log("SC23 verification: packing five moving boxes.");
            room.san.Place(new Vector3(2,.03f,0),Quaternion.identity);Require(!room.TryInteract(),"Distant box interaction was allowed");
            for(int i=0;i<room.boxes.Length;i++){
                room.san.Place(room.boxes[i].position+new Vector3(0,.025f,-.63f),Quaternion.identity);Require(room.TryInteract(),"Nearby moving box failed: "+i);float deadline=Time.realtimeSinceStartup+20;
                while(room.Busy){Require(Time.realtimeSinceStartup<deadline,"Packing or walk-to-sofa stalled");yield return null;}
                Require(room.PackedCount==i+1,"Wrong packed-box count");if(i<room.boxes.Length-1)Require(!room.director.IsPlaying&&!room.MapUnlocked,"Cutscene/map unlocked before all boxes");
            }
            Require(room.director.IsPlaying,"All boxes must trigger the sofa cutscene");var director=room.director;bool reveal=false;var visited=new HashSet<string>();float timeout=Time.realtimeSinceStartup+55;
            while(director.IsPlaying){Require(Time.realtimeSinceStartup<timeout,"SC02 playback timeout");Require(string.IsNullOrEmpty(director.LastError),"SC02 director failed");int index=director.CurrentTurnIndex;if(index>=0){var turn=room.sequence.turns[index];visited.Add(turn.id);if(turn.hasDialogue&&director.view.VisibleCharacters>0){Require(director.view.SpeakerName=="","Thoughts must have no name");Require(turn.voiceMode==CutsceneVoiceMode.Silent,"Thoughts must remain silent");if(director.IsTyping){int before=index;director.Advance();yield return null;Require(director.CurrentTurnIndex==before,"First input skipped thought instead of revealing");reveal=true;}}if(director.State==CutscenePlaybackState.Waiting)director.Advance();}yield return null;}
            Require(room.MapUnlocked&&room.san.InputEnabled==false&&reveal,"Map should open after returning to gameplay");Require(visited.Count==room.sequence.turns.Count,"SC02 missed a turn");
            room.SetMap(false);Require(room.san.InputEnabled,"Closing map did not release movement");room.SetMap(true);Require(!room.san.InputEnabled,"Map did not gate movement");
            Checks.Add("PASS: five nearby box interactions, locked tutorial before all boxes, full sofa cutscene, four silent/unnamed thoughts, reveal input, map movement gate.");
            Debug.Log(Checks[Checks.Count-1]);
            room.Travel();timeout=Time.realtimeSinceStartup+25;SC23PlaybackController bar=null;
            while(bar==null||bar.isRoom){bar=FindFirstObjectByType<SC23PlaybackController>();Require(Time.realtimeSinceStartup<timeout,"Map did not load SC03");yield return null;}
            Require(!bar.director.IsPlaying&&!bar.Completed,"Bar must begin in third-person gameplay");Require(!bar.TryInteract(),"Door opened from too far away");bar.san.Place(bar.entrance.position,bar.entrance.rotation);Require(bar.TryInteract(),"Nearby entrance interaction failed");
            director=bar.director;visited.Clear();int names=0;timeout=Time.realtimeSinceStartup+180;
            var captured=new HashSet<string>();
            while(director.IsPlaying){Require(Time.realtimeSinceStartup<timeout,"SC03 playback timeout");Require(string.IsNullOrEmpty(director.LastError),"SC03 director failed");int index=director.CurrentTurnIndex;if(index>=0){var turn=bar.sequence.turns[index];if(visited.Add(turn.id)&&index%15==0)Debug.Log("SC23 verification: "+index+" / "+bar.sequence.turns.Count+" "+turn.id);if(turn.hasDialogue&&director.view.VisibleCharacters>=2){Require(director.view.SpeakerName==CutsceneTextUtility.SpeakerName(turn,director.stage),"Wrong speaker "+turn.id);names++;if(director.IsTyping)director.Advance();}if(director.State==CutscenePlaybackState.Waiting){if((turn.id=="sc03-d16"||turn.id=="sc03-d76")&&captured.Add(turn.id)){yield return null;yield return null;Capture(director.stage.output,turn.id);}director.Advance();}}yield return null;}
            Require(bar.Completed&&!bar.san.InputEnabled,"SC03 must hold on its ending");Require(visited.Count==bar.sequence.turns.Count&&names>0,"SC03 missed a turn");Require(bar.sequence.turns.FindAll(t=>t.hasDialogue).Count==76,"Original SC03 lines missing");Require(!CutsceneDirector.IsActive,"SC03 left the cutscene input gate active");
            Checks.Add("PASS: map loads SC03; nearby door requirement; every SC03 turn and all 76 original lines; speaker names; time passage; hall/discovery; black ending and input cleanup.");
            bar.Replay();yield return null;Require(director.IsPlaying,"SC03 replay failed");director.Cancel();timeout=Time.realtimeSinceStartup+8;while(director.IsPlaying){Require(Time.realtimeSinceStartup<timeout,"Cancel stalled");yield return null;}Require(!bar.Completed&&bar.san.InputEnabled,"Cancel did not restore entry gameplay");
            bar.Replay();yield return null;director.Skip();timeout=Time.realtimeSinceStartup+8;while(director.IsPlaying){Require(Time.realtimeSinceStartup<timeout,"Skip stalled");yield return null;}Require(bar.Completed&&!CutsceneDirector.IsActive,"Skip did not commit and release director");
            Checks.Add("PASS: F6 replay, cancellation back to entrance gameplay, whole-cutscene skip to ending, no retained input gate.");
        }
        static void Capture(Camera camera,string name)
        {
            System.IO.Directory.CreateDirectory("Logs/SC23/RuntimePreviews");var texture=new RenderTexture(1280,720,24);texture.Create();var before=RenderTexture.active;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=texture});RenderTexture.active=texture;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();System.IO.File.WriteAllBytes("Logs/SC23/RuntimePreviews/"+name+".png",image.EncodeToPNG());RenderTexture.active=before;texture.Release();Destroy(texture);Destroy(image);
        }
    }
}
#endif
