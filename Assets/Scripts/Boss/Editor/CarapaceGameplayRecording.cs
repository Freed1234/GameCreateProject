#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Disposable-project capture only. Does not save gameplay configuration changes.
[InitializeOnLoad]
public static class CarapaceGameplayRecording
{
    const string Key="CarapaceGameplayRecording",Output="CarapaceGameplayCapture";
    static int lastFrame=-1,count,wait,pattern;
    static bool initialized,seenAttack,dead,picked;
    static int rewardFrame=-1,pickedFrame=-1,initialStacks;
    static float nextSkill;
    static string rewardId,referenceError;
    static PlayerHealth player;
    static BossController boss;
    static CarapaceCombat combat;
    static BossDirector.BossTier tier;
    static MediaEncoder encoder;
    static RenderTexture target;
    static Texture2D pixels;

    static StreamWriter manifest;
    static double started;
    static readonly HashSet<string> snapshots=new HashSet<string>();
    static readonly BossController.Pattern[] sequence={BossController.Pattern.Charge,BossController.Pattern.Slam,BossController.Pattern.Radial};
    static readonly string[] stages={"Charge","Slam","Volley"};
    static CarapaceGameplayRecording()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&Application.isPlaying&&(m.Contains("MissingReferenceException")||m.Contains("NullReferenceException")))referenceError=m+"\n"+s;};
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false)){Finish();SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));}};
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Exit",1);EditorSettings.enterPlayModeOptionsEnabled=false;EditorApplication.EnterPlaymode();
    }
    public static FieldInfo Field(object o,string name)
    {for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}throw new Exception(name);}
    public static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static object Call(object o,string name,params object[] args)
    {for(Type t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(m!=null)return m.Invoke(o,args);}throw new Exception(name);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(started==0)started=EditorApplication.timeSinceStartup;
            if(referenceError!=null)throw new Exception(referenceError);
            if(EditorApplication.timeSinceStartup-started>600)throw new Exception("Recording timeout");
            Time.captureDeltaTime=1f/30;
            if(!initialized)
            {
                if(++wait<30)return;
                player=BossUtil.Player();player.gameObject.AddComponent<CarapaceRecordingPilot>();
                var prepared=PlayerProgression.Instance;
                while(!prepared.CanChooseJob)prepared.AddExperience(prepared.RequiredExperience-prepared.Experience);
                prepared.OpenJobSelection();prepared.ConfirmJob(PlayerJob.Archer);
                while(prepared.Level<30)prepared.AddExperience(prepared.RequiredExperience-prepared.Experience);
                var second=SecondJobSystem.Instance;Call(second,"OpenSelection");Call(second,"Select",0);Call(second,"Confirm");
                if(!second.HasAdvanced)throw new Exception("Recording character preparation failed");
                var director=Object.FindAnyObjectByType<BossDirector>();
                var tiers=(BossDirector.BossTier[])Field(director,"tiers").GetValue(director);tier=tiers[0];tier.patterns=new[]{sequence[0]};
                Set(GameSession.Instance,"<SurvivalTime>k__BackingField",299f);
                Directory.CreateDirectory(Output);
                target=new RenderTexture(1280,720,24);target.Create();pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                encoder=new MediaEncoder(Output+"/CarapaceGameplay.mp4",new VideoTrackAttributes{frameRate=new MediaRational(30),width=1280,height=720,includeAlpha=false});
                manifest=new StreamWriter(Output+"/timeline.csv");manifest.WriteLine("frame,stage,playerHP,bossHP");initialized=true;
            }
            if(player.IsDead)throw new Exception("Pilot died; do not publish an incomplete recording");
            if(boss==null&&!dead)
            {
                boss=Object.FindAnyObjectByType<BossController>();
                if(boss!=null){combat=boss.GetComponent<CarapaceCombat>();boss.Died+=b=>{dead=true;Debug.Log("RECORD_BOSS_DEFEATED frame="+count);};}
            }
            if(combat!=null&&pattern<3)
            {
                if(combat.Stage==stages[pattern])seenAttack=true;
                if(seenAttack&&combat.Stage=="Recover")
                {
                    Debug.Log("RECORD_PATTERN "+stages[pattern]+" frame="+count);pattern++;seenAttack=false;
                    tier.patterns=pattern<3?new[]{sequence[pattern]}:sequence;
                }
            }
            var progression=PlayerProgression.Instance;
            if(progression.IsChoosingJob)progression.ConfirmJob(PlayerJob.Archer);
            if(!dead&&Time.timeScale>0&&Time.time>=nextSkill)
            {
                foreach(var skill in player.GetComponentsInChildren<ArcherSkillBase>())
                    if(skill.isActiveAndEnabled&&skill.CooldownRemaining<=0&&(pattern>=3||skill is LeafVortexSkill||skill is CompressedWindSkill))Call(skill,"CastAndStartCooldown");
                nextSkill=Time.time+.25f;
            }
            var augment=AugmentSystem.Instance;
            if(dead&&augment.IsOpen&&!picked)
            {
                if(rewardFrame<0){rewardFrame=count;Debug.Log("RECORD_REWARD_OPEN frame="+count);}
                if(count-rewardFrame==60)
                {
                    var cards=(AugmentCardUI[])Field(augment,"cards").GetValue(augment);cards[0].OnPointerEnter(new PointerEventData(EventSystem.current));
                }
                if(count-rewardFrame>=90)
                {
                    var offers=(List<AugmentSystem.Augment>)Field(augment,"offered").GetValue(augment);rewardId=offers[0].id;initialStacks=augment.GetStacks(rewardId);
                    var cards=(AugmentCardUI[])Field(augment,"cards").GetValue(augment);cards[0].OnPointerClick(new PointerEventData(EventSystem.current));
                    if(!augment.IsOpen){picked=true;pickedFrame=count;Debug.Log("RECORD_REWARD_PICK "+rewardId+" frame="+count);}
                }
            }
            Capture();
            if(picked&&count-pickedFrame>=90)
            {
                if(pattern!=3||augment.GetStacks(rewardId)!=initialStacks+1||Time.timeScale<=0)throw new Exception("Recording acceptance failed");
                Finish();Debug.Log("CARAPACE_GAMEPLAY_RECORDING_PASS frames="+count);SessionState.SetInt(Key+"Exit",0);EditorApplication.ExitPlaymode();
            }
            if(count>5400)throw new Exception("Fight exceeded three minutes");
        }
        catch(Exception e){Debug.LogException(e);Finish();EditorApplication.ExitPlaymode();}
    }
    static void Capture()
    {
        var camera=GameCamera.Get();var oldTarget=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;int mask=camera.cullingMask;
        var overlays=new List<Canvas>();
        var orders=new Dictionary<Canvas,int>();
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.renderMode==RenderMode.ScreenSpaceOverlay)
        {
            overlays.Add(canvas);orders.Add(canvas,canvas.sortingOrder);
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            canvas.sortingOrder=30000+canvas.sortingOrder;
        }
        camera.aspect=16f/9f;camera.targetTexture=target;
        Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
        if(!encoder.AddFrame(pixels))throw new Exception("Encoder rejected frame");
        string stage=AugmentSystem.Instance.IsOpen?"Reward":dead?"Defeated":combat==null?"Arrival":combat.Stage+"-"+pattern;
        if(snapshots.Add(stage))File.WriteAllBytes(Output+"/"+stage+".png",pixels.EncodeToPNG());
        manifest.WriteLine(count+","+stage+","+player.CurrentHealth+","+(boss!=null?boss.CurrentHp:0));count++;
        foreach(var canvas in overlays){canvas.sortingOrder=orders[canvas];canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}

        camera.targetTexture=oldTarget;camera.aspect=aspect;camera.cullingMask=mask;RenderTexture.active=active;
    }
    static void Finish(){encoder?.Dispose();encoder=null;manifest?.Dispose();manifest=null;}
}

