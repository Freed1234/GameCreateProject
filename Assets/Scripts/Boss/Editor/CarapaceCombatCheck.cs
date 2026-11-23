#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Run only in a disposable project copy: exits Unity after Play-mode checks.
[InitializeOnLoad]
public static class CarapaceCombatCheck
{
    const string Key="CarapaceCombatCheck";
    static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
    static int lastFrame=-1;
    static double started;
    static BossController boss;
    static CarapaceCombat combat;
    static PlayerHealth player;
    static bool attackDone;
    static CarapaceCombatCheck()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false))
        {SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));}};
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Exit",1);
        EditorSettings.enterPlayModeOptionsEnabled=false;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        EditorApplication.isPaused=false;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(started==0){started=EditorApplication.timeSinceStartup;stack.Push(Verify());}
            if(EditorApplication.timeSinceStartup-started>360)throw new Exception("Combat validation timeout");
            while(stack.Count>0)
            {
                if(!stack.Peek().MoveNext()){stack.Pop();continue;}
                if(stack.Peek().Current is IEnumerator child){stack.Push(child);continue;}
                return;
            }
            Debug.Log("CARAPACE_COMBAT_PASS");SessionState.SetInt(Key+"Exit",0);EditorApplication.ExitPlaymode();
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.ExitPlaymode();}
    }
    static FieldInfo Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
    static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static void Check(bool ok,string label){if(!ok)throw new Exception("CHECK FAILED: "+label);Debug.Log("CHECK_PASS: "+label);}
    static IEnumerator Verify()
    {
        Time.captureDeltaTime=1f/30;Time.timeScale=1;
        for(int i=0;i<20;i++)yield return null;
        var director=Object.FindAnyObjectByType<BossDirector>();
        Check(director!=null,"production scene has boss director");
        var tiers=(BossDirector.BossTier[])Field(director,"tiers").GetValue(director);
        Check(tiers.Length==3&&tiers[0].goldenCarapace&&tiers[0].spawnTime==300&&tiers[0].maxHealth==250&&tiers[0].patterns.Length==3,"five-minute carapace configuration");
        Check(!tiers[1].goldenCarapace&&tiers[1].spawnTime==600&&!tiers[2].goldenCarapace&&tiers[2].spawnTime==900,"later bosses preserved");
        Set(director,"warningSeconds",.1f);Set(director,"offerAugmentOnKill",false);
        player=BossUtil.Player();
        foreach(var behaviour in player.GetComponentsInChildren<MonoBehaviour>())if(behaviour!=player)behaviour.enabled=false;
        player.GetComponent<Rigidbody2D>().simulated=false;
        var spawner=Object.FindAnyObjectByType<EnemySpawner>();spawner.enabled=false;
        foreach(var enemy in Object.FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None))Object.Destroy(enemy.gameObject);
        Set(GameSession.Instance,"<SurvivalTime>k__BackingField",300f);
        for(int i=0;i<180 && boss==null;i++){yield return null;boss=Object.FindAnyObjectByType<BossController>();}
        Check(boss!=null&&boss.GetComponent<CarapaceCombat>()!=null,"scheduled spawn creates production carapace");
        Check(!SkillUtil.HitsObstacle(boss.transform.position,.8f,SkillUtil.ObstacleMask),"spawn outside obstacles");
        combat=boss.GetComponent<CarapaceCombat>();Set(boss,"busy",true);
        var map=Object.FindAnyObjectByType<MapChunkManager>();map.enabled=false;
        foreach(var obstacle in new List<WorldDepthSort>(WorldDepthSort.Obstacles))if(obstacle!=null)obstacle.gameObject.SetActive(false);
        Physics2D.SyncTransforms();
        Set(player,"maxHealth",1000);Set(player,"currentHealth",1000);Set(player,"invincibleTime",0f);Set(player,"nextDamageTime",0f);
        GeometryChecks();
        yield return AttackCheck(BossController.Pattern.Charge,new Vector2(6.5f,.5f),2,"charge rectangular far corner");
        yield return AttackCheck(BossController.Pattern.Charge,new Vector2(3,.56f),0,"charge outside white width");
        yield return AttackCheck(BossController.Pattern.Charge,SkillUtil.Rotate(new Vector2(6.5f,.5f),45),2,"diagonal charge corner",SkillUtil.Rotate(Vector2.right*4,45));
        yield return AttackCheck(BossController.Pattern.Slam,new Vector2(3.39f,0),2,"slam inside arc");
        yield return AttackCheck(BossController.Pattern.Slam,new Vector2(3.41f,0),0,"slam outside arc");
        yield return AttackCheck(BossController.Pattern.Slam,new Vector2(-2,0),2,"left-facing slam",Vector2.left*4);
        yield return AttackCheck(BossController.Pattern.Radial,new Vector2(8.3f,.9f),2,"shard rectangular far corner");
        yield return AttackCheck(BossController.Pattern.Radial,new Vector2(8.36f,.6f),0,"shard outside full range");
        yield return AttackCheck(BossController.Pattern.Radial,new Vector2(0,.6f),2,"eight-shard overlap damages once without invulnerability");
        Set(player.GetComponent<PlayerMovement>(),"<IsDashing>k__BackingField",true);
        yield return AttackCheck(BossController.Pattern.Slam,new Vector2(2,0),0,"dash invulnerability respected");
        Set(player.GetComponent<PlayerMovement>(),"<IsDashing>k__BackingField",false);
        Set(player,"nextDamageTime",Time.time+20);
        yield return AttackCheck(BossController.Pattern.Slam,new Vector2(2,0),0,"hit invulnerability respected");
        Set(player,"nextDamageTime",0f);
        var wall=new GameObject("Combat check wall");wall.layer=LayerMask.NameToLayer("Obstacle");wall.transform.position=new Vector2(3,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,4);Physics2D.SyncTransforms();
        yield return AttackCheck(BossController.Pattern.Charge,new Vector2(4,0),0,"wall prevents charge and damage behind it");
        Check(boss.transform.position.x<2.4f&&Mathf.Abs(boss.transform.position.x-combat.PlannedChargeLength)<.001f,"charge travel matches shortened indicator");
        Object.Destroy(wall);yield return null;
        boss.transform.position=Vector2.zero;player.transform.position=Vector2.right*4;
        attackDone=false;boss.StartCoroutine(Perform(BossController.Pattern.Slam));yield return null;
        Time.timeScale=0;var pose=boss.GetComponent<SpriteRenderer>().sprite;int hp=player.CurrentHealth;
        for(int i=0;i<5;i++)yield return null;
        Check(combat.TelegraphVisible&&boss.GetComponent<SpriteRenderer>().sprite==pose&&player.CurrentHealth==hp,"pause freezes charge and damage");
        Time.timeScale=1;boss.enabled=false;yield return null;
        Check(!combat.TelegraphVisible&&combat.LiveShards==0,"disable cancels warning and projectiles");
        boss.enabled=true;Set(boss,"busy",true);
        var proxy=Object.FindAnyObjectByType<BossHitProxy>().GetComponent<EnemyHealth>();
        int before=boss.CurrentHp;proxy.TakeDamage(7);yield return null;yield return null;
        Check(boss.CurrentHp==before-7,"player attacks through proxy reduce boss HP once");
        boss.transform.position=Vector2.zero;player.transform.position=Vector2.right*5;
        before=boss.CurrentHp;
        var arrow=SkillProjectile.Spawn(null,Color.white,Vector2.left*2,Vector2.right,12,8,5,.1f,true,true,spawner.transform);
        for(int i=0;i<20;i++)yield return null;
        Check(boss.CurrentHp==before-5,"real piercing skill projectile damages boss only once");
        if(arrow!=null)Object.Destroy(arrow.gameObject);
        before=boss.CurrentHp;SkillUtil.DamageInRadius(spawner.transform,boss.transform.position,3,3);
        yield return null;yield return null;
        Check(boss.CurrentHp==before-3,"real area skill damages boss only once");
        boss.transform.position=Vector2.zero;player.transform.position=Vector2.right*4;
        attackDone=false;boss.StartCoroutine(Perform(BossController.Pattern.Radial));
        while(combat.LiveShards==0)yield return null;
        var progression=player.GetComponent<PlayerProgression>();progression.enabled=true;
        int level=progression.Level,experience=progression.Experience;
        int deaths=0,kills=GameSession.Instance.Kills;boss.Died+=b=>deaths++;
        boss.GetComponent<EnemyHealth>().TakeDamage(boss.CurrentHp);
        yield return null;yield return null;
        Check(boss==null&&Object.FindAnyObjectByType<BossHitProxy>()==null&&Object.FindAnyObjectByType<CarapaceCombat>()==null,"death clears body, proxy, warnings and shards");
        Check(deaths==1&&GameSession.Instance.Kills==kills+1,"single death notification and kill reward");
        Check(progression.Level>level||progression.Experience>experience,"midboss experience reward granted");
        Check(!director.GetComponent<GameClear>().IsCleared,"midboss kill does not end game");
    }
    static void GeometryChecks()
    {
        Check(CarapaceGeometry.RectangleContains(new Vector2(6.55f,.55f),6,.55f)&&!CarapaceGeometry.RectangleContains(new Vector2(6.56f,0),6,.55f),"rectangle exact corner and outside boundary");
        Vector2 dir=SkillUtil.Rotate(Vector2.right,135),origin=new Vector2(8,-3);
        Check(CarapaceGeometry.RectangleContains(CarapaceGeometry.Local(origin+dir*3,origin,dir),6,.55f),"rotated translated geometry");
        Check(CarapaceGeometry.SweptSquare(new Vector2(3,0),new Vector2(3,0),Vector2.zero,Vector2.right*6,Vector2.right,.55f),"large-step charge cannot tunnel");
        Check(!CarapaceGeometry.SweptSquare(new Vector2(3,.56f),new Vector2(3,.56f),Vector2.zero,Vector2.right*6,Vector2.right,.55f),"sweep does not expand width");
        Check(CarapaceGeometry.SlamContains(SkillUtil.Rotate(Vector2.right*3,49.9f))&&!CarapaceGeometry.SlamContains(SkillUtil.Rotate(Vector2.right*3,50.1f)),"slam angle boundary");
    }
    static IEnumerator Perform(BossController.Pattern pattern){yield return combat.Attack(pattern,player,2);attackDone=true;}
    static IEnumerator AttackCheck(BossController.Pattern pattern,Vector2 at,int damage,string label,Vector2? aim=null)
    {
        boss.transform.position=Vector2.zero;player.transform.position=aim??Vector2.right*4;Set(boss,"busy",true);attackDone=false;
        int hp=player.CurrentHealth;boss.StartCoroutine(Perform(pattern));
        player.transform.position=at;
        for(int i=0;i<200&&!attackDone;i++)
        {
            if(i==12)Capture(pattern.ToString());
            yield return null;
        }
        Check(attackDone&&player.CurrentHealth==hp-damage,label);
        Check(!combat.TelegraphVisible&&combat.LiveShards==0,"attack recovery clears warning/projectiles: "+label);
    }
    static void Capture(string name)
    {
        string path="CarapaceCombatCapture/"+name+".png";if(File.Exists(path))return;Directory.CreateDirectory("CarapaceCombatCapture");
        var cam=GameCamera.Get();var old=cam.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1280,720,24);rt.Create();var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
        cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        cam.targetTexture=old;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(tex);
    }
}
#endif
