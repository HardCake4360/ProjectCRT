using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;

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
    private bool isInterogationEnd;
    private Animator UIAnimator;

    private int idx;
    private DialogueObject[] clues; // 다이어로그 오브젝트로 했지만 클루에 따른 패러독스 획득을 위해 변경할 수 있음

    // 하위 매니저들
    [Header("ManagerSetting")]
    public DTB_transformSetter DTB_Setter;

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
            if(item.ToString() == state)
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
                if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.Objection)
                    && !LocalDiaManager.Instance.DUIManager.IsTyping()
                    && !LocalDiaManager.Instance.IsSelecting())
                {
                    LocalDiaManager.Instance.QuestionEvent();
                    InterogationState = InterogationState.Question;
                }
                break;

            case InterogationState.Question:
                // 진술 도중 오브젝션
                
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
}
