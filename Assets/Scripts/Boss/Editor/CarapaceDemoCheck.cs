#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Batch entry point: use a disposable project copy; this exits the Editor after validation.
[InitializeOnLoad]
public static class CarapaceDemoCheck
{
    const string Key="CarapaceDemoCheck.Running";
    static IEnumerator routine;
    static int lastFrame=-1,index;
    static double start;
    static MediaEncoder encoder;
    static RenderTexture target;
    static Texture2D pixels;
    static StreamWriter manifest;
    static bool capturing;
    static CarapaceDemo demo;
    static string Output => SessionState.GetString(Key+"Output","CarapaceCapture");
    static CarapaceDemoCheck()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false))
        {SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));}};
    }
    public static void Run()
    {SessionState.SetString(Key+"Output","CarapaceCapture");Begin();}
    public static void RunIndicators()
    {SessionState.SetString(Key+"Output","CarapaceIndicatorCapture");Begin();}
    public static void RunRectIndicators()
    {SessionState.SetString(Key+"Output","CarapaceRectangleCapture");Begin();}
    static void Begin()
    {
        CarapaceDemoSetup.Setup();
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Exit",1);
        EditorSettings.enterPlayModeOptionsEnabled=false;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();Application.runInBackground=true;
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(routine==null){start=EditorApplication.timeSinceStartup;routine=Verify();}
            if(EditorApplication.timeSinceStartup-start>600)throw new Exception("Demo timeout");
            bool next=routine.MoveNext();if(capturing)Capture();
            if(!next){Finish();Debug.Log("CARAPACE_DEMO_PLAY_PASS");SessionState.SetInt(Key+"Exit",0);EditorApplication.ExitPlaymode();}
        }
        catch(Exception e){Debug.LogException(e);Finish();SessionState.SetInt(Key+"Exit",1);EditorApplication.ExitPlaymode();}
    }
    static void Check(bool pass,string message){if(!pass)throw new Exception("CHECK FAILED: "+message);Debug.Log("CHECK_PASS: "+message);}
    static IEnumerator Verify()
    {
        Time.captureDeltaTime=1f/30;Time.timeScale=1;
        do{yield return null;demo=Object.FindAnyObjectByType<CarapaceDemo>();}while(demo==null||!demo.Ready);
        for(int i=0;i<10;i++)yield return null;
        demo.RestartDemo();
        VerifyIndicatorPixels();
        Check(CarapaceDemo.CapsuleContains(new Vector2(3,.54f),Vector2.zero,Vector2.right,6,CarapaceDemo.ChargeRadius)&&!CarapaceDemo.CapsuleContains(new Vector2(3,.56f),Vector2.zero,Vector2.right,6,CarapaceDemo.ChargeRadius),"charge outline matches swept body width");
        Check(CarapaceDemo.SlamContains(new Vector2(3.39f,0))&&!CarapaceDemo.SlamContains(new Vector2(3.41f,0))&&!CarapaceDemo.SlamContains(Vector2.up),"slam radius and angle boundaries");
        Vector2 origin=CarapaceDemo.ShardOriginOffset;
        Check(CarapaceDemo.CapsuleContains(origin+new Vector2(8.34f,0),origin,Vector2.right,CarapaceDemo.ShardRange,CarapaceDemo.ShardRadius)&&!CarapaceDemo.CapsuleContains(origin+new Vector2(8.36f,0),origin,Vector2.right,CarapaceDemo.ShardRange,CarapaceDemo.ShardRadius),"shard indicator includes full eight-unit flight and tip radius");
        Directory.CreateDirectory(Output+"/frames");
        manifest=new StreamWriter(Output+"/frames.csv");manifest.WriteLine("frame,skill,beat,pose");
        encoder=new MediaEncoder(Output+"/CarapaceDemo.mp4",new VideoTrackAttributes{frameRate=new MediaRational(30),width=1280,height=720,includeAlpha=false});
        capturing=true;bool checkedCharge=false,checkedVolley=false,checkedSlam=false;
        var poses=new System.Collections.Generic.HashSet<string>();
        while(demo.CompletedCycles==0)
        {
            poses.Add(demo.SkillName+":"+demo.FrameIndex);
            if(demo.CurrentBeat==CarapaceDemo.Beat.ChargeRecover&&!checkedCharge)
            {Check(Mathf.Abs(demo.ChargeDistance-6)<.05f,"charge travels six units");checkedCharge=true;}
            if(demo.CurrentBeat==CarapaceDemo.Beat.SlamRecover&&!checkedSlam)
            {Check(demo.SlamCount==1,"single slam effect");checkedSlam=true;}
            if(demo.CurrentBeat==CarapaceDemo.Beat.Volley&&demo.VolleyCount>0&&!checkedVolley)
            {Check(demo.LiveShards==8,"eight simultaneous radial shards");checkedVolley=true;}
            bool tell=demo.CurrentBeat==CarapaceDemo.Beat.ChargeTell||demo.CurrentBeat==CarapaceDemo.Beat.SlamTell||demo.CurrentBeat==CarapaceDemo.Beat.VolleyTell||demo.CurrentBeat==CarapaceDemo.Beat.Slam||(demo.CurrentBeat==CarapaceDemo.Beat.Volley&&demo.VolleyCount==0);
            if(demo.TelegraphVisible!=tell)throw new Exception("Telegraph out of sync");
            yield return null;
        }
        capturing=false;Finish();
        Check(checkedCharge&&checkedSlam&&checkedVolley,"all three skills captured");
        Check(poses.Count==24,"all 24 character frames played");
        Check(demo.LiveShards==0,"shards expire after eight units");
        Vector2 before=demo.BossPosition;int pose=demo.FrameIndex;Time.timeScale=0;
        for(int i=0;i<5;i++)yield return null;
        Check(demo.BossPosition==before&&demo.FrameIndex==pose,"pause freezes motion and pose");Time.timeScale=1;
        var wall=new GameObject("Demo test wall");wall.layer=LayerMask.NameToLayer("Obstacle");wall.transform.position=new Vector2(-3,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,4);Physics2D.SyncTransforms();
        demo.RestartDemo();while(demo.CurrentBeat!=CarapaceDemo.Beat.ChargeRecover)yield return null;
        Check(demo.Blocked&&demo.ChargeDistance<2,"charge stops before wall and enters recovery");Object.Destroy(wall);
        Check(Mathf.Abs(demo.PlannedChargeLength-demo.ChargeDistance)<.001f,"charge warning ends at obstacle-limited travel");
        demo.RestartDemo();Check(demo.LiveShards==0&&demo.CurrentBeat==CarapaceDemo.Beat.RestCharge,"restart clears effects and state");
        Check(new FileInfo(Output+"/CarapaceDemo.mp4").Length>1000,"MP4 encoded from Unity camera");
    }
    static void VerifyIndicatorPixels()
    {
        var shader=Resources.Load<Shader>("CarapaceDemo/Indicator");
        Check(shader!=null&&shader.isSupported,"indicator shader supported");
        var go=new GameObject("Indicator pixel check");go.layer=31;
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-10,-10),new Vector3(10,-10),new Vector3(10,10),new Vector3(-10,10)};mesh.triangles=new[]{0,1,2,0,2,3};
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var mat=new Material(shader);go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        var cameraObject=new GameObject("Indicator check camera");var cam=cameraObject.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=10;cam.transform.position=new Vector3(0,0,-10);cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;
        var rt=new RenderTexture(1000,1000,24);rt.Create();cam.targetTexture=rt;
        var tex=new Texture2D(1000,1000,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        try
        {
            mat.SetFloat("_Group",0);mat.SetFloat("_Progress",1);mat.SetFloat("_Length",6);mat.SetFloat("_Radius",.55f);
            cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1000,1000),0,0);tex.Apply();
            Check(Mathf.Abs(tex.GetPixel(825,525).a-.5f)<.02f,"charge square corner filled at 50 percent alpha");
            Check(tex.GetPixel(830,525).a<.01f,"charge outside rectangle remains clear");
            mat.SetFloat("_Group",2);mat.SetFloat("_Length",8);mat.SetFloat("_Radius",.35f);mat.SetFloat("_OriginY",.6f);
            cam.Render();tex.ReadPixels(new Rect(0,0,1000,1000),0,0);tex.Apply();
            Check(Mathf.Abs(tex.GetPixel(500,530).a-.5f)<.02f&&Mathf.Abs(tex.GetPixel(700,530).a-.5f)<.02f,"radial overlap and arm both use 50 percent alpha");
            Check(Mathf.Abs(tex.GetPixel(915,545).a-.5f)<.02f,"radial square corner filled");
        }
        finally
        {RenderTexture.active=previous;cam.targetTexture=null;Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);Object.DestroyImmediate(mat);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
    }
    static void Capture()
    {
        var camera=GameCamera.Get();
        if(target==null){target=new RenderTexture(1280,720,24);target.Create();pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);}
        var old=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
        if(!encoder.AddFrame(pixels))throw new Exception("Video encoder rejected frame");
        File.WriteAllBytes(Output+"/frames/"+index.ToString("0000")+".png",pixels.EncodeToPNG());
        manifest.WriteLine(index+","+((int)demo.CurrentBeat<=3?"Charge":(int)demo.CurrentBeat<=7?"Slam":"Volley")+","+demo.CurrentBeat+","+(demo.FrameIndex+1));index++;
        camera.targetTexture=old;RenderTexture.active=active;
    }
    static void Finish(){capturing=false;encoder?.Dispose();encoder=null;manifest?.Dispose();manifest=null;}
}
#endif
