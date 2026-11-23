using UnityEngine;

[DisallowMultipleComponent]
public sealed class CompressedWindSkill : ArcherSkillBase, ISecondJobSkill
{
    [SerializeField, Min(1)] private int damage = 40;
    [SerializeField, Min(.1f)] private float range = 16f, speed = 16f, hitWidth = 1.2f;
    [SerializeField, Min(0f)] private float pushDistance = 1f;
    private void Reset() { key = KeyCode.C; cooldown = 12f; aimRange = 16f; }

    protected override bool Cast()
    {
        Vector2 origin = Origin;
        Vector2 direction = SkillTargeting.WindPiercing(Enemies, origin,
            Mathf.Min(range, aimRange), hitWidth * .5f, LastMoveDirection).Aim;
        var arrow = SkillProjectile.Spawn(SecondArcherFx.Frame("C", 0), Color.white,
            origin, direction, speed, range, SkillModifiers.Damage(damage), hitWidth * .5f, true, true, Enemies);
        SecondArcherFx.Animate(arrow.gameObject, "C", this);
        arrow.OnHit += (p, at, enemy) => {
            PushAside(enemy, origin, direction, pushDistance);
            SecondArcherFx.Burst(at, this);
        };
        GetComponent<PlayerAttackVisual>()?.PlayAttack(direction);
        return true;
    }

    internal static void PushAside(EnemyHealth enemy, Vector2 origin, Vector2 direction, float distance)
    {
        if (enemy == null || enemy.IsDead) return;
        Vector2 side = new Vector2(-direction.y, direction.x);
        float sign = Vector2.Dot((Vector2)enemy.transform.position - origin, side) >= 0 ? 1 : -1;
        enemy.GetComponent<EnemyMovement>()?.Push(side * sign * distance, .2f);
    }
}
