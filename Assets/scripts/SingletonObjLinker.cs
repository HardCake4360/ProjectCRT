using UnityEngine;

public class SingletonObjLinker : MonoBehaviour
{
    //인스펙터에서 현재 씬에 존재하지 않는 싱글톤 오브젝트 함수 연결용으로 사용
    MainLoop mainLoop;
    public GameObject MainLoopPrefab;
    SceneLoader sceneLoader;
    public GameObject SceneLoaderPrefab;
    DialogueManager diaManager;
    public GameObject DiaManagerPrefab;
    TipUIControler tipUI;
    public GameObject TipUIPrefab;

    void Start()
    {
        if (MainLoop.Instance == null) Instantiate(MainLoopPrefab, gameObject.transform);
        mainLoop = MainLoop.Instance;

        if (SceneLoader.Instance == null) Instantiate(SceneLoaderPrefab, gameObject.transform);
        sceneLoader = SceneLoader.Instance;
        
        if (DialogueManager.Instance == null) Instantiate(DiaManagerPrefab, gameObject.transform);
        diaManager = DialogueManager.Instance;

        if (TipUIControler.Instance == null) Instantiate(TipUIPrefab, gameObject.transform);
        tipUI = TipUIControler.Instance;
    }

    //MainLoop
    public void SetMainLoopState_Main() { mainLoop.SetMainLoopState(MainState.Main); }
    public void SetMainLoopState_Interacting() { mainLoop.SetMainLoopState(MainState.Interacting); }



    //SceneLoader
    public void LoadScene(string name)
    {
        sceneLoader.LoadScene(name);
    }
    public void StaticLoadScene(string name)
    {
        SceneLoader.Instance.LoadScene(name);
    }

    //DiaManager



    //TipUI



}
