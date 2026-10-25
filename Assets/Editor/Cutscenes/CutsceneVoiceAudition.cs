using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectCRT.Cutscenes.Editor
{
    /// <summary>Explicit editor-only audition; creates a disposable PCM clip and never writes source assets.</summary>
    [InitializeOnLoad]
    public static class CutsceneVoiceAudition
    {
        const int SampleRate=22050;
        const float MaximumDuration=30;
        static AudioClip audition;
        static double stopAt;
        static MethodInfo playPreview,stopPreview;
        public static bool IsPlaying=>audition!=null;
        public static bool WasTruncated {get;private set;}
        public static float LastDuration {get;private set;}
        struct Sound { public float time,pitch;public AudioClip clip; }
        sealed class Samples {public float[] data;public int channels,frames,frequency;}

        static CutsceneVoiceAudition()
        {
            AssemblyReloadEvents.beforeAssemblyReload+=Stop;
            EditorApplication.quitting+=Stop;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Stop();};
        }

        public static string Play(CutsceneTurn turn,CutsceneStage stage,CutsceneVoiceMode mode)
        {
            Stop();WasTruncated=false;LastDuration=0;
            if(EditorApplication.isPlayingOrWillChangePlaymode)return "에디터 미리듣기는 Play 모드를 종료한 다음 사용하세요.";
            if(turn==null||!turn.hasDialogue)return "대사가 있는 턴을 선택하세요.";
            if(turn.dialogueKind!=CutsceneDialogueKind.Spoken)return "화자가 말하는 일반 대사를 선택하세요. 나레이션과 속마음은 기본 무음입니다.";
            if(stage==null)return "화자의 음성 프로필을 찾을 수 있도록 현재 씬의 무대를 연결하세요.";
            var actor=stage.FindActor(turn.speakerId);
            var profile=actor!=null?actor.voice:null;
            if(profile==null)return "선택한 화자에 음성 프로필이 연결되어 있지 않습니다.";
            if(!FindPreviewApi())return "이 Unity 에디터에서는 오디오 미리듣기 API를 사용할 수 없습니다. 실제 재생에서 F8로 음성을 비교하세요.";
            if(mode==CutsceneVoiceMode.Inherit)mode=profile.mode;
            if(mode!=CutsceneVoiceMode.Blip&&mode!=CutsceneVoiceMode.Syllable)return "반복음 또는 음절형 미리듣기를 선택하세요.";

            try
            {
                var sounds=Schedule(turn,profile,mode);
                if(sounds.Count==0)return "재생할 보이스 샘플이 없습니다. 화자 프로필의 반복음/음절 매핑을 확인하세요.";
                var buffers=new Dictionary<AudioClip,Samples>();
                foreach(var sound in sounds)
                {
                    if(buffers.ContainsKey(sound.clip))continue;
                    string error=ReadSamples(sound.clip,out var samples);
                    if(error!=null)return error;
                    buffers.Add(sound.clip,samples);
                }
                var last=sounds[sounds.Count-1];
                float naturalEnd=last.time+last.clip.length/last.pitch;
                LastDuration=Mathf.Min(MaximumDuration,Mathf.Max(0.05f,naturalEnd+0.06f));
                WasTruncated=naturalEnd>MaximumDuration || CutsceneTextUtility.TextDuration(turn.text,turn.charsPerSecond,turn.punctuationPause)>MaximumDuration;
                var pcm=new float[Mathf.CeilToInt(LastDuration*SampleRate)];
                for(int i=0;i<sounds.Count;i++)
                {
                    var sound=sounds[i];var samples=buffers[sound.clip];
                    int begin=Mathf.RoundToInt(sound.time*SampleRate);
                    int end=i+1<sounds.Count?Mathf.Min(pcm.Length,Mathf.RoundToInt(sounds[i+1].time*SampleRate)):pcm.Length;
                    double stride=(double)samples.frequency/SampleRate*sound.pitch;
                    for(int n=begin;n<end&&n<pcm.Length;n++)
                    {
                        double cursor=(n-begin)*stride;int frame=(int)cursor;
                        if(frame>=samples.frames)break;
                        int following=Mathf.Min(frame+1,samples.frames-1);float fraction=(float)(cursor-frame),value=0;
                        for(int channel=0;channel<samples.channels;channel++)
                            value+=Mathf.Lerp(samples.data[frame*samples.channels+channel],samples.data[following*samples.channels+channel],fraction);
                        pcm[n]=Mathf.Clamp(value/samples.channels*Mathf.Clamp01(profile.volume),-1,1);
                    }
                }
                audition=AudioClip.Create("Cutscene voice audition (temporary)",pcm.Length,1,SampleRate,false);
                audition.hideFlags=HideFlags.HideAndDontSave;
                if(!audition.SetData(pcm,0))throw new InvalidOperationException("임시 보이스 파형을 만들지 못했습니다.");
                playPreview.Invoke(null,new object[]{audition,0,false});
                stopAt=EditorApplication.timeSinceStartup+LastDuration+0.2;
                EditorApplication.update-=Update;EditorApplication.update+=Update;
                return null;
            }
            catch(Exception e)
            {
                Stop();var cause=e is TargetInvocationException&&e.InnerException!=null?e.InnerException:e;
                return "음성 미리듣기를 시작하지 못했습니다: "+cause.Message+" 실제 재생의 F8 비교도 사용할 수 있습니다.";
            }
        }

        static List<Sound> Schedule(CutsceneTurn turn,CutsceneVoiceProfile profile,CutsceneVoiceMode mode)
        {
            var result=new List<Sound>();
            string plain=CutsceneTextUtility.PlainText(turn.text);
            float time=0,next=0;int index=0;
            float interval=1/Mathf.Max(1,turn.charsPerSecond);
            for(int i=0;i<plain.Length;i++,index++)
            {
                char c=plain[i];
                if(char.IsHighSurrogate(c)&&i+1<plain.Length&&char.IsLowSurrogate(plain[i+1])){c='\0';i++;}
                if(time>MaximumDuration){WasTruncated=true;break;}
                if(!char.IsWhiteSpace(c)&&!char.IsPunctuation(c)&&!char.IsControl(c)&&c!='\uFFFC'&&index%Mathf.Max(1,profile.characterStride)==0&&time+0.00001f>=next)
                {
                    AudioClip clip=mode==CutsceneVoiceMode.Syllable?profile.FindSyllable(c):profile.GetBlip(index);
                    if(clip!=null)
                    {
                        float pitch=Mathf.Clamp(profile.pitch+Mathf.Sin(index*12.9898f)*profile.pitchVariation,0.01f,3);
                        result.Add(new Sound{time=time,pitch=pitch,clip=clip});
                        next=time+Mathf.Max(0.015f,profile.minimumInterval);
                    }
                }
                time+=interval;
                if(CutsceneTextUtility.IsPunctuation(c))time+=Mathf.Max(0,turn.punctuationPause);
            }
            return result;
        }
        static string ReadSamples(AudioClip clip,out Samples samples)
        {
            samples=null;
            if(clip==null)return "음성 샘플이 연결되어 있지 않습니다.";
            if(clip.loadType==AudioClipLoadType.Streaming)return "'"+clip.name+"'은 Streaming 오디오입니다. 미리듣기용 샘플은 Decompress On Load로 준비하거나 실제 재생에서 비교하세요.";
            if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
            if(clip.loadState!=AudioDataLoadState.Loaded)return "'"+clip.name+"' 오디오가 아직 준비되지 않았습니다. 잠시 후 미리듣기를 다시 누르세요.";
            int frames=Mathf.Min(clip.samples,Mathf.CeilToInt(MaximumDuration*3*clip.frequency));
            if(frames<=0||clip.channels<=0)return "'"+clip.name+"' 오디오 샘플이 비어 있습니다.";
            var data=new float[frames*clip.channels];
            if(!clip.GetData(data,0))return "'"+clip.name+"'의 PCM을 읽을 수 없습니다. Decompress On Load 설정 또는 실제 재생의 음성 비교를 사용하세요.";
            samples=new Samples{data=data,channels=clip.channels,frames=frames,frequency=clip.frequency};return null;
        }
        static bool FindPreviewApi()
        {
            if(playPreview!=null&&stopPreview!=null)return true;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type=assembly.GetType("UnityEditor.AudioUtil");if(type==null)continue;
                const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
                playPreview=type.GetMethod("PlayPreviewClip",flags,null,new[]{typeof(AudioClip),typeof(int),typeof(bool)},null);
                stopPreview=type.GetMethod("StopAllPreviewClips",flags,null,Type.EmptyTypes,null);
                return playPreview!=null&&stopPreview!=null;
            }
            return false;
        }
        static void Update(){if(audition!=null&&EditorApplication.timeSinceStartup>=stopAt)Stop();}
        public static void Stop()
        {
            EditorApplication.update-=Update;
            if(audition==null)return;
            try{stopPreview?.Invoke(null,null);}catch{/* Optional editor API may disappear during reload. */}
            finally{UnityEngine.Object.DestroyImmediate(audition);audition=null;}
        }
    }
}
