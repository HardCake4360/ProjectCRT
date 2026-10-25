using System;
using System.Linq;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using UnityEditor;
using UnityEngine;

namespace ProjectCRT.Prologue.Editor
{
    public static partial class SC01CutsceneAuthoring
    {
        sealed class Rig
        {
            public GameObject root;public Transform model;public CutsceneActor actor;public Pose[] rest;
            public Transform Bone(string name)=>model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
            public void CaptureRest()=>rest=Capture(this);
            public void RestoreRest(){foreach(var p in rest)p.Apply();}
        }
        struct Pose
        {
            public Transform target;public Vector3 position,scale;public Quaternion rotation;
            public void Apply(){target.localPosition=position;target.localRotation=rotation;target.localScale=scale;}
        }
        static Pose[] Capture(Rig rig)=>rig.model.GetComponentsInChildren<Transform>(true).Select(t=>new Pose{target=t,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
        static void ApplyLerp(Pose[] a,Pose[] b,float t)
        {
            for(int i=0;i<a.Length;i++){a[i].target.localPosition=Vector3.Lerp(a[i].position,b[i].position,t);a[i].target.localRotation=Quaternion.Slerp(a[i].rotation,b[i].rotation,t);a[i].target.localScale=Vector3.Lerp(a[i].scale,b[i].scale,t);}
        }
        static AnimationClip SaveClip(string id,Rig rig,float duration,Action<float> sample)
        {
            int count=Mathf.CeilToInt(duration*20)+1;var frames=new Pose[count][];
            for(int i=0;i<count;i++){rig.RestoreRest();sample(i*duration/(count-1));frames[i]=Capture(rig);}
            var clip=new AnimationClip{name=id,frameRate=20};
            for(int bone=0;bone<frames[0].Length;bone++)
            {
                string path=AnimationUtility.CalculateTransformPath(frames[0][bone].target,rig.root.transform);
                for(int channel=0;channel<10;channel++)
                {
                    var values=new float[count];
                    Quaternion previous=frames[0][bone].rotation;
                    for(int i=0;i<count;i++)
                    {
                        var f=frames[i][bone];var q=f.rotation;
                        if(Quaternion.Dot(previous,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                        values[i]=channel<3?f.position[channel]:channel<7?q[channel-3]:f.scale[channel-7];
                    }
                    string property=channel<3?"m_LocalPosition."+"xyz"[channel]:channel<7?"m_LocalRotation."+"xyzw"[channel-3]:"m_LocalScale."+"xyz"[channel-7];
                    // Preserve absolute pose resets while collapsing constant channels to two keys.
                    bool constant=values.All(v=>Mathf.Abs(v-values[0])<.0000001f);
                    var keys=constant?new[]{new Keyframe(0,values[0]),new Keyframe(duration,values[0])}:values.Select((v,i)=>new Keyframe(i*duration/(count-1),v)).ToArray();
                    var curve=new AnimationCurve(keys);
                    for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
                }
            }
            clip.EnsureQuaternionContinuity();AssetDatabase.CreateAsset(clip,Folder+"/Animation/"+id+".anim");clips[id]=clip;rig.RestoreRest();
            rig.actor.poses.Add(new CutscenePosePreset{id=id,clip=clip});return clip;
        }
        static void Aim(Transform bone,Transform end,Vector3 direction)
        {
            var current=end.position-bone.position;if(current.sqrMagnitude>.000001f)bone.rotation=Quaternion.FromToRotation(current,direction.normalized)*bone.rotation;
        }
        static void TwoBone(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            Vector3 origin=upper.position,delta=target-origin;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,end.position);
            if(a<.0001f||b<.0001f||delta.sqrMagnitude<.00001f)return;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);var forward=delta.normalized;
            var bend=Vector3.ProjectOnPlane(pole,forward).normalized;if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(forward,Vector3.right).normalized;
            float along=(a*a+distance*distance-b*b)/(2*distance);float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var elbow=origin+forward*along+bend*height;Aim(upper,lower,elbow-origin);Aim(lower,end,origin+forward*distance-lower.position);
        }
        static void RotateWorld(Transform target,Vector3 axis,float degrees)=>target.rotation=Quaternion.AngleAxis(degrees,axis)*target.rotation;
        static void Nod(Transform target,float degrees)=>RotateWorld(target,Vector3.right,degrees);
    }
}
