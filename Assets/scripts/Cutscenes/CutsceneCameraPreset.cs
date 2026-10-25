using UnityEngine;

namespace ProjectCRT.Cutscenes
{
    public struct CutsceneCameraState
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView;
        public CutsceneCameraState(Vector3 p,Quaternion r,float f){position=p;rotation=r;fieldOfView=f;}
        public static CutsceneCameraState From(Camera c)=>new CutsceneCameraState(c.transform.position,c.transform.rotation,c.fieldOfView);
        public static CutsceneCameraState Lerp(CutsceneCameraState a,CutsceneCameraState b,float t)=>new CutsceneCameraState(Vector3.Lerp(a.position,b.position,t),Quaternion.Slerp(a.rotation,b.rotation,t),Mathf.Lerp(a.fieldOfView,b.fieldOfView,t));
        public void Apply(Camera camera){camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fieldOfView;}
    }
    [DisallowMultipleComponent]
    public sealed class CutsceneCameraPreset : MonoBehaviour
    {
        public string id="shot";
        [Range(15,100)] public float fieldOfView=38;
        public Transform lookAt;
        public Vector3 lookAtOffset;
        [Tooltip("World-unit offset in camera right/up axes. Positive X places the subject toward screen left.")]
        public Vector2 framingOffset;
        public CutsceneCameraState Sample()
        {
            Quaternion rotation=transform.rotation;
            if(lookAt!=null)
            {
                Vector3 aim=lookAt.position+lookAtOffset+transform.right*framingOffset.x+transform.up*framingOffset.y-transform.position;
                if(aim.sqrMagnitude>0.0001f)rotation=Quaternion.LookRotation(aim,transform.up);
            }
            return new CutsceneCameraState(transform.position,rotation,fieldOfView);
        }
        void OnDrawGizmosSelected()
        {
            var sample=Sample();Gizmos.color=Color.cyan;
            var old=Gizmos.matrix;Gizmos.matrix=Matrix4x4.TRS(sample.position,sample.rotation,Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero,fieldOfView,1,0.05f,16f/9f);Gizmos.matrix=old;
        }
    }
}
