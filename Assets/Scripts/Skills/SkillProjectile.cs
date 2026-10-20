using System;
using System.Collections.Generic;
using UnityEngine;

// 스킬용 화살. 직선으로 날아가며 적에게 피해를 줍니다.
//  - pierce      : 적을 관통하는지 (관통이면 같은 적은 한 번만 맞음)
//  - ignoreWalls : 돌/나무를 무시하는지 (궁극기용)
//  - OnHit       : 적에게 맞을 때 실행 (예: 폭발, 분열). 맞은 위치와 적을 넘겨줌
// 사거리를 넘거나 카메라 밖으로 나가면 사라집니다.
public class SkillProjectile : MonoBehaviour
{
    public Action<SkillProjectile, Vector2, EnemyHealth> OnHit;

    public Vector2 Direction => direction;

    // 이 적은 맞히지 않음 (분열된 화살이 방금 맞은 적에게 바로 다시 맞는 것 방지)
    public void Ignore(EnemyHealth enemy)
    {
        if (enemy != null)
            alreadyHit.Add(enemy);
    }

    private Vector2 direction;
    private float speed;
    private float range;
    private float travelled;
    private float radius;
    private int damage;
    private bool pierce;
    private bool ignoreWalls;
    private int obstacleMask;
    private Transform enemies;
    private readonly HashSet<EnemyHealth> alreadyHit = new HashSet<EnemyHealth>();

    public static SkillProjectile Spawn(Sprite sprite, Color color, Vector2 position, Vector2 direction,
        float speed, float range, int damage, float radius, bool pierce, bool ignoreWalls,
        Transform enemyContainer, float scale = 1f, int sortingOrder = 60)
    {
        GameObject obj = new GameObject("SkillArrow");
        obj.transform.position = position;
        obj.transform.rotation = SkillUtil.RotationFor(direction);
        obj.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : SkillUtil.ArrowSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        SkillProjectile p = obj.AddComponent<SkillProjectile>();
        p.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        p.speed = speed;
        p.range = range;
        p.damage = damage;
        p.radius = radius;
        p.pierce = pierce;
        p.ignoreWalls = ignoreWalls;
        p.obstacleMask = SkillUtil.ObstacleMask;
        p.enemies = enemyContainer;
        return p;
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;
        if (step <= 0f)
            return; // 일시정지 중

        Vector2 position = (Vector2)transform.position + direction * step;
        transform.position = position;
        travelled += step;

        if (!ignoreWalls && SkillUtil.HitsObstacle(position, radius * 0.5f, obstacleMask))
        {
            Destroy(gameObject);
            return;
        }

        System.Collections.Generic.List<EnemyHealth> list = SkillUtil.EnemiesNow(enemies);
        for (int i = 0; i < list.Count; i++)
        {
            EnemyHealth enemy = list[i];
            if (enemy == null || !enemy.isActiveAndEnabled || alreadyHit.Contains(enemy))
                continue;
            if (Vector2.Distance(position, enemy.transform.position) > radius + SkillUtil.EnemyRadius)
                continue;

            alreadyHit.Add(enemy);
            enemy.TakeDamage(damage);
            OnHit?.Invoke(this, position, enemy);

            if (!pierce)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (travelled >= range || !SkillUtil.IsInsideCamera(position, 2f))
            Destroy(gameObject);
    }
}
