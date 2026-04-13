using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Playables;
using System.Collections;

enum emotionState
{
    happy,
    sad,
    surprise,
}

public class NPC_script : Interactable
{
    public UnityEvent OnInteract;
    [SerializeField] private NpcInvestigationController investigationController;

    [Header("Investigation Animation")]
    [SerializeField] private Animator npcAnimator;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip onInteractionClip;
    [SerializeField] private AnimationClip onInteractionIdleClip;
    [SerializeField] private AnimationClip startThinkingClip;
    [SerializeField] private AnimationClip thinkingClip;
    [SerializeField] private AnimationClip onAnswerClip;

    [Header("Conversation Look At")]
    [SerializeField] private bool lookAtCameraDuringInteraction = true;
    [SerializeField] private Transform lookAtTargetOverride;
    [SerializeField, Range(0f, 1f)] private float lookAtWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float lookAtBodyWeight = 0.15f;
    [SerializeField, Range(0f, 1f)] private float lookAtHeadWeight = 0.85f;
    [SerializeField, Range(0f, 1f)] private float lookAtEyesWeight = 0.4f;
    [SerializeField, Range(0f, 1f)] private float lookAtClampWeight = 0.7f;
    [SerializeField] private float lookAtBlendSpeed = 6f;
    [SerializeField] private Vector3 lookAtTargetOffset = new Vector3(0f, -0.05f, 0f);

    private PlayableGraph animationGraph;
    private Coroutine oneShotCoroutine;
    private NpcLookAtIKDriver lookAtDriver;
    private int animationSequence;

    protected override void Awake()
    {
        base.Awake();

        if (npcAnimator == null)
        {
            npcAnimator = GetComponentInChildren<Animator>();
        }

        EnsureLookAtDriver();
        PlayIdle();
    }

    private void OnDisable()
    {
        StopOneShotCoroutine();
        DestroyAnimationGraph();
    }

    override public void Interact()
    {
        if (!canInteract) return;

        if (investigationController == null)
        {
            investigationController = GetComponent<NpcInvestigationController>();
        }

        if (investigationController != null && investigationController.enabled)
        {
            investigationController.BeginInteraction();
            return;
        }

        OnInteract?.Invoke();
    }

    public void StaticDiaEventTrigger(DialogueObject data)
    {
        DialogueManager.Instance.DialogueEventTrigger(data);
    }

    public void Emotion()
    {

    }

    public void PlayIdle()
    {
        PlayLoop(idleClip);
    }

    public void PlayInteractionStart()
    {
        PlayOneShotThenLoop(onInteractionClip, onInteractionIdleClip);
    }

    public void PlayInteractionIdle()
    {
        PlayLoop(onInteractionIdleClip);
    }

    public void PlayThinking()
    {
        PlayOneShotThenLoop(startThinkingClip, thinkingClip);
    }

    public void PlayAnswerStart()
    {
        PlayOneShotThenLoop(onAnswerClip, onInteractionIdleClip);
    }

    public void SetLookAtCamera(bool enabled)
    {
        if (!lookAtCameraDuringInteraction)
        {
            enabled = false;
        }

        EnsureLookAtDriver();

        if (lookAtDriver == null)
        {
            return;
        }

        Transform target = lookAtTargetOverride;
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        lookAtDriver.Configure(
            lookAtWeight,
            lookAtBodyWeight,
            lookAtHeadWeight,
            lookAtEyesWeight,
            lookAtClampWeight,
            lookAtBlendSpeed,
            lookAtTargetOffset);
        lookAtDriver.SetLookAtTarget(target, enabled);
    }

    private void PlayLoop(AnimationClip clip)
    {
        StopOneShotCoroutine();
        animationSequence++;

        if (clip == null)
        {
            return;
        }

        PlayClip(clip, WrapMode.Loop);
    }

    private void PlayOneShotThenLoop(AnimationClip oneShotClip, AnimationClip fallbackLoopClip)
    {
        StopOneShotCoroutine();
        int sequence = ++animationSequence;

        if (oneShotClip == null)
        {
            PlayLoop(fallbackLoopClip);
            return;
        }

        PlayClip(oneShotClip, WrapMode.Once);
        oneShotCoroutine = StartCoroutine(PlayFallbackAfterOneShot(sequence, oneShotClip.length, fallbackLoopClip));
    }

    private IEnumerator PlayFallbackAfterOneShot(int sequence, float delay, AnimationClip fallbackLoopClip)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));

        if (sequence == animationSequence)
        {
            PlayLoop(fallbackLoopClip);
        }
    }

    private void PlayClip(AnimationClip clip, WrapMode wrapMode)
    {
        if (npcAnimator == null || clip == null)
        {
            return;
        }

        clip.wrapMode = wrapMode;
        npcAnimator.enabled = true;
        DestroyAnimationGraph();

        animationGraph = PlayableGraph.Create($"{name}_NpcInvestigationAnimation");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(animationGraph, "NPC Animation", npcAnimator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(animationGraph, clip);
        clipPlayable.SetTime(0);
        output.SetSourcePlayable(clipPlayable);
        animationGraph.Play();
    }

    private void StopOneShotCoroutine()
    {
        if (oneShotCoroutine == null)
        {
            return;
        }

        StopCoroutine(oneShotCoroutine);
        oneShotCoroutine = null;
    }

    private void DestroyAnimationGraph()
    {
        if (animationGraph.IsValid())
        {
            animationGraph.Destroy();
        }
    }

    private void EnsureLookAtDriver()
    {
        if (lookAtDriver != null || npcAnimator == null)
        {
            return;
        }

        lookAtDriver = npcAnimator.GetComponent<NpcLookAtIKDriver>();
        if (lookAtDriver == null)
        {
            lookAtDriver = npcAnimator.gameObject.AddComponent<NpcLookAtIKDriver>();
        }
    }
}
