using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StormBarrageSkill : ArcherSkillBase, ISecondJobSkill
{
    [SerializeField, Min(1)] private int shotCount = 40;
    [SerializeField, Min(.1f)] private float duration = 2f;
    [SerializeField, Min(1)] private int damage = 6;
    [SerializeField, Min(1f)] private float speed = 20f, range = 12f;
    [SerializeField, Min(.01f)] private float hitWidth = .22f;
    private readonly List<EnemyHealth> candidates = new List<EnemyHealth>();
    private EnemyHealth lastTarget;
    private GameObject marker;
    private bool casting;

    private void Reset() { key = KeyCode.Z; cooldown = 7f; aimRange = 10f; }

    private EnemyHealth NextTarget()
    {
        candidates.Clear();
        foreach (var enemy in SkillUtil.EnemiesNow(Enemies))
            if (enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead
                && Vector2.Distance(Origin, enemy.transform.position) <= aimRange) candidates.Add(enemy);
        candidates.Sort((a, b) => {
            int distance = ((Vector2)a.transform.position - Origin).sqrMagnitude.CompareTo(((Vector2)b.transform.position - Origin).sqrMagnitude);
            return distance != 0 ? distance : a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
        });
        if (candidates.Count > 3) candidates.RemoveRange(3, candidates.Count - 3);
        if (candidates.Count == 0) return null;
        int index = candidates.IndexOf(lastTarget);
        return candidates[(index + 1) % candidates.Count];
    }

    protected override bool Cast()
    {
        if (casting || NextTarget() == null) return false;
        StartCoroutine(Fire());
        return true;
    }

    private IEnumerator Fire()
    {
        casting = true; lastTarget = null;
        float start = Time.time;
        int nextShot = 0;
        while ((Time.time - start < duration || nextShot < shotCount) && !OwnerDead)
        {
            if (Time.timeScale <= 0f) { yield return null; continue; }
            while (nextShot < shotCount && Time.time - start + .0001f >= nextShot * duration / shotCount)
            {
                nextShot++;
                EnemyHealth target = NextTarget();
                if (target == null) { if (marker != null) marker.SetActive(false); continue; }
                lastTarget = target;
                Vector2 direction = ((Vector2)target.transform.position - Origin).normalized;
                if (direction == Vector2.zero) direction = LastMoveDirection;
                var arrow = SkillProjectile.Spawn(SecondArcherFx.Frame("Z", 0), Color.white, Origin,
                    direction, speed, range, SkillModifiers.Damage(damage), hitWidth * .5f, true, false, Enemies);
                arrow.transform.localScale = Vector3.one * 1.5f;
                SecondArcherFx.Animate(arrow.gameObject, "Z", this);
                arrow.OnHit += (p, at, e) => SecondArcherFx.Burst(at, this);
                GetComponent<PlayerAttackVisual>()?.PlayAttack(direction);
                if (marker == null)
                {
                    marker = new GameObject("Second Archer Target");
                    var sr = marker.AddComponent<SpriteRenderer>();
                    sr.sprite = Resources.Load<Sprite>("SecondArcher/Target"); sr.sortingOrder = 65;
                }
                marker.SetActive(true); marker.transform.position = target.transform.position;
            }
            if (marker != null && lastTarget != null && !lastTarget.IsDead) marker.transform.position = lastTarget.transform.position;
            else if (marker != null) marker.SetActive(false);
            yield return null;
        }
        casting = false;
        if (marker != null) marker.SetActive(false);
    }

    protected override void OnDisable()
    {
        base.OnDisable(); casting = false;
        if (marker != null) Destroy(marker);
    }
}
