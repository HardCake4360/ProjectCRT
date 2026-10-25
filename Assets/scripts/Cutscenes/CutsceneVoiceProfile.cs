using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectCRT.Cutscenes
{
    [Serializable] public sealed class CutsceneSyllableSample
    {
        [Tooltip("Hangul syllable, vowel, latin letter, or * fallback.")]
        public string key;
        public AudioClip clip;
    }
    [CreateAssetMenu(menuName="ProjectCRT/Cutscene/Character Voice",fileName="CharacterVoice")]
    public sealed class CutsceneVoiceProfile : ScriptableObject
    {
        public string displayName;
        public CutsceneVoiceMode mode=CutsceneVoiceMode.Blip;
        public AudioClip[] blips=Array.Empty<AudioClip>();
        public List<CutsceneSyllableSample> syllables=new List<CutsceneSyllableSample>();
        [Range(0.25f,3)] public float pitch=1;
        [Range(0,0.5f)] public float pitchVariation=0.06f;
        [Range(0,1)] public float volume=0.45f;
        [Min(0.01f)] public float minimumInterval=0.04f;
        [Min(1)] public int characterStride=1;
        public AudioClip GetBlip(int deterministicIndex)
        {
            if(blips==null || blips.Length==0)return null;
            return blips[(deterministicIndex&int.MaxValue)%blips.Length];
        }
        public AudioClip FindSyllable(char c)
        {
            string key=char.ToLowerInvariant(c).ToString();
            foreach(var sample in syllables)if(sample!=null && sample.key==key && sample.clip!=null)return sample.clip;
            // Korean precomposed syllables share a vowel bank: 가 -> ㅏ, 겨 -> ㅕ, etc.
            if(c>='가' && c<='힣')
            {
                string vowel=((char)('ㅏ'+((c-'가')/28)%21)).ToString();
                foreach(var sample in syllables)if(sample!=null && sample.key==vowel && sample.clip!=null)return sample.clip;
                string group="v"+(((c-'가')/28)%21);
                foreach(var sample in syllables)if(sample!=null && sample.key==group && sample.clip!=null)return sample.clip;
            }
            foreach(var sample in syllables)if(sample!=null && (sample.key=="*" || sample.key=="default") && sample.clip!=null)return sample.clip;
            return null;
        }
    }
}
