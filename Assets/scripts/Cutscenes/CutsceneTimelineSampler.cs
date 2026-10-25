using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ProjectCRT.Cutscenes
{
    /// <summary>Manual Timeline sampling shared by the game and editor; no signals or scene-changing tracks.</summary>
    public sealed class CutsceneTimelineSampler : IDisposable
    {
        PlayableDirector director;
        readonly Dictionary<Animator,bool> eventStates=new Dictionary<Animator,bool>();
        readonly List<Animator> boundAnimators=new List<Animator>();
        public float Duration=>director!=null&&director.playableAsset!=null?(float)director.playableAsset.duration:0;
        public void Open(CutsceneStage stage,CutsceneTurn turn,bool preview=false)
        {
            Dispose();if(turn.timeline==null)return;
            var go=new GameObject("Cutscene Timeline Evaluation");go.hideFlags=HideFlags.HideAndDontSave;
            director=go.AddComponent<PlayableDirector>();director.playOnAwake=false;director.timeUpdateMode=DirectorUpdateMode.Manual;
            director.extrapolationMode=DirectorWrapMode.Hold;director.playableAsset=turn.timeline;
            foreach(var track in turn.timeline.GetOutputTracks())
            {
                if(!(track is AnimationTrack) && !(track is AudioTrack))
                    throw new InvalidOperationException("Cutscene Timeline supports Animation and Audio tracks; use turn commands for other actions: "+track.name);
                if(track is AnimationTrack)
                {
                    string id=track.name.StartsWith("actor:",StringComparison.Ordinal)?track.name.Substring(6):track.name;
                    var actor=stage.FindActor(id);var animator=actor!=null?actor.GetComponentInChildren<Animator>():null;
                    if(animator==null)throw new InvalidOperationException("Timeline animation track has no actor Animator: "+track.name);
                    director.SetGenericBinding(track,animator);
                    if(!boundAnimators.Contains(animator))boundAnimators.Add(animator);
                    if(preview&&!eventStates.ContainsKey(animator)){eventStates.Add(animator,animator.fireEvents);animator.fireEvents=false;}
                }
                else if(track is AudioTrack)
                {
                    var audio=go.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;audio.volume=preview?0:1;
                    director.SetGenericBinding(track,audio);
                }
            }
            if(Application.isPlaying&&!preview)director.Play();else director.RebuildGraph();
            if(preview)
                for(int i=0;i<director.playableGraph.GetOutputCount();i++)
                {var output=director.playableGraph.GetOutput(i);if(output.IsPlayableOutputOfType<AudioPlayableOutput>())output.SetWeight(0);}
            Evaluate(0);
        }
        public void Evaluate(float time)
        {
            if(director==null)return;
            director.time=Math.Max(0,Math.Min(time,Duration));director.Evaluate();
        }
        public void Dispose()
        {
            foreach(var pair in eventStates)if(pair.Key!=null)pair.Key.fireEvents=pair.Value;eventStates.Clear();
            if(director==null)return;
            var poses=new List<(Transform target,Vector3 position,Quaternion rotation,Vector3 scale)>();
            foreach(var animator in boundAnimators)if(animator!=null)foreach(var bone in animator.GetComponentsInChildren<Transform>(true))
                poses.Add((bone,bone.localPosition,bone.localRotation,bone.localScale));
            director.Stop();var go=director.gameObject;director=null;
            if(Application.isPlaying)UnityEngine.Object.Destroy(go);else UnityEngine.Object.DestroyImmediate(go);
            foreach(var pose in poses)if(pose.target!=null){pose.target.localPosition=pose.position;pose.target.localRotation=pose.rotation;pose.target.localScale=pose.scale;}
            boundAnimators.Clear();
        }
    }
}
