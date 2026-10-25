using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace ProjectCRT.Cutscenes
{
    /// <summary>Scene bindings and reversible, absolute-time cutscene evaluation shared by runtime and preview.</summary>
    public sealed class CutsceneStage : MonoBehaviour
    {
        public const float DefaultWindowFade=0.28f;
        public string stageId="shangrila";
        public Camera output;
        public PlayerControler player;
        public List<CutsceneActor> actors=new List<CutsceneActor>();
        public List<CutsceneCameraPreset> shots=new List<CutsceneCameraPreset>();
        public List<CutscenePath> paths=new List<CutscenePath>();
        public List<CutsceneSpawn> spawns=new List<CutsceneSpawn>();
        public bool IsCaptured => captured;
        bool captured;
        Camera authoredOutput;
        CutsceneCameraState originalCamera,cameraStart;
        CutsceneCameraCue activeCamera;
        float cameraElapsedBase,lastElapsed;
        CutsceneTurn activeTurn;
        bool previewCapture;
        CutsceneTimelineSampler previewTimeline;
        readonly HashSet<CutsceneActorCue> appliedCues=new HashSet<CutsceneActorCue>();
        readonly Dictionary<CutsceneActorCue,float> dialogueCueTimes=new Dictionary<CutsceneActorCue,float>();
        int dialogueVisible;
        bool dialogueComplete,reconstructing;
        float advanceAt=-1;

        public CutsceneActor FindActor(string id)=>actors.Find(a=>a!=null && a.id==id);
        public CutsceneCameraPreset FindShot(string id)=>shots.Find(s=>s!=null && s.id==id);
        public CutscenePath FindPath(string id)=>paths.Find(p=>p!=null && p.id==id);
        public Transform FindSpawn(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            var spawn=spawns.Find(s=>s!=null && s.id==id);return spawn!=null?spawn.target:null;
        }
        public Transform FindTarget(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            if(id=="camera")return output!=null?output.transform:null;
            var actor=FindActor(id);if(actor!=null)return actor.lookAtBone!=null?actor.lookAtBone:actor.transform;
            var marker=FindSpawn(id);if(marker!=null)return marker;
            var shot=FindShot(id);if(shot!=null)return shot.transform;
            var path=FindPath(id);return path!=null?path.transform:null;
        }
        public void Capture(Camera camera,bool preview=false)
        {
            if(captured)Restore();
            authoredOutput=output;output=camera!=null?camera:output;
            if(output==null)throw new InvalidOperationException("Cutscene stage has no output camera.");
            originalCamera=CutsceneCameraState.From(output);cameraStart=originalCamera;captured=true;previewCapture=preview;
            activeCamera=null;activeTurn=null;lastElapsed=cameraElapsedBase=0;
            try{foreach(var actor in actors)if(actor!=null)actor.Capture(preview);}
            catch{Restore();throw;}
        }
        public void BeginTurn(CutsceneTurn turn)
        {
            if(!captured)throw new InvalidOperationException("Capture the stage before beginning a turn.");
            if(turn==null)throw new ArgumentNullException(nameof(turn));
            foreach(var actor in actors)if(actor!=null)
            {
                actor.BeginTurn();
                actor.SetExternalAnimationControl(TimelineControls(turn,actor.id));
            }
            appliedCues.Clear();dialogueCueTimes.Clear();dialogueVisible=0;dialogueComplete=false;advanceAt=-1;
            if(turn.camera!=null && turn.camera.mode!=CutsceneCameraMode.Maintain)
            {
                cameraStart=CutsceneCameraState.From(output);activeCamera=turn.camera;cameraElapsedBase=0;
            }
            else cameraElapsedBase+=lastElapsed;
            activeTurn=turn;lastElapsed=0;EvaluateTurn(turn,0);
        }
        public void EvaluateTurn(CutsceneTurn turn,float elapsed)
        {
            if(!captured || output==null)return;
            if(activeTurn!=turn)BeginTurn(turn);
            lastElapsed=Mathf.Max(0,elapsed);
            ApplyPendingCues(turn,lastElapsed);
            foreach(var actor in actors)if(actor!=null)actor.Evaluate(lastElapsed);
            if(activeCamera!=null)EvaluateCamera(activeCamera,cameraElapsedBase+lastElapsed).Apply(output);
        }
        public void SetDialogueProgress(int visible,bool complete,bool advance=false)
        {
            if(activeTurn==null)return;
            dialogueVisible=Mathf.Max(dialogueVisible,visible);dialogueComplete|=complete;
            if(advance && advanceAt<0)advanceAt=lastElapsed;
            if(activeTurn.actors==null)return;
            foreach(var cue in activeTurn.actors)
            {
                if(cue==null || dialogueCueTimes.ContainsKey(cue))continue;
                if((cue.timing==CutsceneCueTiming.TextCharacter && dialogueVisible>=Mathf.Max(1,cue.characterIndex)) ||
                    (cue.timing==CutsceneCueTiming.TextComplete && dialogueComplete))dialogueCueTimes.Add(cue,lastElapsed);
            }
        }
        void ApplyPendingCues(CutsceneTurn turn,float elapsed)
        {
            if(turn.actors==null)return;
            // Apply in chronological order during a seek, preserving list order for ties.
            while(true)
            {
                CutsceneActorCue next=null;float earliest=float.PositiveInfinity;
                foreach(var cue in turn.actors)
                {
                    if(cue==null || appliedCues.Contains(cue))continue;
                    float due=CueEventTime(turn,cue,previewCapture||reconstructing)+Mathf.Max(0,cue.delay);
                    if(due<=elapsed && due<earliest){next=cue;earliest=due;}
                }
                if(next==null)break;
                appliedCues.Add(next);var actor=FindActor(next.actorId);
                if(actor!=null)actor.ApplyCue(next,this,earliest);
            }
        }
        float CueEventTime(CutsceneTurn turn,CutsceneActorCue cue,bool canonical)
        {
            switch(cue.timing)
            {
                case CutsceneCueTiming.TurnStart:return 0;
                case CutsceneCueTiming.AfterCameraArrives:return CameraDuration(turn);
                case CutsceneCueTiming.OnAdvance:return advanceAt>=0?advanceAt:float.PositiveInfinity;
                case CutsceneCueTiming.TextCharacter:
                case CutsceneCueTiming.TextComplete:
                    if(!turn.hasDialogue)return float.PositiveInfinity;
                    if(canonical)return TextStart(turn)+(cue.timing==CutsceneCueTiming.TextComplete?
                        CutsceneTextUtility.TextDuration(turn.text,turn.charsPerSecond,turn.punctuationPause):CharacterTime(turn,cue.characterIndex));
                    return dialogueCueTimes.TryGetValue(cue,out float time)?time:float.PositiveInfinity;
                default:return float.PositiveInfinity;
            }
        }
        float TextStart(CutsceneTurn turn)=>(turn.dialogueAfterCamera?CameraDuration(turn):0)+DefaultWindowFade;
        static float CharacterTime(CutsceneTurn turn,int index)
        {
            string text=CutsceneTextUtility.PlainText(turn.text??"");float time=0;int count=1;
            for(int i=0;i<text.Length && count<Mathf.Max(1,index);count++)
            {
                char c=text[i];i+=char.IsHighSurrogate(c)&&i+1<text.Length&&char.IsLowSurrogate(text[i+1])?2:1;
                time+=1f/Mathf.Max(1,turn.charsPerSecond);
                if(CutsceneTextUtility.IsPunctuation(c))time+=Mathf.Max(0,turn.punctuationPause);
            }
            return time;
        }
        public float AdvanceDuration(CutsceneTurn turn)
        {
            float duration=0;if(turn?.actors==null)return 0;
            foreach(var cue in turn.actors)if(cue!=null && cue.timing==CutsceneCueTiming.OnAdvance)
            {
                var actor=FindActor(cue.actorId);
                duration=Mathf.Max(duration,Mathf.Max(0,cue.delay)+(turn.waitForActors && actor!=null?actor.CueDuration(cue):0));
            }
            return duration;
        }
        public void CompleteTurn()
        {
            if(activeTurn==null)return;
            SetDialogueProgress(CutsceneTextUtility.CharacterCount(activeTurn.text),true,true);
            EvaluateTurn(activeTurn,lastElapsed+AdvanceDuration(activeTurn));
        }
        CutsceneCameraState EvaluateCamera(CutsceneCameraCue cue,float elapsed)
        {
            var shot=FindShot(cue.shotId);var target=AimAtOverride(shot!=null?shot.Sample():cameraStart,cue);
            if(cue.mode==CutsceneCameraMode.Path)
            {
                var path=FindPath(cue.pathId);
                if(path!=null)
                {
                    float u=Ease(cue,Progress(Mathf.Max(0,elapsed-cue.joinDuration),cue.duration));
                    float pathTime=Mathf.Lerp(cue.pathStart,cue.pathEnd,u);
                    target=AimAtOverride(path.Sample(pathTime,cue.constantPathSpeed),cue);
                    if(cue.useEndFieldOfView)target.fieldOfView=Mathf.Lerp(path.fieldOfView,cue.endFieldOfView,u);
                    if(elapsed<cue.joinDuration)target=CutsceneCameraState.Lerp(cameraStart,AimAtOverride(path.Sample(cue.pathStart,cue.constantPathSpeed),cue),Ease(cue,Progress(elapsed,cue.joinDuration)));
                }
            }
            else if(cue.mode==CutsceneCameraMode.CutThenZoom)
                target.fieldOfView=Mathf.Lerp(target.fieldOfView,cue.endFieldOfView,Ease(cue,Progress(elapsed,cue.duration)));
            else if(cue.mode==CutsceneCameraMode.Blend || cue.mode==CutsceneCameraMode.Rough)
            {
                if(cue.useEndFieldOfView)target.fieldOfView=cue.endFieldOfView;
                target=CutsceneCameraState.Lerp(cameraStart,target,Ease(cue,Progress(elapsed,EffectiveDuration(cue))));
            }
            else if(cue.useEndFieldOfView)target.fieldOfView=cue.endFieldOfView;
            return target;
        }
        CutsceneCameraState AimAtOverride(CutsceneCameraState target,CutsceneCameraCue cue)
        {
            var look=FindTarget(cue.lookAtId);
            if(look!=null)
            {
                Vector3 direction=look.position-target.position;
                if(direction.sqrMagnitude>0.0001f)target.rotation=Quaternion.LookRotation(direction,Vector3.up);
            }
            return target;
        }
        static float Progress(float elapsed,float duration)=>duration<=0?1:Mathf.Clamp01(elapsed/duration);
        static float Ease(CutsceneCameraCue cue,float t)=>Mathf.Clamp01(cue.easing!=null && cue.easing.length>0?cue.easing.Evaluate(t):t*t*(3-2*t));
        static float EffectiveDuration(CutsceneCameraCue cue)=>cue.mode==CutsceneCameraMode.Rough?Mathf.Min(0.2f,cue.duration):cue.duration;
        public float CameraDuration(CutsceneTurn turn)
        {
            var cue=turn!=null?turn.camera:null;
            if(cue==null || cue.mode==CutsceneCameraMode.Maintain || cue.mode==CutsceneCameraMode.Cut)return 0;
            return Mathf.Max(0,EffectiveDuration(cue))+(cue.mode==CutsceneCameraMode.Path?Mathf.Max(0,cue.joinDuration):0);
        }
        public float ActorDuration(CutsceneTurn turn)
        {
            float duration=0;if(turn?.actors==null)return 0;
            foreach(var cue in turn.actors)if(cue!=null && cue.timing!=CutsceneCueTiming.OnAdvance)
            {
                var actor=FindActor(cue.actorId);
                if(actor!=null)duration=Mathf.Max(duration,CueEventTime(turn,cue,true)+Mathf.Max(0,cue.delay)+actor.CueDuration(cue));
            }
            return duration;
        }
        public float PreviewDuration(CutsceneTurn turn)
        {
            if(turn==null)return 0;
            float camera=CameraDuration(turn);
            float textEnd=turn.hasDialogue?(turn.dialogueAfterCamera?camera:0)+DefaultWindowFade+
                CutsceneTextUtility.TextDuration(turn.text??"",turn.charsPerSecond,turn.punctuationPause):0;
            return Mathf.Max(turn.minimumDuration,turn.waitForCamera?camera:0,turn.waitForActors?ActorDuration(turn):0,
                turn.waitForTimeline && turn.timeline!=null?(float)turn.timeline.duration:0,textEnd+Mathf.Max(0,turn.autoDelay));
        }
        public float ActorCompletionTime(CutsceneTurn turn)
        {
            float duration=0;if(turn?.actors==null)return 0;
            foreach(var cue in turn.actors)if(cue!=null&&cue.timing!=CutsceneCueTiming.OnAdvance)
            {
                var actor=FindActor(cue.actorId);
                if(actor!=null)duration=Mathf.Max(duration,CueEventTime(turn,cue,previewCapture||reconstructing)+Mathf.Max(0,cue.delay)+actor.CueDuration(cue));
            }
            return duration;
        }
        public void Reconstruct(CutsceneSequence sequence,int index,float elapsed)
        {
            if(!captured)throw new InvalidOperationException("Capture before preview reconstruction.");
            if(sequence==null || sequence.turns==null || index<0 || index>=sequence.turns.Count)throw new ArgumentOutOfRangeException(nameof(index));
            previewTimeline?.Dispose();previewTimeline=null;
            foreach(var actor in actors)if(actor!=null)actor.ResetToCaptured();
            originalCamera.Apply(output);activeTurn=null;activeCamera=null;cameraElapsedBase=lastElapsed=0;
            reconstructing=true;
            try{for(int i=0;i<=index;i++)
            {
                var turn=sequence.turns[i];BeginTurn(turn);float time=i==index?elapsed:PreviewDuration(turn);EvaluateTurn(turn,time);
                if(i<index){CompleteTurn();time=lastElapsed;}
                if(turn.timeline!=null)
                {
                    previewTimeline=new CutsceneTimelineSampler();previewTimeline.Open(this,turn,true);previewTimeline.Evaluate(time);RefreshCamera(time);
                    if(i<index || !previewCapture){previewTimeline.Dispose();previewTimeline=null;}
                }
            }}finally{reconstructing=false;}
        }
        public void RefreshCamera(float elapsed)
        {
            if(captured && output!=null && activeCamera!=null)EvaluateCamera(activeCamera,cameraElapsedBase+Mathf.Max(0,elapsed)).Apply(output);
        }
        public void Restore()
        {
            if(!captured)return;
            previewTimeline?.Dispose();previewTimeline=null;
            foreach(var actor in actors)if(actor!=null)
            {
                try{actor.Restore();}catch(Exception exception){Debug.LogException(exception,actor);}
            }
            if(output!=null)originalCamera.Apply(output);
            output=authoredOutput;captured=false;activeTurn=null;activeCamera=null;
        }
        public List<string> Validate(CutsceneSequence sequence)
        {
            var errors=new List<string>();
            if(sequence==null){errors.Add("No cutscene sequence selected.");return errors;}
            if(sequence.turns==null || sequence.turns.Count==0){errors.Add("Sequence has no turns.");return errors;}
            CheckIds(actors,a=>a.id,"actor",errors);CheckIds(shots,s=>s.id,"shot",errors);CheckIds(paths,p=>p.id,"path",errors);
            var spawnIds=new HashSet<string>();
            foreach(var spawn in spawns)
                if(spawn==null || string.IsNullOrWhiteSpace(spawn.id) || spawn.target==null || !spawnIds.Add(spawn.id))errors.Add("Missing or duplicate spawn binding.");
            var ids=new HashSet<string>();
            foreach(var turn in sequence.turns)
            {
                if(turn==null){errors.Add("Null turn in sequence.");continue;}
                string label="Turn "+turn.label+": ";
                if(string.IsNullOrWhiteSpace(turn.id) || !ids.Add(turn.id))errors.Add(label+"missing or duplicate stable turn ID.");
                if(turn.timeline!=null)
                {
                    foreach(var track in turn.timeline.GetOutputTracks())
                    {
                        if(!(track is AnimationTrack) && !(track is AudioTrack))errors.Add(label+"Timeline supports only Animation and Audio tracks: "+track.name);
                        if(track is AnimationTrack)
                        {
                            string actorId=track.name.StartsWith("actor:",StringComparison.Ordinal)?track.name.Substring(6):track.name;
                            var bound=FindActor(actorId);
                            if(bound==null || (bound.animator==null && bound.GetComponentInChildren<Animator>()==null))errors.Add(label+"Timeline animation track has no actor Animator: "+track.name);
                        }
                    }
                }
                if(turn.hasDialogue && turn.dialogueKind==CutsceneDialogueKind.Spoken && FindActor(turn.speakerId)==null)errors.Add(label+"speaker not bound: "+turn.speakerId);
                if(turn.camera!=null)
                {
                    var cue=turn.camera;
                    if(cue.mode==CutsceneCameraMode.Path)
                    {
                        var path=FindPath(cue.pathId);
                        if(path==null || path.knots==null || path.knots.Length<2)errors.Add(label+"path missing or needs at least two knots: "+cue.pathId);
                    }
                    else if(cue.mode!=CutsceneCameraMode.Maintain && FindShot(cue.shotId)==null)errors.Add(label+"shot not bound: "+cue.shotId);
                    if(!string.IsNullOrEmpty(cue.lookAtId) && FindTarget(cue.lookAtId)==null)errors.Add(label+"camera target not bound: "+cue.lookAtId);
                }
                if(turn.actors==null)continue;
                foreach(var cue in turn.actors)
                {
                    if(cue==null)continue;var actor=FindActor(cue.actorId);
                    if(actor==null){errors.Add(label+"actor not bound: "+cue.actorId);continue;}
                    if(float.IsNaN(cue.delay) || float.IsInfinity(cue.delay) || cue.delay<0)errors.Add(label+"actor cue delay must be finite and non-negative.");
                    if(cue.timing==CutsceneCueTiming.TextCharacter && (!turn.hasDialogue || cue.characterIndex<1 || cue.characterIndex>CutsceneTextUtility.CharacterCount(turn.text)))errors.Add(label+"actor cue character index is outside the displayed dialogue.");
                    if(cue.timing==CutsceneCueTiming.TextComplete && !turn.hasDialogue)errors.Add(label+"text-complete cue requires a dialogue turn.");
                    if(cue.move && !string.IsNullOrEmpty(cue.positionId) && FindSpawn(cue.positionId)==null)errors.Add(label+"position not bound: "+cue.positionId);
                    if(!string.IsNullOrEmpty(cue.poseId) && actor.FindPose(cue.poseId)==null)errors.Add(label+"pose not mapped: "+cue.poseId);
                    if(!string.IsNullOrEmpty(cue.expressionId) && !actor.HasExpression(cue.expressionId))errors.Add(label+"expression not mapped: "+cue.expressionId);
                    if(cue.changeLookAt && !string.IsNullOrEmpty(cue.lookAtId) && FindTarget(cue.lookAtId)==null)errors.Add(label+"look target not bound: "+cue.lookAtId);
                    if((cue.clip!=null || !string.IsNullOrEmpty(cue.poseId)) && TimelineControls(turn,actor.id))errors.Add(label+"actor animation cue conflicts with Timeline track for "+actor.id);
                }
            }
            return errors;
        }
        static void CheckIds<T>(List<T> values,Func<T,string> id,string kind,List<string> errors) where T:UnityEngine.Object
        {
            var ids=new HashSet<string>();
            foreach(var value in values)if(value==null)errors.Add("Null "+kind+" binding.");else if(string.IsNullOrWhiteSpace(id(value)) || !ids.Add(id(value)))errors.Add("Missing or duplicate "+kind+" ID: "+id(value));
        }
        static bool TimelineControls(CutsceneTurn turn,string actorId)
        {
            if(turn.timeline==null)return false;
            foreach(var track in turn.timeline.GetOutputTracks())if(track is AnimationTrack && (track.name==actorId || track.name=="actor:"+actorId))return true;
            return false;
        }
        void OnDisable(){Restore();}
    }
}
