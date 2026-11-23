using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Visual-only showcase. No EnemyHealth, damage, rewards or production boss registration.
[DefaultExecutionOrder(1000)]
public sealed class CarapaceDemo : MonoBehaviour
{
    // Shared preview geometry for telegraphs and future damage checks (player damage is not wired here).
    public const float ChargeRadius=CarapaceGeometry.ChargeRadius, ChargeLength=CarapaceGeometry.ChargeLength, SlamRadius=CarapaceGeometry.SlamRadius, SlamHalfAngle=CarapaceGeometry.SlamHalfAngle;
    public const float ShardRadius=CarapaceGeometry.ShardRadius, ShardRange=CarapaceGeometry.ShardRange, ShardSpeed=CarapaceGeometry.ShardSpeed;
    public static readonly Vector2 ShardOriginOffset=CarapaceGeometry.ShardOriginOffset;
    public enum Beat { RestCharge, ChargeTell, Charge, ChargeRecover, RestSlam, SlamTell, Slam, SlamRecover, RestVolley, VolleyTell, Volley, VolleyRecover, End }
    static readonly float[] durations = { 1f, .9f, .6f, 1.2f, 1f, .7f, .15f, 1f, 1f, .8f, .15f, 1.35f, 1f };
    public Beat CurrentBeat { get; private set; }
    public int FrameIndex { get; private set; }
    public int CompletedCycles { get; private set; }
    public int VolleyCount { get; private set; }
    public int SlamCount { get; private set; }
    public bool Ready { get; private set; }
    public bool Blocked { get; private set; }
    public float ChargeDistance { get; private set; }
    public Vector2 BossPosition => actor != null ? (Vector2)actor.position : Vector2.zero;
    public int LiveShards => particles.FindAll(p => p.shard && p.renderer != null).Count;
    public bool TelegraphVisible => telegraph != null && telegraph.activeSelf;
    public float PlannedChargeLength { get; private set; } = ChargeLength;
    public string SkillName => (int)CurrentBeat <= 3 ? "D-C1  HORN CHARGE" : (int)CurrentBeat <= 7 ? "S-C2  DOUBLE FIST SLAM" : "V-C2  SHELL SHARDS";
    Transform actor;
    SpriteRenderer body;
    TextMesh caption;
    Camera demoCamera;
    bool volleyFired;
    Sprite[][] frames;
    float elapsed, dustTimer;
    GameObject telegraph;
    Mesh tellMesh;
    MeshRenderer tellRenderer;
    Material material;
    Sprite rock, shardSprite, dust, shadow;
    readonly List<Texture2D> textures = new List<Texture2D>();
    readonly List<Sprite> sprites = new List<Sprite>();
    readonly List<Particle> particles = new List<Particle>();
    readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    sealed class Particle
    {
        public SpriteRenderer renderer;
        public Vector2 origin, velocity;
        public Vector3 scale;
        public float age, life, hop;
        public bool shard;
    }

    IEnumerator Start()
    {
        Time.timeScale = 1;
        // MapChunkManager populates the original forest before we freeze the showcase camera.
        yield return null; yield return null;
        var map = FindAnyObjectByType<MapChunkManager>();
        if (map != null) map.enabled = false;
        foreach (var depth in new List<WorldDepthSort>(WorldDepthSort.Obstacles))
            if (depth != null && Mathf.Abs(depth.transform.position.x) < 11 && Mathf.Abs(depth.transform.position.y) < 9)
                depth.gameObject.SetActive(false);
        var camera = GameCamera.Get();
        demoCamera=camera;camera.aspect=16f/9f;camera.orthographicSize = 7; camera.transform.position = new Vector3(0, 0, -10);
        foreach(var canvas in FindObjectsByType<Canvas>())canvas.gameObject.SetActive(false);
        var title=new GameObject("Preview caption");title.transform.SetParent(transform);title.transform.position=new Vector3(-14,7.7f,0);
        caption=title.AddComponent<TextMesh>();caption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");caption.fontSize=48;caption.characterSize=.14f;caption.anchor=TextAnchor.UpperLeft;caption.color=new Color(1,.88f,.55f);
        var textRenderer=caption.GetComponent<MeshRenderer>();textRenderer.sharedMaterial=caption.font.material;textRenderer.sortingLayerName="WorldActors";textRenderer.sortingOrder=500;
        material = new Material(Resources.Load<Shader>("CarapaceDemo/Indicator"));
        rock=Resources.Load<Sprite>("CarapaceDemo/Effects/Rock");
        shardSprite=Resources.Load<Sprite>("CarapaceDemo/Effects/Shard");
        dust=Resources.Load<Sprite>("CarapaceDemo/Effects/Dust");shadow=MakeShadow();
        frames = new Sprite[3][];
        string[] names = { "Charge", "Slam", "Volley" };
        for (int i = 0; i < 3; i++)
        {
            frames[i] = Resources.LoadAll<Sprite>("CarapaceDemo/" + names[i]);
            System.Array.Sort(frames[i], (a,b) => string.CompareOrdinal(a.name,b.name));
            if (frames[i].Length != 8) throw new System.InvalidOperationException("Expected eight frames for " + names[i]);
        }
        actor = new GameObject("Golden Carapace - visual preview").transform; actor.SetParent(transform);
        var groundShadow = NewRenderer("Shadow", shadow, actor);
        groundShadow.color = new Color(0,0,0,.25f); groundShadow.sortingOrder = -1;
        groundShadow.transform.localScale = new Vector3(2.2f,.65f,1);
        body = NewRenderer("Animated body", frames[0][0], actor); body.sortingOrder = 20;
        telegraph = new GameObject("Attack telegraph");telegraph.transform.SetParent(transform);
        tellMesh = new Mesh();telegraph.AddComponent<MeshFilter>().sharedMesh = tellMesh;
        tellRenderer = telegraph.AddComponent<MeshRenderer>();tellRenderer.sharedMaterial = material;
        tellRenderer.sortingLayerName="WorldActors";tellRenderer.sortingOrder=-2;
        Ready = true; RestartDemo();
    }

