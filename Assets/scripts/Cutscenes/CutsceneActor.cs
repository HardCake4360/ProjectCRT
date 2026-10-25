using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectCRT.Cutscenes
{
    [Serializable] public sealed class CutscenePosePreset
    {
        public string id;
        public AnimationClip clip;
        [Min(0)] public float time;
    }
    [Serializable] public sealed class CutsceneBlendShapeValue
    {
        public SkinnedMeshRenderer renderer;
        public string shape;
        [Range(0,100)] public float weight;
    }
    [Serializable] public sealed class CutsceneExpressionPreset
    {
        public string id;
        public List<CutsceneBlendShapeValue> shapes=new List<CutsceneBlendShapeValue>();
        public Renderer materialRenderer;
        [Min(0)] public int materialSlot;
        public Material material;
    }
    [Serializable] public sealed class CutsceneSpawn
    {
        public string id;
        public Transform target;
    }

    [DisallowMultipleComponent]
    public sealed class CutsceneActor : MonoBehaviour
    {
        public string id="actor";
        public string displayName;
        public CutsceneVoiceProfile voice;
        public Animator animator;
        public Transform lookAtBone;
        public Vector3 localLookAxis=Vector3.forward;
        public bool calibrateLookAxis=true;
        [Range(0,1)] public float lookAtWeight=0.75f;
        public List<CutscenePosePreset> poses=new List<CutscenePosePreset>();
        public List<CutsceneExpressionPreset> expressions=new List<CutsceneExpressionPreset>();

        struct TransformState {public Transform target;public Vector3 position,scale;public Quaternion rotation;}
        struct RendererState {public Renderer target;public Material[] materials;}
        struct ShapeState {public SkinnedMeshRenderer target;public float[] weights;}
        readonly List<TransformState> transforms=new List<TransformState>();
        readonly List<RendererState> renderers=new List<RendererState>();
        readonly List<ShapeState> shapes=new List<ShapeState>();
        readonly List<Behaviour> suspended=new List<Behaviour>();
        readonly List<bool> suspendedEnabled=new List<bool>();
        bool captured,preview,animatorEnabled,animatorRootMotion,animatorEvents;
        float animatorSpeed;
        Animator authoredAnimator;
        Transform authoredLookAtBone;
        Vector3 effectiveLookAxis;
        AnimatorCullingMode animatorCulling;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationClip currentClip;
        float clipClock,lastElapsed,speed=1;
        bool loop=true,frozen,externalAnimationControl;
        CutsceneExpressionPreset currentExpression;
        Transform currentLookTarget;
        Vector3 currentLookOffset;
        Quaternion lookBaseRotation;
        bool lookBaseValid;
        public bool IsCaptured => captured;
        public void Capture(bool editorPreview)
        {
            if(captured)return;
            preview=editorPreview;
            authoredAnimator=animator;authoredLookAtBone=lookAtBone;
            if(animator==null)animator=GetComponentInChildren<Animator>();
            transforms.Clear();renderers.Clear();shapes.Clear();suspended.Clear();suspendedEnabled.Clear();
            foreach(var t in GetComponentsInChildren<Transform>(true))transforms.Add(new TransformState{target=t,position=t.localPosition,rotation=t.localRotation,scale=t.localScale});
            foreach(var r in GetComponentsInChildren<Renderer>(true))renderers.Add(new RendererState{target=r,materials=r.sharedMaterials});
            foreach(var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int count=r.sharedMesh!=null?r.sharedMesh.blendShapeCount:0;var weights=new float[count];
                for(int i=0;i<count;i++)weights[i]=r.GetBlendShapeWeight(i);
                shapes.Add(new ShapeState{target=r,weights=weights});
            }
            foreach(var behaviour in GetComponentsInChildren<Behaviour>(true))
            {
                if(behaviour is NPC_script || behaviour is NpcLookAtIKDriver)
                {suspended.Add(behaviour);suspendedEnabled.Add(behaviour.enabled);behaviour.enabled=false;}
            }
            if(animator!=null)
            {
                animatorEnabled=animator.enabled;animatorRootMotion=animator.applyRootMotion;animatorCulling=animator.cullingMode;animatorEvents=animator.fireEvents;animatorSpeed=animator.speed;
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.speed=0;
                if(preview)animator.fireEvents=false;
                if(lookAtBone==null && animator.isHuman)lookAtBone=animator.GetBoneTransform(HumanBodyBones.Head);
            }
            effectiveLookAxis=calibrateLookAxis&&lookAtBone!=null?lookAtBone.InverseTransformDirection(transform.forward):localLookAxis;
            captured=true;ResetState();
        }
        public void ResetToCaptured()
        {
            if(!captured)return;
            DestroyGraph();RestoreVisualState();ResetState();
        }
        void ResetState(){currentClip=null;currentExpression=null;currentLookTarget=null;clipClock=lastElapsed=0;speed=1;loop=true;frozen=false;lookBaseValid=false;externalAnimationControl=false;}
        public void SetExternalAnimationControl(bool value)
        {
            if(externalAnimationControl==value)return;
            externalAnimationControl=value;
            if(value){DestroyGraph();currentClip=null;}
        }
        public void BeginTurn()
        {
            if(!frozen)clipClock+=lastElapsed*speed;
            lastElapsed=0;
        }
        public CutscenePosePreset FindPose(string poseId)=>poses.Find(p=>p!=null && p.id==poseId);
        public bool HasExpression(string expressionId)=>expressions.Exists(e=>e!=null && e.id==expressionId);
        public void ApplyCue(CutsceneActorCue cue,CutsceneStage stage,float elapsedAtStart=0)
        {
            if(cue.move)
            {
                var marker=stage.FindSpawn(cue.positionId);
                transform.position=(marker!=null?marker.position:transform.position)+cue.positionOffset;
                if(marker!=null && !cue.rotate)transform.rotation=marker.rotation;
                lookBaseValid=false;
            }
            if(cue.rotate){transform.rotation=Quaternion.Euler(cue.eulerAngles);lookBaseValid=false;}
            var pose=!string.IsNullOrEmpty(cue.poseId)?FindPose(cue.poseId):null;
            if(cue.clip!=null || pose!=null)
            {
                currentClip=cue.clip!=null?cue.clip:pose.clip;
                speed=Mathf.Max(0.01f,cue.speed);loop=cue.loop;frozen=cue.freezePose || pose!=null;
                clipClock=cue.startTime+(pose!=null?pose.time:0)-(frozen?0:Mathf.Max(0,elapsedAtStart)*speed);
                CreateGraph();
            }
            if(!string.IsNullOrEmpty(cue.expressionId))
            {
                var expression=expressions.Find(e=>e!=null && e.id==cue.expressionId);
                if(expression!=null){ResetExpression();currentExpression=expression;}
            }
            if(cue.changeLookAt){currentLookTarget=stage.FindTarget(cue.lookAtId);currentLookOffset=cue.lookAtOffset;lookBaseValid=false;}
        }
        public float CueDuration(CutsceneActorCue cue)
        {
            if(cue==null || cue.freezePose || !string.IsNullOrEmpty(cue.poseId))return 0;
            var clip=cue.clip;
            return clip!=null?Mathf.Max(0,clip.length-cue.startTime)/Mathf.Max(0.01f,cue.speed):0;
        }
        public void Evaluate(float elapsed)
        {
            if(!captured)return;
            lastElapsed=Mathf.Max(0,elapsed);
            if(graph.IsValid() && currentClip!=null)
            {
                float t=clipClock+(frozen?0:lastElapsed*speed);
                t=loop && !frozen && currentClip.length>0?Mathf.Repeat(t,currentClip.length):Mathf.Clamp(t,0,currentClip.length);
                playable.SetTime(t);graph.Evaluate(0);lookBaseValid=false;
            }
            ApplyExpression();
            if(currentLookTarget!=null && lookAtBone!=null)
            {
                if(!lookBaseValid){lookBaseRotation=lookAtBone.rotation;lookBaseValid=true;}
                lookAtBone.rotation=lookBaseRotation;
                Vector3 direction=currentLookTarget.position+currentLookOffset-lookAtBone.position;
                Vector3 axis=lookAtBone.TransformDirection(effectiveLookAxis);
                if(direction.sqrMagnitude>0.0001f && axis.sqrMagnitude>0.0001f)
                {
                    var delta=Quaternion.FromToRotation(axis,direction.normalized);
                    delta=Quaternion.RotateTowards(Quaternion.identity,delta,65);
                    lookAtBone.rotation=Quaternion.Slerp(lookBaseRotation,delta*lookBaseRotation,lookAtWeight);
                }
            }
        }
        void CreateGraph()
        {
            DestroyGraph();
            if(animator==null || currentClip==null || externalAnimationControl)return;
            animator.enabled=true;
            graph=PlayableGraph.Create(name+" Cutscene");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable=AnimationClipPlayable.Create(graph,currentClip);
            playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph,"Actor",animator).SetSourcePlayable(playable);graph.Play();
        }
        void ResetExpression()
        {
            foreach(var shape in shapes)if(shape.target!=null)for(int i=0;i<shape.weights.Length;i++)shape.target.SetBlendShapeWeight(i,shape.weights[i]);
            foreach(var renderer in renderers)if(renderer.target!=null)renderer.target.sharedMaterials=renderer.materials;
        }
        void ApplyExpression()
        {
            if(currentExpression==null)return;
            foreach(var shape in currentExpression.shapes)
                if(shape!=null && shape.renderer!=null && shape.renderer.sharedMesh!=null)
                {int index=shape.renderer.sharedMesh.GetBlendShapeIndex(shape.shape);if(index>=0)shape.renderer.SetBlendShapeWeight(index,shape.weight);}
            if(currentExpression.materialRenderer!=null && currentExpression.material!=null)
            {
                var materials=currentExpression.materialRenderer.sharedMaterials;int slot=currentExpression.materialSlot;
                if(slot>=0 && slot<materials.Length){materials[slot]=currentExpression.material;currentExpression.materialRenderer.sharedMaterials=materials;}
            }
        }
        void RestoreVisualState()
        {
            foreach(var state in transforms)if(state.target!=null){state.target.localPosition=state.position;state.target.localRotation=state.rotation;state.target.localScale=state.scale;}
            ResetExpression();
        }
        public void Restore()
        {
            if(!captured)return;
            DestroyGraph();RestoreVisualState();
            if(animator!=null){animator.enabled=animatorEnabled;animator.applyRootMotion=animatorRootMotion;animator.cullingMode=animatorCulling;animator.fireEvents=animatorEvents;animator.speed=animatorSpeed;}
            for(int i=0;i<suspended.Count;i++)if(suspended[i]!=null)
            {
                suspended[i].enabled=suspendedEnabled[i];
                if(!preview && Application.isPlaying && suspendedEnabled[i] && suspended[i] is NPC_script npc)
                {
                    try{npc.PlayIdle();}catch(Exception exception){Debug.LogException(exception,npc);}
                }
            }
            animator=authoredAnimator;lookAtBone=authoredLookAtBone;
            captured=false;ResetState();
        }
        void DestroyGraph(){if(graph.IsValid())graph.Destroy();}
        void OnDisable(){Restore();}
    }
}
