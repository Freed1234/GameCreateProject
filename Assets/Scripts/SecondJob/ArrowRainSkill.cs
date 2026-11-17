using System.Collections;
using UnityEngine;

// [2차 X] 화살비 (임시, 미스포츈 E 느낌)
// 가까운 적 위치(없으면 앞쪽)에 원형 장판을 깔고, 그 안에 일정 시간 화살이 쏟아지며 계속 피해.
[DisallowMultipleComponent]
public class ArrowRainSkill : ArcherSkillBase, ISecondJobSkill
{
    [SerializeField] private bool preferMultipleTargets = true;
    [Header("화살비")]
    [SerializeField, Min(0.5f)] private float radius = 2.5f;
    [SerializeField, Min(0.2f)] private float duration = 2.5f;
    [SerializeField, Min(0.05f)] private float tickInterval = 0.25f;
    [SerializeField, Min(1)] private int damagePerTick = 2;
    [Tooltip("적이 없을 때 앞쪽 몇 칸에 깔지")]
    [SerializeField, Min(0f)] private float defaultDistance = 3f;

    private GameObject rainFx;
    private readonly SpriteRenderer[] arrows = new SpriteRenderer[4];

    private void Reset()
    {
        key = KeyCode.X;
        cooldown = 8f;
        aimRange = 7f;
    }

    protected override bool Cast()
    {
        StopAllCoroutines();
        ClearRainFx();
        Vector2 center = Origin + LastMoveDirection * defaultDistance;
        if (preferMultipleTargets)
            center = SkillTargeting.Rain(Enemies, Origin, aimRange, radius, center).Aim;
        else
        {
            EnemyHealth target = SkillUtil.Nearest(Enemies, Origin, aimRange);
            if (target != null) center = target.transform.position;
        }
        StartCoroutine(Rain(center));
        GetComponentInParent<PlayerAttackVisual>()?.PlayAttack(center - Origin);
        return true;
    }

    private IEnumerator Rain(Vector2 center)
    {
        rainFx = new GameObject("Rain boundary");
        rainFx.transform.position = center;
        rainFx.transform.localScale = Vector3.one * (2f * radius);
        var boundary = rainFx.AddComponent<SpriteRenderer>();
        boundary.sprite = Resources.Load<Sprite>("ArcherFx/RainBoundary");
        boundary.sortingOrder = 61;
        Sprite[] frames = ArcherSkillFrames.Load("X");
        for (int i = 0; i < arrows.Length; i++)
        {
            var arrow = new GameObject("Rain arrow").AddComponent<SpriteRenderer>();
            arrow.transform.SetParent(rainFx.transform, false);
            arrow.sortingOrder = 62;
            arrows[i] = arrow;
        }

        double interval = Mathf.Max(0.05f, tickInterval);
        double started = Time.timeAsDouble;
        int tickCount = Mathf.CeilToInt(duration / (float)interval);
        int nextTick = 0;
        var lastDrop = new int[arrows.Length];
        while (!OwnerDead)
        {
            double elapsed = Time.timeAsDouble - started;
            for (int i = 0; i < arrows.Length; i++)
            {
                // Phase zero is impact. Each arrow starts falling half a cycle earlier.
                double phase = elapsed / interval - (double)i / arrows.Length;
                int drop = (int)System.Math.Floor(phase + 0.5);
                if (drop < 0 || (drop + (double)i / arrows.Length) * interval >= duration)
                {
                    arrows[i].sprite = null;
                    continue;
                }
                if (lastDrop[i] != drop + 1)
                {
                    arrows[i].transform.localPosition = Random.insideUnitCircle * 0.5f;
                    lastDrop[i] = drop + 1;
                }
                int frame = Mathf.Clamp((int)((phase - drop + 0.5) * 6), 0, 5);
                arrows[i].sprite = frames[frame];
            }

            while (nextTick < tickCount && nextTick * interval <= elapsed && !OwnerDead)
            {
                SkillUtil.DamageInRadius(Enemies, center, radius, SkillModifiers.Damage(damagePerTick));
                nextTick++;
            }
            if (elapsed >= duration) break;
            yield return null;
        }
        ClearRainFx();
    }

    private void ClearRainFx()
    {
        if (rainFx == null) return;
        rainFx.SetActive(false);
        Destroy(rainFx);
        rainFx = null;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ClearRainFx();
    }
}
