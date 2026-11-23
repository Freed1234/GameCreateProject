#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using Object = UnityEngine.Object;

// Wall-clock video sampling only; never changes Unity's simulation clock.
public sealed class CarapaceNaturalVideo : IDisposable
{
    const string Folder = "NaturalPlayVideo";
    readonly MediaEncoder encoder;
    readonly RenderTexture target;
    readonly Texture2D pixels;
    readonly StreamWriter timeline;
    readonly double started;
    int frames;
    bool disposed;
    public CarapaceNaturalVideo()
    {
        Directory.CreateDirectory(Folder);
        target=new RenderTexture(1280,720,24);target.Create();
        pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
        encoder=new MediaEncoder(Folder+"/CarapaceNaturalGameplay.mp4",new VideoTrackAttributes
        {frameRate=new MediaRational(30),width=1280,height=720,includeAlpha=false});
        timeline=new StreamWriter(Folder+"/frames.csv");
        timeline.WriteLine("frame,wallSeconds,survivalSeconds,stage");
        started=EditorApplication.timeSinceStartup;
        Capture("RecordingStart");
        Debug.Log("NATURAL_VIDEO_START survival="+GameSession.Instance.SurvivalTime);
    }
    public void Capture(string stage)
    {
        double elapsed=EditorApplication.timeSinceStartup-started;
        int due=(int)Math.Floor(elapsed*30)+1;
        if(frames>=due)return;
        // Repeat the previous image for elapsed slots if rendering was slower than 30 Hz.
        // This preserves real duration instead of speeding up the movie or simulation.
        while(frames>0 && frames<due-1)AddFrame("HeldFrame");
        var camera=GameCamera.Get();
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;float oldAspect=camera.aspect;
        var canvases=new List<(Canvas canvas,int order,Camera camera,float distance)>();
        try
        {
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(canvas.renderMode==RenderMode.ScreenSpaceOverlay)
                {
                    canvases.Add((canvas,canvas.sortingOrder,canvas.worldCamera,canvas.planeDistance));
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
                    canvas.planeDistance=1;canvas.sortingOrder+=30000;
                }
            camera.aspect=16f/9f;camera.targetTexture=target;
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
            AddFrame(stage);
        }
        finally
        {
            foreach(var item in canvases)
            {
                item.canvas.sortingOrder=item.order;item.canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                item.canvas.worldCamera=item.camera;item.canvas.planeDistance=item.distance;
            }
            camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=oldActive;
        }
    }
    void AddFrame(string stage)
    {
        if(!encoder.AddFrame(pixels))throw new Exception("Natural video encoder rejected frame");
        timeline.WriteLine(frames+","+(frames/30d).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+
            GameSession.Instance.SurvivalTime.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+stage);
        frames++;
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        encoder.Dispose();timeline.Dispose();if(target!=null){target.Release();Object.DestroyImmediate(target);}if(pixels!=null)Object.DestroyImmediate(pixels);
        Debug.Log("NATURAL_VIDEO_FINISHED frames="+frames+" duration="+(frames/30d).ToString("F3"));
    }
}
#endif
