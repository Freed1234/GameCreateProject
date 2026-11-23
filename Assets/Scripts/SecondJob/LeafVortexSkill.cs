using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeafVortexSkill : ArcherSkillBase, ISecondJobSkill
{
    [SerializeField, Min(.1f)] private float radius = 5f, duration = 3f;
    [SerializeField, Min(.01f)] private float tickInterval = .25f;
    [SerializeField, Min(1)] private int damage = 2;
    [SerializeField, Min(0f)] private float pullSpeed = 2f;
    private GameObject effect;
    private void Reset() { key = KeyCode.X; cooldown = 10f; aimRange = 7f; }

    protected override bool Cast()
    {
        if (effect != null) return false;
        // Share first-job ArrowRain's distance-weighted coverage: nearby enemies score 4 down to 1 at aimRange.
        Vector2 at = SkillTargeting.Rain(Enemies, Origin, aimRange, radius, Origin + LastMoveDirection * 3f).Aim;
        effect = SecondArcherFx.Spawn("X", at, this, duration);
        effect.transform.localScale = Vector3.one * (radius / 2.5f);
        StartCoroutine(Vortex(at));
        GetComponentInParent<PlayerAttackVisual>()?.PlayAttack(at - Origin);
        return true;
    }

    private IEnumerator Vortex(Vector2 center)
    {
        double interval = Mathf.Max(.05f, tickInterval);
        double start = Time.timeAsDouble;
        int nextTick = 0, count = Mathf.CeilToInt(duration / (float)interval);
        while (!OwnerDead)
        {
            if (Time.timeScale <= 0f) { yield return null; continue; }
            double elapsed = Time.timeAsDouble - start;
            while (nextTick < count && nextTick * interval <= elapsed && !OwnerDead)
            {
                SkillUtil.DamageInRadius(Enemies, center, radius, SkillModifiers.Damage(damage));
                nextTick++;
            }
            if (elapsed >= duration) break;
            foreach (var enemy in SkillUtil.EnemiesNow(Enemies))
                if (InReach(enemy, center)) enemy.GetComponent<EnemyMovement>()?.PullTowards(center, pullSpeed);
            yield return null;
        }
        if (effect != null) Destroy(effect);
    }

    private bool InReach(EnemyHealth enemy, Vector2 center)
    {
        return enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead
            && Vector2.Distance(center, enemy.transform.position) <= radius + SkillUtil.EnemyRadius;
    }

    protected override void OnDisable() { base.OnDisable(); if (effect != null) Destroy(effect); }
}
