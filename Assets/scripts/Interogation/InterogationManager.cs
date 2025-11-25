using UnityEngine;

public enum InterogationState
{
    Idle,
    Testify,
    PresentClue,
    NormalizeParadox
}

public class InterogationManager : MonoBehaviour
{
    private InterogationState interogationState;
    private bool isInterogationEnd;
    private DialogueUIManager DUIManager;

    // 디버깅 완료 후 OnStart 함수에서 호출하는 함수로 변경할 것
    void Start()
    {
        isInterogationEnd = false;
    }

    // 디버깅 완료 후 메인루프에서 호출하는 함수로 변경할 것
    void Update()
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

                break;
            case InterogationState.PresentClue:
                // 클루 선택

                break;
            case InterogationState.NormalizeParadox:
                // 여기는 코루틴으로 한번에 진행
                break;

        }
    }

    private void processDialogue(DialogueObject dia)
    {

    } 
}
