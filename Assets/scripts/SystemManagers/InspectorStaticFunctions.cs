using UnityEngine;

public class InspectorStaticFunctions : MonoBehaviour
{
    //인스펙터 연결용 전역함수 스크립트 호출함수 모음
    public void PlayBGM(string name)
    {
        AudioManager.Instance.PlayBGM(name);
    }
    public void PlaySFX(string name)
    {
        AudioManager.Instance.PlaySFX(name);
    }
    public void LoadScene(string name)
    {
        SceneLoader.Instance.LoadScene("name");
    }
}
