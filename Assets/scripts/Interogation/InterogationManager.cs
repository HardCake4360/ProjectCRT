using UnityEngine;
using System;
using System.Collections;

[System.Serializable]
public enum InterogationState
{
    Idle,
    Testify,
    Clue,
    Paradox
}

public class InterogationManager : MonoBehaviour
{
    private InterogationState interogationState;
    private bool isInterogationEnd;
    private DialogueUIManager DUIManager;

    private DialogueObject testamony;
    private DialogueObject[] clues; // 다이어로그 오브젝트로 했지만 클루에 따른 패러독스 획득을 위해 변경할 수 있음

    // 디버깅 완료 후 OnStart 함수에서 호출하는 함수로 변경할 것
    void Start()
    {
        isInterogationEnd = false;
        interogationState = InterogationState.Idle;
    }

    public void SetInterogation(DialogueObject tes, DialogueObject[] clues)
    {
        testamony = tes;
        this.clues = clues;
    }

    [Tooltip("상태 이름 Idle, Testify, Clue, Paradox")]
    public void SetInterogationState(string state)
    {
        // 모든 열거형 값 순회
        foreach (InterogationState item in Enum.GetValues(typeof(InterogationState)))
        {
            if(item.ToString() == state)
            {
                interogationState = item;
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

        switch (interogationState)
        {
            case InterogationState.Idle:
                // 플레이어 액션 대기
                break;

            case InterogationState.Testify:
                // 진술 다이어로그 진행
                Debug.Log("진술 상태");
                processDialogue(testamony);
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

    private IEnumerator processDialogue(DialogueObject dia)
    {
        int idx = 0;

        //대사 출력 루프(DUIManager 유사)
        while (true)
        {
            if ((idx == 0 || 
                InputManager.Instance.IsAnyKeyPressedIn(
                InputManager.Instance.DialogueAdvanceKeys)))
            {
                // 타이핑 중이면 스킵
                if (DUIManager.IsTyping())
                {
                    DUIManager.StopAllCoroutines();
                    DUIManager.SkipText(dia.lines[idx - 1].text);
                    continue;
                }

                if (dia.lines[idx].characterName == "end")
                {
                    if (dia.TailDia)
                    {
                        dia.TailDia.DetonateEvent();
                        continue;
                    }
                    DUIManager.SetCanvasActive(false);

                    StaticOnDialogueEnd?.Invoke();
                    OnDialogueEnd?.Invoke();

                    //모든 상호작용 오브젝트에 딜레이 생성
                    foreach (var interactObj in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
                        interactObj.SetInteractableWithDelay(0.2f);

                    continue;
                }

                // 대사 타이핑 시작
                if (idx < dia.lines.Length)
                {
                    DUIManager.DisplayDialogue(dia.lines[idx]);
                    idx++;
                }
            }

            //선택지 표시, 선택 상태 진입
            if (dia.lines[idx].choices && !DUIManager.IsTyping())
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }
    } 
}
