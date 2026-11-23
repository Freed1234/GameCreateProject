#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

// Run only in a disposable project: exits the editor when finished.
// Automates player actions; never changes time, experience, combat stats or boss selection.
[InitializeOnLoad]
public static class CarapaceNaturalPlayCheck
{
    const string Key = "CarapaceNaturalPlayCheck";
    static int frame = -1, wait, deaths, stacks;
    static double started;
    static CarapaceNaturalVideo video;
    static float nextReport, nextSkill, rewardAt = -1, resumedAt = -1;
    static string error, reward, previousStage, pendingPattern;
    static PlayerHealth player;
    static BossController boss;
    static bool initialized, warning, spawned, dead, picked;
    static readonly HashSet<string> patterns = new HashSet<string>();
    public static bool AllPatterns => patterns.Count == 3;
    static CarapaceNaturalPlayCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (m,s,t) => { if(SessionState.GetBool(Key,false) && Application.isPlaying && (t==LogType.Exception || t==LogType.Error)) error=m+"\n"+s; };
        EditorApplication.playModeStateChanged += s => { if(s==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false)) { video?.Dispose(); SessionState.SetBool(Key,false); EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1)); } };
    }
    public static void Run() { Begin(false); }
    public static void Record() { Begin(true); }
    static void Begin(bool record)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        SessionState.SetBool(Key,true); SessionState.SetBool(Key+"Record",record); SessionState.SetInt(Key+"Exit",1);
        EditorSettings.enterPlayModeOptionsEnabled=false; EditorApplication.EnterPlaymode();
    }
    public static object Call(object o,string name,params object[] args)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType) { var m=t.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); if(m!=null)return m.Invoke(o,args); }
        throw new Exception(name);
    }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    static void Note(string message) { Debug.Log("NATURAL_PLAY t="+GameSession.Instance.SurvivalTime.ToString("F3")+" "+message); }
    static float EffectValue(AugmentSystem.Augment a)
    {
        if(a.effect==AugmentSystem.Effect.StatPoints){Check(PlayerStats.Instance!=null,"Stat point reward has no PlayerStats in this scene");return PlayerStats.Instance.TotalPoints;}
        if(a.effect==AugmentSystem.Effect.SkillDamage)return SkillModifiers.DamageMultiplier;
        if(a.effect==AugmentSystem.Effect.SkillCooldown)return SkillModifiers.CooldownMultiplier;
        return (float)CarapaceGameplayRecording.Field(AugmentSystem.Instance,"lifestealPercent").GetValue(AugmentSystem.Instance);
    }
    static void Snapshot(string name)
    {
        Directory.CreateDirectory("NaturalPlayCapture");
        var camera=GameCamera.Get();var old=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1280,720,24);rt.Create();var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        var overlays=new Dictionary<Canvas,int>();
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.renderMode==RenderMode.ScreenSpaceOverlay) {overlays.Add(canvas,canvas.sortingOrder);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.sortingOrder+=30000;}
        camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("NaturalPlayCapture/"+name+".png",image.EncodeToPNG());
        foreach(var pair in overlays){pair.Key.sortingOrder=pair.Value;pair.Key.renderMode=RenderMode.ScreenSpaceOverlay;pair.Key.worldCamera=null;}
        camera.targetTexture=old;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        Application.runInBackground=true; EditorApplication.QueuePlayerLoopUpdate();
        if(frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            if(started==0)started=EditorApplication.timeSinceStartup;
            Check(error==null,error);Check(EditorApplication.timeSinceStartup-started<1000,"Wall timeout");
            if(!initialized)
            {
                if(++wait<5)return;
                player=BossUtil.Player(); Check(player!=null,"Player missing");
                Check(GameSession.Instance.SurvivalTime<5 && PlayerProgression.Instance.Level==1,"Not a fresh game");
                Check(Time.captureDeltaTime==0 && Time.timeScale==1,"Non-default clock");
                var pilot=new CarapaceNaturalPilot(player);
                var loop=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
                for(int i=0;i<loop.subSystemList.Length;i++)if(loop.subSystemList[i].type==typeof(UnityEngine.PlayerLoop.FixedUpdate)) { var children=new List<UnityEngine.LowLevel.PlayerLoopSystem>(loop.subSystemList[i].subSystemList);children.Insert(0,new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(CarapaceNaturalPilot),updateDelegate=()=>{if(player!=null && PlayerProgression.Instance.CurrentJob==PlayerJob.Archer)pilot.Step();}});loop.subSystemList[i].subSystemList=children.ToArray(); }
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
                player.Damaged+=()=>Note("DAMAGE hp="+player.CurrentHealth+" pos="+player.transform.position); initialized=true;Note("START level=1 hp="+player.CurrentHealth); Snapshot("Start");
            }
            Check(Time.captureDeltaTime==0 && Time.timeScale<=1,"Clock modified");Check(!player.IsDead,"Player died; incomplete run");
            var session=GameSession.Instance; var progress=PlayerProgression.Instance;
            if(SessionState.GetBool(Key+"Record",false) && session.SurvivalTime>=295) { if(video==null)video=new CarapaceNaturalVideo();video.Capture(AugmentSystem.Instance.IsOpen?"Reward":dead?"Defeated":previousStage??"Arrival"); }
            if(progress.IsChoosingJob) { Check(progress.ConfirmJob(PlayerJob.Archer),"First job selection"); Note("ARCHER level="+progress.Level); }
            var second=SecondJobSystem.Instance;
            if(second!=null && second.IsChoosing) { Call(second,"OpenSelection");Call(second,"Select",0);Call(second,"Confirm");Note("SECOND_JOB level="+progress.Level); }
            if(JobTierSystem.Instance!=null && JobTierSystem.Instance.IsChoosing){Call(JobTierSystem.Instance,"Later");Note("THIRD_JOB_DEFERRED");}
            var director=Object.FindAnyObjectByType<BossDirector>();
            bool spawning=(bool)CarapaceGameplayRecording.Field(director,"spawning").GetValue(director);
            if(spawning&&!warning) { Check(session.SurvivalTime>=300,"Early warning");warning=true;Note("WARNING");Snapshot("Warning"); }
            if(!spawned)
            {
                boss=Object.FindAnyObjectByType<BossController>();
                if(boss!=null)
                {
                    Check(warning&&session.SurvivalTime>=303,"Early boss");Check(boss.GetComponent<CarapaceCombat>()!=null,"Wrong boss");
                    Check(Object.FindObjectsByType<BossController>(FindObjectsSortMode.None).Length==1,"Duplicate bosses");spawned=true;boss.Died+=b=>{dead=true;deaths++;Note("BOSS_DEFEATED patterns="+patterns.Count);};
                    Note("SPAWN hp="+boss.CurrentHp);Snapshot("Spawn");
                }
                Check(session.SurvivalTime<330 || spawned,"Boss failed to spawn within 30 seconds");
            }
            if(boss!=null&&!dead)
            {
                string stage=boss.GetComponent<CarapaceCombat>().Stage;
                if(stage!=previousStage)
                {
                    if(stage=="Charge"||stage=="Slam"||stage=="Volley") { pendingPattern=stage;Note("PATTERN_START "+stage+" bossHP="+boss.CurrentHp);Snapshot(stage); }
                    if(stage=="Recover" && pendingPattern!=null){patterns.Add(pendingPattern);Note("PATTERN_COMPLETE "+pendingPattern);Snapshot(pendingPattern+"Complete");pendingPattern=null;}
                    previousStage=stage;
                }
            }
            if(Time.timeScale>0 && Time.time>=nextSkill)
            {
                // Wait for natural pattern coverage by withholding optional skill keys.
                if(!spawned || AllPatterns || dead)
                    foreach(var skill in player.GetComponentsInChildren<ArcherSkillBase>())
                        if(skill.isActiveAndEnabled&&skill.CooldownRemaining<=0)Call(skill,"CastAndStartCooldown");
                var chests=Object.FindAnyObjectByType<ChestManager>();
                if(chests!=null&&player.CurrentHealth<=player.MaxHealth-3)chests.TryUsePotion();
                nextSkill=Time.time+.25f;
            }
            var augment=AugmentSystem.Instance;
            if(dead)
            {
                Check(AllPatterns,"Boss died before all three patterns: incomplete coverage");Check(deaths==1,"Duplicate death");
                if(!augment.enabled&&!picked) { Check(!augment.IsOpen && (int)CarapaceGameplayRecording.Field(augment,"pending").GetValue(augment)==0,"Disabled augments must not receive boss rewards"); Check(!(bool)CarapaceGameplayRecording.Field(director,"offerAugmentOnKill").GetValue(director),"Boss rewards disabled");picked=true;resumedAt=session.SurvivalTime;Note("AUGMENTS_DISABLED combat continues"); }
                if(augment.IsOpen&&!picked)
                {
                    if(rewardAt<0) {rewardAt=Time.unscaledTime;Note("REWARD_OPEN");Snapshot("Reward");}
                    if(Time.unscaledTime-rewardAt>(SessionState.GetBool(Key+"Record",false)?3:1))
                    {
                        var offered=(List<AugmentSystem.Augment>)CarapaceGameplayRecording.Field(augment,"offered").GetValue(augment);
                        var cards=(AugmentCardUI[])CarapaceGameplayRecording.Field(augment,"cards").GetValue(augment);
                        Check(offered.Count==3&&cards.Length==3,"Three cards missing");
                        int choice=offered.FindIndex(a=>a.effect!=AugmentSystem.Effect.StatPoints||PlayerStats.Instance!=null);Check(choice>=0,"No connected reward in this offer");
                        if(PlayerStats.Instance==null)Note("LIMITATION PlayerStats missing; selecting a connected reward");
                        reward=offered[choice].id;stacks=augment.GetStacks(reward);
                        float effectBefore=EffectValue(offered[choice]);
                        cards[choice].OnPointerClick(new PointerEventData(EventSystem.current));
                        Check(!augment.IsOpen&&augment.GetStacks(reward)==stacks+1&&Time.timeScale==1,"Reward selection failed");
                        float expected=offered[choice].effect==AugmentSystem.Effect.SkillCooldown?effectBefore*(1-offered[choice].value/100f):effectBefore+offered[choice].value*(offered[choice].effect==AugmentSystem.Effect.SkillDamage?.01f:1f); Check(Mathf.Abs(EffectValue(offered[choice])-expected)<.001f,"Reward effect mismatch"); Note("EFFECT "+offered[choice].effect+" before="+effectBefore+" after="+EffectValue(offered[choice]));
                        picked=true;resumedAt=session.SurvivalTime;Note("PICK "+reward);Snapshot("Selected");
                    }
                }
                if(picked)
                {
                    Check(!augment.IsOpen,"Duplicate reward");
                    if(session.SurvivalTime-resumedAt>=10) {Note("NATURAL_PLAY_PASS hp="+player.CurrentHealth+" level="+progress.Level);Snapshot("Resumed");video?.Dispose();SessionState.SetInt(Key+"Exit",0);EditorApplication.ExitPlaymode();}
                }
            }
            if(session.SurvivalTime>=nextReport) {Note("STATUS hp="+player.CurrentHealth+" level="+progress.Level+" kills="+session.Kills);nextReport=session.SurvivalTime+30;}
            Check(session.SurvivalTime<570,"Run incomplete before next boss");
        }
        catch(Exception e) { Debug.LogException(e);video?.Dispose();EditorApplication.ExitPlaymode(); }
    }
}
#endif
