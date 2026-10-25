#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class StreetWalkVerification : MonoBehaviour
{
    public bool Done { get; private set; }
    public string Failure { get; private set; }
    public readonly List<string> Checks = new List<string>();
    IEnumerator Start()
    {
        var sequence = Run();
        while (true)
        {
            bool more;
            try { more = sequence.MoveNext(); }
            catch (Exception e) { Failure = e.ToString(); break; }
            if (!more) break;
            yield return sequence.Current;
        }
        Done = true;
    }
    IEnumerator Run()
    {
        var context = FindFirstObjectByType<StreetWalkContext>();
        if (context == null) throw new Exception("Missing walk context.");
        float deadline = Time.realtimeSinceStartup + 5;
        while (MainLoop.Instance == null || InputManager.Instance == null || MainLoop.Instance.PC != context.player)
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Existing player/managers did not bind.");
            yield return null;
        }
        Checks.Add("Existing PlayerControler bound to MainLoop and InputManager");
        context.enabled = false;
        MainLoop.Instance.SetMainLoopState(MainState.Freeze);
        var player = context.player;
        var cc = player.GetComponent<CharacterController>();
        var routes = new[]
        {
            new Vector3(context.entranceOutside.position.x, 0, context.streetSpawn.position.z),
            context.entranceOutside.position,
            context.entranceInside.position,
            new Vector3(-3, context.entranceInside.position.y, 10),
        };
        foreach (var target in routes)
        {
            float elapsed = 0;
            while (Distance(player.transform.position, target) > 0.12f && elapsed < 12)
            {
                Vector3 direction = target - player.transform.position; direction.y = 0;
                player.transform.rotation = Quaternion.LookRotation(direction);
                cc.Move((direction.normalized * 3.5f + Vector3.down * 15) * Time.deltaTime);
                elapsed += Time.deltaTime;
                if (player.transform.position.y < -1) throw new Exception("Player fell through floor.");
                yield return null;
            }
            if (Distance(player.transform.position, target) > 0.2f) throw new Exception("Walking blocked on entry: target=" + target + ", actual=" + player.transform.position);
        }
        if (!context.IsInside || !cc.isGrounded) throw new Exception("Player did not stand inside Shangrila.");
        Checks.Add("Walked from street through the physical doorway into Shangrila; grounded");
        Capture(player.GetComponentInChildren<Camera>(), "inside");
        Vector3 beforeWall = player.transform.position;
        float wallTime = 0;
        while (wallTime < 2.7f)
        {
            cc.Move((Vector3.right * 3.5f + Vector3.down * 15) * Time.deltaTime);
            wallTime += Time.deltaTime;
            yield return null;
        }
        if (player.transform.position.x > 2.5f) throw new Exception("Player passed through the interior wall.");
        Checks.Add("Interior wall blocks the player capsule");
        cc.enabled = false; player.transform.position = beforeWall; cc.enabled = true; Physics.SyncTransforms();
        foreach (var target in new[] { context.entranceInside.position, context.entranceOutside.position, context.streetSpawn.position })
        {
            float elapsed = 0;
            while (Distance(player.transform.position, target) > 0.12f && elapsed < 12)
            {
                Vector3 direction = target - player.transform.position; direction.y = 0;
                player.transform.rotation = Quaternion.LookRotation(direction);
                cc.Move((direction.normalized * 3.5f + Vector3.down * 15) * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (Distance(player.transform.position, target) > 0.2f) throw new Exception("Walking blocked on exit: " + player.transform.position);
        }
        if (context.IsInside || !cc.isGrounded) throw new Exception("Player did not return to the street.");
        Checks.Add("Walked back through the doorway to street spawn; grounded");
        Capture(player.GetComponentInChildren<Camera>(), "outside");
        context.ReturnToStreet();
        MainLoop.Instance.SetMainLoopState(MainState.Main);
        context.enabled = true;
        yield return null;
        if (MainLoop.Instance.PC != player || !player.enabled) throw new Exception("Player control did not resume.");
        Checks.Add("Existing player control resumes after verification");
    }
    static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    static void Capture(Camera source, string name)
    {
        var go = new GameObject("Bar Retro Verification"); var camera = go.AddComponent<Camera>(); camera.CopyFrom(source);
        camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation); camera.enabled = false;
        camera.GetUniversalAdditionalCameraData().SetRenderer(1); camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var rt = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32); rt.Create(); var previous = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=rt });
            RenderTexture.active=rt; var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes("Logs/StreetWalk/"+name+".png",image.EncodeToPNG());DestroyImmediate(image);
        }
        finally {RenderTexture.active=previous;rt.Release();DestroyImmediate(rt);DestroyImmediate(go);}
    }
}
#endif
