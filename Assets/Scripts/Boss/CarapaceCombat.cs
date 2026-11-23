using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Production attacks. BossController owns health, rewards, target selection and UI.
public sealed class CarapaceCombat : MonoBehaviour
{
    public bool TelegraphVisible => warning!=null && warning.activeSelf;
    public int LiveShards => shards.Count;
    public float PlannedChargeLength { get; private set; }
    public string Stage { get; private set; }="Idle";
    BossController owner;
    PlayerHealth player;
    SpriteRenderer body;
    Sprite[][] frames;
    Sprite shardSprite,rockSprite,dustSprite;
    GameObject warning;
    Mesh mesh;
    Material material;
    Vector2 previousPlayer;
    int damage;
    bool volleyHit;
    readonly List<RaycastHit2D> walls=new List<RaycastHit2D>();
    readonly List<Shard> shards=new List<Shard>();
    readonly List<Effect> effects=new List<Effect>();
    sealed class Shard { public SpriteRenderer art; public Vector2 origin,direction; public float travelled; }
    sealed class Effect { public SpriteRenderer art; public Vector2 origin; public float age,life; }
    bool Alive => isActiveAndEnabled && owner!=null && !owner.IsDead && player!=null && !player.IsDead;

    public void Initialize(BossController boss,SpriteRenderer renderer)
    {
        owner=boss;body=renderer;body.sortingLayerName="WorldActors";
        frames=new Sprite[3][];string[] names={"Charge","Slam","Volley"};
        for(int i=0;i<3;i++)
        {
            frames[i]=Resources.LoadAll<Sprite>("CarapaceDemo/"+names[i]);
            System.Array.Sort(frames[i],(a,b)=>string.CompareOrdinal(a.name,b.name));
            if(frames[i].Length!=8)throw new System.InvalidOperationException("Missing carapace frames: "+names[i]);
        }
        body.sprite=frames[0][0];body.color=Color.white;
        shardSprite=Resources.Load<Sprite>("CarapaceDemo/Effects/Shard");
        rockSprite=Resources.Load<Sprite>("CarapaceDemo/Effects/Rock");
        dustSprite=Resources.Load<Sprite>("CarapaceDemo/Effects/Dust");
        warning=new GameObject("Carapace warning");warning.transform.SetParent(transform,false);
        mesh=new Mesh();material=new Material(Resources.Load<Shader>("CarapaceDemo/Indicator"));
        warning.AddComponent<MeshFilter>().sharedMesh=mesh;
        var mr=warning.AddComponent<MeshRenderer>();mr.sharedMaterial=material;mr.sortingLayerName="WorldActors";mr.sortingOrder=-31000;
        warning.SetActive(false);
    }

    public float SafeDistance(Vector2 origin,Vector2 direction,float distance)
    {
        if(distance<=0)return 0;
        var filter=new ContactFilter2D();filter.SetLayerMask(SkillUtil.ObstacleMask);filter.useTriggers=false;
        Physics2D.BoxCast(origin,Vector2.one*(CarapaceGeometry.ChargeRadius*2),Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,direction,filter,walls,distance);
        foreach(var wall in walls)distance=Mathf.Min(distance,Mathf.Max(0,wall.distance-.02f));
        return distance;
    }

    public void Chase(PlayerHealth target,float speed)
    {
        player=target;Vector2 offset=(Vector2)target.transform.position-(Vector2)transform.position;
        if(offset.magnitude<2.2f)return;
        var navigation=EnemyFlowField.Instance;
        Vector2 heading=offset.normalized;
        if(navigation!=null && navigation.Ready && navigation.TryGetWaypoint(transform.position,out Vector2 waypoint))
            heading=(waypoint-(Vector2)transform.position).normalized;
        float step=SafeDistance(transform.position,heading,speed*Time.deltaTime);
        transform.position+=(Vector3)(heading*step);body.flipX=offset.x<0;
    }

