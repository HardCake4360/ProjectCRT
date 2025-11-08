using UnityEngine;

public class SingletonObjLinker : MonoBehaviour
{
    //인스펙터에서 현재 씬에 존재하지 않는 싱글톤 오브젝트 함수 연결용으로 사용
    MainLoop mainLoop;
    SceneLoader sceneLoader;
    DialogueManager diaManager;

    void Start()
    {
        mainLoop = MainLoop.Instance;
        sceneLoader = SceneLoader.Instance;
        diaManager = DialogueManager.Instance;        
    }
}
