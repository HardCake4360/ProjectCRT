using UnityEngine;

/// <summary>Opt-in scene effect: fades a canvas in when its existing UI enables it.</summary>
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
[DefaultExecutionOrder(1100)]
public sealed class ConversationCanvasFade : MonoBehaviour
{
    [Min(0.05f)] public float duration=0.28f;
    Canvas canvas;
    CanvasGroup group;
    bool wasVisible;
    float elapsed;
    public float Opacity => group!=null?group.alpha:0;
    void Awake(){canvas=GetComponent<Canvas>();group=GetComponent<CanvasGroup>();Hide();}
    void Hide(){group.alpha=0;group.interactable=false;group.blocksRaycasts=false;wasVisible=false;elapsed=0;}
    void LateUpdate()
    {
        if(!canvas.enabled){Hide();return;}
        if(!wasVisible){wasVisible=true;elapsed=0;}
        else elapsed+=Time.unscaledDeltaTime;
        float t=Mathf.Clamp01(elapsed/Mathf.Max(0.05f,duration));
        group.alpha=t*t*(3-2*t);
        group.interactable=t>=1;group.blocksRaycasts=t>=1;
    }
    void OnDisable(){if(group!=null){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}}
}
