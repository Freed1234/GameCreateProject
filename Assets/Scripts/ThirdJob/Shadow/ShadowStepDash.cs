using System.Collections;
using UnityEngine;

// [3차 그림자 SHIFT] 그림자 걸음 : 이동 방향으로 대쉬, 출발 자리에 그림자 분신이 남아 2초간 같이 사격
// 3차 전직 시 기존 대쉬(PlayerDash) 대신 켜집니다. (클래스 이름에 Dash가 있어 대쉬 칸에 자동 연결)
[DisallowMultipleComponent]
public class ShadowStepDash : ThirdJobSkill
{
    [Header("그림자 걸음")]
    [SerializeField, Min(0.5f)] private float distance = 3.5f;
    [SerializeField, Min(0.05f)] private float duration = 0.18f;
    [SerializeField, Min(0.2f)] private float cloneSeconds = 2f;
    [SerializeField, Min(0.05f)] private float cloneFireInterval = 0.3f;
    [SerializeField, Min(1)] private int cloneDamage = 7;

    public override ArcherBranch Branch => ArcherBranch.Shadow;
    private bool dashing;

    private void Reset() { key = KeyCode.LeftShift; cooldown = 2.5f; arrowColor = JobTierUtil.ShadowColor; }

    protected override bool Cast()
    {
        if (dashing) return false;
        Vector2 input = SkillUtil.MoveInput();
        Vector2 dir = input != Vector2.zero ? input : LastMoveDirection;
        StartCoroutine(Clone(Origin));
        StartCoroutine(Run(dir.normalized));
        return true;
    }

    private IEnumerator Run(Vector2 dir)
    {
        dashing = true;
        yield return JobTierUtil.Dash(this, dir, distance, duration, new Color(0.35f, 0.1f, 0.55f, 0.55f));
        dashing = false;
    }

    private IEnumerator Clone(Vector2 at)
    {
        GameObject clone = ShadowClone.Create(gameObject, at);
        float t = 0f, next = 0.1f;
        while (t < cloneSeconds && clone != null)
        {
            t += Time.deltaTime;
            if (t >= next)
            {
                next += cloneFireInterval;
                EnemyHealth target = SkillUtil.Nearest(Enemies, at, aimRange);
                if (target != null)
                {
                    Vector2 d = ((Vector2)target.transform.position - at).normalized;
                    SkillProjectile.Spawn(arrowSprite, arrowColor, at, d, 18f, aimRange,
                        Dmg(cloneDamage, target), 0.25f, false, false, Enemies);
                }
            }
            yield return null;
        }
        if (clone != null) ShadowClone.FadeOut(clone, 0.3f);
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
