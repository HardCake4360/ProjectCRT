using ProjectCRT.Cutscenes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectCRT.Prologue
{
    /// <summary>Authoring-only background inspection. No story playback or save writes.</summary>
    public sealed class PrologueBackgroundPreview : MonoBehaviour
    {
        public string sceneLabel;
        public CutsceneStage stage;
        public Transform preservedCast;
        public int shotIndex;
        [ContextMenu("Preview/Next Camera")]
        void NextCamera()=>SelectShot(shotIndex+1);
        [ContextMenu("Preview/Previous Camera")]
        void PreviousCamera()=>SelectShot(shotIndex-1);
        public void SelectShot(int index)
        {
            if(stage==null||stage.output==null||stage.shots.Count==0)return;
            shotIndex=(index%stage.shots.Count+stage.shots.Count)%stage.shots.Count;
            stage.shots[shotIndex].Sample().Apply(stage.output);
            var viewer=stage.output.GetComponent<OutdoorStreetViewer>();
            if(viewer!=null){viewer.enabled=false;viewer.enabled=true;}
        }
        void Update()
        {
            var keys=Keyboard.current;if(keys==null)return;
            if(keys.rightBracketKey.wasPressedThisFrame||keys.pageDownKey.wasPressedThisFrame)SelectShot(shotIndex+1);
            if(keys.leftBracketKey.wasPressedThisFrame||keys.pageUpKey.wasPressedThisFrame)SelectShot(shotIndex-1);
        }
        void OnGUI()
        {
            if(stage==null||stage.shots.Count==0)return;
            var style=new GUIStyle(GUI.skin.label){fontSize=15};style.normal.textColor=new Color(.85f,.9f,.95f);
            GUI.Label(new Rect(18,15,1200,30),sceneLabel+"  |  "+stage.shots[Mathf.Clamp(shotIndex,0,stage.shots.Count-1)].id+"  |  [ / ] shot · RMB look · WASD move · Q/E up/down",style);
        }
    }
}
