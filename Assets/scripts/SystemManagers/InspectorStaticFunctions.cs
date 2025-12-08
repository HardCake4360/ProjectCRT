using UnityEngine;

public class InspectorStaticFunctions : MonoBehaviour
{
    //인스펙터 연결용 전역함수 스크립트 호출함수 모음

    //=====오디오 매니저=====
    public void PlayBGM(string name)
    {
        AudioManager.Instance.PlayBGM(name);
    }
    public void PlaySFX(string name)
    {
        AudioManager.Instance.PlaySFX(name);
    }

    public void StopBGM()
    {
        AudioManager.Instance.StopBGM();
    }

    //=====씬 로더=====
    public void LoadScene(string name)
    {
        SceneLoader.Instance.LoadScene(name);
    }

    //=====심문 매니저=====
    public void StartItg()
    {
        InterogationManager.Instance.StartInterogation();
    }
    public void EndItg()
    {
        InterogationManager.Instance.EndInterogation();
    }
    public void PlayUIAnim(string name)
    {
        InterogationManager.Instance.PlayUIAnim(name);
    }
    public void ParadoxBuildAndRun()
    {
        InterogationManager.Instance.BuildParadoxAndExecute();
    }
    public void PrintTip()
    {
        InterogationManager.Instance.ITG_tip.DetonateEvent();
    }

    //=====다이어로그 매니저들=====
    public void ForceEndDia()
    {
        DialogueManager.Instance.ForceEndDia();
    }
    public void LocalForceEndDia()
    {
        LocalDiaManager.Instance.ForceEndDia();
    }
    
}
