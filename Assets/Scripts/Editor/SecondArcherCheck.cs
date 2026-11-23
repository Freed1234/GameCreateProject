#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in the disposable validation copy. Never saves Play-mode changes.
[InitializeOnLoad]
public static class SecondArcherCheck
{
    const string Running = "SecondArcherCheck.Running";
    static IEnumerator checks;
    static int lastFrame = -1;
    static double started;
    static PlayerProgression player;
    static EnemySpawner spawner;
    static Camera camera;
    static RenderTexture target;
    static Texture2D pixels;
    static string capture;
    static int captureIndex;
    static SecondArcherCheck()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Running, false))
            { SessionState.SetBool(Running, false); EditorApplication.Exit(SessionState.GetInt("SecondArcherCheck.Exit", 1)); }
        };
    }
    public static void Run()
    {
        SessionState.SetBool("SecondArcherCheck.Progression", false);
        Begin();
    }
    public static void RunProgression()
    {
        SessionState.SetBool("SecondArcherCheck.Progression", true);
        Begin();
    }
    private static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        SessionState.SetBool(Running, true); SessionState.SetInt("SecondArcherCheck.Exit",1);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Running,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (!Application.isPlaying) return;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        Application.runInBackground = true;
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (checks == null) { started=EditorApplication.timeSinceStartup; checks=SessionState.GetBool("SecondArcherCheck.Progression",false)?Progression():Verify(); }
            if (EditorApplication.timeSinceStartup-started>600) throw new Exception("Validation timeout");
            bool next=checks.MoveNext();
            if (capture != null) Capture();
            if (!next)
            {
                Debug.Log("SECOND_ARCHER_PLAY_PASS"); SessionState.SetInt("SecondArcherCheck.Exit",0);
                EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception e) { Debug.LogException(e); capture=null; SessionState.SetInt("SecondArcherCheck.Exit",1); EditorApplication.ExitPlaymode(); }
    }
    static void Require(bool ok,string message) { if (!ok) throw new Exception("CHECK FAILED: "+message); Debug.Log("CHECK_PASS: "+message); }
    static FieldInfo Field(object o,string name)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType)
        { var field=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public); if(field!=null)return field; }
        throw new Exception("Missing field "+name);
    }
    static void Set(object o,string name,object value) { Field(o,name).SetValue(o,value); }
    static object Call(object o,string name,params object[] args)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType)
        {var method=t.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public); if(method!=null)return method.Invoke(o,args);}
        throw new Exception("Missing method "+name);
    }
    static void Cast(ArcherSkillBase skill) { Call(skill,"CastAndStartCooldown"); }
    static EnemyHealth Spawn(Vector2 at,int health=300,bool goblin=false)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Perfeb/"+(goblin?"SlingshotGoblin":"Enemy")+".prefab");
        var obj=Object.Instantiate(prefab,at,Quaternion.identity,spawner.transform);
        var movement=obj.GetComponent<EnemyMovement>(); movement.SetPlayer(player.transform); movement.HoldPosition=true;
        if(goblin)obj.GetComponent<SlingshotGoblin>().enabled=false;
        var h=obj.GetComponent<EnemyHealth>();h.InitializeSpawnHealth(health);return h;
    }
    static void Clear()
    {
        foreach(Transform child in spawner.transform) Object.Destroy(child.gameObject);
        foreach(var p in Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None))Object.Destroy(p.gameObject);
        foreach(var fx in Object.FindObjectsByType<SecondArcherFx>(FindObjectsSortMode.None))Object.Destroy(fx.gameObject);
    }
    static void Position(Vector2 at)
    { player.transform.position=at;player.GetComponent<Rigidbody2D>().position=at; }
    static void StartCapture(string name)
    {capture=name;captureIndex=0;Directory.CreateDirectory("SecondArcherFrames/"+name);}
    static void Capture()
    {
        if(target==null){target=new RenderTexture(960,540,24);target.Create();pixels=new Texture2D(960,540,TextureFormat.RGB24,false);}
        var old=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
        File.WriteAllBytes($"SecondArcherFrames/{capture}/{captureIndex++:000}.png",pixels.EncodeToPNG());
        camera.targetTexture=old;RenderTexture.active=active;
    }
    static IEnumerator Verify()
    {
        for(int i=0;i<20;i++)yield return null;
        Time.timeScale=1;Time.captureDeltaTime=1f/30;
        player=PlayerProgression.Instance;spawner=Object.FindAnyObjectByType<EnemySpawner>();spawner.enabled=false;
        player.GetComponent<PlayerHealth>().enabled=false;Set(player.GetComponent<PlayerHealth>(),"maxHealth",100000);Set(player.GetComponent<PlayerHealth>(),"currentHealth",100000);
        foreach(var o in Object.FindObjectsByType<WorldDepthSort>(FindObjectsSortMode.None))
            if(WorldDepthSort.Obstacles.Contains(o))o.gameObject.SetActive(false);
        Object.FindAnyObjectByType<MapChunkManager>().enabled=false;
        camera=GameCamera.Get();foreach(var script in camera.GetComponents<MonoBehaviour>())if(script is CameraFollow)script.enabled=false;
        camera.orthographicSize=5;camera.transform.position=new Vector3(0,0,-10);Position(new Vector2(-4,0));Clear();
        var system=player.GetComponent<SecondJobSystem>();
        var z=player.GetComponent<StormBarrageSkill>();var x=player.GetComponent<LeafVortexSkill>();var c=player.GetComponent<CompressedWindSkill>();
        var dash=player.GetComponent<FeatherDashSkill>();var movement=player.GetComponent<PlayerMovement>();
        Require(system.enabled&&!z.enabled&&!x.enabled&&!c.enabled&&!dash.enabled,"second skills locked initially");
        player.AddExperience(5);player.OpenJobSelection();Require(player.ConfirmJob(PlayerJob.Archer),"first archer selection");
        player.AddExperience(270);yield return null;
        Require(player.Level==29&&!system.IsChoosing,"no second prompt at 29");
        player.AddExperience(10);yield return null;yield return null;
        Require(player.Level==30&&system.IsChoosing&&Time.timeScale==0,"level 30 prompt pauses");
        Call(system,"Later");Require(!system.HasAdvanced&&Time.timeScale==1,"defer retains first job");
        Call(system,"OnLaterButton");Call(system,"Select",0);Call(system,"Confirm");
        Require(system.HasAdvanced&&z.enabled&&x.enabled&&c.enabled&&dash.enabled,"second job activates four skills");
        player.GetComponent<JobAttackController>().ApplyCurrentJob();
        Require(!player.GetComponent<ArcherVolleySkill>().enabled&&!player.GetComponent<EvasiveShotSkill>().enabled,"first skills stay disabled after reapply");
        Require(movement.DashCharges==3&&movement.DashRechargeDuration==2f,"dash charges unchanged");
        foreach(var a in player.GetComponentsInChildren<AutoProjectileAttack>(true))a.enabled=false;
        var parity = VerifyXParity(x);
        while (parity.MoveNext()) yield return parity.Current;
        var piercing = VerifyZPiercing(z);
        while (piercing.MoveNext()) yield return piercing.Current;
        int[] normal={3,6,12,18,24,30},gob={3,6,12,24,30,36};
        for(int i=0;i<6;i++)
        {
            Require(spawner.HealthAt(i*120,false)==normal[i]&&spawner.HealthAt(i*120,true)==gob[i],"health boundary "+i);
            if(i>0)Require(spawner.HealthAt(i*120-.01f,false)==normal[i-1],"health before boundary "+i);
        }
        var enemy=Spawn(Vector2.zero);yield return null;
        StartCapture("Z");Cast(z);
        for(int i=0;i<100;i++)yield return null;
        capture=null;Require(enemy.CurrentHealth==60,"Z 40 arrows x 6 on one target");
        Clear();yield return null;Set(z,"nextCastTime",0f);Cast(z);Require(z.CooldownRemaining==0,"empty Z does not spend cooldown");
        enemy=Spawn(Vector2.zero);yield return null;
        StartCapture("X");Cast(x);
        for(int i=0;i<105;i++)yield return null;
        capture=null;Require(enemy.CurrentHealth==276,"X exactly 12 ticks x 2");
        Clear();yield return null;
        enemy=Spawn(new Vector2(0,.2f));var second=Spawn(new Vector2(2,.2f));yield return null;
        StartCapture("C");Cast(c);
        for(int i=0;i<90;i++)yield return null;
        capture=null;Require(enemy.CurrentHealth==260&&second.CurrentHealth==260,"C pierces and hits once per enemy");
        Require(Mathf.Abs(enemy.transform.position.y-.2f)>.5f,"C lateral push");
        Clear();yield return null;Position(new Vector2(-2,0));
        enemy=Spawn(new Vector2(0,.2f));yield return null;
        StartCapture("Shift");Call(movement,"TryStartDash");
        for(int i=0;i<40;i++)yield return null;
        Require(enemy.CurrentHealth==294,"Shift hits once");
        Require(movement.DashCharges==2,"Shift consumes one charge");
        Require(Mathf.Abs(player.transform.position.x-1.6f)<.15f,"Shift travels 3.6 units");
        for(int i=0;i<35;i++)yield return null;
        capture=null;Require(movement.DashCharges==3,"dash charge recovers in two seconds");
        float time=GameSession.Instance.SurvivalTime;Time.timeScale=0;
        for(int i=0;i<5;i++)yield return null;
        Require(GameSession.Instance.SurvivalTime==time,"pause freezes survival timer");Time.timeScale=1;
        // Same seeded stationary wave for first/second skill efficiency; not human play.
        float[] clearTimes = new float[2];
        var firstZ=player.GetComponent<ArcherVolleySkill>();var firstX=player.GetComponent<ArrowRainSkill>();var firstC=player.GetComponent<CrystalArrowSkill>();
        for(int tier=0;tier<2;tier++)
        {
            Clear();yield return null;Position(new Vector2(-4,0));
            system.enabled=tier==1;
            firstZ.enabled=firstX.enabled=firstC.enabled=tier==0;
            z.enabled=x.enabled=c.enabled=tier==1;
            ArcherSkillBase[] skills=tier==0?new ArcherSkillBase[]{firstX,firstZ,firstC}:new ArcherSkillBase[]{x,z,c};
            foreach(var skill in skills)Set(skill,"nextCastTime",0f);
            UnityEngine.Random.InitState(42);
            var wave=new EnemyHealth[24];
            for(int i=0;i<wave.Length;i++)
            {float a=i*Mathf.PI*2/12f;float radius=i<12?1.6f:3f;wave[i]=Spawn(new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius),30);}
            yield return null;
            float begin=Time.time;
            while(Time.time-begin<60)
            {
                bool alive=false;foreach(var e in wave)if(e!=null&&!e.IsDead)alive=true;
                if(!alive)break;
                foreach(var skill in skills)if(skill.CooldownRemaining<=0f)Cast(skill);
                yield return null;
            }
            clearTimes[tier]=Time.time-begin;
            Debug.Log($"BALANCE_WAVE tier={tier+1} health=30 count=24 seconds={clearTimes[tier]:F3}");
        }
        Debug.Log($"BALANCE_WAVE_REDUCTION_PERCENT={(1f-clearTimes[1]/clearTimes[0])*100f:F2}");
        // Explicitly exercise obstacle-safe push without changing the saved scene.
        Clear();yield return null;enemy=Spawn(new Vector2(0,0));yield return null;
        var wall=new GameObject("Check Wall");wall.layer=LayerMask.NameToLayer("Obstacle");
        if(wall.layer<0)wall.layer=3;
        wall.transform.position=new Vector3(0,.8f,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(2,.2f);
        Physics2D.SyncTransforms();enemy.GetComponent<EnemyMovement>().Push(Vector2.up,.2f);
        for(int i=0;i<10;i++)yield return null;
        Require(enemy.transform.position.y<.7f,"push stops before obstacle");Object.Destroy(wall);
        Clear();yield return null;Position(new Vector2(-4,0));enemy=Spawn(Vector2.zero);
        yield return null;Set(z,"nextCastTime",0f);Cast(z);yield return null;
        Set(player.GetComponent<PlayerHealth>(),"isDead",true);
        for(int i=0;i<4;i++)yield return null;
        Require(Object.FindObjectsByType<SecondArcherFx>(FindObjectsSortMode.None).Length==0,"death clears second effects");
        Debug.Log("SECOND_ARCHER_CHECKS_COMPLETE");
    }

    static IEnumerator VerifyZPiercing(StormBarrageSkill z)
    {
        Clear();yield return null;Position(new Vector2(-4,0));
        var a=Spawn(Vector2.zero);var b=Spawn(new Vector2(2,0));var behind=Spawn(new Vector2(4,0));
        foreach(var e in new[]{a,b,behind})e.GetComponent<EnemyMovement>().enabled=false;
        var wall=new GameObject("Z piercing wall");wall.layer=LayerMask.NameToLayer("Obstacle");
        wall.transform.position=new Vector2(3,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,4);
        Physics2D.SyncTransforms();yield return null;
        Require((int)Field(z,"shotCount").GetValue(z)==40 && (float)Field(z,"duration").GetValue(z)==2f,"Z forty shots over two seconds");
        Cast(z);z.StopAllCoroutines(); // Isolate the first real projectile for per-arrow collision checks.
        var arrow=Object.FindAnyObjectByType<SkillProjectile>();
        Require(arrow!=null && arrow.transform.localScale==Vector3.one*1.5f,"Z graphic scale 1.5");
        Require(Mathf.Abs((float)Field(arrow,"radius").GetValue(arrow)-.11f)<.00001f,"Z hit width unchanged");
        Vector3 before=arrow.transform.position;Time.timeScale=0;
        for(int i=0;i<3;i++)yield return null;
        Require(arrow.transform.position==before,"Z projectile pauses");Time.timeScale=1;
        for(int i=0;i<30;i++)yield return null;
        Require(a.CurrentHealth==294 && b.CurrentHealth==294,"Z pierces two enemies once each");
        Require(behind.CurrentHealth==300,"Z stops at obstacle");
        Object.Destroy(wall);z.enabled=false;yield return null;z.enabled=true;
        Clear();yield return null;Set(z,"nextCastTime",0f);
    }

    static IEnumerator VerifyXParity(LeafVortexSkill vortex)
    {
        var rain = player.GetComponent<ArrowRainSkill>();
        Require((float)Field(vortex,"radius").GetValue(vortex)==5f,"X saved radius five");
        Set(vortex,"radius",2.5f); // Compare equal geometry, then restore the upgraded radius.
        Require((float)Field(rain,"aimRange").GetValue(rain)==7f &&
            (float)Field(vortex,"aimRange").GetValue(vortex)==7f,"X saved aim ranges both seven");
        // Exercise the actual Cast methods on identical stationary layouts, including the old 7/8 counterexample.
        Vector2[][] layouts = {
            new[]{new Vector2(-1,0),new Vector2(-1,0),new Vector2(5.8f,0),new Vector2(5.8f,0),new Vector2(5.8f,0),new Vector2(5.8f,0)},
            new[]{new Vector2(-5,0),new Vector2(5,0)},
            new[]{new Vector2(9.8f,0),new Vector2(10,0)},
            new Vector2[0]
        };
        Position(Vector2.zero);
        for(int i=0;i<layouts.Length;i++)
        {
            Clear();yield return null;
            foreach(var at in layouts[i])Spawn(at).GetComponent<EnemyMovement>().enabled=false;
            yield return null;
            rain.enabled=true;Cast(rain);
            var rainFx=(GameObject)Field(rain,"rainFx").GetValue(rain);
            Vector2 expected=rainFx.transform.position;
            Cast(vortex);
            var vortexFx=(GameObject)Field(vortex,"effect").GetValue(vortex);
            Require(Vector2.Distance(expected,vortexFx.transform.position)<.00001f,"X identical cast center layout "+i);
            if(i==0)Require(Vector2.Distance(expected,new Vector2(-1,0))<=2.85f &&
                Vector2.Distance(expected,new Vector2(5.8f,0))>2.85f,"X favors nearby pair over distant four");
            rain.enabled=false;vortex.enabled=false;yield return null;vortex.enabled=true;
        }
        Clear();yield return null;
        var targetEnemy=Spawn(new Vector2(2,0));targetEnemy.GetComponent<EnemyMovement>().enabled=false;
        var outside=Spawn(new Vector2(2.86f,0));outside.GetComponent<EnemyMovement>().enabled=false;
        var wall=new GameObject("X parity wall");wall.layer=LayerMask.NameToLayer("Obstacle");
        wall.transform.position=Vector2.right;wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,4);
        Physics2D.SyncTransforms();yield return null;
        Require(Physics2D.Linecast(Vector2.zero,targetEnemy.transform.position,SkillUtil.ObstacleMask).collider!=null,"X test wall blocks line of sight");
        rain.enabled=true;
        var firstTick=(IEnumerator)Call(rain,"Rain",Vector2.zero);firstTick.MoveNext();
        int firstDamage=300-targetEnemy.CurrentHealth;
        rain.enabled=false;targetEnemy.InitializeSpawnHealth(300);
        var secondTick=(IEnumerator)Call(vortex,"Vortex",Vector2.zero);secondTick.MoveNext();
        Require(firstDamage==2 && targetEnemy.CurrentHealth==298,"X equal damage through wall");
        Require(outside.CurrentHealth==300,"X both exclude outside radius plus enemy size");
        (firstTick as IDisposable)?.Dispose();(secondTick as IDisposable)?.Dispose();
        Object.Destroy(wall);Clear();yield return null;
        Set(vortex,"radius",5f);
        var expanded=Spawn(new Vector2(5.34f,0));expanded.GetComponent<EnemyMovement>().enabled=false;
        var beyond=Spawn(new Vector2(5.36f,0));beyond.GetComponent<EnemyMovement>().enabled=false;
        yield return null;
        var expandedTick=(IEnumerator)Call(vortex,"Vortex",Vector2.zero);expandedTick.MoveNext();
        Require(expanded.CurrentHealth==298 && beyond.CurrentHealth==300,"X expanded damage boundary 5.35");
        Require((Vector2)Field(expanded.GetComponent<EnemyMovement>(),"pullVelocity").GetValue(expanded.GetComponent<EnemyMovement>())!=Vector2.zero,"X expanded radius pulls enemies");
        (expandedTick as IDisposable)?.Dispose();
        Clear();yield return null;Cast(vortex);
        var expandedFx=(GameObject)Field(vortex,"effect").GetValue(vortex);
        Require(expandedFx.transform.localScale==Vector3.one*2f,"X graphic scale doubled");
        vortex.enabled=false;yield return null;vortex.enabled=true;
        Set(vortex,"nextCastTime",0f);Position(new Vector2(-4,0));
    }

    static IEnumerator Progression()
    {
        for(int i=0;i<10;i++)yield return null;
        Time.captureDeltaTime=1f/30;Time.timeScale=1;
        player=PlayerProgression.Instance;
        Require(player.Level==1&&!player.GetComponent<SecondJobSystem>().HasAdvanced,"fresh game resets level and second job");
        Require(GameSession.Instance.SurvivalTime<5,"fresh game resets survival time");
        var health=player.GetComponent<PlayerHealth>();
        Set(health,"maxHealth",100000);Set(health,"currentHealth",100000);
        var movement=player.GetComponent<PlayerMovement>();
        var input=Field(movement,"moveInput");var facing=Field(movement,"lastDirection");
        var firstSkills=new ArcherSkillBase[]{player.GetComponent<ArcherVolleySkill>(),player.GetComponent<ArrowRainSkill>(),player.GetComponent<CrystalArrowSkill>()};
        int milestone=0;
        while(player.Level<30&&GameSession.Instance.SurvivalTime<600)
        {
            if(player.CanChooseJob)
            {
                if(!player.IsChoosingJob)player.OpenJobSelection();
                player.ConfirmJob(PlayerJob.Archer);
            }
            Vector2 at=player.transform.position;
            Vector2 destination=at+new Vector2(Mathf.Cos(Time.time*.3f),Mathf.Sin(Time.time*.3f))*3;
            float nearest=float.PositiveInfinity;
            foreach(var orb in Object.FindObjectsByType<ExperiencePickupView>())
            {
                float distance=Vector2.Distance(at,orb.transform.position);
                if(distance<nearest){nearest=distance;destination=orb.transform.position;}
            }
            Vector2 wanted=(destination-at).normalized,chosen=Vector2.zero;
            // Uses the real movement/obstacle collision path; only input is synthetic.
            foreach(float angle in new[]{0f,45f,-45f,90f,-90f,135f,-135f,180f})
            {
                Vector2 dir=SkillUtil.Rotate(wanted,angle);
                if(!Physics2D.CircleCast(at,.3f,dir,1f,SkillUtil.ObstacleMask)){chosen=dir;break;}
            }
            input.SetValue(movement,chosen);if(chosen!=Vector2.zero)facing.SetValue(movement,chosen);
            if(player.CurrentJob==PlayerJob.Archer)
                foreach(var skill in firstSkills)if(skill.isActiveAndEnabled&&skill.CooldownRemaining<=0)Cast(skill);
            int minute=Mathf.FloorToInt(GameSession.Instance.SurvivalTime/60);
            if(minute>milestone){milestone=minute;Debug.Log($"PROGRESSION_SAMPLE seconds={GameSession.Instance.SurvivalTime:F1} level={player.Level} kills={GameSession.Instance.Kills}");}
            yield return null;
        }
        Debug.Log($"PROGRESSION_RESULT level={player.Level} seconds={GameSession.Instance.SurvivalTime:F2} kills={GameSession.Instance.Kills} immortal_autopilot=true");
    }
}
#endif
