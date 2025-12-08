using UnityEngine;
using System;
using RotaryHeart.Lib.SerializableDictionary;

[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Scriptable Objects/AudioLibrary")]
public class AudioLibrary : ScriptableObject
{
    [Serializable] public class BgmDict : SerializableDictionaryBase<string, AudioClip> { }
    [Serializable] public class SfxDict : SerializableDictionaryBase<string, AudioClip> { }

    [Header("BGM Clips (key ¡æ AudioClip)")]
    public BgmDict bgmClips = new BgmDict();

    [Header("SFX Clips (key ¡æ AudioClip)")]
    public SfxDict sfxClips = new SfxDict();
}
