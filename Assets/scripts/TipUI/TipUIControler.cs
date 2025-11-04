using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Events;
using TMPro;

public class TipUIControler : MonoBehaviour
{
    public static TipUIControler Instance { get; private set; }

    Canvas canvas;
    private TipObj tipObj;
    private UnityEvent OnStart;
    private UnityEvent OnEnd;
    //인스펙터에서 정하는 스타트/엔드 이벤트 (setEvent로 바뀌지 않음, 모든 이벤트가 공유)
    public UnityEvent StaticOnStart; 
    public UnityEvent StaticOnEnd;

    public TextMeshProUGUI TipName;
    public TextMeshProUGUI TipText;
    public Image TipImage;
    public GameObject CloseReminder;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 전환에도 유지
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        canvas = GetComponent<Canvas>();
        CloseReminder.SetActive(false);
        canvas.enabled = false;
    }

    public void TipEventTrigger(TipObj tip)
    {
        canvas.enabled = true;
        tipObj = tip;
        StartEvent();
        StartCoroutine(printTipEvent());
    }

    public void SetOnStartEvent(UnityEvent evt)
    {
        OnStart = evt;
    }

    public void SetOnEndEvent(UnityEvent evt)
    {
        OnEnd = evt;
    }

    public void StartEvent()
    {
        StaticOnStart?.Invoke();
        OnStart?.Invoke();
    }
    public void EndEvent()
    {
        StaticOnEnd?.Invoke();
        OnEnd?.Invoke();
    }

    IEnumerator printTipEvent()
    {
        int idx = 0;
        int len = tipObj.lines.Length;
        int prevIdx = -1;

        if (!tipObj)
        {
            Debug.Log("TipObj is null you idiot!!");
            yield break;
        }

        while (true)
        {
            yield return null;
            if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.ToLeft))
            {
                idx = Mathf.Clamp(idx - 1, 0, len);
            }
            else if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.ToRight)
                || InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.DialogueAdvanceKeys))
            {
                idx = Mathf.Clamp(idx + 1, 0, len);
            }

            if (idx == len) break;

            CloseReminder.SetActive(idx == len - 1);

            if (prevIdx != idx)
            {
                prevIdx = idx;
                TipName.text = tipObj.TipName;
                TipText.text = tipObj.lines[idx].TipText;
                TipImage.sprite = tipObj.lines[idx].TipImage;
            }
        }

        CloseReminder.SetActive(false);
        canvas.enabled = false;
        EndEvent();
    }


}
