using System;
using System.Collections.Generic;
using UnityEngine;

// Evaluates a cast-time snapshot. It predicts distinct hits, not kills or future enemy movement.
public static class SkillTargeting
{
    public struct Result
    {
        public Vector2 Aim;
        public int HitCount;
        public float MeanDistance;
        public bool Found => HitCount > 0;
    }
    struct Target { public EnemyHealth health; public Vector2 position; public float distance; }
    static List<Target> Snapshot(Transform container, Vector2 origin, float limit)
    {
        var result = new List<Target>();
        foreach (var e in SkillUtil.EnemiesNow(container))
        {
            if (e == null || e.IsDead || !e.isActiveAndEnabled) continue;
            Vector2 p = e.transform.position; float distance = Vector2.Distance(origin,p);
            if (distance <= limit) result.Add(new Target {health=e,position=p,distance=distance});
        }
        // Hierarchy order is a reproducible final tiebreaker, without depending on hash iteration.
        return result;
    }
    static Vector2 Unit(Vector2 v, Vector2 fallback) => v.sqrMagnitude > .000001f ? v.normalized : (fallback.sqrMagnitude > .000001f ? fallback.normalized : Vector2.right);
    static Vector2 NearestDirection(List<Target> targets,Vector2 origin,float range,Vector2 fallback)
    {
        float best=float.PositiveInfinity;Vector2 result=fallback;
        foreach(var t in targets)if(t.distance<=range && t.distance<=best){best=t.distance;result=Unit(t.position-origin,fallback);}
        return Unit(result,Vector2.right);
    }
    static List<float> Angles(List<Target> targets,Vector2 origin,float range,Vector2 fallback,int arrows=1,float spread=0)
    {
        var centers=new List<float>();
        foreach(var t in targets)if(t.distance<=range){var v=Unit(t.position-origin,fallback);centers.Add(Mathf.Repeat(Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg,360));}
        centers.Sort();int count=centers.Count;
        for(int i=0;i<count && count>1;i++)
        {float a=centers[i],b=centers[(i+1)%count];if(i==count-1)b+=360;centers.Add(Mathf.Repeat((a+b)*.5f,360));}
        var result=new List<float>();var unique=new HashSet<int>();
        foreach(float angle in centers) for(int i=0;i<Mathf.Max(1,arrows);i++)
        {
            float offset=arrows<=1?0:Mathf.Lerp(-spread*.5f,spread*.5f,(float)i/(arrows-1));
            float candidate=Mathf.Repeat(angle-offset,360);
            if(unique.Add(Mathf.RoundToInt(candidate*10000)))result.Add(candidate);
        }
        return result;
    }
    static Vector2 Direction(float angle){float r=angle*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(r),Mathf.Sin(r));}
    static void Consider(ref Result best,ref float bestAlignment,Vector2 aim,int count,float sum,Vector2 direction,Vector2 nearest)
    {
        if(count==0)return;
        float mean=sum/count,alignment=Vector2.Dot(Unit(direction,nearest),nearest);
        if(count<best.HitCount)return;
        if(count==best.HitCount && (mean>best.MeanDistance+.00001f || (Mathf.Abs(mean-best.MeanDistance)<=.00001f && alignment<=bestAlignment+.000001f)))return;
        best=new Result{Aim=aim,HitCount=count,MeanDistance=mean};bestAlignment=alignment;
    }
    static float WallDistance(Vector2 origin,Vector2 direction,float length,float radius,int mask,List<RaycastHit2D> hits,Transform owner=null)
    {
        var filter=new ContactFilter2D();filter.SetLayerMask(mask);filter.useTriggers=false;
        Physics2D.CircleCast(origin,radius,direction,filter,hits,length);
        float wall=SkillUtil.HitsObstacle(origin,radius,mask)?0f:float.PositiveInfinity;
        foreach(var hit in hits)if(hit.collider!=null && (owner==null || !hit.collider.transform.IsChildOf(owner)))wall=Mathf.Min(wall,hit.distance);
        return wall;
    }
    // First-job projectile uses real colliders, unlike the second-job analytic enemy discs.
    public static Result Piercing(Transform container,Transform owner,Vector2 origin,float aimRange,float radius,int obstacles,Vector2 fallback)
    {
        var targets=Snapshot(container,origin,aimRange);var allowed=new Dictionary<EnemyHealth,float>();
        foreach(var t in targets)allowed[t.health]=t.distance;
        Vector2 nearest=NearestDirection(targets,origin,aimRange,fallback);
        var best=new Result{Aim=fallback};float alignment=-2;
        var hits=new List<RaycastHit2D>();var unique=new HashSet<EnemyHealth>();
        var filter=new ContactFilter2D();filter.SetLayerMask(~0);filter.useTriggers=false;
        foreach(float angle in Angles(targets,origin,aimRange,fallback))
        {
            Vector2 dir=Direction(angle);Physics2D.CircleCast(origin,Mathf.Max(.01f,radius),dir,filter,hits,aimRange);
            float wall=float.PositiveInfinity;
            foreach(var h in hits)if(h.collider!=null && (owner==null || !h.collider.transform.IsChildOf(owner)) && (obstacles&(1<<h.collider.gameObject.layer))!=0)wall=Mathf.Min(wall,h.distance);
            unique.Clear();float sum=0;
            foreach(var h in hits)
            {
                if(h.collider==null || h.distance>=wall || (owner!=null && h.collider.transform.IsChildOf(owner)))continue;
                var e=h.collider.GetComponentInParent<EnemyHealth>();
                if(e!=null && allowed.TryGetValue(e,out float distance) && unique.Add(e))sum+=distance;
            }
            Consider(ref best,ref alignment,dir,unique.Count,sum,dir,nearest);
        }
        return best;
    }
    public static Result Volley(Transform container,Vector2 origin,float aimRange,float projectileRange,float radius,int arrowCount,float spread,Vector2 fallback)
    {
        float length=Mathf.Min(aimRange,projectileRange);
        var targets=Snapshot(container,origin,length);Vector2 nearest=NearestDirection(targets,origin,length,fallback);
        var best=new Result{Aim=fallback};float alignment=-2;int arrows=Mathf.Max(1,arrowCount);
        var hits=new List<RaycastHit2D>();var marked=new int[targets.Count];int stamp=0;
        var rays=new Dictionary<int,int[]>();
        var overlaps=new List<Collider2D>();var wallFilter=new ContactFilter2D();wallFilter.SetLayerMask(SkillUtil.ObstacleMask);wallFilter.useTriggers=false;
        bool hasWalls=Physics2D.OverlapCircle(origin,length+radius,wallFilter,overlaps)>0;
        var offsets=new Vector2[targets.Count];var circleTerms=new float[targets.Count];
        float combined=radius+SkillUtil.EnemyRadius;
        for(int j=0;j<targets.Count;j++){offsets[j]=targets[j].position-origin;circleTerms[j]=offsets[j].sqrMagnitude-combined*combined;}
        foreach(float angle in Angles(targets,origin,length,fallback,arrows,spread))
        {
            stamp++;int count=0;float sum=0;
            for(int i=0;i<arrows;i++)
            {
                float offset=arrows==1?0:Mathf.Lerp(-spread*.5f,spread*.5f,(float)i/(arrows-1));
                float rayAngle=Mathf.Repeat(angle+offset,360);int rayKey=Mathf.RoundToInt(rayAngle*10000);
                if(!rays.TryGetValue(rayKey,out int[] rayHits))
                {
                    Vector2 dir=Direction(rayAngle);
                    float wall=hasWalls?WallDistance(origin,dir,length,radius*.5f,SkillUtil.ObstacleMask,hits):float.PositiveInfinity;
                    var indices=new List<int>();
                    for(int j=0;j<targets.Count;j++)
                    {
                        float c=circleTerms[j],entry=0;
                        if(c>0)
                        {
                            Vector2 v=offsets[j];float along=v.x*dir.x+v.y*dir.y;
                            float discriminant=along*along-c;
                            if(along<0 || discriminant<0)continue;
                            entry=along-Mathf.Sqrt(discriminant);
                        }
                        if(entry<=length && entry<wall)indices.Add(j);
                    }
                    rayHits=indices.ToArray();rays.Add(rayKey,rayHits);
                }
                foreach(int j in rayHits)if(marked[j]!=stamp){marked[j]=stamp;count++;sum+=targets[j].distance;}
            }
            var direction=Direction(angle);Consider(ref best,ref alignment,direction,count,sum,direction,nearest);
        }
        return best;
    }
    public static Result Explosion(Transform container,Vector2 origin,float aimRange,float projectileRange,float radius,float blastRadius,Vector2 fallback)
    {
        var targets=Snapshot(container,origin,projectileRange+radius+SkillUtil.EnemyRadius);
        Vector2 nearest=NearestDirection(targets,origin,aimRange,fallback);var best=new Result{Aim=fallback};float alignment=-2;
        foreach(float angle in Angles(targets,origin,Mathf.Min(aimRange,projectileRange),fallback))
        {
            Vector2 dir=Direction(angle);int first=-1;float entry=float.PositiveInfinity;
            for(int i=0;i<targets.Count;i++)
            {float hit=SkillProjectile.SegmentEntryDistance(origin,dir,projectileRange,targets[i].position,radius+SkillUtil.EnemyRadius);if(hit<entry){entry=hit;first=i;}}
            if(first<0)continue;
            Vector2 point=origin+dir*entry;int count=0;float sum=0;
            for(int i=0;i<targets.Count;i++)if(i==first || (targets[i].position-point).sqrMagnitude<=(blastRadius+SkillUtil.EnemyRadius)*(blastRadius+SkillUtil.EnemyRadius))
            {count++;sum+=targets[i].distance;}
            Consider(ref best,ref alignment,dir,count,sum,dir,nearest);
        }
        return best;
    }
    // Exact circle-boundary candidates for the stationary snapshot, clipped to the cast-centre range.
    public static Result Rain(Transform container,Vector2 origin,float aimRange,float radius,Vector2 fallbackCenter)
    {
        float reach=radius+SkillUtil.EnemyRadius;
        var targets=Snapshot(container,origin,aimRange+reach);
        Vector2 nearest=NearestDirection(targets,origin,aimRange,fallbackCenter-origin);
        var best=new Result{Aim=fallbackCenter};float alignment=-2;
        var spatial = new CircleIndex(targets);
        Action<Vector2> score=center=>{
            if((center-origin).sqrMagnitude>aimRange*aimRange+.0001f)return;
            int count=0;float sum=0;
            spatial.Count(center.x,center.y,reach*reach+.0001f,ref count,ref sum);
            Consider(ref best,ref alignment,center,count,sum,center-origin,nearest);
        };
        score(origin);
        foreach(var t in targets)
        {
            score(t.position);
            score(origin+Vector2.ClampMagnitude(t.position-origin,aimRange));
            CircleIntersections(origin,aimRange,t.position,reach,score);
        }
        for(int i=0;i<targets.Count;i++)for(int j=i+1;j<targets.Count;j++)
        {
            score((targets[i].position+targets[j].position)*.5f);
            CircleIntersections(targets[i].position,reach,targets[j].position,reach,score);
        }
        return best;
    }
    // Aggregate fully covered spatial nodes instead of scanning every enemy for every circle candidate.
    sealed class CircleIndex
    {
        sealed class Node { public float minX,minY,maxX,maxY,sum; public int start,count; public Node left,right; }
        readonly Target[] data;
        readonly Node root;
        public CircleIndex(List<Target> targets) { data=targets.ToArray();root=Build(0,data.Length,0); }
        Node Build(int start,int count,int depth)
        {
            if(count==0)return null;
            var n=new Node{start=start,count=count,minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity};
            for(int i=start;i<start+count;i++){var t=data[i];n.minX=Mathf.Min(n.minX,t.position.x);n.maxX=Mathf.Max(n.maxX,t.position.x);n.minY=Mathf.Min(n.minY,t.position.y);n.maxY=Mathf.Max(n.maxY,t.position.y);n.sum+=t.distance;}
            if(count>8)
            {
                bool horizontal=(depth&1)==0;
                Array.Sort(data,start,count,Comparer<Target>.Create((a,b)=>horizontal?a.position.x.CompareTo(b.position.x):a.position.y.CompareTo(b.position.y)));
                int half=count/2;n.left=Build(start,half,depth+1);n.right=Build(start+half,count-half,depth+1);
            }
            return n;
        }
        public void Count(float x,float y,float squared,ref int count,ref float sum) { Query(root,x,y,squared,ref count,ref sum); }
        void Query(Node n,float x,float y,float squared,ref int count,ref float sum)
        {
            if(n==null)return;
            float dx=x<n.minX?n.minX-x:x>n.maxX?x-n.maxX:0,dy=y<n.minY?n.minY-y:y>n.maxY?y-n.maxY:0;
            if(dx*dx+dy*dy>squared)return;
            dx=Mathf.Max(Mathf.Abs(x-n.minX),Mathf.Abs(x-n.maxX));dy=Mathf.Max(Mathf.Abs(y-n.minY),Mathf.Abs(y-n.maxY));
            if(dx*dx+dy*dy<=squared){count+=n.count;sum+=n.sum;return;}
            if(n.left!=null){Query(n.left,x,y,squared,ref count,ref sum);Query(n.right,x,y,squared,ref count,ref sum);return;}
            for(int i=n.start;i<n.start+n.count;i++){var t=data[i];dx=t.position.x-x;dy=t.position.y-y;if(dx*dx+dy*dy<=squared){count++;sum+=t.distance;}}
        }
    }
    static void CircleIntersections(Vector2 a,float ar,Vector2 b,float br,Action<Vector2> emit)
    {
        Vector2 delta=b-a;float d=delta.magnitude;
        if(d<.00001f || d>ar+br || d<Mathf.Abs(ar-br))return;
        float along=(ar*ar-br*br+d*d)/(2*d);float h=Mathf.Sqrt(Mathf.Max(0,ar*ar-along*along));
        Vector2 direction=delta/d,mid=a+direction*along,normal=new Vector2(-direction.y,direction.x)*h;
        emit(mid+normal);emit(mid-normal);
    }
}
