using UnityEngine;
using UnityEngine.Events;

namespace ProjectCRT.Cutscenes
{
    /// <summary>Reusable interaction/trigger entry point with an exactly-once story commit.</summary>
    public sealed class CutsceneTrigger : Interactable
    {
        public CutsceneDirector director;
        public CutsceneSequence sequence;
        public bool playOnce;
        public bool enterToPlay;
        public UnityEvent onCommitted=new UnityEvent();
        public UnityEvent onCompleted=new UnityEvent();
        public UnityEvent onSkipped=new UnityEvent();
        bool played,subscribed;
        public override void Interact()=>Play();
        public void Play()
        {
            if((playOnce&&played)||director==null||sequence==null)return;
            if(director.Play(sequence))
            {
                director.Committed+=Commit;director.Finished+=Finish;subscribed=true;canInteract=false;
            }
            else if(MainLoop.Instance!=null&&!CutsceneDirector.IsActive&&NpcInvestigationController.CurrentInteraction==null)MainLoop.Instance.SetMainLoopState(MainState.Main);
        }
        void OnTriggerEnter(Collider other){if(enterToPlay&&other.GetComponentInParent<PlayerControler>()!=null)Play();}
        void Commit(CutsceneResult result)
        {
            played=true;onCommitted.Invoke();
            if(result==CutsceneResult.Completed)onCompleted.Invoke();else if(result==CutsceneResult.Skipped)onSkipped.Invoke();
        }
        void Finish(CutsceneResult result){Unsubscribe();canInteract=!(playOnce&&played);}
        void Unsubscribe(){if(!subscribed||director==null)return;director.Committed-=Commit;director.Finished-=Finish;subscribed=false;}
        void OnDestroy()=>Unsubscribe();
    }
}