    public IEnumerator Attack(BossController.Pattern pattern,PlayerHealth target,int attackDamage)
    {
        player=target;damage=attackDamage;
        if(!Alive)yield break;
        int group=pattern==BossController.Pattern.Charge?0:pattern==BossController.Pattern.Slam?1:2;
        Vector2 start=transform.position,dir=((Vector2)player.transform.position-start).normalized;
        if(dir==Vector2.zero)dir=Vector2.right;
        body.flipX=dir.x<0;
        if(group==0)PlannedChargeLength=SafeDistance(start,dir,CarapaceGeometry.ChargeLength);
        float length=group==0?PlannedChargeLength:CarapaceGeometry.ShardRange;
        Vector2 origin=group==2?start+CarapaceGeometry.ShardOriginOffset:start;
        warning.transform.SetPositionAndRotation(origin,Quaternion.Euler(0,0,group==2?0:Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg));
        Stage="Tell";warning.SetActive(true);
        float tell=group==0?.9f:group==1?.7f:.8f;
        for(float t=0;t<tell && Alive;t+=Time.deltaTime)
        {
            float progress=Mathf.Clamp01(t/tell);body.sprite=frames[group][progress<.45f?1:2];
            CarapaceGeometry.Draw(mesh,material,group,progress,length);yield return null;
        }
        if(!Alive){Cancel();yield break;}
        CarapaceGeometry.Draw(mesh,material,group,1,length);
        if(group==0)
        {
            warning.SetActive(false);Stage="Charge";float moved=0,time=0;bool hit=false;
            Vector2 beforePlayer=player.transform.position;
            if(CarapaceGeometry.RectangleContains(CarapaceGeometry.Local(beforePlayer,start,dir),0,CarapaceGeometry.ChargeRadius))
            {hit=true;player.TakeDamage(damage);}
            while(moved<length && Alive)
            {
                if(Time.deltaTime<=0){yield return null;continue;}
                float requested=Mathf.Min(10*Time.deltaTime,length-moved),step=SafeDistance(transform.position,dir,requested);
                Vector2 from=transform.position,to=from+dir*step,currentPlayer=player.transform.position;
                if(!hit && CarapaceGeometry.SweptSquare(beforePlayer,currentPlayer,from,to,dir,CarapaceGeometry.ChargeRadius))
                {hit=true;player.TakeDamage(damage);}
                transform.position=to;moved+=step;time+=Time.deltaTime;beforePlayer=currentPlayer;
                body.sprite=frames[0][time<.09f?3:4+(Mathf.FloorToInt(time*12)%2)];
                if(effects.Count<12)Emit(dustSprite,from-dir*.4f,.5f,.6f);
                if(step+.0001f<requested)break;
                yield return null;
            }
            yield return Recover(0,1.2f);
        }
        else if(group==1)
        {
            Stage="Slam";body.sprite=frames[1][3];yield return Delay(.15f);
            warning.SetActive(false);
            if(Alive)
            {
                if(CarapaceGeometry.SlamContains(CarapaceGeometry.Local(player.transform.position,start,dir)))player.TakeDamage(damage);
                for(int ring=0;ring<3;ring++)for(int i=0;i<5;i++)
                    Emit(rockSprite,start+SkillUtil.Rotate(dir,-45+i*22.5f)*(1+ring*.85f),.9f,1.1f);
            }
            yield return Recover(1,1);
        }
        else
        {
            Stage="Volley";body.sprite=frames[2][3];yield return Delay(.075f);warning.SetActive(false);
            if(Alive)
            {
                body.sprite=frames[2][4];volleyHit=false;previousPlayer=player.transform.position;
                for(int i=0;i<8;i++)
                {
                    Vector2 heading=SkillUtil.Rotate(Vector2.right,i*45);
                    var art=NewArt("Carapace shard",shardSprite,origin,1.05f);art.transform.rotation=Quaternion.Euler(0,0,i*45);
                    shards.Add(new Shard{art=art,origin=origin,direction=heading});
                }
            }
            yield return Delay(.075f);yield return Recover(2,1.35f);
        }
        warning.SetActive(false);Stage="Idle";body.sprite=frames[0][0];
    }

    IEnumerator Delay(float seconds)
    {for(float t=0;t<seconds && Alive;t+=Time.deltaTime)yield return null;}
    IEnumerator Recover(int group,float duration)
    {
        Stage="Recover";
        for(float t=0;t<duration && Alive;t+=Time.deltaTime)
        {
            float p=t/duration;
            int frame=group==0?(p<.4f?6:7):group==1?(p<.18f?4:p<.42f?5:p<.78f?6:7):(p<.25f?5:p<.65f?6:7);
            body.sprite=frames[group][frame];yield return null;
        }
    }

    void Update()
    {
        if(player!=null && player.IsDead){Cancel();return;}
        float dt=Time.deltaTime;if(dt<=0)return;
        Vector2 current=player!=null?(Vector2)player.transform.position:Vector2.zero;
        for(int i=shards.Count-1;i>=0;i--)
        {
            var s=shards[i];float next=Mathf.Min(CarapaceGeometry.ShardRange,s.travelled+CarapaceGeometry.ShardSpeed*dt);
            Vector2 from=s.origin+s.direction*s.travelled,to=s.origin+s.direction*next;
            if(!volleyHit && Alive && CarapaceGeometry.SweptSquare(previousPlayer,current,from,to,s.direction,CarapaceGeometry.ShardRadius))
            {volleyHit=true;player.TakeDamage(damage);}
            s.art.transform.position=to;s.travelled=next;
            if(next>=CarapaceGeometry.ShardRange){Destroy(s.art.gameObject);shards.RemoveAt(i);}
        }
        previousPlayer=current;
        for(int i=effects.Count-1;i>=0;i--)
        {
            var e=effects[i];e.age+=dt;
            if(e.age>=e.life){Destroy(e.art.gameObject);effects.RemoveAt(i);continue;}
            e.art.transform.position=e.origin+Vector2.up*(Mathf.Sin(e.age/e.life*Mathf.PI)*.3f);
            e.art.color=new Color(1,1,1,Mathf.Clamp01((e.life-e.age)/.2f));
        }
    }
    SpriteRenderer NewArt(string name,Sprite sprite,Vector2 at,float scale)
    {
        var go=new GameObject(name);go.transform.SetParent(transform);go.transform.position=at;go.transform.localScale=Vector3.one*scale;
        var art=go.AddComponent<SpriteRenderer>();art.sprite=sprite;art.sortingLayerName="WorldActors";art.sortingOrder=body.sortingOrder+1;return art;
    }
    void Emit(Sprite sprite,Vector2 at,float life,float scale)
    {effects.Add(new Effect{art=NewArt("Carapace impact",sprite,at,scale),origin=at,life=life});}
    void LateUpdate()
    {
        var camera=GameCamera.Get();if(camera!=null && body!=null)body.sortingOrder=Mathf.Clamp(Mathf.RoundToInt((camera.transform.position.y-transform.position.y)*100),-30000,30000);
    }
    public void Cancel()
    {
        if(warning!=null)warning.SetActive(false);
        foreach(var s in shards)if(s.art!=null)Destroy(s.art.gameObject);shards.Clear();
        foreach(var e in effects)if(e.art!=null)Destroy(e.art.gameObject);effects.Clear();Stage="Idle";
    }
    void OnDisable(){Cancel();}
    void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
}
