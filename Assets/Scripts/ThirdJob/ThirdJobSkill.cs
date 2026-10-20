using UnityEngine;

// 3차 스킬 공통: ArcherSkillBase + 갈래 표시. JobTierSystem이 켜고 끕니다.
public abstract class ThirdJobSkill : ArcherSkillBase, IThirdJobSkill
{
    public abstract ArcherBranch Branch { get; }

    protected int Dmg(int baseDamage, EnemyHealth target = null) => JobTierUtil.Damage(baseDamage, Branch, target);
}
