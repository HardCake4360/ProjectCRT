using UnityEngine;

namespace ProjectCRT.Cutscenes
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class CutsceneVoicePlayer : MonoBehaviour
    {
        AudioSource source;
        float nextSound;
        public int SoundsPlayed{get;private set;}
        public void ResetLine(){Stop();nextSound=0;}
        public void Reveal(char character,int index,CutsceneVoiceProfile profile,CutsceneVoiceMode mode)
        {
            if(profile==null||char.IsWhiteSpace(character)||char.IsPunctuation(character)||char.IsControl(character))return;
            if(mode==CutsceneVoiceMode.Inherit)mode=profile.mode;
            if(mode==CutsceneVoiceMode.Silent||index%Mathf.Max(1,profile.characterStride)!=0||Time.unscaledTime<nextSound)return;
            var clip=mode==CutsceneVoiceMode.Syllable?profile.FindSyllable(character):profile.GetBlip(index);
            if(clip==null)return;
            if(source==null){source=GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.loop=false;}
            source.Stop();source.clip=clip;source.pitch=Mathf.Clamp(profile.pitch+Mathf.Sin(index*12.9898f)*profile.pitchVariation,.01f,3);
            source.volume=Mathf.Clamp01(profile.volume)*(AudioManager.Instance!=null?AudioManager.Instance.EffectiveSfxVolume:1);
            source.Play();SoundsPlayed++;nextSound=Time.unscaledTime+Mathf.Max(.015f,profile.minimumInterval);
        }
        public void Stop(){if(source!=null)source.Stop();}
    }
}
