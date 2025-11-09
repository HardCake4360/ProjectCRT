using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class terminalManager : MonoBehaviour
{
    public GameObject dirLine;
    public GameObject responseLine;

    public MeshTMPInputField terminalInput;
    public GameObject userInputLine;
    public ScrollRect scrollRect;
    public GameObject msgList;

    public List<ClueObj> Clues;

    Interpreter interpreter;


    private void Start()
    {
        interpreter = GetComponent<Interpreter>();
        foreach(var clue in Clues)
        {
            clue.isCleared = false;
        }
    }

    private void OnGUI()
    {
        if(terminalInput.isFocused && terminalInput.inputField.text != "" && Input.GetKeyDown(KeyCode.Return))
        {
            //유저의 입력을 저장
            string userInput = terminalInput.inputField.text;

            //인풋 필드 클리어
            ClearInputField();

            //디렉토리 라인 인스턴스화
            AddUserLine(userInput);

            // 프리팹 미리 생성
            GameObject response = Instantiate(responseLine, msgList.transform);
            response.transform.SetAsLastSibling();

            // 높이 증가
            Vector2 listSize = msgList.GetComponent<RectTransform>().sizeDelta;
            msgList.GetComponent<RectTransform>().sizeDelta = new Vector2(listSize.x, listSize.y + 35f);

            //입력 라인을 마지막줄로 옮기고 출력하는동안 숨기기
            userInputLine.transform.SetAsLastSibling();
            userInputLine.SetActive(false);

            TextMeshProUGUI uiText = response.GetComponentInChildren<TextMeshProUGUI>();

            StartCoroutine(interpreter.Interpret(userInput,uiText, () =>
            {
                //출력 끝난 뒤(OnComplete) 구문
                userInputLine.SetActive(true);

                //입력 라인을 마지막줄로 옮기기
                userInputLine.transform.SetAsLastSibling();

                //바닥쪽으로 스크롤
                scrollRect.verticalNormalizedPosition = 0;

                //일반적으로 답변 완성시 특정 문자열의 포함을 확인
                StartCoroutine(CheckCluesSequentially(uiText.text));


                //입력 필드 리포커싱
                terminalInput.inputField.ActivateInputField();
                terminalInput.inputField.Select();
            }));
        }
    }

    void ClearInputField()
    {
        terminalInput.inputField.text = "";
    }

    void AddUserLine(string userInput)
    {
        //커맨드 라인 컨테이너 리사이즈
        Vector2 msgListSize = msgList.GetComponent<RectTransform>().sizeDelta;
        msgList.GetComponent<RectTransform>().sizeDelta = new Vector2(msgListSize.x, msgListSize.y + 35f);

        //디렉토리 라인 객체 생성
        GameObject msg = Instantiate(dirLine, msgList.transform);

        //자식객체 인덱스 설정
        msg.transform.SetSiblingIndex(msgList.transform.childCount - 1);

        //생성한 객체 텍스트 설정
        msg.GetComponentsInChildren<TextMeshProUGUI>()[1].text = userInput;
    }

    int AddInterpreterLines(List<string> interpretation)
    {
        for(int i = 0; i < interpretation.Count; i++)
        {
            //답변 객체화
            GameObject response = Instantiate(responseLine, msgList.transform);

            //마지막에 위치하도록 함
            response.transform.SetAsLastSibling();

            //메시지 리스트의 크기를 받아온 후 리사이즈
            Vector2 listSize = msgList.GetComponent<RectTransform>().sizeDelta;
            msgList.GetComponent<RectTransform>().sizeDelta = new Vector2(listSize.x, listSize.y + 35f);

            response.GetComponentInChildren<TextMeshProUGUI>().text = interpretation[i];

        }

        return interpretation.Count;
    }

    void ScrollToBottom(int lines)
    {
        if(lines > 4)
        {
            scrollRect.velocity = new Vector2(0, 900);
        }
        else
        {
            scrollRect.verticalNormalizedPosition = 0;
        }
    }

    private IEnumerator CheckCluesSequentially(string text)
    {
        foreach (var clue in Clues)
        {
            if (clue.isCleared) continue;
            Debug.Log("----------------checking " + clue.Keyword);
            bool dialogueFinished = false;

            // 종료 이벤트 감시
            UnityAction onEnd = () => dialogueFinished = true;
            DialogueManager.Instance.StaticOnDialogueEnd.AddListener(onEnd);

            // 키워드 있으면 Dialogue 시작
            clue.FindKeywordFrom(text);

            // Dialogue 발생했으면 종료될 때까지 기다리기
            while (DialogueManager.Instance.dialogueData != null && !dialogueFinished)
                yield return null;

            // 리스너 제거 (중복 방지)
            DialogueManager.Instance.StaticOnDialogueEnd.RemoveListener(onEnd);
        }
        Debug.Log("checking end");
    }

}
