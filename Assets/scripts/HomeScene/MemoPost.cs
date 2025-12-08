using UnityEngine;

public class MemoPost : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject postItPrefab;
    [SerializeField] private float offset = 0.01f;

    private GameObject previewPostIt; // 미리보기 오브젝트

    void Start()
    {
        // 처음에 프리뷰 오브젝트 생성 (반투명 머터리얼 적용해두면 더 직관적!)
        previewPostIt = Instantiate(postItPrefab);
        SetPreviewMode(previewPostIt, true);
    }

    public void PostMode()
    {
        UpdatePreviewPosition();

        if (Input.GetMouseButtonDown(0))
        {
            PlacePostIt();
            //MainLoop.Instance.posting = false;
        }
    }

    void UpdatePreviewPosition()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red);
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 spawnPos = hit.point + hit.normal * offset;
            Quaternion spawnRot = Quaternion.LookRotation(-hit.normal, Vector3.up);

            previewPostIt.transform.position = spawnPos;
            previewPostIt.transform.rotation = spawnRot;
            
            Debug.DrawRay(hit.point, Vector3.up * 0.5f, Color.blue);
        }
    }

    void PlacePostIt()
    {
        // 실제 Post-it 고정
        Instantiate(postItPrefab, previewPostIt.transform.position, previewPostIt.transform.rotation);
    }

    void SetPreviewMode(GameObject obj, bool isPreview)
    {
        // 예시: 미리보기 오브젝트는 반투명 처리
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                Color c = m.color;
                c.a = isPreview ? 0.5f : 1f;
                m.color = c;
            }
        }
    }
}

