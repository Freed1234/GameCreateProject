using System;
using UnityEngine;

// Presentation only: root position, collider and attack animation remain combat-owned.
[DefaultExecutionOrder(250)]
public sealed class CarapaceLocomotion : MonoBehaviour
{
    BossController owner;
    CarapaceCombat combat;
    SpriteRenderer body;
    Sprite[] walk, idle;
    Vector3 previousPosition;
    float walkPhase, idlePhase;
    public bool IsWalking { get; private set; }
    public int FrameIndex { get; private set; }

    public void Initialize(BossController boss, CarapaceCombat attacks, SpriteRenderer renderer)
    {
        owner=boss;combat=attacks;body=renderer;
        walk=Load("Walk",8);idle=Load("Idle",4);
        previousPosition=transform.position;
        body.sprite=idle[0];
    }
    static Sprite[] Load(string name,int count)
    {
        var frames=Resources.LoadAll<Sprite>("CarapaceDemo/"+name);
        Array.Sort(frames,(a,b)=>string.CompareOrdinal(a.name,b.name));
        if(frames.Length!=count)throw new InvalidOperationException("Missing carapace "+name+" frames");
        return frames;
    }
    void LateUpdate()
    {
        float distance=Vector2.Distance(transform.position,previousPosition);
        previousPosition=transform.position;
        if(owner==null||owner.IsDead||body==null||Time.deltaTime<=0)return;
        if(combat.Stage!="Idle"){IsWalking=false;return;}
        IsWalking=distance>.0001f;
        if(IsWalking)
        {
            walkPhase=Mathf.Repeat(walkPhase+distance/1.28f,1f);
            FrameIndex=Mathf.Min(7,Mathf.FloorToInt(walkPhase*8));
            body.sprite=walk[FrameIndex];idlePhase=0;
        }
        else
        {
            idlePhase=Mathf.Repeat(idlePhase+Time.deltaTime/1.2f,1f);
            FrameIndex=Mathf.Min(3,Mathf.FloorToInt(idlePhase*4));
            body.sprite=idle[FrameIndex];
        }
    }
}