    public void RestartDemo()
    {
        if (!Ready) return;
        ClearParticles(); elapsed=0; CompletedCycles=0;VolleyCount=0;SlamCount=0;
        Enter(Beat.RestCharge);
    }
    void Enter(Beat beat)
    {
        CurrentBeat=beat;elapsed=0;dustTimer=0;telegraph.SetActive(false);
        if(beat==Beat.RestCharge){actor.position=new Vector2(-5,0);Blocked=false;ChargeDistance=0;}
        if(beat==Beat.ChargeTell)PlannedChargeLength=StepBeforeWall(BossPosition,Vector2.right,ChargeLength);
        if(beat==Beat.SlamRecover){SlamCount++;SlamBurst();}
        if(beat==Beat.Volley)volleyFired=false;
        UpdatePose();
    }
    void Update()
    {
        if(!Ready || Time.timeScale<=0)return;
        if(Input.GetKeyDown(KeyCode.R)){RestartDemo();return;}
        float dt=Time.deltaTime;elapsed+=dt;
        if(CurrentBeat==Beat.Volley&&!volleyFired&&elapsed>=.075f)
        {volleyFired=true;VolleyCount++;for(int i=0;i<8;i++)
        {Vector2 direction=Direction(i*45);Emit(shardSprite,BossPosition+ShardOriginOffset,direction*ShardSpeed,ShardRange/ShardSpeed,Vector3.one*1.05f,0,true,i*45);}}
        if(CurrentBeat==Beat.Charge)
        {
            float step=Mathf.Min(10f*dt,PlannedChargeLength-ChargeDistance);
            float allowed=StepBeforeWall(BossPosition,Vector2.right,step);
            actor.position+=(Vector3)(Vector2.right*allowed);ChargeDistance+=allowed;
            if(allowed+ .0001f<step || (PlannedChargeLength<ChargeLength && ChargeDistance>=PlannedChargeLength-.0001f))
            {Blocked=true;elapsed=durations[(int)CurrentBeat];}
            dustTimer+=dt;if(dustTimer>=.06f){dustTimer=0;ChargeDust();}
        }
        UpdateParticles(dt);
        if(elapsed>=durations[(int)CurrentBeat])
        {
            int next=(int)CurrentBeat+1;
            if(next>=durations.Length){CompletedCycles++;ClearParticles();next=0;}
            Enter((Beat)next);
        }
        UpdatePose();
    }
    public float StepBeforeWall(Vector2 start,Vector2 direction,float length)
    {
        var filter=new ContactFilter2D();filter.SetLayerMask(SkillUtil.ObstacleMask);filter.useTriggers=false;
        Physics2D.CircleCast(start,ChargeRadius,direction,filter,hits,length);
        foreach(var hit in hits)length=Mathf.Min(length,Mathf.Max(0,hit.distance-.02f));
        return length;
    }
    void UpdatePose()
    {
        int b=(int)CurrentBeat;float p=Mathf.Clamp01(elapsed/durations[b]);int group=b<=3?0:b<=7?1:2;
        int frame=0;
        switch(CurrentBeat)
        {
            case Beat.ChargeTell:case Beat.SlamTell:case Beat.VolleyTell:frame=p<.45f?1:2;break;
            case Beat.Charge:frame=p<.15f?3:4+(Mathf.FloorToInt(elapsed*12)%2);break;
            case Beat.ChargeRecover:frame=p<.4f?6:7;break;
            case Beat.Slam:frame=3;break;
            case Beat.SlamRecover:frame=p<.18f?4:p<.42f?5:p<.78f?6:7;break;
            case Beat.Volley:frame=p<.5f?3:4;break;
            case Beat.VolleyRecover:frame=p<.25f?5:p<.65f?6:7;break;
        }
        FrameIndex=frame;body.sprite=frames[group][frame];
        if(CurrentBeat==Beat.RestCharge)demoCamera.orthographicSize=Mathf.Lerp(demoCamera.orthographicSize,7,Mathf.Clamp01(elapsed*3));
        if(CurrentBeat==Beat.RestVolley)demoCamera.orthographicSize=Mathf.Lerp(demoCamera.orthographicSize,9,Mathf.Clamp01(elapsed*3));
        caption.transform.position=demoCamera.ViewportToWorldPoint(new Vector3(.06f,.93f,10));
        caption.characterSize=.14f*demoCamera.orthographicSize/9f;
        caption.text="GOLDEN CARAPACE - UNITY PLAY\n"+SkillName+"  |  "+CurrentBeat+"  |  "+(frame+1).ToString("00")+"/08";
        bool tell=CurrentBeat==Beat.ChargeTell||CurrentBeat==Beat.SlamTell||CurrentBeat==Beat.VolleyTell||CurrentBeat==Beat.Slam||(CurrentBeat==Beat.Volley&&!volleyFired);
        telegraph.SetActive(tell);if(tell)DrawTelegraph(group,CurrentBeat==Beat.Slam||CurrentBeat==Beat.Volley?1:p);
    }
    void DrawTelegraph(int group,float fill)
    {
        CarapaceGeometry.Draw(tellMesh,material,group,fill,group==0?PlannedChargeLength:ShardRange,ShardOriginOffset.y);
        telegraph.transform.position=BossPosition;
    }
    public static bool CapsuleContains(Vector2 point,Vector2 origin,Vector2 direction,float length,float radius)
    {float along=Mathf.Clamp(Vector2.Dot(point-origin,direction),0,length);return (point-origin-direction*along).sqrMagnitude<=radius*radius;}
    public static bool SlamContains(Vector2 localPoint)
    {return localPoint.sqrMagnitude<=SlamRadius*SlamRadius&&(localPoint.sqrMagnitude<.000001f||Vector2.Angle(Vector2.right,localPoint)<=SlamHalfAngle);}
    static Vector2 Direction(float angle)=>new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
    void ChargeDust()
    {
        Emit(dust,BossPosition+new Vector2(-.7f,.1f),new Vector2(-.7f,.15f),.5f,Vector3.one*.7f,.1f);
        foreach(float side in new[]{-.65f,.65f})Emit(rock,BossPosition+new Vector2(-.3f,side),Vector2.zero,1.1f,Vector3.one*.45f,.15f);
    }
    void SlamBurst()
    {
        for(int ring=0;ring<3;ring++)for(int i=0;i<5;i++)
        {
            Vector2 offset=Direction(-45+i*22.5f)*(1+ring*.85f);
            Emit(rock,BossPosition+offset,offset.normalized*.12f,.9f,Vector3.one*1.1f,.45f+ring*.15f,false,-25+i*12);
            Emit(dust,BossPosition+offset,new Vector2(.15f,.1f),.95f,Vector3.one*.7f,.15f);
        }
    }
    void Emit(Sprite sprite,Vector2 origin,Vector2 velocity,float life,Vector3 scale,float hop,bool shard=false,float angle=0)
    {
        var sr=NewRenderer(shard?"Shell shard":"Earth effect",sprite,transform);sr.sortingOrder=shard?30:1;
        sr.transform.position=origin;sr.transform.localScale=scale;sr.transform.rotation=Quaternion.Euler(0,0,angle);
        particles.Add(new Particle{renderer=sr,origin=origin,velocity=velocity,life=life,scale=scale,hop=hop,shard=shard});
    }
    void UpdateParticles(float dt)
    {
        for(int i=particles.Count-1;i>=0;i--)
        {
            var p=particles[i];p.age+=dt;
            if(p.renderer==null||p.age>=p.life){if(p.renderer!=null)Destroy(p.renderer.gameObject);particles.RemoveAt(i);continue;}
            float u=p.age/p.life;
            p.renderer.transform.position=p.origin+p.velocity*p.age+Vector2.up*(Mathf.Sin(u*Mathf.PI)*p.hop);
            p.renderer.color=new Color(1,1,1,Mathf.Clamp01((p.life-p.age)/.2f));
        }
    }
    SpriteRenderer NewRenderer(string name,Sprite sprite,Transform parent)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingLayerName="WorldActors";return r;}
    Sprite MakeShadow()
    {
        const int size=32;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Point;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float nx=(x-15.5f)/15.5f,ny=(y-15.5f)/15.5f;
            if(nx*nx+ny*ny<1)pixels[y*size+x]=Color.white;
        }
        tex.SetPixels32(pixels);tex.Apply();textures.Add(tex);
        var sprite=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);sprites.Add(sprite);return sprite;
    }
    void ClearParticles(){foreach(var p in particles)if(p.renderer!=null)Destroy(p.renderer.gameObject);particles.Clear();}
    void OnDisable(){ClearParticles();if(telegraph!=null)telegraph.SetActive(false);}
    void OnDestroy(){if(tellMesh!=null)Destroy(tellMesh);if(material!=null)Destroy(material);foreach(var s in sprites)Destroy(s);foreach(var t in textures)Destroy(t);}
    void OnGUI()
    {
        if(!Ready)return;
        GUI.Box(new Rect(16,16,340,76),"GOLDEN CARAPACE | UNITY PREVIEW\n"+SkillName+"\n"+CurrentBeat+"   FRAME "+(FrameIndex+1).ToString("00")+"   [R] RESTART");
    }
}
