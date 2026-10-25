using System.Collections;
using ProjectCRT.Cutscenes;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectCRT.Prologue
{
    public sealed class SC23PlaybackController : MonoBehaviour
    {
        public bool isRoom;
        public CutsceneDirector director;
        public CutsceneSequence sequence;
        public PrologueSanMotor san;
        public Transform[] boxes;
        public Transform sofaApproach,afterSofa,entrance;
        public AudioSource rain,jazz,crowd,effects;
        public AudioClip unpackSound,doorOpen,doorClose,thud;
        public TMP_FontAsset font;
        public bool MapUnlocked{get;private set;}
        public bool Completed{get;private set;}
        public bool Busy{get;private set;}
        public int PackedCount{get;private set;}
        public int LastTurn{get;private set;}=-1;
        bool[] packed;
        Vector3[] boxScales;
        bool mapOpen,loading;
        float entered;
        Vector3 returnPosition;
        Quaternion returnRotation;
        Canvas canvas;
        TMP_Text goal,hint,ending;
        GameObject map;
        Image black;
        void Awake()
        {
            packed=new bool[boxes.Length];boxScales=new Vector3[boxes.Length];for(int i=0;i<boxes.Length;i++)boxScales[i]=boxes[i].localScale;
            BuildUI();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void OnEnable(){director.Finished+=Finished;director.Committed+=Commit;}
        void Start(){StartLoop(rain);if(isRoom)SetAudio(rain,.035f);foreach(var p in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))p.Play(true);}
        void Update()
        {
            var key=Keyboard.current;
            if(key!=null&&key.f6Key.wasPressedThisFrame&&!loading&&!director.IsPlaying&&!Busy){Replay();return;}
            bool playing=CutsceneDirector.IsActive;
            if(!playing&&!Busy&&!loading&&!Completed&&key!=null)
            {
                if(MapUnlocked&&key.mKey.wasPressedThisFrame)SetMap(!mapOpen);
                if(mapOpen&&(key.escapeKey.wasPressedThisFrame||key.mKey.wasPressedThisFrame&&!MapUnlocked))SetMap(false);
                if(key.eKey.wasPressedThisFrame||key.enterKey.wasPressedThisFrame){if(mapOpen)Travel();else TryInteract();}
            }
            if(goal!=null){goal.gameObject.SetActive(!playing&&!Completed);goal.text=loading?"샹그리아로 이동 중…":isRoom?(MapUnlocked?"목적지: 샹그리아 · M 지도 열기":"이삿짐 정리하기  "+PackedCount+" / "+boxes.Length):"샹그리아 · 출입문으로 이동";}
            if(hint!=null){hint.gameObject.SetActive(!playing&&!Completed&&!mapOpen);hint.text=Busy?"정리하는 중…":isRoom?(MapUnlocked?"WASD 이동 · M 지도 · F6 컷씬 다시 보기":NearestBox()>=0?"E 짐 상자 정리하기":"WASD 이동 · 상자에 가까이 가서 E"):NearDoor()?"E 샹그리아 입장":"WASD 이동 · 출입문에 가까이 가서 E";}
        }
        void LateUpdate()
        {
            if(!director.IsPlaying)return;
            int index=director.CurrentTurnIndex;
            if(index!=LastTurn){LastTurn=index;entered=Time.unscaledTime;OnTurn(index);}
            if(index<0||index>=sequence.turns.Count)return;
            float time=Time.unscaledTime-entered;string id=sequence.turns[index].id;
            if(!isRoom)
            {
                bool hall=index>=sequence.turns.FindIndex(t=>t.id=="sc03-hall-walk");
                float mix=hall?Mathf.Clamp01(time/6):0;
                SetAudio(jazz,hall?Mathf.Lerp(.065f,.008f,mix):.065f);SetAudio(crowd,hall?Mathf.Lerp(.035f,.004f,mix):.035f);
                SetAudio(rain,id=="sc03-enter"?Mathf.Lerp(.17f,.018f,Mathf.Clamp01(time/3)):.018f);
                if(id=="sc03-timepassage")director.view.SetBlack(Mathf.SmoothStep(0,1,time/.75f));
                else if(id=="sc03-later")director.view.SetBlack(1-Mathf.SmoothStep(0,1,time/.85f));
                else if(id=="sc03-black")black.color=Color.black;
            }
        }
        void OnTurn(int index)
        {
            if(index<0||index>=sequence.turns.Count)return;
            switch(sequence.turns[index].id){case "sc03-enter":PlayEffect(doorOpen);StartLoop(jazz);StartLoop(crowd);break;case "sc03-counter-arrive":PlayEffect(doorClose);break;case "sc03-thud":PlayEffect(thud);break;case "sc03-door-open":PlayEffect(doorOpen);break;}
        }
        int NearestBox(){if(!isRoom||MapUnlocked)return -1;int result=-1;float distance=1.18f;for(int i=0;i<boxes.Length;i++){if(packed[i]||boxes[i]==null)continue;float d=Vector3.Distance(Vector3.ProjectOnPlane(san.transform.position-boxes[i].position,Vector3.up),Vector3.zero);if(d<distance){distance=d;result=i;}}return result;}
        bool NearDoor()=>entrance!=null&&Vector3.ProjectOnPlane(san.transform.position-entrance.position,Vector3.up).magnitude<1.35f;
        public bool TryInteract()
        {
            if(Busy||loading||director.IsPlaying||Completed)return false;
            if(isRoom){int i=NearestBox();if(i<0)return false;StartCoroutine(Pack(i));return true;}
            if(!NearDoor())return false;return PlayPerformance();
        }
        IEnumerator Pack(int index)
        {
            Busy=true;san.InputEnabled=false;var dir=boxes[index].position-san.transform.position;dir.y=0;if(dir.sqrMagnitude>.001f)san.transform.rotation=Quaternion.LookRotation(dir);
            PlayEffect(unpackSound);float t=0;while(t<1.2f){san.Sample(san.unpack,t);boxes[index].localScale=Vector3.Lerp(boxScales[index],boxScales[index]*.38f,Mathf.SmoothStep(0,1,t/1.2f));t+=Time.deltaTime;yield return null;}
            packed[index]=true;PackedCount++;boxes[index].localScale=boxScales[index]*.38f;san.Sample(san.idle,0);
            if(PackedCount==boxes.Length)
            {
                // Reach the authored take-off mark continuously before switching to the sofa shot.
                var route=new[]{new Vector3(.1f,sofaApproach.position.y,0),new Vector3(.1f,sofaApproach.position.y,1.65f),sofaApproach.position};
                foreach(var target in route)while(Vector3.Distance(san.transform.position,target)>.04f){var delta=target-san.transform.position;san.transform.position=Vector3.MoveTowards(san.transform.position,target,1.6f*Time.deltaTime);if(delta.sqrMagnitude>.01f)san.transform.rotation=Quaternion.LookRotation(delta);san.Sample(san.walk,t);t+=Time.deltaTime;yield return null;}
                Busy=false;PlayPerformance();
            }
            else{san.InputEnabled=true;Busy=false;}
        }
        public bool PlayPerformance()
        {
            if(Busy||loading||director.IsPlaying)return false;
            returnPosition=san.transform.position;returnRotation=san.transform.rotation;LastTurn=-1;
            if(isRoom)san.PreparePerformance(sofaApproach.position,sofaApproach.rotation);else san.PreparePerformance(entrance.position,entrance.rotation);
            if(director.Play(sequence))return true;
            san.Place(returnPosition,returnRotation);san.EnableControl();return false;
        }
        void Commit(CutsceneResult result)
        {
            if(isRoom){MapUnlocked=true;san.Place(afterSofa.position,afterSofa.rotation);}
            else black.color=Color.black;
        }
        void Finished(CutsceneResult result)
        {
            bool success=result==CutsceneResult.Completed||result==CutsceneResult.Skipped;
            if(isRoom){if(!success)san.Place(returnPosition,returnRotation);san.EnableControl();if(success)SetMap(true);}
            else if(success){Completed=true;san.InputEnabled=false;ending.gameObject.SetActive(true);ending.text="SC-03 종료\n\nF6 다시 보기";StopLoop(jazz);StopLoop(crowd);StopLoop(rain);}
            else{black.color=Color.clear;san.Place(returnPosition,returnRotation);san.EnableControl();StopLoop(jazz);StopLoop(crowd);SetAudio(rain,.17f);}
        }
        public void Replay()
        {
            if(director.IsPlaying||Busy||loading)return;
            Completed=false;black.color=Color.clear;ending.gameObject.SetActive(false);SetMap(false);StartLoop(rain);
            if(isRoom){MapUnlocked=false;san.Place(sofaApproach.position,sofaApproach.rotation);}else san.Place(entrance.position,entrance.rotation);
            PlayPerformance();
        }
        public void SetMap(bool open){mapOpen=open&&MapUnlocked;map.SetActive(mapOpen);san.InputEnabled=!mapOpen&&!director.IsPlaying&&!Completed;}
        public void Travel(){if(!MapUnlocked||loading||director.IsPlaying)return;StartCoroutine(LoadBar());}
        IEnumerator LoadBar()
        {
            loading=true;SetMap(false);san.InputEnabled=false;float t=0;while(t<.6f){t+=Time.unscaledDeltaTime;black.color=new Color(0,0,0,Mathf.Clamp01(t/.6f));yield return null;}
            var operation=SceneManager.LoadSceneAsync("Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity");while(!operation.isDone)yield return null;
        }
        void PlayEffect(AudioClip clip){if(effects!=null&&clip!=null)effects.PlayOneShot(clip);}
        static void StartLoop(AudioSource a){if(a!=null&&a.clip!=null&&!a.isPlaying)a.Play();}
        static void StopLoop(AudioSource a){if(a!=null)a.Stop();}
        static void SetAudio(AudioSource a,float v){if(a!=null)a.volume=v;}
        void OnDisable(){if(director!=null){director.Finished-=Finished;director.Committed-=Commit;}foreach(var a in new[]{rain,jazz,crowd,effects})StopLoop(a);}
        void BuildUI()
        {
            var root=new GameObject("Prologue controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=650;
            var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);root.AddComponent<GraphicRaycaster>();
            goal=Text("Objective",root.transform,new Vector2(.035f,.90f),new Vector2(.8f,.97f),23);hint=Text("Controls",root.transform,new Vector2(.035f,.035f),new Vector2(.95f,.10f),20);
            map=Panel("Map - Shangrila destination",root.transform,new Vector2(.21f,.22f),new Vector2(.79f,.79f),new Color(.035f,.05f,.07f,.99f));
            Text("Map title",map.transform,new Vector2(.08f,.77f),new Vector2(.92f,.93f),29).text="로스 시뮬라 · 목적지 선택";
            Text("Route",map.transform,new Vector2(.08f,.47f),new Vector2(.92f,.75f),23).text="산의 방  ──────  샹그리아\n\n레베카의 가게에 인사하러 가자.";
            var button=Panel("Travel to Shangrila",map.transform,new Vector2(.12f,.17f),new Vector2(.88f,.36f),new Color(.20f,.25f,.27f,1));button.GetComponent<Image>().raycastTarget=true;button.AddComponent<Button>().onClick.AddListener(Travel);Text("Label",button.transform,Vector2.zero,Vector2.one,25).text="샹그리아로 이동  [ E / Enter ]";
            Text("Map controls",map.transform,new Vector2(.08f,.02f),new Vector2(.92f,.14f),17).text="M 지도 열기/닫기 · E 목적지 선택";map.SetActive(false);
            black=Panel("End cover",root.transform,Vector2.zero,Vector2.one,Color.clear).GetComponent<Image>();ending=Text("End",black.transform,new Vector2(.2f,.25f),new Vector2(.8f,.65f),25);ending.alignment=TextAlignmentOptions.Center;ending.gameObject.SetActive(false);
            if(UnityEngine.EventSystems.EventSystem.current==null){var es=new GameObject("Prologue UI input",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();}
        }
        static GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;go.GetComponent<Image>().raycastTarget=false;return go;}
        TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var t=go.GetComponent<TMP_Text>();t.font=font;t.fontSize=size;t.color=new Color(.92f,.88f,.77f);t.raycastTarget=false;return t;}
    }
}
