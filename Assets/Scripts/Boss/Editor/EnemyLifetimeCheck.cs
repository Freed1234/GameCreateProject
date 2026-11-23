#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Disposable-project regression and live-scene integration. Exits the Editor.
[InitializeOnLoad]
public static class EnemyLifetimeCheck
{
    const string Key="EnemyLifetimeCheck";
    static IEnumerator routine;
    static int frame=-1;
    static double started;
    static string referenceError;
    static EnemyLifetimeCheck()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(message,trace,type)=>
        {
            if(SessionState.GetBool(Key,false) && Application.isPlaying &&
                (message.Contains("MissingReferenceException")||message.Contains("NullReferenceException")))referenceError=message+"\n"+trace;
        };
        EditorApplication.playModeStateChanged+=state=>
        {if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));}};
    }
    public static void Reproduce(){Begin(true);}
    public static void Run(){Begin(false);}
    static void Begin(bool before)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Before",before);SessionState.SetInt(Key+"Exit",1);
        EditorSettings.enterPlayModeOptionsEnabled=false;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();
        if(frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            if(routine==null){started=EditorApplication.timeSinceStartup;routine=Verify();}
            if(referenceError!=null)throw new Exception("Runtime reference error: "+referenceError);
            if(EditorApplication.timeSinceStartup-started>300)throw new Exception("Lifetime check timeout");
            if(!routine.MoveNext()){Debug.Log("ENEMY_LIFETIME_PASS");SessionState.SetInt(Key+"Exit",0);EditorApplication.ExitPlaymode();}
        }
        catch(Exception e){SessionState.SetInt(Key+"Exit",1);Debug.LogException(e);EditorApplication.ExitPlaymode();}
    }
    static FieldInfo Field(object o,string name)
    {for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}throw new Exception("Missing field "+name);}
    static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,args);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("CHECK_PASS: "+message);}
    static EnemyHealth Enemy(Transform parent,Vector2 position)
    {var go=new GameObject("Lifetime enemy");go.transform.SetParent(parent);go.transform.position=position;var h=go.AddComponent<EnemyHealth>();h.InitializeSpawnHealth(3);return h;}
    static void Skills(bool before)
    {
        var container=new GameObject("Lifetime enemies").AddComponent<EnemySpawner>();container.enabled=false;
        var actor=new GameObject("Lifetime archer");
        var dash=actor.AddComponent<WindRushDash>();dash.enabled=false;Set(dash,"enemyContainer",container.transform);
        var snipe=actor.AddComponent<DarkSnipeSkill>();snipe.enabled=false;Set(snipe,"enemyContainer",container.transform);Set(snipe,"aimTime",0f);
        foreach(bool isDash in new[]{true,false})
        {
            var e=Enemy(container.transform,Vector2.right);
            var cache=SkillUtil.EnemiesNow(container.transform);cache.Clear();cache.Add(e);Object.DestroyImmediate(e.gameObject);
            bool failed=false;
            try
            {
                if(isDash){var outer=(IEnumerator)Call(dash,"Run",Vector2.right);outer.MoveNext();var inner=(IEnumerator)outer.Current;inner.MoveNext();inner.MoveNext();(inner as IDisposable)?.Dispose();}
                else{var shot=(IEnumerator)Call(snipe,"Snipe");shot.MoveNext();}
            }
            catch(MissingReferenceException){failed=true;}
            Check(failed==before,(before?"reproduced destroyed-cache exception: ":"destroyed-cache regression safe: ")+(isDash?"WindRushDash":"DarkSnipeSkill"));
            cache.Clear();
        }
        if(!before)
        {
            Set(snipe,"aimTime",.1f);var e=Enemy(container.transform,Vector2.right);
            var shot=(IEnumerator)Call(snipe,"Snipe");shot.MoveNext();Object.DestroyImmediate(e.gameObject);
            for(int i=0;i<30&&shot.MoveNext();i++){}
            Check(e==null,"target destroyed during delayed aim is safe");
            Set(snipe,"aimTime",0f);var cache=SkillUtil.EnemiesNow(container.transform);cache.Clear();
            var dead=Enemy(container.transform,Vector2.right);Set(dead,"currentHealth",0);cache.Add(dead);
            var inactive=Enemy(container.transform,Vector2.right*2);inactive.gameObject.SetActive(false);cache.Add(inactive);
            var live=Enemy(container.transform,Vector2.right*3);cache.Add(live);
            ((IEnumerator)Call(snipe,"Snipe")).MoveNext();
            Check(dead.CurrentHealth==0&&inactive.CurrentHealth==3&&live.IsDead,"skip dead/inactive targets while damaging live target");
            actor.AddComponent<PlayerHealth>();
            var feather=actor.AddComponent<FeatherDashSkill>();
            cache.Clear();var first=Enemy(container.transform,new Vector2(.1f,0));var second=Enemy(container.transform,new Vector2(.2f,0));
            cache.Add(first);cache.Add(second);Set(feather,"tracking",true);Set(feather,"direction",Vector2.right);
            Call(feather,"LateUpdate");
            Check(first.IsDead&&second.IsDead,"feather dash kills multiple targets and keeps hit effects safe");
        }
        Object.Destroy(actor);Object.Destroy(container.gameObject);
    }
    static IEnumerator Verify()
    {
        Time.captureDeltaTime=1f/30;Time.timeScale=1;for(int i=0;i<4;i++)yield return null;
        bool before=SessionState.GetBool(Key+"Before",false);Skills(before);if(before)yield break;
        yield return null;
        var load=SceneManager.LoadSceneAsync("GameCreateProject");while(!load.isDone)yield return null;
        for(int i=0;i<30;i++)yield return null;
        var player=BossUtil.Player();Set(player,"maxHealth",10000);Set(player,"currentHealth",10000);
        Set(PlayerProgression.Instance,"autoPromptShown",true);
        var director=Object.FindAnyObjectByType<BossDirector>();var spawner=Object.FindAnyObjectByType<EnemySpawner>();
        Check(spawner.enabled&&Object.FindAnyObjectByType<MobHealthBars>().enabled,"normal spawns and health bars enabled");
        Check(AugmentSystem.Instance!=null,"augment system exists");
        Set(GameSession.Instance,"<SurvivalTime>k__BackingField",300f);
        BossController boss=null;
        for(int i=0;i<300&&boss==null;i++){yield return null;boss=Object.FindAnyObjectByType<BossController>();}
        Check(boss!=null,"five-minute warning and natural boss spawn");
        var combat=boss.GetComponent<CarapaceCombat>();var tier=(BossDirector.BossTier)Field(boss,"tier").GetValue(boss);
        var patterns=new[]{BossController.Pattern.Charge,BossController.Pattern.Slam,BossController.Pattern.Radial};
        var stages=new[]{"Charge","Slam","Volley"};
        // Force pattern order for coverage; retain production attack timing and normal enemies.
        for(int p=0;p<3;p++)
        {
            tier.patterns=new[]{patterns[p]};boss.transform.position=(Vector2)player.transform.position+Vector2.left*2;
            Set(boss,"attackTimer",0f);bool seen=false;
            for(int i=0;i<300;i++)
            {
                if(combat.Stage==stages[p])seen=true;
                if(seen&&combat.Stage=="Idle")break;
                if(i%20==0)
                {
                    SkillUtil.EnemiesNow(spawner.transform);
                    foreach(var enemy in Object.FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None))
                        if(enemy!=null)enemy.GetComponent<EnemyHealth>().TakeDamage(999);
                }
                yield return null;
            }
            Check(seen,"live-scene pattern with concurrent mob deaths: "+stages[p]);
        }
        // Exercise the actual distant-enemy cleanup in the same frame as a cached list.
        var prefab=(EnemyMovement)Field(spawner,"enemyPrefab").GetValue(spawner);
        var distant=Object.Instantiate(prefab,player.transform.position+Vector3.right*80,Quaternion.identity,spawner.transform);
        distant.enabled=false;Set(spawner,"despawnDelay",0f);SkillUtil.EnemiesNow(spawner.transform);
        Call(spawner,"LateUpdate");yield return null;yield return null;
        Check(distant==null,"distant enemy cleanup with health bars active");
        int deaths=0;boss.Died+=b=>deaths++;
        int kills=GameSession.Instance.Kills,level=PlayerProgression.Instance.Level,xp=PlayerProgression.Instance.Experience;
        boss.GetComponent<EnemyHealth>().TakeDamage(boss.CurrentHp);
        for(int i=0;i<30;i++)yield return null;
        Check(deaths==1&&boss==null&&GameSession.Instance.Kills>=kills+1,"boss death and kill registration");
        Check(PlayerProgression.Instance.Level>level||PlayerProgression.Instance.Experience>xp,"experience reward");
        var augment=AugmentSystem.Instance;
        Check(!augment.enabled&&!augment.IsOpen&&(int)Field(augment,"pending").GetValue(augment)==0,"augments disabled without queued reward");
        Check(!((GameObject)Field(augment,"panel").GetValue(augment)).activeInHierarchy,"reward panel remains hidden");
        Check(Object.FindAnyObjectByType<BossHitProxy>()==null&&Object.FindAnyObjectByType<CarapaceCombat>()==null,"boss proxy and attack effects removed");
        Check(Time.timeScale>0,"boss defeat does not pause combat");
        float time=GameSession.Instance.SurvivalTime;for(int i=0;i<301;i++)yield return null;
        Check(GameSession.Instance.SurvivalTime-time>=10&&!augment.IsOpen,"ten seconds of combat without augment offer");        var oldSession=GameSession.Instance;GameSession.Instance.Restart();
        for(int i=0;i<180&&(GameSession.Instance==null||GameSession.Instance==oldSession);i++)yield return null;
        for(int i=0;i<15;i++)yield return null;
        Check(GameSession.Instance!=null&&GameSession.Instance!=oldSession&&GameSession.Instance.SurvivalTime<5,"scene restart resets session");
        Check(Object.FindAnyObjectByType<BossController>()==null&&Object.FindAnyObjectByType<BossHitProxy>()==null,"restart has no stale boss references");
    }
}
#endif
