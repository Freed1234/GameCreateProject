#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CarapaceNaturalPilot
{
    PlayerMovement movement;
    Vector2 lastPosition,escape;
    float stuckTime,escapeTime,nextLog;
    readonly Transform transform;
    public CarapaceNaturalPilot(PlayerHealth player){transform=player.transform;movement=transform.GetComponent<PlayerMovement>();}
    public void Step()
    {
        if(movement==null)movement=transform.GetComponent<PlayerMovement>(); if(Time.timeScale<=0)return;
        Vector2 at=transform.position,wanted=at;
        var boss=Object.FindAnyObjectByType<BossController>();
        if(boss!=null)
        {
            var combat=boss.GetComponent<CarapaceCombat>();var pattern=(BossController.Pattern)CarapaceGameplayRecording.Field(boss,"lastPattern").GetValue(boss);
            Vector2 center=boss.transform.position,away=(at-center).normalized;if(away==Vector2.zero)away=Vector2.right;
            float radius=CarapaceNaturalPlayCheck.AllPatterns?4f:7.5f;
            if(combat.Stage=="Tell"||combat.Stage=="Charge"||combat.Stage=="Slam"||combat.Stage=="Volley")
            {
                radius=CarapaceNaturalPlayCheck.AllPatterns?4.6f:7.5f;
                if(pattern==BossController.Pattern.Radial)
                {
                    center+=CarapaceGeometry.ShardOriginOffset;
                    float angle=Mathf.Atan2(at.y-center.y,at.x-center.x)*Mathf.Rad2Deg;
                    away=SkillUtil.Rotate(Vector2.right,Mathf.Round((angle-22.5f)/45f)*45+22.5f);
                }
                else away=SkillUtil.Rotate(away,45);
            }
            else away=SkillUtil.Rotate(away,8);
            wanted=center+away*radius;
        }
        else
        {
            var enemy=SkillUtil.Nearest(SkillUtil.EnemyContainer(null),at,20);
            if(enemy!=null){Vector2 away=(at-(Vector2)enemy.transform.position).normalized;wanted=(Vector2)enemy.transform.position+SkillUtil.Rotate(away,20)*(PlayerProgression.Instance.CurrentJob==PlayerJob.Archer?5f:1.3f);}
        }
        if(boss==null)
        {
            wanted=at+movement.LastMoveDirection*5;
            var chests=Object.FindAnyObjectByType<ChestManager>();
            if(chests!=null)
            {
                var records=(System.Collections.IEnumerable)CarapaceGameplayRecording.Field(chests,"active").GetValue(chests);
                float closest=100;
                foreach(var record in records)if(!(bool)CarapaceGameplayRecording.Field(record,"opened").GetValue(record)){Vector2 point=(Vector2)CarapaceGameplayRecording.Field(record,"position").GetValue(record);float distance=Vector2.Distance(at,point);if(distance<closest){closest=distance;wanted=point;}}
            }
        }        Vector2 heading=wanted-at;if(heading.magnitude<.12f)heading=Vector2.zero;else heading=heading.normalized;
        stuckTime=Vector2.Distance(lastPosition,at)<.01f&&heading!=Vector2.zero?stuckTime+Time.deltaTime:0;
        lastPosition=at;
        if(stuckTime>.6f){escape=SkillUtil.Rotate(movement.LastMoveDirection,120);escapeTime=1.2f;stuckTime=0;}
        if(escapeTime>0){heading=escape;escapeTime-=Time.deltaTime;}
        if(heading!=Vector2.zero)
        {
            Vector2 best=Vector2.zero;float bestScore=float.NegativeInfinity;
            for(int i=0;i<16;i++)
            {
                Vector2 candidate=SkillUtil.Rotate(heading,i*22.5f);
                var filter=new ContactFilter2D();filter.SetLayerMask(SkillUtil.ObstacleMask);filter.useTriggers=false;
                var hits=new RaycastHit2D[8];if(Physics2D.CircleCast(at,.27f,candidate,filter,hits,.65f)>0)continue;
                float score=Vector2.Dot(candidate,heading);
                foreach(var enemy in SkillUtil.EnemiesNow(SkillUtil.EnemyContainer(null)))
                {
                    if(enemy==null||enemy.IsDead||enemy.GetComponent<BossHitProxy>()!=null)continue;
                    float d=Vector2.Distance(at+candidate*1.5f,enemy.transform.position);
                    if(d<3f)score-=(3f-d)*3;
                }
                if(score>bestScore){bestScore=score;best=candidate;}
            }
            heading=best;
        }
        if(Time.time>=nextLog){Debug.Log("PILOT t="+Time.time+" at="+at+" wanted="+wanted+" heading="+heading+" velocity="+transform.GetComponent<Rigidbody2D>().linearVelocity);nextLog=Time.time+5;} CarapaceGameplayRecording.Set(movement,"moveInput",heading);
        if(heading!=Vector2.zero)CarapaceGameplayRecording.Set(movement,"lastDirection",heading);
        foreach(var enemy in SkillUtil.EnemiesNow(SkillUtil.EnemyContainer(null)))
            if(enemy!=null&&!enemy.IsDead&&enemy.GetComponent<BossHitProxy>()==null&&Vector2.Distance(at,enemy.transform.position)<1.6f)
            {typeof(PlayerMovement).GetMethod("TryStartDash",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(movement,null);break;}
    }
}
#endif








