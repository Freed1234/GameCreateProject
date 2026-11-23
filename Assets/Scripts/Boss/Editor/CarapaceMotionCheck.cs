#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Isolated motion showcase and assertions. Never saves its controlled test scene.
[InitializeOnLoad]
public static class CarapaceMotionCheck
{
    const string Key="CarapaceMotionCheck",Output="CarapaceMotionCapture";
    static readonly Stack<IEnumerator> routines=new Stack<IEnumerator>();
    static int frame=-1,frames;
    static double started;
    static string error,label;
    static BossController boss;
    static CarapaceCombat combat;
    static CarapaceLocomotion motion;
    static PlayerHealth player;
    static SpriteRenderer body,shadow;
    static Text title;
    static MediaEncoder encoder;
    static RenderTexture target;
    static Texture2D pixels;
    static bool recording,attackDone;
    static CarapaceMotionCheck()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&Application.isPlaying&&(t==LogType.Error||t==LogType.Exception))error=m;};
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false)){Finish();SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));}};
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Exit",1);
        EditorSettings.enterPlayModeOptionsEnabled=false;EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);Debug.Log("MOTION_CHECK_PASS "+message);}
    static void Set(object o,string name,object value)=>CarapaceGameplayRecording.Set(o,name,value);
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying||EditorApplication.isCompiling)return;
        Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
        if(frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            if(started==0){started=EditorApplication.timeSinceStartup;routines.Push(Verify());}
            if(error!=null)throw new Exception(error);
            if(EditorApplication.timeSinceStartup-started>240)throw new Exception("Motion timeout");
            if(recording)Capture();
            while(routines.Count>0)
            {
                if(!routines.Peek().MoveNext()){routines.Pop();continue;}
                if(routines.Peek().Current is IEnumerator child){routines.Push(child);continue;}
                return;
            }
            Finish();Debug.Log("CARAPACE_MOTION_PASS frames="+frames);SessionState.SetInt(Key+"Exit",0);EditorApplication.ExitPlaymode();
        }
        catch(Exception e){Debug.LogException(e);Finish();EditorApplication.ExitPlaymode();}
    }
    static IEnumerator Verify()
    {
        Time.captureDeltaTime=1f/30;Time.timeScale=1;for(int i=0;i<10;i++)yield return null;
        var director=Object.FindAnyObjectByType<BossDirector>();player=BossUtil.Player();
        Check(!(bool)CarapaceGameplayRecording.Field(director,"offerAugmentOnKill").GetValue(director),"boss rewards disabled in saved scene");
        Check(AugmentSystem.Instance!=null&&!AugmentSystem.Instance.enabled,"augment component disabled");
        foreach(var script in player.GetComponentsInChildren<MonoBehaviour>())if(script!=player)script.enabled=false;
        player.GetComponent<Rigidbody2D>().simulated=false;
        Set(player,"maxHealth",1000);Set(player,"currentHealth",1000);
        foreach(var art in player.GetComponentsInChildren<SpriteRenderer>())art.enabled=false;
        Object.FindAnyObjectByType<EnemySpawner>().enabled=false;
        foreach(var enemy in Object.FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None))Object.Destroy(enemy.gameObject);
        CarapaceNaturalPlayCheck.Call(director,"Test1");
        for(int i=0;i<180&&boss==null;i++){yield return null;boss=Object.FindAnyObjectByType<BossController>();}
        Check(boss!=null,"production boss spawned");Set(boss,"busy",true);
        combat=boss.GetComponent<CarapaceCombat>();motion=boss.GetComponent<CarapaceLocomotion>();body=boss.GetComponent<SpriteRenderer>();
        Check(motion!=null&&boss.GetComponent<CharacterGroundShadow>()!=null,"motion and shadow connected at spawn");
        Object.FindAnyObjectByType<MapChunkManager>().enabled=false;
        foreach(var obstacle in new List<WorldDepthSort>(WorldDepthSort.Obstacles))if(obstacle!=null)obstacle.gameObject.SetActive(false);
        // Use direct steering in this isolated clearing; normal gameplay retains its navigation.
        typeof(EnemyFlowField).GetField("<Instance>k__BackingField",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)?.SetValue(null,null);
        boss.transform.position=Vector2.zero;player.transform.position=Vector2.right*10;Physics2D.SyncTransforms();
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.gameObject.SetActive(false);
        var camera=GameCamera.Get();foreach(var script in camera.GetComponents<MonoBehaviour>())script.enabled=false;
        camera.transform.position=new Vector3(0,1.5f,-10);camera.orthographicSize=5;
        var ui=new GameObject("Motion review captions").AddComponent<Canvas>();ui.renderMode=RenderMode.ScreenSpaceOverlay;
        var text=new GameObject("Caption").AddComponent<Text>();text.transform.SetParent(ui.transform,false);title=text;
        title.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");title.fontSize=28;title.alignment=TextAnchor.UpperCenter;title.color=Color.white;
        var rect=title.rectTransform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,70);rect.anchoredPosition=new Vector2(0,-18);
        Directory.CreateDirectory(Output);target=new RenderTexture(1280,720,24);target.Create();pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
        encoder=new MediaEncoder(Output+"/CarapaceMotion.mp4",new VideoTrackAttributes{width=1280,height=720,frameRate=new MediaRational(30),bitRateMode=VideoBitrateMode.High,includeAlpha=false});recording=true;
        yield return Hold("IDLE / GROUND SHADOW",60);
        shadow=Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)[0];
        foreach(var art in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))if(art.name==boss.name+" GroundShadow (Runtime)")shadow=art;
        Check(shadow!=body&&shadow.sortingLayerName=="WorldGround"&&Mathf.Abs(shadow.color.a-.28f)<.001f,"ground shadow sorting and opacity");
        Check(Vector2.Distance(shadow.transform.position,boss.transform.position)<.001f,"shadow anchored at feet");
        Check(Mathf.Abs(shadow.bounds.size.x-2.2f)<.01f&&Mathf.Abs(shadow.bounds.size.y-.65f)<.01f,"shadow dimensions");
        var idleSprites=new HashSet<Sprite>();for(int i=0;i<40;i++){idleSprites.Add(body.sprite);yield return null;}Check(idleSprites.Count==4,"four breathing frames");
        boss.transform.position=Vector2.left*2.4f;
        yield return Walk("WALK RIGHT",Vector2.right*20,90);
        Check(!body.flipX,"right facing");yield return Walk("WALK LEFT",Vector2.left*20,90);Check(body.flipX,"left facing");
        yield return Walk("WALK UP",Vector2.up*20,30);yield return Walk("WALK DOWN",Vector2.down*20,30);
        yield return Hold("STOP / IDLE",45);Check(!motion.IsWalking,"stopped uses idle");
        var frozen=body.sprite;var position=boss.transform.position;Time.timeScale=0;yield return Hold("PAUSED",20);
        Check(body.sprite==frozen&&boss.transform.position==position,"pause freezes locomotion");Time.timeScale=1;
        boss.transform.position=Vector2.zero;
        var wall=new GameObject("Motion test wall");wall.layer=LayerMask.NameToLayer("Obstacle");wall.transform.position=Vector2.right*.67f;wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,3);Physics2D.SyncTransforms();
        yield return Walk("BLOCKED / FEET STOP",Vector2.right*10,30,false);Check(!motion.IsWalking,"blocked movement does not walk in place");Object.Destroy(wall);yield return null;
        foreach(var pattern in new[]{BossController.Pattern.Charge,BossController.Pattern.Slam,BossController.Pattern.Radial})
        {
            boss.transform.position=new Vector2(-2,0);player.transform.position=new Vector2(3,0);Physics2D.SyncTransforms();
            label=pattern.ToString().ToUpper()+" / ATTACK PRIORITY";title.text=label;attackDone=false;boss.StartCoroutine(Attack(pattern));
            while(!attackDone){if(combat.Stage!="Idle")CheckAttackSprite();yield return null;}
            yield return Hold("RECOVER TO IDLE",35);
        }
        Finish();recording=false;
        var oldShadow=shadow;int kills=GameSession.Instance.Kills;boss.GetComponent<EnemyHealth>().TakeDamage(boss.CurrentHp);
        yield return null;yield return null;
        Check(boss==null&&oldShadow==null,"death cleans boss and ground shadow");Check(GameSession.Instance.Kills==kills+1,"kill counted once");
        float time=GameSession.Instance.SurvivalTime;for(int i=0;i<301;i++)yield return null;
        Check(GameSession.Instance.SurvivalTime-time>=10&&Time.timeScale==1&&!AugmentSystem.Instance.IsOpen,"ten seconds after defeat without reward pause");
    }
    static void CheckAttackSprite()
    {
        foreach(var s in Resources.LoadAll<Sprite>("CarapaceDemo/Walk"))if(body.sprite==s)throw new Exception("Walk overrides attack");
        foreach(var s in Resources.LoadAll<Sprite>("CarapaceDemo/Idle"))if(body.sprite==s)throw new Exception("Idle overrides attack");
    }
    static IEnumerator Attack(BossController.Pattern pattern){yield return combat.Attack(pattern,player,2);attackDone=true;}
    static IEnumerator Walk(string name,Vector2 goal,int count,bool requireFrames=true)
    {
        label=name;title.text=name;player.transform.position=goal;var sprites=new HashSet<Sprite>();
        for(int i=0;i<count;i++){combat.Chase(player,1.6f);yield return null;if(motion.IsWalking)sprites.Add(body.sprite);}
        if(requireFrames)Check(sprites.Count>=Math.Min(8,count/4),name+" animates with displacement");
    }
    static IEnumerator Hold(string name,int count){label=name;title.text=name;for(int i=0;i<count;i++)yield return null;}
    static readonly HashSet<string> snapshots=new HashSet<string>();
    static void Capture()
    {
        var camera=GameCamera.Get();var old=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
        var canvas=title.canvas;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.sortingOrder=32000;
        camera.aspect=16f/9;camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();if(!encoder.AddFrame(pixels))throw new Exception("Video encoding failed");
        if(snapshots.Add(label))File.WriteAllBytes(Output+"/"+label.Replace(" / ","-")+".png",pixels.EncodeToPNG());
        frames++;camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
    }
    static void Finish(){encoder?.Dispose();encoder=null;recording=false;}
}
#endif
