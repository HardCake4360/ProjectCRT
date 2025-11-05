using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "TipObj", menuName = "Scriptable Objects/TipObj")]
public class TipObj : ScriptableObject
{
    public string TipName;
    [System.Serializable]
    public class TipLine
    {
        public Sprite TipImage;
        public string TipText;
    }
    public TipLine[] lines;

    private bool isActive = false;//이벤트가 발생한 적이 있으면 True
    public UnityEvent OnStart;
    public UnityEvent OnEnd;

    public void DetonateEvent()
    {
        if (isActive) return;
        TipUIControler.Instance.TipEventTrigger(this);
        //이벤트 실행시 널이면 아무것도 실행 안되기 때문에 널체크 안함
        TipUIControler.Instance.SetOnStartEvent(OnStart);
        TipUIControler.Instance.SetOnEndEvent(OnEnd);
    }
}
