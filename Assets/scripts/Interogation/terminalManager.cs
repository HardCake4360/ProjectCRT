using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class terminalManager : MonoBehaviour
{
    public GameObject dirLine;
    public GameObject responseLine;

    public TMP_InputField terminalInput;
    public GameObject userInputLine;
    public ScrollRect scrollRect;
    public GameObject msgList;

    Interpreter interpreter;

    private void Start()
    {
        interpreter = GetComponent<Interpreter>();
    }

    private void OnGUI()
    {
        if(terminalInput.isFocused && terminalInput.text != "" && Input.GetKeyDown(KeyCode.Return))
        {
            //유저의 입력을 저장
            string userInput = terminalInput.text;

            //인풋 필드 클리어
            ClearInputField();

            //디렉토리 라인 인스턴스화
            AddDirectoryLine(userInput);

            // 프리팹 미리 생성
            GameObject response = Instantiate(responseLine, msgList.transform);
            response.transform.SetAsLastSibling();

            // 높이 증가
            Vector2 listSize = msgList.GetComponent<RectTransform>().sizeDelta;
            msgList.GetComponent<RectTransform>().sizeDelta = new Vector2(listSize.x, listSize.y + 35f);

            //입력 라인을 마지막줄로 옮기고 출력하는동안 숨기기
            userInputLine.transform.SetAsLastSibling();
            userInputLine.SetActive(false);

            Text uiText = response.GetComponentInChildren<Text>();

            StartCoroutine(interpreter.Interpret(userInput,uiText, () =>
            {
                userInputLine.SetActive(true);

                //입력 라인을 마지막줄로 옮기기
                userInputLine.transform.SetAsLastSibling();

                //바닥쪽으로 스크롤
                scrollRect.verticalNormalizedPosition = 0;

                //입력 필드 리포커싱
                terminalInput.ActivateInputField();
                terminalInput.Select();
            }));
        }
    }

    void ClearInputField()
    {
        terminalInput.text = "";
    }

    void AddDirectoryLine(string userInput)
    {
        //커맨드 라인 컨테이너 리사이즈
        Vector2 msgListSize = msgList.GetComponent<RectTransform>().sizeDelta;
        msgList.GetComponent<RectTransform>().sizeDelta = new Vector2(msgListSize.x, msgListSize.y + 35f);

        //디렉토리 라인 객체 생성
        GameObject msg = Instantiate(dirLine, msgList.transform);

        //자식객체 인덱스 설정
        msg.transform.SetSiblingIndex(msgList.transform.childCount - 1);

        //생성한 객체 텍스트 설정
        msg.GetComponentsInChildren<Text>()[1].text = userInput;
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

            response.GetComponentInChildren<Text>().text = interpretation[i];

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

}
