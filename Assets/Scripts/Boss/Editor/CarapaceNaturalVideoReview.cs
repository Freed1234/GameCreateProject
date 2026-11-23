#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

// Decode the delivered MP4 itself in a disposable editor, then export review frames.
[InitializeOnLoad]
public static class CarapaceNaturalVideoReview
{
    const string Key="CarapaceNaturalVideoReview";
    static string Folder=>SessionState.GetBool(Key+"Motion",false)?"CarapaceMotionCapture":"NaturalPlayVideo";
    static string VideoName=>SessionState.GetBool(Key+"Motion",false)?"CarapaceMotion.mp4":"CarapaceNaturalGameplay.mp4";
    static VideoPlayer player;
    static RenderTexture target;
    static readonly List<(long frame,string name)> samples=new List<(long,string)>();
    static int index;
    static bool prepared,ready,playing;
    static double started;
    static string error;
    static CarapaceNaturalVideoReview()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));}};
    }
    public static void Run(){SessionState.SetBool(Key+"Motion",false);Begin();}
    public static void Motion(){SessionState.SetBool(Key+"Motion",true);Begin();}
    static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Exit",1);
        EditorSettings.enterPlayModeOptionsEnabled=false;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(started==0)started=EditorApplication.timeSinceStartup;
            if(error!=null)throw new Exception(error);
            if(EditorApplication.timeSinceStartup-started>120)throw new Exception("Video decode review timeout");
            if(player==null)
            {
                Directory.CreateDirectory(Folder+"/review");
                if(SessionState.GetBool(Key+"Motion",false))
                { samples.Add((30,"Idle"));samples.Add((145,"WalkRight"));samples.Add((235,"WalkLeft"));samples.Add((325,"WalkDown"));samples.Add((420,"Blocked"));samples.Add((465,"Charge"));samples.Add((570,"Slam"));samples.Add((670,"Volley")); }
                else {
                var found=new HashSet<string>();long last=0,lastVolley=-1;string previousStage=null;
                foreach(var line in File.ReadLines(Folder+"/frames.csv"))
                {
                    var row=line.Split(',');if(!long.TryParse(row[0],out long frame))continue;last=frame;
                    string stage=row[3];
                    if(stage!="HeldFrame"){if(stage=="Volley"&&previousStage!=stage)lastVolley=frame;previousStage=stage;}
                    if((stage=="Charge"||stage=="Slam"||stage=="Volley"||stage=="Reward")&&found.Add(stage))samples.Add((frame+(stage=="Reward"?30:2),stage));
                }
                if(found.Count!=4)throw new Exception("Missing movie stages");
                if(lastVolley>=0)samples.Add((lastVolley+12,"VolleyLater"));
                samples.Insert(0,(0,"Start"));samples.Add((last-30,"Resumed"));
                }
                target=new RenderTexture(1280,720,0);target.Create();
                player=new GameObject("MP4 review").AddComponent<VideoPlayer>();
                player.playOnAwake=false;player.source=VideoSource.Url;player.url=Path.GetFullPath(Folder+"/"+VideoName);
                player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=target;player.audioOutputMode=VideoAudioOutputMode.None;
                player.errorReceived+=(p,m)=>error=m;player.prepareCompleted+=p=>prepared=true;
                player.sendFrameReadyEvents=true;
                player.frameReady+=(p,f)=>{if(index<samples.Count&&f>=samples[index].frame){p.Pause();ready=true;}};
                player.Prepare();return;
            }
            if(!prepared)return;
            if(!playing)
            {
                if(player.width!=1280||player.height!=720||Math.Abs(player.frameRate-30)>.01)throw new Exception("Unexpected MP4 format");
                Debug.Log("NATURAL_VIDEO_METADATA width="+player.width+" height="+player.height+" fps="+player.frameRate+" duration="+player.length+" frames="+player.frameCount);
                playing=true;player.frame=samples[0].frame;player.Play();return;
            }
            if(!ready)return;ready=false;
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes(Folder+"/review/"+samples[index].name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
            Debug.Log("NATURAL_VIDEO_REVIEW_FRAME "+samples[index].name+" frame="+player.frame);index++;
            if(index==samples.Count){Debug.Log("NATURAL_VIDEO_REVIEW_PASS");SessionState.SetInt(Key+"Exit",0);EditorApplication.ExitPlaymode();return;}
            player.frame=samples[index].frame;player.Play();
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.ExitPlaymode();}
    }
}
#endif
