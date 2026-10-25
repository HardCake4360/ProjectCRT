using System.Collections;
using ProjectCRT.Cutscenes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectCRT.Prologue
{
    /// <summary>Scene-local entry point and sound mix. Character/prop/camera animation remains in the sequence.</summary>
    public sealed class SC01PlaybackController : MonoBehaviour
    {
        public CutsceneDirector director;
        public CutsceneSequence sequence;
        public PrologueBackgroundPreview inspection;
        public OutdoorStreetViewer viewer;
        public bool autoPlay=true;
        public AudioSource rain,engine,wipers,jazz,effects;
        public AudioClip cdClick,doorOpen,doorClose;
        public Transform[] filmingSets;
        int previous=-1;
        float entered;
        bool playedCD;
        void Awake(){if(autoPlay&&director!=null&&director.view!=null){director.view.Begin();director.view.SetBlack(1);}}
        void OnEnable(){if(director!=null)director.Finished+=Finished;}
        IEnumerator Start(){yield return null;if(autoPlay)Play();}
        public bool Play()
        {
            if(director==null||sequence==null||director.IsPlaying)return false;
            previous=-1;playedCD=false;
            if(inspection!=null)inspection.enabled=false;if(viewer!=null)viewer.enabled=false;
            if(director.Play(sequence)){StartLoop(rain);StartLoop(engine);StartLoop(wipers);return true;}
            RestoreInspection();return false;
        }
        void Update(){if(Keyboard.current!=null&&Keyboard.current.f6Key.wasPressedThisFrame)Play();}
        void LateUpdate()
        {
            if(director==null||!director.IsPlaying)return;
            int index=director.CurrentTurnIndex;
            if(index!=previous){previous=index;entered=Time.unscaledTime;}
            float time=Time.unscaledTime-entered;
            string id=index>=0&&index<sequence.turns.Count?sequence.turns[index].id:"";
            int cd=sequence.turns.FindIndex(t=>t.id=="sc01-cd"),city=sequence.turns.FindIndex(t=>t.id=="sc01-city"),arrival=sequence.turns.FindIndex(t=>t.id=="sc01-arrival");
            bool cabin=index>0&&index<city;
            SetVolume(rain,cabin?.035f:.13f);SetVolume(engine,cabin?.035f:.09f);SetVolume(wipers,cabin?.1f:0);
            if(index>cd||(index==cd&&time>=3.0f))StartLoop(jazz);
            SetVolume(jazz,index<city?.055f:index<arrival?.13f:.05f);
            if(id=="sc01-cd"&&time>2.9f&&!playedCD){playedCD=true;if(effects!=null&&cdClick!=null)effects.PlayOneShot(cdClick);}
            if(index>=0&&time<.12f&&id=="sc01-exit"&&effects!=null&&doorOpen!=null&&!effects.isPlaying)effects.PlayOneShot(doorOpen);
            if(index>=0&&time<.12f&&id=="sc01-lookcity"&&effects!=null&&doorClose!=null&&!effects.isPlaying)effects.PlayOneShot(doorClose);
            // The director owns its loading/final fades; this cover is only the scripted time jump.
            if(director.State==CutscenePlaybackState.Entering||director.State==CutscenePlaybackState.Typing||director.State==CutscenePlaybackState.Waiting)
            {
                if(id=="sc01-timepassage")director.view.SetBlack(Mathf.SmoothStep(0,1,time/.65f));
                else if(id=="sc01-arrival")director.view.SetBlack(1-Mathf.SmoothStep(0,1,time/.8f));
            }
        }
        static void StartLoop(AudioSource source){if(source!=null&&source.clip!=null&&!source.isPlaying)source.Play();}
        static void SetVolume(AudioSource source,float volume){if(source!=null)source.volume=volume;}
        void Finished(CutsceneResult result)
        {
            StopAudio();RestoreInspection();
            // A successful scene transition leaves this old director root in DontDestroyOnLoad.
            if(result==CutsceneResult.Completed||result==CutsceneResult.Skipped)
                if(gameObject.scene.name=="DontDestroyOnLoad")Destroy(gameObject);
        }
        void RestoreInspection(){if(filmingSets!=null)foreach(var set in filmingSets)if(set!=null)set.localPosition=Vector3.zero;if(inspection!=null)inspection.enabled=true;if(viewer!=null)viewer.enabled=true;}
        void StopAudio(){foreach(var source in new[]{rain,engine,wipers,jazz,effects})if(source!=null)source.Stop();}
        void OnDisable(){if(director!=null)director.Finished-=Finished;StopAudio();}
    }
}
