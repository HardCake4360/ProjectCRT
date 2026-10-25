using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectCRT.Cutscenes
{
    public sealed class CutsceneView : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Min(0.05f)] public float fadeDuration=0.28f;
        Canvas canvas;
        CanvasGroup panel,black;
        TMP_Text speaker,body,hint,loading;
        GameObject namePlate;
        float target,fadeFrom,fadeTime;
        public bool IsVisible=>panel!=null&&panel.alpha>0.001f;
        public float Opacity=>panel!=null?panel.alpha:0;
        public float BlackOpacity=>black!=null?black.alpha:0;
        public string SpeakerName=>speaker!=null?speaker.text:"";
        public int VisibleCharacters=>body!=null?Mathf.Min(body.maxVisibleCharacters,body.textInfo.characterCount):0;
        public string FullText=>body!=null?body.text:"";
        public bool FadeComplete=>panel!=null&&Mathf.Abs(panel.alpha-target)<0.001f;

        public void EnsureBuilt()
        {
            if(canvas!=null)return;
            var root=UI("Cutscene Canvas",transform);canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;
            var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=0.5f;
            var box=UI("Dialogue",root.transform);Rect(box,new Vector2(.055f,.045f),new Vector2(.945f,.31f));
            panel=box.AddComponent<CanvasGroup>();panel.alpha=0;panel.blocksRaycasts=false;panel.interactable=false;
            Image(box,new Color(.035f,.045f,.065f,.96f));
            var border=UI("Top border",box.transform);Rect(border,new Vector2(0,.985f),Vector2.one);Image(border,new Color(.66f,.61f,.47f,1));
            namePlate=UI("Name",box.transform);Rect(namePlate,new Vector2(0,1),new Vector2(.28f,1.21f));Image(namePlate,new Color(.035f,.045f,.065f,.96f));
            speaker=Text("Speaker",namePlate.transform,new Vector2(.06f,.08f),new Vector2(.94f,.95f),22);speaker.color=new Color(.94f,.78f,.48f);
            body=Text("Text",box.transform,new Vector2(.035f,.24f),new Vector2(.965f,.87f),25);body.textWrappingMode=TextWrappingModes.Normal;
            hint=Text("Advance",box.transform,new Vector2(.035f,.025f),new Vector2(.965f,.18f),14);hint.alignment=TextAlignmentOptions.Right;hint.color=new Color(.65f,.68f,.71f);
            var shade=UI("Loading cover",root.transform);Rect(shade,Vector2.zero,Vector2.one);Image(shade,Color.black);black=shade.AddComponent<CanvasGroup>();black.alpha=0;black.blocksRaycasts=false;
            loading=Text("Loading",shade.transform,new Vector2(.1f,.06f),new Vector2(.9f,.15f),20);loading.alignment=TextAlignmentOptions.Right;
            root.SetActive(false);
        }
        public void Begin()
        {
            EnsureBuilt();canvas.gameObject.SetActive(true);Hide();SetBlack(0);SetLoading("");
        }
        public void End(){if(canvas!=null)canvas.gameObject.SetActive(false);}
        public void SetLine(string name,string text,CutsceneDialogueKind kind)
        {
            EnsureBuilt();speaker.text=name??"";namePlate.SetActive(!string.IsNullOrWhiteSpace(name));
            body.fontStyle=kind==CutsceneDialogueKind.Thought?FontStyles.Italic:FontStyles.Normal;
            body.text=text??"";body.ForceMeshUpdate();body.maxVisibleCharacters=0;
        }
        public void Reveal(int count){body.maxVisibleCharacters=Mathf.Clamp(count,0,body.textInfo.characterCount);}
        public void Show(){fadeFrom=panel.alpha;target=1;fadeTime=0;}
        public void Hide(){if(panel==null)return;target=0;fadeFrom=0;fadeTime=0;panel.alpha=0;hint.text="";}
        public void SetHint(string text){if(hint!=null)hint.text=text;}
        public void SetBlack(float alpha){EnsureBuilt();black.alpha=Mathf.Clamp01(alpha);black.blocksRaycasts=alpha>0.01f;}
        public void SetLoading(string text){EnsureBuilt();loading.text=text??"";}
        void Update()
        {
            if(panel==null||Mathf.Approximately(panel.alpha,target))return;
            fadeTime+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(fadeTime/Mathf.Max(.05f,fadeDuration));
            panel.alpha=Mathf.Lerp(fadeFrom,target,t*t*(3-2*t));
        }
        static GameObject UI(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return go;}
        static void Rect(GameObject go,Vector2 min,Vector2 max){var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        static void Image(GameObject go,Color color){var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=false;}
        TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size)
        {
            var go=UI(name,parent);Rect(go,min,max);var text=go.AddComponent<TextMeshProUGUI>();if(font!=null)text.font=font;
            text.fontSize=size;text.color=new Color(.92f,.92f,.85f);text.raycastTarget=false;text.verticalAlignment=VerticalAlignmentOptions.Middle;return text;
        }
    }
}
