using UnityEngine;
using System.Collections;

public class Door : Interactable
{
    public bool IsOpened;
    [SerializeField] private float openTime;
    [SerializeField] private float animationDuration = 1.0f;
    [SerializeField] private float angleDelta;
    private Quaternion originalAngle;
    private float openCnt;
    private float animationTime;

    private void Start()
    {
        originalAngle = gameObject.transform.localRotation;
        openCnt = 0;
        animationTime = 0;
    }

    private void Update()
    {
        if (IsOpened)
        {
            if (openCnt < openTime)
            {
                openCnt += Time.deltaTime;
            }
            else
            {
                Close();
                openCnt = 0f;
            }
        }
    }

    public void Open()
    {
        StartCoroutine(AnimateDoor(true));
    }

    public void Close()
    {
        StartCoroutine(AnimateDoor(false));
    }


    private IEnumerator AnimateDoor(bool open)
    {
        Quaternion targetAngle = originalAngle * Quaternion.Euler(0, 0, angleDelta);

        // 애니메이션 시작 시점의 각도(startAngle)와 목표 각도(endAngle)를 설정합니다.
        Quaternion startAngle;
        Quaternion endAngle;

        if (open)
        {
            // 여는 경우: 현재 각도에서 목표 각도(열린 각도)로 이동
            startAngle = gameObject.transform.localRotation;
            endAngle = targetAngle;
            IsOpened = true; // 문 열림 상태로 설정
            openCnt = 0f; // 자동 닫힘 카운트 초기화
        }
        else
        {
            // 닫는 경우: 현재 각도에서 초기 각도(닫힌 각도)로 이동
            startAngle = gameObject.transform.localRotation;
            endAngle = originalAngle;
            IsOpened = false; // 문 닫힘 상태로 설정 (Update 루프의 자동 닫힘 방지)
            openCnt = 0f; // 자동 닫힘 카운트 초기화
        }

        // 애니메이션 경과 시간입니다.
        float elapsed = 0f;

        // 문을 여는 동안(elapsed가 duration보다 작은 동안) 반복합니다.
        while (elapsed < animationDuration)
        {
            // 경과 시간을 증가시킵니다.
            elapsed += Time.deltaTime;

            // 애니메이션 진행 정도(0.0 ~ 1.0)를 계산합니다.
            float t = Mathf.Clamp01(elapsed / animationDuration);

            // Mathf.SmoothStep 함수를 사용하여 Easing(감속/가속) 효과를 적용합니다.
            float easedT = Mathf.SmoothStep(0.0f, 1.0f, t);

            // 시작 각도에서 목표 각도까지 부드럽게 보간하여 회전시킵니다.
            gameObject.transform.localRotation = Quaternion.Lerp(startAngle, endAngle, easedT);

            // 다음 프레임까지 기다립니다.
            yield return null;
        }

        // 애니메이션 완료 후 최종 각도로 정확히 설정하여 오차를 줄입니다.
        gameObject.transform.localRotation = endAngle;
    }

    override public void Interact()
    {
        if (IsOpened)
        {
            Close();
            Debug.Log("상호작용 대상: " + gameObject.name + " 닫힘");
        }
        else
        {
            Open();
            Debug.Log("상호작용 대상: " + gameObject.name + " 열림");
        }
        PlayerControler.Instance.SetInteract(false);
    }
}
