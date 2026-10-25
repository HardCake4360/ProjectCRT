#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ProjectCRT.Cutscenes;
using UnityEditor;
using UnityEngine;

namespace ProjectCRT.Cases
{
    public sealed class ShangrilaCaseVerification : MonoBehaviour
    {
        public bool Done;
        public string Failure;
        public readonly List<string> Checks = new();
        ShangrilaCaseManager manager;
        PlayerControler player;
        Camera cameraOutput;
        StreetWalkContext walking;
        CutsceneDirector director;
        InvestigationInventoryManager inventory;
        IEnumerator Start()
        {
            var stack=new Stack<IEnumerator>(); stack.Push(Run());
            while(stack.Count>0)
            {
                bool more=false; object next=null;
                try { more=stack.Peek().MoveNext(); if(more)next=stack.Peek().Current; }
                catch(Exception e) { Failure=e.ToString(); break; }
                if(!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if(next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            Done=true;
        }
        IEnumerator Run()
        {
            manager=FindFirstObjectByType<ShangrilaCaseManager>(); Require(manager!=null,"Case manager missing.");
            yield return Until(()=>manager.Ready,6,"Case initialization failed: "+manager.LastError);
            player=manager.Player; cameraOutput=player.GetComponentInChildren<Camera>(); walking=manager.walking;
            walking.enabled=false; director=manager.director; inventory=InvestigationInventoryManager.Instance;
            MainLoop.Instance.SetMainLoopState_Main();
            foreach(var npc in FindObjectsByType<NpcInvestigationController>(FindObjectsSortMode.None)) Set(npc,"saveConversationHistory",false);
            if(SessionState.GetInt("ProjectCRT.CaseVerification.Phase",1)==2)
            {
                var expected=JsonUtility.FromJson<ShangrilaCaseSave>(File.ReadAllText(manager.SavePath));
                Require(manager.IntroductionCompleted && manager.InteractionsUnlocked,"Cold Play restart lost milestone/unlocks.");
                Require(manager.IntroductionStartCount==0 && !director.IsPlaying,"Cold restart replayed introduction.");
                Require(inventory.GetUnlockedIds(InvestigationItemCategory.Information).Contains("arrival_notice"),"Cold restart lost inspected information.");
                Require(Vector3.Distance(player.transform.position,expected.playerPosition)<.15f &&
                    Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y,expected.playerYaw))<.1f &&
                    Mathf.Abs(player.ViewPitch-expected.playerPitch)<.1f,"Cold restart lost walking pose.");
                Checks.Add("A second independent Play session restores completion, permissions, inspected information and player position/yaw/pitch from disk; introduction does not replay");
                yield return LeaveAndEnter();
                Require(!director.IsPlaying && manager.IntroductionStartCount==0,"Re-entry after cold load replayed introduction.");
                Checks.Add("Re-entering the physical doorway after restarting Play still does not replay the introduction");
                yield break;
            }
            Require(!manager.IntroductionCompleted && !manager.InteractionsUnlocked,"Fresh case must be locked.");
            Require(Vector3.Distance(player.transform.position,walking.streetSpawn.position)<.15f,"Fresh case did not start on street.");
            var gates=FindObjectsByType<CaseInteractionGate>(FindObjectsSortMode.None);
            Require(gates.Length==4 && gates.All(g=>!g.AllowsInteraction),"Expected two locked NPCs and two locked props.");
            var props=FindObjectsByType<InspectableProp>(FindObjectsSortMode.None).Where(p=>p.GetComponent<CaseInteractionGate>()!=null).ToArray();
            var npcController=FindObjectsByType<NpcInvestigationController>(FindObjectsSortMode.None).First();
            npcController.BeginInteraction(); foreach(var prop in props)prop.Interact();
            Require(NpcInvestigationController.CurrentInteraction==null && inventory.GetUnlockedIds(InvestigationItemCategory.Information).Count==0,"Direct interaction bypassed arrival gate.");
            var locked=gates[0].GetComponent<Interactable>(); locked.SetInteractableWithDelay(.02f);
            yield return new WaitForSecondsRealtime(.08f);
            Require(locked.canInteract && !locked.IsInteractionAllowed,"Legacy cooldown bypassed case gate.");
            Require(manager.SaveProgress(),"Could not save pending arrival.");
            string pending=File.ReadAllText(manager.SavePath);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/Cases/street-start.png");
            VerifyInteractionRay();
            Checks.Add("Fresh case starts on street; NPC calls, props and delayed interaction re-enable remain gated; pending arrival can be saved");

            yield return WalkTo(walking.entranceOutside.position);
            yield return WalkTo(walking.entranceInside.position,true);
            yield return Until(()=>director.IsPlaying,4,"Physical doorway did not start cutscene.");
            Require(manager.IntroductionStartCount==1 && !manager.InteractionsUnlocked,"Doorway started more than once or unlocked early.");
            Require(!manager.SaveProgress() && !manager.LoadProgress(),"Save/load was allowed inside cutscene.");
            director.Cancel(); yield return Until(()=>!director.IsPlaying,6,"Cancel cleanup failed.");
            Require(!manager.IntroductionCompleted && !manager.InteractionsUnlocked,"Cancelled cutscene committed progression.");
            yield return new WaitForSecondsRealtime(.3f);
            Require(manager.IntroductionStartCount==1,"Cancelled cutscene retriggered without leaving entrance.");
            Checks.Add("Physical entry starts exactly one cutscene; save/load is blocked during playback; cancellation keeps progression locked and waits for re-entry");

            yield return LeaveAndEnter();
            yield return Until(()=>director.IsPlaying,4,"Cancelled introduction did not retry on re-entry. State="+MainLoop.Instance.MainLoopState+" gate="+player.cameraTransitionInProgress+" position="+player.transform.position+" error="+manager.LastError);
            director.Skip(); yield return Until(()=>!director.IsPlaying,7,"Skip cleanup failed.");
            Require(manager.IntroductionCompleted && manager.InteractionsUnlocked,"Skip did not commit/unlock.");
            var committed=JsonUtility.FromJson<ShangrilaCaseSave>(File.ReadAllText(manager.SavePath));
            Require(committed.completedCutsceneIds.Contains(manager.introduction.sequenceId),"Completion not durable before returning to walking.");
            yield return LeaveAndEnter();
            Require(!director.IsPlaying && manager.IntroductionStartCount==2,"Completed arrival replayed on doorway re-entry.");
            Checks.Add("Skip counts as completion; the completion record is durable; repeated entry does not replay");

            // Loading an earlier save must undo both permissions and inventory, not merge future discoveries.
            var menu=props.First(p=>p.HintName=="메뉴판");
            yield return Inspect(menu,"arrival_menu");
            File.WriteAllText(manager.SavePath,pending);
            Require(manager.LoadProgress(),"Could not load earlier arrival save.");
            Require(!manager.IntroductionCompleted && !manager.InteractionsUnlocked && inventory.GetUnlockedIds(InvestigationItemCategory.Information).Count==0,"Earlier save did not rewind progression/inventory.");
            foreach(var context in FindObjectsByType<InvestigationContextBuilder>(FindObjectsSortMode.None))
                Require(!context.GetDiscoveredInformationIds().Contains("arrival_menu"),"NPC context retained future information after load.");
            Checks.Add("Loading an earlier save rewinds milestone, interaction gates, inventory and cached NPC investigation context");

            yield return WalkTo(walking.entranceOutside.position);
            yield return WalkTo(walking.entranceInside.position,true);
            yield return Until(()=>director.IsPlaying,4,"Loaded pending arrival did not trigger.");
            float deadline=Time.realtimeSinceStartup+45;
            while(director.IsPlaying && Time.realtimeSinceStartup<deadline)
            { director.Advance(); yield return new WaitForSecondsRealtime(.12f); }
            Require(!director.IsPlaying && manager.IntroductionCompleted && manager.InteractionsUnlocked,"Normal completion did not unlock investigation.");
            Checks.Add("All authored introduction turns can complete normally; permissions unlock after the return fade");

            // Existing NPC camera/UI flow must remain reachable through the player's ray.
            bool rebecca=npcController.name.IndexOf("reb",StringComparison.OrdinalIgnoreCase)>=0;
            Aim(npcController.GetComponent<Collider>(),rebecca?new Vector3(-6.5f,1.43f,9.5f):new Vector3(-2.1f,1.43f,10.5f));
            yield return null;
            Require(player.TryInteractInView(),"Unlocked NPC was unreachable via interaction ray.");
            yield return new WaitForSecondsRealtime(1.1f);
            Require(NpcInvestigationController.CurrentInteraction==npcController,"Existing NPC dialogue did not open.");
            Require(!manager.LoadProgress(),"Load was allowed during NPC conversation.");
            if(DialogueManager.Instance.IsDialogueRunning)DialogueManager.Instance.ForceEndDia();
            npcController.CloseInteraction();
            yield return new WaitForSecondsRealtime(1.0f); MainLoop.Instance.SetMainLoopState_Main();
            Checks.Add("Unlocked NPC opens the existing closeup/dialogue flow through the player ray; loading is blocked during conversation");

            yield return Inspect(props.First(p=>p.HintName=="영업 안내문"),"arrival_notice");
            player.RestoreWalkingPose(new Vector3(-4.6f,1.43f,9.3f),137,11);
            Require(manager.SaveProgress(),"Manual final save failed.");
            player.RestoreWalkingPose(walking.streetSpawn.position,0,0);
            Require(manager.LoadProgress(),"Manual restore failed.");
            Require(Vector3.Distance(player.transform.position,new Vector3(-4.6f,1.43f,9.3f))<.05f && Mathf.Abs(player.ViewPitch-11)<.1f,"Manual restore lost pose.");
            Require(inventory.TryGetDefinition("arrival_notice",out var definition) && definition.DisplayName=="영업 안내문","Inspected information has no existing inventory detail.");
            Checks.Add("Inspection uses the player ray, displays its text, unlocks a defined inventory item and auto-saves; manual save/load restores the walking pose");
            VerifyStore();
        }
        IEnumerator Inspect(InspectableProp prop,string id)
        {
            Vector3 front=prop.transform.position-prop.transform.forward*1.2f; front.y=1.43f;
            Aim(prop.GetComponent<Collider>(),front,prop.transform.TransformPoint(new Vector3(0,1.12f,0))); yield return null;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Logs/Cases/"+id+".png");
            bool interacted=player.TryInteractInView();
            Physics.Raycast(cameraOutput.transform.position,cameraOutput.transform.forward,out var rayHit,3);
            Require(interacted,"Prop interaction ray failed: "+prop.name+" hit="+(rayHit.collider!=null?rayHit.collider.name:"none")+" position="+player.transform.position+" camera="+cameraOutput.transform.position+" forward="+cameraOutput.transform.forward+" state="+MainLoop.Instance.MainLoopState+" enabled="+player.enabled+" gate="+player.cameraTransitionInProgress+" permission="+prop.IsInteractionAllowed);
            Require(inventory.GetUnlockedIds(InvestigationItemCategory.Information).Contains(id),"Inspection did not unlock "+id);
            Require(DialogueManager.Instance.IsDialogueRunning,"Inspection text did not open.");
            Require(!manager.SaveProgress(),"Save was allowed during inspection dialogue.");
            DialogueManager.Instance.ForceEndDia(); yield return new WaitForSecondsRealtime(.3f);
            MainLoop.Instance.SetMainLoopState_Main();
        }
        void VerifyStore()
        {
            string fixture=manager.SavePath+".fixture";
            var store=new ShangrilaCaseStore(fixture); var state=ShangrilaCaseSave.New(gameObject.scene.path);
            Require(store.Save(state,out _),"First atomic save failed.");
            state.completedCutsceneIds.Add("fixture"); Require(store.Save(state,out _),"Second atomic save failed.");
            File.WriteAllText(fixture,"{ damaged");
            Require(store.Load(gameObject.scene.path,out var restored,out _)==CaseLoadResult.RecoveredBackup && restored.completedCutsceneIds.Count==0,"Broken primary did not recover backup.");
            File.WriteAllText(fixture+".bak","{}");
            Require(store.Load(gameObject.scene.path,out _,out _)==CaseLoadResult.Invalid,"Invalid save schema was accepted.");
            Checks.Add("Atomic save retains one previous backup; corrupted primary recovers from backup; incomplete schema is rejected");
        }
        void VerifyInteractionRay()
        {
            float pitch=player.ViewPitch;
            player.RestoreWalkingPose(player.transform.position,player.transform.eulerAngles.y,25);
            var target=new GameObject("Interaction volume fixture",typeof(BoxCollider),typeof(Interactable));
            var zone=new GameObject("Non-interactive trigger fixture",typeof(BoxCollider));
            var wall=new GameObject("Occlusion fixture",typeof(BoxCollider));
            try
            {
                target.transform.position=cameraOutput.transform.position+cameraOutput.transform.forward*1.2f;
                target.GetComponent<BoxCollider>().size=Vector3.one*.3f; target.GetComponent<BoxCollider>().isTrigger=true;
                zone.transform.position=cameraOutput.transform.position+cameraOutput.transform.forward*.4f;
                zone.GetComponent<BoxCollider>().size=Vector3.one*.1f; zone.GetComponent<BoxCollider>().isTrigger=true;
                wall.SetActive(false); Physics.SyncTransforms();
                Require(player.TryInteractInView(),"Player ray cannot reach an interactive trigger through a non-interactive zone.");
                MainLoop.Instance.SetMainLoopState_Main();
                wall.transform.position=cameraOutput.transform.position+cameraOutput.transform.forward*.75f;
                wall.GetComponent<BoxCollider>().size=Vector3.one*.2f; wall.SetActive(true); Physics.SyncTransforms();
                Require(!player.TryInteractInView(),"Player interaction ray passed through a solid wall.");
                Checks.Add("Player ray preserves interactive trigger volumes, ignores non-interactive zones and its own body, and still respects solid wall occlusion");
            }
            finally
            {
                DestroyImmediate(target); DestroyImmediate(zone); DestroyImmediate(wall);
                player.RestoreWalkingPose(player.transform.position,player.transform.eulerAngles.y,pitch);
                MainLoop.Instance.SetMainLoopState_Main(); Physics.SyncTransforms();
            }
        }
        void Aim(Collider collider,Vector3 position,Vector3? focus=null)
        {
            Vector3 offset=cameraOutput.transform.position-player.transform.position;
            Vector3 direction=(focus??collider.bounds.center)-(position+offset);
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
            player.RestoreWalkingPose(position,yaw,pitch); MainLoop.Instance.SetMainLoopState_Main();
        }
        IEnumerator LeaveAndEnter()
        {
            player.RestoreWalkingPose(walking.entranceOutside.position,0,0); yield return new WaitForSecondsRealtime(.15f);
            MainLoop.Instance.SetMainLoopState_Main();
            yield return WalkTo(walking.entranceInside.position,true);
        }
        IEnumerator WalkTo(Vector3 point,bool stopOnCutscene=false)
        {
            var cc=player.GetComponent<CharacterController>(); float deadline=Time.realtimeSinceStartup+15;
            while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(point.x,point.z))>.08f)
            {
                if(stopOnCutscene && director.IsPlaying)yield break;
                Require(Time.realtimeSinceStartup<deadline,"Physical walk blocked at "+player.transform.position+" toward "+point);
                var direction=point-player.transform.position; direction.y=0;
                cc.Move(Vector3.ClampMagnitude(direction,3.5f*Time.unscaledDeltaTime)); yield return null;
            }
            yield return new WaitForSecondsRealtime(.1f);
        }
        static IEnumerator Until(Func<bool> condition,float timeout,string error)
        {
            float until=Time.realtimeSinceStartup+timeout;
            while(!condition()){Require(Time.realtimeSinceStartup<until,error);yield return null;}
        }
        static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    }
}
#endif
