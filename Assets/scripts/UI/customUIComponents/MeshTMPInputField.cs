using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(TMP_InputField))]
public class MeshTMPInputField : MeshRayReciver
{
    [Header("Target Graphic (any UI element for hit area)")]
    public Graphic TargetGraphic;  // Image, RawImage, TMP_Text 등 가능
    public bool isFocused = false;
    public TMP_InputField inputField;


    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        inputField.interactable = true;

        // targetGraphic 자동 탐색
        if (TargetGraphic == null)
        {
            TargetGraphic = GetComponentInChildren<Graphic>();
        }
    }

    private void Start()
    {
        // TMP가 런타임 중 caret을 만들기 때문에, Start에서 조금 늦게 비활성화 시도
        StartCoroutine(DisableCaretRaycastNextFrame());
    }

    private System.Collections.IEnumerator DisableCaretRaycastNextFrame()
    {
        yield return null; // 한 프레임 기다려 caret 생성 완료 대기

        var caret = GetCaretGraphic();
        if (caret != null)
        {
            caret.raycastTarget = false;
            Debug.Log($"[{name}] caret raycastTarget disabled!");
        }
    }

    private Graphic GetCaretGraphic()
    {
        if (inputField.textViewport == null) return null;

        // textViewport 아래에서 TMP_SelectionCaret 컴포넌트를 탐색
        return inputField.textViewport.GetComponentInChildren<TMP_SelectionCaret>(true);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (!IsWithinTarget(eventData))
        {
            ActivateInput();
        }
        else
        {
            DeactivateInput();
        }
        
    }

    private bool IsWithinTarget(PointerEventData eventData)
    {
        if (TargetGraphic == null)
            return true;

        RectTransform rect = TargetGraphic.rectTransform;
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return rect.rect.Contains(localPoint);
        }
        return false;
    }

    private void ActivateInput()
    {
        if (!isFocused)
        {
            isFocused = true;
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    public void DeactivateInput()
    {
        if (isFocused)
        {
            isFocused = false;
            inputField.DeactivateInputField();
        }
    }
}
