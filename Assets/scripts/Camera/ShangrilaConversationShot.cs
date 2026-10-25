using UnityEngine;

/// <summary>Scene-authored conversation shot activated by Interactable's existing closeup hook.</summary>
[DefaultExecutionOrder(1000)]
public sealed class ShangrilaConversationShot : MonoBehaviour, ICloseupCameraTransition
{
    public Camera output;
    public Transform focusTarget;
    public Vector3 focusOffset=new Vector3(0,-0.08f,0);
    public float horizontalFraming=0.12f;
    [Range(20,70)] public float fieldOfView=38;
    [Min(0.05f)] public float enterDuration=0.7f;
    [Min(0.05f)] public float returnDuration=0.6f;
    public bool IsReturning => captured && returning;
    public bool IsEntering => captured && !returning && elapsed < Mathf.Max(0.05f,enterDuration);
    Vector3 savedPosition;
    Quaternion savedRotation;
    float savedFieldOfView;
    bool captured;
    bool returning;
    float elapsed;
    Vector3 blendPosition;
    Quaternion blendRotation;
    float blendFieldOfView;
    PlayerControler player;
    public void BeginCloseup()
    {
        if(!gameObject.activeSelf)gameObject.SetActive(true);
        else BeginEntry();
    }
    public void EndCloseup()
    {
        if(!captured || returning)return;
        returning=true;StartBlend();
    }
    void OnEnable()
    {
        if(Application.isPlaying)BeginEntry();
    }
    void BeginEntry()
    {
        if(!Application.isPlaying || output==null)return;
        if(!captured)
        {
            savedPosition=output.transform.position;savedRotation=output.transform.rotation;
            savedFieldOfView=output.fieldOfView;
        }
        player=output.GetComponentInParent<PlayerControler>();
        captured=true;returning=false;StartBlend();
    }
    void StartBlend()
    {
        blendPosition=output.transform.position;blendRotation=output.transform.rotation;blendFieldOfView=output.fieldOfView;elapsed=0;
        if(player!=null){player.cameraTransitionInProgress=true;player.HideHint();}
    }
    void LateUpdate()
    {
        if(!captured || output==null)return;
        elapsed+=Time.unscaledDeltaTime;
        float progress=Mathf.Clamp01(elapsed/Mathf.Max(0.05f,returning?returnDuration:enterDuration));
        float eased=progress*progress*(3f-2f*progress);
        Quaternion rotation=transform.rotation;
        if(focusTarget!=null)
            rotation=Quaternion.LookRotation(focusTarget.position+focusOffset+transform.right*horizontalFraming-transform.position);
        output.transform.SetPositionAndRotation(Vector3.Lerp(blendPosition,returning?savedPosition:transform.position,eased),Quaternion.Slerp(blendRotation,returning?savedRotation:rotation,eased));
        output.fieldOfView=Mathf.Lerp(blendFieldOfView,returning?savedFieldOfView:fieldOfView,eased);
        if(returning && progress>=1)
        {
            captured=false;
            if(player!=null)player.cameraTransitionInProgress=false;
            gameObject.SetActive(false);
        }
    }
    void OnDisable()
    {
        if(captured && output!=null){output.transform.SetPositionAndRotation(savedPosition,savedRotation);output.fieldOfView=savedFieldOfView;}
        if(player!=null)player.cameraTransitionInProgress=false;
        captured=false;
    }
}
