using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [3차 폭풍 SHIFT] 바람 돌진 : 이동 방향으로 길게 돌진, 경로의 적에게 피해 + 양옆으로 밀쳐냄
[DisallowMultipleComponent]
public class WindRushDash : ThirdJobSkill
{
    [Header("바람 돌진")]
    [SerializeField, Min(0.5f)] private float distance = 4.5f;
    [SerializeField, Min(0.05f)] private float duration = 0.2f;
    [SerializeField, Min(0.2f)] private float hitRadius = 1f;
    [SerializeField, Min(1)] private int damage = 10;
    [SerializeField, Min(0f)] private float knockback = 8f;

    public override ArcherBranch Branch => ArcherBranch.Storm;
    private bool dashing;

    private void Reset() { key = KeyCode.LeftShift; cooldown = 2.5f; }

    protected override bool Cast()
    {
        if (dashing) return false;
        Vector2 input = SkillUtil.MoveInput();
        Vector2 dir = (input != Vector2.zero ? input : LastMoveDirection).normalized;
        StartCoroutine(Run(dir));
        return true;
    }

    private IEnumerator Run(Vector2 dir)
    {
        dashing = true;
        HashSet<EnemyHealth> hit = new HashSet<EnemyHealth>();
        Vector2 side = new Vector2(-dir.y, dir.x);
        yield return JobTierUtil.Dash(this, dir, distance, duration, new Color(0.7f, 1f, 0.9f, 0.5f), (from, to) =>
        {
            foreach (EnemyHealth e in SkillUtil.Enemies(Enemies))
            {
                if (hit.Contains(e) || Vector2.Distance(to, e.transform.position) > hitRadius) continue;
                hit.Add(e);
                JobTierUtil.Hit(e, Dmg(damage));
                float s = Vector2.Dot((Vector2)e.transform.position - to, side) >= 0f ? 1f : -1f;
                JobTierUtil.Knockback(e, side * s * knockback);
            }
        });
        dashing = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        dashing = false;
        PlayerMovement movement = GetComponent<PlayerMovement>();
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (movement != null && (health == null || !health.IsDead)) movement.enabled = true;
    }
}
