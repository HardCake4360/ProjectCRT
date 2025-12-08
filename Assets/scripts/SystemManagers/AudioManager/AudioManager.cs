using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Library (SO)")]
    public AudioLibrary library;

    [Header("Audio Sources (2D)")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Fade Defaults (seconds)")]
    [SerializeField] private float defaultFade = 1.0f;

    [Header("Settings")]
    [SerializeField] private float masterVolume;
    [SerializeField] private bool masterMute;
    [SerializeField] private float bgmVolume;
    [SerializeField] private bool bgmMute;
    [SerializeField] private float sfxVolume;
    [SerializeField] private bool sfxMute;
    void ApplyVolumesNow()
    {
        bgmSource.volume = (bgmMute || masterMute) ? 0f : masterVolume * bgmVolume;
        sfxSource.volume = (sfxMute || masterMute) ? 0f : masterVolume * sfxVolume;
    }

    public bool IsBGMPlaying()
    {
        return bgmSource.isPlaying;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!bgmSource) bgmSource = gameObject.AddComponent<AudioSource>();
        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();

        bgmSource.playOnAwake = false; bgmSource.loop = true; bgmSource.spatialBlend = 0f;
        sfxSource.playOnAwake = false; sfxSource.loop = false; sfxSource.spatialBlend = 0f;

        ApplyVolumesNow();
    }



    private void Update()
    {
        ApplyVolumesNow();
    }

    // ===== BGM (키) =====
    public void PlayBGM(string key)
    {
        if (!library || !library.bgmClips.TryGetValue(key, out var clip) || !clip)
        {
            Debug.LogWarning($"[AudioManager] BGM key not found: {key}");
            return;
        }
        PlayBGM(clip);
        Debug.Log("audio play Successful");
    }

    // BGM (클립)
    public void PlayBGM(AudioClip clip, float fade = -1f, float volume = -1f, float pitch = 1f)
    {
        if (!clip) return;
        fade = (fade < 0f) ? defaultFade : fade;
        volume = (volume < 0f) ? masterVolume * bgmVolume : volume;
        StopAllCoroutines();
        StartCoroutine(FadeInBGM(clip, fade, Mathf.Clamp01(volume), pitch));
    }

    public void StopBGM(float fade = -1f)
    {
        fade = (fade < 0f) ? defaultFade : fade;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndStop(bgmSource, fade));
    }

    // ===== SFX (키) =====
    public void PlaySFX(string key)
    {
        if (!library || !library.sfxClips.TryGetValue(key, out var clip) || !clip)
        {
            Debug.LogWarning($"[AudioManager] SFX key not found: {key}");
            return;
        }
        PlaySFX(clip);
    }
    public void PlayTypeSFX()
    {
        string key = "type" + Random.Range(0, 6).ToString();
        if (!library || !library.sfxClips.TryGetValue(key, out var clip) || !clip)
        {
            Debug.LogWarning($"[AudioManager] SFX key not found: {key}");
            return;
        }
        PlaySFX(clip);
    }

    // SFX (클립)
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (!clip) return;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // ===== 페이드 유틸 =====
    IEnumerator FadeInBGM(AudioClip clip, float fade, float targetVol, float pitch)
    {
        if (bgmSource.isPlaying)
            yield return StartCoroutine(FadeVolume(bgmSource, 0f, fade));

        bgmSource.clip = clip;
        bgmSource.pitch = pitch;
        //bgmSource.volume = 0f;
        bgmSource.Play();

        //yield return StartCoroutine(FadeVolume(bgmSource, targetVol, fade));
    }

    IEnumerator FadeOutAndStop(AudioSource src, float fade)
    {
        yield return StartCoroutine(FadeVolume(src, 0f, fade));
        src.Stop();
    }

    IEnumerator FadeVolume(AudioSource src, float target, float duration)
    {
        float start = src.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        src.volume = target;
    }
}
