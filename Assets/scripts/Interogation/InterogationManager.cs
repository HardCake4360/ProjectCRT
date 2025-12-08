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
    [SerializeField] private float questionDelay; // 추궁 애니메이션 후 이벤트 진행까지 걸리는 시간
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

    // 하위 매니저들
    [Header("ManagerSetting")]
    public DTB_transformSetter DTB_Setter;
    public ChoicesUIControler CUI;

    public void StartInterogation()
    {
        MainLoop.Instance.SetMainLoopState(MainState.Interogate);
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

    // 디버깅 완료 후 OnStart 함수에서 호출하는 함수로 변경할 것
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
        // 모든 열거형 값 순회
        foreach (InterogationState item in Enum.GetValues(typeof(InterogationState)))
        {
            if (item.ToString() == state)
            {
                InterogationState = item;
            }
        }
    }

    // 선택 이벤트 발생할 때 마다 호출
    public void OnSelect()
    {
        if (isInterogationEnd)
        {
            // 종료 동작 실행
            return;
        }

        switch (InterogationState)
        {
            case InterogationState.Idle:
                // 플레이어 액션 대기
                break;

            case InterogationState.Testify:
                // 진술 다이어로그 진행
                Debug.Log("진술 상태");

                // 키 입력 && 타이핑 상태 아닐때 && 선택지 나오는 중 아닐때
                // 추궁 시작
                if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.Objection)
                    && !LocalDiaManager.Instance.DUIManager.IsTyping()
                    && !LocalDiaManager.Instance.IsSelecting())
                {
                    StartCoroutine(questionAni());
                }
                break;

            case InterogationState.Question:
                // 진술 도중 추궁

                break;

            case InterogationState.Clue:
                // 클루 선택
                Debug.Log("증거 제시 상태");

                break;

            case InterogationState.Paradox:
                Debug.Log("패러독스 노멀라이즈 상태");
                break;

        }
    }

    public void BuildParadoxAndExecute()
    {
        LocalDiaManager.Instance.SetSelecting(false);
        LocalDiaManager.Instance.CUI.SetSelfActive(false);

        // 1) 런타임 전용 복제본 리스트 구성
        var runtimeSet = new List<ParadoxParts>();

        // 기존 ParadoxSet의 항목들을 전부 런타임 복제
        foreach (var p in ParadoxSet)
            runtimeSet.Add(ScriptableObject.Instantiate(p));

        // 오프닝/엔딩도 복제해서 사용
        var op = ScriptableObject.Instantiate(paradoxOpening);
        var ed = ScriptableObject.Instantiate(paradoxEnding);

        runtimeSet.Add(op);
        runtimeSet.Sort((a, b) => a.SortingPriority.CompareTo(b.SortingPriority));
        runtimeSet.Add(ed);

        // 2) 복제본들끼리만 TailDia 연결 (원본 SO는 건드리지 않음)
        for (int i = 0; i < runtimeSet.Count - 1; i++)
            runtimeSet[i].TailDia = runtimeSet[i + 1];

        // 3) 실행
        runtimeSet[0].DetonateEvent();
    }


    private IEnumerator questionAni()
    {
        // 애니메이션 재생+딜레이
        UIAnimator.Play("question", 0, 0f);
        yield return new WaitForSeconds(questionDelay);
        
        //이벤트 실행
        LocalDiaManager.Instance.QuestionEvent();
        InterogationState = InterogationState.Question;

        yield break;
    }

    // 스크립터블 오브젝트 전역함수 연결용
    public void PlayUIAnim(string name)
    {
        UIAnimator.Play(name);
    }
}