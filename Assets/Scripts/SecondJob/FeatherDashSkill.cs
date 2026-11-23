using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(PlayerMovement))]
public sealed class FeatherDashSkill : MonoBehaviour, ISecondJobSkill
{
    [SerializeField, Min(1)] private int damage = 6;
    [SerializeField, Min(.01f)] private float hitWidth = 1.2f;
    [SerializeField, Min(0f)] private float pushDistance = 1f;
    private PlayerMovement movement;
    private Vector2 previous, origin, direction;
    private bool tracking;
    private GameObject effect;
    private readonly HashSet<EnemyHealth> hit = new HashSet<EnemyHealth>();
    private void OnEnable() { movement = GetComponent<PlayerMovement>(); movement.DashStarted += Begin; }
    private void Begin(Vector2 start, Vector2 dir)
    {
        previous = origin = start; direction = dir; hit.Clear(); tracking = true;
        if (effect != null) Destroy(effect);
        effect = SecondArcherFx.Spawn("Shift", start, this, .3f);
        effect.transform.rotation = SkillUtil.RotationFor(dir);
    }
    private void LateUpdate()
    {
        if (!tracking || Time.timeScale <= 0f) return;
        if (GetComponent<PlayerHealth>().IsDead) { tracking = false; return; }
        Vector2 at = transform.position, delta = at - previous;
        float length = delta.magnitude;
        foreach (var enemy in SkillUtil.EnemiesNow(SkillUtil.EnemyContainer(null)))
        {
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || hit.Contains(enemy)) continue;
            if (SkillProjectile.SegmentEntryDistance(previous, length > .0001f ? delta / length : direction,
                length, enemy.transform.position, hitWidth * .5f + SkillUtil.EnemyRadius) > length) continue;
            if (Physics2D.Linecast(at, enemy.transform.position, SkillUtil.ObstacleMask)) continue;
            Vector2 hitPosition = enemy.transform.position;
            hit.Add(enemy); enemy.TakeDamage(SkillModifiers.Damage(damage));
            CompressedWindSkill.PushAside(enemy, origin, direction, pushDistance);
            SecondArcherFx.Burst(hitPosition, this);
        }
        previous = at;
        if (effect != null) effect.transform.position = at;
        if (!movement.IsDashing) tracking = false;
    }
    private void OnDisable()
    {
        if (movement != null) movement.DashStarted -= Begin;
        tracking = false; hit.Clear();
        if (effect != null) Destroy(effect);
    }
}