[DefaultExecutionOrder(31000)]
public sealed class CarapaceRecordingPilot : MonoBehaviour
{
    PlayerMovement movement;
    Vector2 lastPosition,escape;
    float stuckTime,escapeTime;
    void Awake(){movement=GetComponent<PlayerMovement>();}
    void Update()
    {
        if(Time.timeScale<=0)return;
        Vector2 at=transform.position,wanted=at;
        var boss=Object.FindAnyObjectByType<BossController>();
        if(boss!=null)
        {
            var combat=boss.GetComponent<CarapaceCombat>();var pattern=(BossController.Pattern)CarapaceGameplayRecording.Field(boss,"lastPattern").GetValue(boss);
            Vector2 center=boss.transform.position,away=(at-center).normalized;if(away==Vector2.zero)away=Vector2.right;
            float radius=PlayerProgression.Instance.CurrentJob==PlayerJob.Archer?4f:1.25f;
            if(combat.Stage=="Tell"||combat.Stage=="Charge"||combat.Stage=="Slam"||combat.Stage=="Volley")
            {
                radius=4.6f;
                if(pattern==BossController.Pattern.Radial)
                {
                    center+=CarapaceGeometry.ShardOriginOffset;
                    float angle=Mathf.Atan2(at.y-center.y,at.x-center.x)*Mathf.Rad2Deg;
                    away=SkillUtil.Rotate(Vector2.right,Mathf.Round((angle-22.5f)/45f)*45+22.5f);
                }
                else away=SkillUtil.Rotate(away,45);
            }
            else away=SkillUtil.Rotate(away,8);
            wanted=center+away*radius;
        }
        else
        {
            var enemy=SkillUtil.Nearest(SkillUtil.EnemyContainer(null),at,20);
            if(enemy!=null){Vector2 away=(at-(Vector2)enemy.transform.position).normalized;wanted=(Vector2)enemy.transform.position+away*1.2f;}
        }
        Vector2 heading=wanted-at;if(heading.magnitude<.12f)heading=Vector2.zero;else heading=heading.normalized;
        stuckTime=Vector2.Distance(lastPosition,at)<.01f&&heading!=Vector2.zero?stuckTime+Time.deltaTime:0;
        lastPosition=at;
        if(stuckTime>.6f){escape=SkillUtil.Rotate(movement.LastMoveDirection,120);escapeTime=1.2f;stuckTime=0;}
        if(escapeTime>0){heading=escape;escapeTime-=Time.deltaTime;}
        if(heading!=Vector2.zero)
        {
            Vector2 best=Vector2.zero;float bestScore=float.NegativeInfinity;
            for(int i=0;i<16;i++)
            {
                Vector2 candidate=SkillUtil.Rotate(heading,i*22.5f);
                if(Physics2D.CircleCast(at,.35f,candidate,1f,SkillUtil.ObstacleMask).collider!=null)continue;
                float score=Vector2.Dot(candidate,heading);
                foreach(var enemy in SkillUtil.EnemiesNow(SkillUtil.EnemyContainer(null)))
                {
                    if(enemy==null||enemy.IsDead||enemy.GetComponent<BossHitProxy>()!=null)continue;
                    float d=Vector2.Distance(at+candidate*.6f,enemy.transform.position);
                    if(d<1.1f)score-=(1.1f-d)*5;
                }
                if(score>bestScore){bestScore=score;best=candidate;}
            }
            heading=best;
        }
        CarapaceGameplayRecording.Set(movement,"moveInput",heading);
        if(heading!=Vector2.zero)CarapaceGameplayRecording.Set(movement,"lastDirection",heading);
        foreach(var enemy in SkillUtil.EnemiesNow(SkillUtil.EnemyContainer(null)))
            if(enemy!=null&&!enemy.IsDead&&enemy.GetComponent<BossHitProxy>()==null&&Vector2.Distance(at,enemy.transform.position)<1.6f)
            {typeof(PlayerMovement).GetMethod("TryStartDash",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(movement,null);break;}
    }
}
#endif
