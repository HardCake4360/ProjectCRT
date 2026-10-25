using UnityEngine;

namespace ProjectCRT.Cutscenes
{
    /// <summary>Explicit test-scene launcher; never starts the sample on scene load.</summary>
    public sealed class CutsceneDemoControls : MonoBehaviour
    {
        public CutsceneDirector director;
        public CutsceneSequence sequence;
        public KeyCode playKey=KeyCode.F6;
        void Update(){if(Input.GetKeyDown(playKey)&&director!=null&&!CutsceneDirector.IsActive)director.Play(sequence);}
        void OnGUI()
        {
            if(CutsceneDirector.IsActive)return;
            var saved=GUI.color;GUI.color=new Color(.94f,.87f,.66f);
            GUI.Label(new Rect(18,65,600,24),"F6  샹그리아 컷씬 재생   ·   컷씬 중 E / Enter 진행, ESC 건너뛰기, F8 음성 비교");GUI.color=saved;
        }
    }
}
