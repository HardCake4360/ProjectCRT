using System;
using System.IO;
using System.Linq;
using ProjectCRT.Cutscenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ProjectCRT.Prologue.Editor
{
    public static class SC23FinalPolish
    {
        public static void BatchCameraOnly(){Tune(false);}
        public static void BatchRun(){Tune(true);}
        static void Tune(bool runtime)
        {
            try{
                EditorSceneManager.OpenScene(SC23CutsceneAuthoring.BarScene);var stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var seq=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SC23CutsceneAuthoring.BarSequence);
                void Shot(string id,Vector3 position,Vector3 target,float fov){var s=stage.FindShot(id);s.transform.position=position;s.transform.LookAt(target);s.fieldOfView=fov;}
                Shot("sc03-rebecca-close",new Vector3(-4.7f,1.60f,10.65f),new Vector3(-4.6f,1.445f,11.93f),37);
                Shot("sc03-over-shoulder",new Vector3(3.2f,1.67f,11.9f),new Vector3(5.85f,.48f,12.15f),60);
                EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);
                stage.Capture(stage.output,true);var view=stage.GetComponent<CutsceneView>();view.Begin();var canvas=stage.GetComponentInChildren<Canvas>(true);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=stage.output;canvas.planeDistance=.15f;
                foreach(string id in new[]{"sc03-d16","sc03-hall-walk","sc03-discovery","sc03-d76"}){
                    int index=seq.turns.FindIndex(t=>t.id==id);var turn=seq.turns[index];stage.Reconstruct(seq,index,turn.hasDialogue?2:stage.PreviewDuration(turn));view.SetLine(CutsceneTextUtility.SpeakerName(turn,stage),turn.text,turn.dialogueKind);view.Reveal(9999);foreach(var group in canvas.GetComponentsInChildren<CanvasGroup>(true))group.alpha=group.name=="Dialogue"&&turn.hasDialogue?1:0;Canvas.ForceUpdateCanvases();SC23Verification.Capture(stage.output,"Final-"+id);
                    if(turn.hasDialogue){var head=stage.output.WorldToViewportPoint(stage.FindActor(turn.speakerId).lookAtBone.position);if(head.z<0||head.x<.15f||head.x>.85f||head.y<.35f||head.y>.88f)throw new Exception("Speaker outside dialogue-safe frame: "+id+" "+head);}
                }
                stage.Restore();File.WriteAllText("Logs/SC23/camera-status.txt","PASS");if(runtime)SC23RuntimeVerificationMenu.BatchRun();else EditorApplication.Exit(0);
            }catch(Exception e){File.WriteAllText("Logs/SC23/camera-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
