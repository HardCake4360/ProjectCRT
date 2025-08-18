using UnityEngine;
using System.Collections;


public class MonitorCam : CustomCinemachineCamera
{
    [SerializeField] private Vector3 deltaPos;
    [SerializeField] private float duration;

    public IEnumerator LookAt(bool dir)
    {
        Vector3 startPos = gameObject.transform.position;
        Vector3 targetPos = dir ? -(deltaPos + startPos) : deltaPos + startPos;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            gameObject.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        gameObject.transform.position = targetPos; // 마지막 위치 보정

    }
}
