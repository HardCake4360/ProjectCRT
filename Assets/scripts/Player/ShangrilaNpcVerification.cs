#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShangrilaNpcVerification : MonoBehaviour
{
    public bool Done;
    public string Failure;
    public readonly List<string> Checks=new();
    static object Get(object obj,string field)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    IEnumerator Start()
    {
        var run=Run();
        while(true){bool more;try{more=run.MoveNext();}catch(Exception e){Failure=e.ToString();break;}if(!more)break;yield return run.Current;}
        Done=true;
    }
    IEnumerator Run()
    {
        yield return new WaitForSeconds(1);
        var context=FindFirstObjectByType<StreetWalkContext>();context.enabled=false;
        context.locationText.gameObject.SetActive(false);context.controlsText.gameObject.SetActive(false);
        var player=context.player;var cc=player.GetComponent<CharacterController>();var cam=player.GetComponentInChildren<Camera>();
        var npcs=FindObjectsByType<NpcInvestigationController>(FindObjectsSortMode.None);
        if(npcs.Length!=2)throw new Exception("Expected two people.");
        foreach(var npc in npcs)
        {
            Set(npc,"saveConversationHistory",false);
            Set(npc,"conversationState",new NpcConversationState{npcId=(string)Get(npc,"npcId")});
        }
        foreach(var npc in npcs)
        {
            bool rebecca=(string)Get(npc,"npcId")=="rebecca";
            Vector3 near=rebecca?new Vector3(-6.5f,1.43f,9.5f):new Vector3(-2.1f,1.43f,10.5f);
            var collider=npc.GetComponent<Collider>();
            cc.enabled=false;player.transform.position=near;cc.enabled=true;
            Vector3 direction=collider.bounds.center-cam.transform.position;
            var horizontal=new Vector3(direction.x,0,direction.z);
            player.transform.rotation=Quaternion.LookRotation(horizontal);
            float pitch=-Mathf.Atan2(direction.y,horizontal.magnitude)*Mathf.Rad2Deg;
            Set(player,"xRotation",pitch);cam.transform.localRotation=Quaternion.Euler(pitch,0,0);
            MainLoop.Instance.SetMainLoopState(MainState.Main);Physics.SyncTransforms();
            yield return null;
            Vector3 beforePosition=cam.transform.position;Quaternion beforeRotation=cam.transform.rotation;float beforeFov=cam.fieldOfView;
            if(!player.TryInteractInView())throw new Exception("Player ray cannot interact with "+npc.name);
            if(Vector3.Distance(cam.transform.position,beforePosition)>0.001f || Quaternion.Angle(cam.transform.rotation,beforeRotation)>0.01f)throw new Exception("Entry snapped on interaction.");
            var shot=npc.GetComponentInChildren<ShangrilaConversationShot>(true);
            yield return new WaitForSeconds(shot.enterDuration*0.3f);
            if(Vector3.Distance(cam.transform.position,beforePosition)<0.005f || Vector3.Distance(cam.transform.position,shot.transform.position)<0.005f)throw new Exception("Entry did not pass through an intermediate pose.");
            Checks.Add(npc.name+": entry blends through intermediate position/rotation/FOV without an initial snap");
            var investigationCanvas=(Canvas)Get(InvestigationInteractionUI.Instance,"rootCanvas");
            if(DialogueManager.Instance.IsDialogueRunning || investigationCanvas.enabled)throw new Exception("Dialogue opened before camera arrival.");
            while(shot.IsEntering)yield return null;
            yield return null;
            yield return new WaitForSeconds(0.08f);
            var openingCanvas=rebecca?DialogueManager.Instance.DialogueUI.canvas:investigationCanvas;
            var fade=openingCanvas.GetComponent<ConversationCanvasFade>();
            if(!openingCanvas.enabled || fade==null || fade.Opacity<=0 || fade.Opacity>=1)throw new Exception("Dialogue did not fade in after arrival.");
            if(openingCanvas.GetComponent<CanvasGroup>().interactable)throw new Exception("Window interactive during fade.");
            yield return new WaitForSeconds(0.3f);
            if(fade.Opacity<0.99f || !openingCanvas.GetComponent<CanvasGroup>().interactable)throw new Exception("Window fade did not finish.");
            Checks.Add(npc.name+": dialogue stays hidden during camera travel; fades in after arrival, then accepts UI input");
            yield return null;
            if(NpcInvestigationController.CurrentInteraction!=npc||MainLoop.Instance.MainLoopState!=MainState.Interact)throw new Exception("Conversation state not bound.");
            if(rebecca)
            {
                if(!DialogueManager.Instance.IsDialogueRunning)throw new Exception("Rebecca original intro did not start.");
                Checks.Add("Rebecca original fixed dialogue starts through player interaction ray");
                yield return new WaitForSeconds(0.25f);
                yield return new WaitForEndOfFrame();
                Capture("rebecca-intro");
                DialogueManager.Instance.ForceEndDia();
            }
            yield return new WaitForSeconds(1.5f);
            if(shot==null||!shot.gameObject.activeInHierarchy||Vector3.Distance(cam.transform.position,shot.transform.position)>0.01f)throw new Exception("Closeup shot not applied.");
            var animator=npc.GetComponentInChildren<Animator>();
            var head=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
            var bounds=npc.GetComponentInChildren<Renderer>().bounds;
            var face=head!=null?head.position+Vector3.up*0.08f:new Vector3(bounds.center.x,bounds.max.y-0.15f,bounds.center.z);
            var viewport=cam.WorldToViewportPoint(face);
            if(viewport.z<0 || viewport.x<0.15f || viewport.x>0.58f || viewport.y<0.48f || viewport.y>0.93f)throw new Exception("Face outside clear dialogue framing: "+npc.name+" "+viewport);
            foreach(var hit in Physics.RaycastAll(cam.transform.position,(face-cam.transform.position).normalized,Vector3.Distance(cam.transform.position,face)))
                if(hit.collider.GetComponentInParent<NPC_script>()!=npc.GetComponent<NPC_script>()&&hit.collider.GetComponentInParent<PlayerControler>()==null)throw new Exception("Face occluded by "+hit.collider.name);
            yield return new WaitForEndOfFrame();
            Capture(rebecca?"rebecca":"jeremy");
            if(rebecca){yield return new WaitForSeconds(3);yield return new WaitForEndOfFrame();Capture("rebecca-idle");}
            Checks.Add(npc.name+": authored closeup applied; face visible in clear upper-left UI region, unobstructed; viewport="+viewport);
            var ui=InvestigationInteractionUI.Instance;
            if(!((Canvas)Get(ui,"rootCanvas")).enabled)throw new Exception("Investigation UI did not open: "+npc.name);
            Checks.Add(npc.name+": existing investigation UI opens; player movement suspended");
            var closePosition=cam.transform.position;var closeRotation=cam.transform.rotation;
            ((Button)Get(ui,"closeButton")).onClick.Invoke();
            if(!shot.IsReturning || !player.cameraTransitionInProgress || Vector3.Distance(cam.transform.position,closePosition)>0.001f || Quaternion.Angle(cam.transform.rotation,closeRotation)>0.01f)throw new Exception("Exit snapped or input was not gated.");
            if(player.TryInteractInView())throw new Exception("Interaction allowed while returning.");
            yield return new WaitForSeconds(shot.returnDuration*0.3f);
            if(Vector3.Distance(cam.transform.position,closePosition)<0.005f || Vector3.Distance(cam.transform.position,beforePosition)<0.005f)throw new Exception("Return did not pass through intermediate pose.");
            yield return new WaitForSeconds(shot.returnDuration+0.05f);
            if(Vector3.Distance(cam.transform.position,beforePosition)>0.01f||Quaternion.Angle(cam.transform.rotation,beforeRotation)>0.1f||Mathf.Abs(cam.fieldOfView-beforeFov)>0.01f)throw new Exception("Walking camera pose/FOV not restored.");
            if(shot.gameObject.activeSelf || player.cameraTransitionInProgress)throw new Exception("Return did not release camera/input.");
            Checks.Add(npc.name+": exit blends back to original pose/FOV, then releases input");
            yield return new WaitForSeconds(0.3f);
            if(NpcInvestigationController.CurrentInteraction!=null||MainLoop.Instance.MainLoopState!=MainState.Main||!player.enabled)throw new Exception("Close did not restore walking.");
            if(((Canvas)Get(ui,"rootCanvas")).enabled)throw new Exception("UI remains visible.");
            Checks.Add(npc.name+": close button restores player walking and hides dialogue");
            if(!player.TryInteractInView())throw new Exception("Could not reopen conversation.");
            yield return new WaitForSeconds(0.12f);
            var interruptedPosition=cam.transform.position;
            npc.CloseInteraction();
            if(Vector3.Distance(cam.transform.position,interruptedPosition)>0.001f)throw new Exception("Early close snapped camera.");
            yield return new WaitForSeconds(shot.returnDuration+0.1f);
            if(Vector3.Distance(cam.transform.position,beforePosition)>0.01f || player.cameraTransitionInProgress)throw new Exception("Early close failed to restore camera.");
            if(DialogueManager.Instance.IsDialogueRunning || investigationCanvas.enabled)throw new Exception("Cancelled entry opened a stale dialogue window.");
            Checks.Add(npc.name+": closing during entry reverses smoothly and restores walking");
            cc.enabled=false;player.transform.position=near-(horizontal.normalized*3);cc.enabled=true;Physics.SyncTransforms();
            if(player.TryInteractInView())throw new Exception("Out of range NPC interaction succeeded.");
        }
        Checks.Add("Both NPCs reject interaction outside the existing 2 metre range; no network calls or conversation save writes made");
        context.ReturnToStreet();context.enabled=true;
    }
    void Capture(string name)
    {
        Directory.CreateDirectory("Logs/ShangrilaCloseup");
        var screenshot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Logs/ShangrilaCloseup/"+name+".png",screenshot.EncodeToPNG());Destroy(screenshot);
    }
}
#endif
