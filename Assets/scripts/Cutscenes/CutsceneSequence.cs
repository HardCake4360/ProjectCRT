using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace ProjectCRT.Cutscenes
{
    public enum CutsceneExitMode { Return, LoadScene, ReturnToPreviousScene }
    public enum CutsceneDialogueKind { Spoken, Narration, Thought }
    public enum CutsceneNameMode { Profile, Hidden, Alias }
    public enum CutsceneVoiceMode { Inherit, Silent, Blip, Syllable }
    public enum CutsceneAdvanceMode { Manual, Auto }
    public enum CutsceneCameraMode { Maintain, Cut, Blend, Rough, CutThenZoom, Path }
    public enum CutsceneCueTiming { TurnStart, AfterCameraArrives, TextCharacter, TextComplete, OnAdvance }

    [Serializable]
    public sealed class CutsceneCameraCue
    {
        public CutsceneCameraMode mode=CutsceneCameraMode.Maintain;
        public string shotId;
        public string pathId;
        [Min(0)] public float duration=0.7f;
        [Min(0)] public float joinDuration=0.35f;
        [Range(0,1)] public float pathStart;
        [Range(0,1)] public float pathEnd=1;
        public bool constantPathSpeed=true;
        public string lookAtId;
        public bool useEndFieldOfView;
        [Range(15,100)] public float endFieldOfView=28;
        public AnimationCurve easing=AnimationCurve.EaseInOut(0,0,1,1);
    }

    [Serializable]
    public sealed class CutsceneActorCue
    {
        public string actorId;
        public CutsceneCueTiming timing=CutsceneCueTiming.TurnStart;
        [Min(0)] public float delay;
        [Min(1),Tooltip("One-based visible character index. 1 means the first displayed glyph.")]
        public int characterIndex=1;
        public bool move;
        public string positionId;
        public Vector3 positionOffset;
        public bool rotate;
        public Vector3 eulerAngles;
        public string poseId;
        public AnimationClip clip;
        public bool loop=true;
        public bool freezePose;
        [Min(0)] public float startTime;
        [Min(0.01f)] public float speed=1;
        [Tooltip("Empty keeps the previous expression. Use a mapped neutral expression to reset.")]
        public string expressionId;
        public bool changeLookAt;
        public string lookAtId;
        public Vector3 lookAtOffset;
    }

    [Serializable]
    public sealed class CutsceneTurn
    {
        public string id=Guid.NewGuid().ToString("N");
        public string label="New turn";
        public bool hasDialogue=true;
        public CutsceneDialogueKind dialogueKind;
        public string speakerId;
        public CutsceneNameMode nameMode;
        public string alias;
        [TextArea(2,8)] public string text;
        [Min(1)] public float charsPerSecond=25;
        [Min(0)] public float punctuationPause=0.16f;
        public CutsceneVoiceMode voiceMode=CutsceneVoiceMode.Inherit;
        public CutsceneCameraCue camera=new CutsceneCameraCue();
        public List<CutsceneActorCue> actors=new List<CutsceneActorCue>();
        public CutsceneAdvanceMode advance=CutsceneAdvanceMode.Manual;
        [Min(0)] public float minimumDuration;
        [Min(0)] public float autoDelay=0.25f;
        public bool dialogueAfterCamera=true;
        public bool waitForCamera=true;
        public bool waitForActors;
        public TimelineAsset timeline;
        public bool waitForTimeline=true;
    }

    [CreateAssetMenu(menuName="ProjectCRT/Cutscene/Sequence",fileName="CutsceneSequence")]
    public sealed class CutsceneSequence : ScriptableObject
    {
        public string sequenceId=Guid.NewGuid().ToString("N");
        [Tooltip("Empty uses the currently loaded stage. Otherwise load this scene additively.")]
        public string stageScene;
        public bool unloadStageOnFinish=true;
        public bool allowSkip=true;
        [Tooltip("Use when this scene opens directly into a cutscene, rather than fading out gameplay first.")]
        public bool beginWithBlack;
        [Min(0)] public float fadeDuration=0.3f;
        public CutsceneExitMode exitMode=CutsceneExitMode.Return;
        public string destinationScene;
        public string destinationSpawnId;
        public List<AudioClip> preloadAudio=new List<AudioClip>();
        public List<CutsceneTurn> turns=new List<CutsceneTurn>();
    }
}
