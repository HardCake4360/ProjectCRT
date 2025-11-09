using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    [Header("UI References")]
    public CanvasGroup FadeCanvas;     // 페이드용 CanvasGroup (검정 Image 포함)
    public Canvas canvas;
    public GameObject LoadingObjs;
    public Image ProgressBar;         // 로딩 진행바
    public GameObject PressAnyKeyText; // "Press any key" 텍스트

    [Header("Settings")]
    public float fadeDuration = 1f;    // 페이드 속도

    private bool isLoading = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        FadeCanvas.alpha = 1; // 처음엔 검정 화면
        StartCoroutine(FadeIn()); // 실행 시 밝게 전환
        if (PressAnyKeyText != null) PressAnyKeyText.SetActive(false);
    }

    public void LoadScene(string sceneName)
    {
        canvas.enabled = true;
        if (!isLoading)
            StartCoroutine(LoadSceneProcess(sceneName));
    }

    private IEnumerator LoadSceneProcess(string sceneName)
    {
        isLoading = true;
        LoadingObjs.SetActive(true);

        // 페이드 아웃 (화면 어두워짐)
        yield return StartCoroutine(FadeOut());

        // 비동기 로드 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            ProgressBar.fillAmount = Mathf.Clamp01(op.progress / 0.9f);
            yield return null;
        }

        // 로딩 완료
        ProgressBar.fillAmount = 1f;
        MainLoop.Instance.OnLoadSceneEnd();

        // "Press Any Key" 텍스트 표시
        if (PressAnyKeyText != null)
            PressAnyKeyText.SetActive(true);

        // 씬 활성화
        op.allowSceneActivation = true;

        // 키 입력 대기
        yield return StartCoroutine(WaitForAnyKey());

        // 페이드 인 (새 씬 보이게)
        yield return StartCoroutine(FadeIn());

        if (PressAnyKeyText != null)
            PressAnyKeyText.SetActive(false);

        isLoading = false;
    }

    private IEnumerator WaitForAnyKey()
    {
        while (!Input.anyKeyDown)
            yield return null;
    }

    private IEnumerator FadeIn()
    {
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            FadeCanvas.alpha = 1 - (t / fadeDuration);
            yield return null;
        }
        canvas.enabled = false;
        FadeCanvas.alpha = 0;
    }

    private IEnumerator FadeOut()
    {
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            FadeCanvas.alpha = t / fadeDuration;
            yield return null;
        }
        FadeCanvas.alpha = 1;
    }
}
