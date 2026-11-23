using UnityEngine;

// World-unit geometry shared by the warning mesh and combat. The player's position
// is the hurt point: no hidden radius is added outside the white boundary.
public static class CarapaceGeometry
{
    public const float ChargeRadius=.55f, ChargeLength=6f, SlamRadius=3.4f, SlamHalfAngle=50f;
    public const float ShardRadius=.35f, ShardRange=8f, ShardSpeed=6f;
    public static readonly Vector2 ShardOriginOffset=new Vector2(0,.6f);

    public static Vector2 Local(Vector2 point,Vector2 origin,Vector2 direction)
    {Vector2 p=point-origin;return new Vector2(Vector2.Dot(p,direction),Vector2.Dot(p,new Vector2(-direction.y,direction.x)));}

    public static bool RectangleContains(Vector2 local,float length,float radius)
    {return local.x>=-radius-.000001f && local.x<=length+radius+.000001f && Mathf.Abs(local.y)<=radius+.000001f;}

    public static bool SlamContains(Vector2 local)
    {return local.sqrMagnitude<=SlamRadius*SlamRadius && local.x>=0 && Mathf.Abs(local.y)*Mathf.Cos(SlamHalfAngle*Mathf.Deg2Rad)<=local.x*Mathf.Sin(SlamHalfAngle*Mathf.Deg2Rad);}

    // Segment versus moving square in relative coordinates. This tests the whole
    // frame, including fast dashes and the final partial step, without a trailing hazard.
    public static bool SweptSquare(Vector2 playerBefore,Vector2 playerAfter,Vector2 from,Vector2 to,Vector2 direction,float radius)
    {
        Vector2 a=Local(playerBefore,from,direction),b=Local(playerAfter,to,direction),d=b-a;
        float enter=0,exit=1;
        for(int axis=0;axis<2;axis++)
        {
            if(Mathf.Abs(d[axis])<.000001f){if(Mathf.Abs(a[axis])>radius)return false;continue;}
            float t1=(-radius-a[axis])/d[axis],t2=(radius-a[axis])/d[axis];
            enter=Mathf.Max(enter,Mathf.Min(t1,t2));exit=Mathf.Min(exit,Mathf.Max(t1,t2));
            if(enter>exit)return false;
        }
        return true;
    }

    public static void Draw(Mesh mesh,Material material,int group,float progress,float length,float originY=0)
    {
        float radius=group==0?ChargeRadius:group==1?SlamRadius:ShardRadius;
        material.SetFloat("_Group",group);material.SetFloat("_Progress",progress);
        material.SetFloat("_Length",length);material.SetFloat("_Radius",radius);
        material.SetFloat("_HalfAngle",SlamHalfAngle);material.SetFloat("_OriginY",originY);
        Vector2 min,max;
        if(group==0){min=new Vector2(-radius,-radius);max=new Vector2(length+radius,radius);}
        else if(group==1){min=new Vector2(0,-radius);max=new Vector2(radius,radius);}
        else{min=new Vector2(0,originY)-Vector2.one*(length+radius);max=new Vector2(0,originY)+Vector2.one*(length+radius);}
        min-=Vector2.one*.04f;max+=Vector2.one*.04f;
        mesh.Clear();mesh.vertices=new[]{(Vector3)min,new Vector3(max.x,min.y,0),(Vector3)max,new Vector3(min.x,max.y,0)};
        mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
    }
}
