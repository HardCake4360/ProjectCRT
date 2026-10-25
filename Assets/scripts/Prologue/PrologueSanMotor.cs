using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectCRT.Prologue
{
    /// <summary>Fixed-camera, third-person control for the two playable prologue stages.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrologueSanMotor : MonoBehaviour
    {
        public Transform model;
        public Camera view;
        public AnimationClip idle,walk,unpack;
        public float speed=1.8f;
        public Vector3 initialPosition;
        public float initialYaw;
        public bool InputEnabled=true;
        public bool IsMoving{get;private set;}
        CharacterController controller;
        float clock,vertical;
        void Awake(){controller=GetComponent<CharacterController>();Place(initialPosition,Quaternion.Euler(0,initialYaw,0));}
        void Update()
        {
            if(!InputEnabled||ProjectCRT.Cutscenes.CutsceneDirector.IsActive)return;
            var k=Keyboard.current;Vector2 input=Vector2.zero;
            if(k!=null){input.x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);input.y=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);}
            var forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            var direction=Vector3.ClampMagnitude(forward*input.y+right*input.x,1);
            vertical=controller.isGrounded?-1:vertical+Physics.gravity.y*Time.deltaTime;
            controller.Move((direction*speed+Vector3.up*vertical)*Time.deltaTime);
            IsMoving=direction.sqrMagnitude>.01f;clock+=Time.deltaTime;
            Sample(IsMoving?walk:idle,clock);
            if(IsMoving)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),540*Time.deltaTime);
        }
        public void Sample(AnimationClip clip,float time){if(clip!=null)clip.SampleAnimation(gameObject,clip.length>0?Mathf.Repeat(time,clip.length):0);}
        public void Place(Vector3 feet,Quaternion rotation)
        {
            if(controller==null)controller=GetComponent<CharacterController>();bool on=controller.enabled;controller.enabled=false;
            transform.SetPositionAndRotation(feet,rotation);model.localPosition=Vector3.zero;model.localRotation=Quaternion.identity;vertical=0;Sample(idle,0);controller.enabled=on;
        }
        public void PreparePerformance(Vector3 feet,Quaternion rotation)
        {
            InputEnabled=false;if(controller==null)controller=GetComponent<CharacterController>();controller.enabled=false;
            transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);Sample(idle,0);model.SetPositionAndRotation(feet,rotation);
        }
        public void EnableControl(){controller.enabled=true;InputEnabled=true;}
    }
}
