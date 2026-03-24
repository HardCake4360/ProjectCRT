using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public enum InterogationState
{
    Idle,
    Testify,
    Question,
    Clue,
    Paradox
}

public class InterogationManager : MonoBehaviour
{
    public static InterogationManager Instance { get; private set; }

    public InterogationState InterogationState;
    public List<ParadoxParts> ParadoxSet;

    [Header("Setting")]
    [SerializeField] private float questionDelay;
    [SerializeField] private Animator UIAnimator;

    private bool isInterogationEnd;

    private int idx;
    public TipObj ITG_tip;

    [Header("Properties")]
    public GameObject ItgGroup;
    public ReactiveButton button_tes;
    public ReactiveButton button_clues;
    public ReactiveButton button_paradox;
    [SerializeField] private ParadoxParts paradoxOpening;
    [SerializeField] private ParadoxParts paradoxEnding;

    [Header("ManagerSetting")]
    public DTB_transformSetter DTB_Setter;
    public ChoicesUIControler CUI;

    public void StartInterogation()
    {
        MainLoop.Instance.SetMainLoopState(MainState.Interogate);
        InterogationState = InterogationState.Idle;
        ItgGroup.SetActive(true);
    }

    public void EndInterogation()
    {
        SceneLoader.Instance.LoadScene("Lspd1");
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        ITG_tip = ScriptableObject.Instantiate(ITG_tip);
        isInterogationEnd = false;
        InterogationState = InterogationState.Idle;
    }

    private void Update()
    {
        if (Input.anyKeyDown) OnSelect();
    }

    [Tooltip("상태 이름 Idle, Testify, Clue, Paradox")]
    public void SetInterogationState(string state)
    {
        foreach (InterogationState item in Enum.GetValues(typeof(InterogationState)))
        {
            if (item.ToString() == state)
            {
                InterogationState = item;
            }
        }
    }

    public void OnSelect()
    {
        if (isInterogationEnd)
        {
            return;
        }

        switch (InterogationState)
        {
            case InterogationState.Idle:
                break;

            case InterogationState.Testify:
                Debug.Log("진술 상태");

                if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.Objection)
                    && !DialogueManager.Instance.IsTyping()
                    && !LocalDiaManager.Instance.IsSelecting())
                {
                    StartCoroutine(questionAni());
                }
                break;

            case InterogationState.Question:
                break;

            case InterogationState.Clue:
                Debug.Log("증거 제시 상태");
                break;

            case InterogationState.Paradox:
                Debug.Log("패러독스 노멀라이즈 상태");
                break;
        }
    }

    public void BuildParadoxAndExecute()
    {
        DialogueManager.Instance.SetSelecting(false);
        CUI.SetSelfActive(false);
        InterogationState = InterogationState.Paradox;

        var runtimeSet = new List<ParadoxParts>();

        foreach (var part in ParadoxSet)
        {
            runtimeSet.Add(ScriptableObject.Instantiate(part));
        }

        var opening = ScriptableObject.Instantiate(paradoxOpening);
        var ending = ScriptableObject.Instantiate(paradoxEnding);

        runtimeSet.Add(opening);
        runtimeSet.Sort((a, b) => a.SortingPriority.CompareTo(b.SortingPriority));
        runtimeSet.Add(ending);

        for (int i = 0; i < runtimeSet.Count - 1; i++)
        {
            runtimeSet[i].TailDia = runtimeSet[i + 1];
        }

        runtimeSet[0].DetonateEvent();
    }

    private IEnumerator questionAni()
    {
        UIAnimator.Play("question", 0, 0f);
        yield return new WaitForSeconds(questionDelay);

        DialogueManager.Instance.QuestionEvent();
        InterogationState = InterogationState.Question;
    }

    public void ReturnToSelection()
    {
        InterogationState = InterogationState.Idle;
        if (CUI != null)
        {
            CUI.SetSelfActive(false);
        }
    }

    public void PlayUIAnim(string name)
    {
        UIAnimator.Play(name);
    }
}
