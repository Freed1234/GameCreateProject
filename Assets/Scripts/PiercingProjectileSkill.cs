using System.Collections.Generic;
using UnityEngine;

public class PiercingProjectileSkill : MonoBehaviour, ISkillCooldown
{
    [Header("연결")]
    [SerializeField] private Transform enemyContainer;
    [SerializeField] private MovingProjectile projectilePrefab;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private Camera gameCamera;

    [SerializeField] private bool preferMultipleTargets = true;
    [Header("공격")]
    [SerializeField, Min(0.02f)] private float cooldown = 5f;
    [SerializeField, Min(0.1f)] private float attackRange = 8f;
    [SerializeField, Min(1)] private int damage = 10;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 12f;
    [SerializeField, Min(0.01f)] private float projectileRadius = 0.06f;

    [Header("발동 방식")]
    [SerializeField] private KeyCode skillKey = KeyCode.Space;
    [SerializeField] private bool showCooldown = true;

    [Header("발사음 (선택)")]
    [SerializeField] private AudioClip shotSound;
    [SerializeField, Range(0f, 1f)] private float shotVolume = 0.4f;

    private Transform owner;
    private AudioSource shotSource;
    private ContactFilter2D obstacleFilter;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
    private float nextShotTime;
    public float Cooldown => cooldown;
    public float CooldownRemaining => Mathf.Max(0f, nextShotTime - Time.time);
    public string KeyLabel => skillKey.ToString();

    private PlayerAttackVisual attackVisual;

    private void Awake()
    {
        attackVisual = GetComponentInParent<PlayerAttackVisual>();
        if (gameCamera == null)
            gameCamera = Camera.main;
        PlayerHealth player = GetComponentInParent<PlayerHealth>();
        owner = player != null ? player.transform : transform;
        if (obstacleLayers.value == 0)
            obstacleLayers = LayerMask.GetMask("Obstacle");

        shotSource = gameObject.AddComponent<AudioSource>();
        shotSource.playOnAwake = false;
        shotSource.loop = false;
        shotSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || Time.time < nextShotTime
            || enemyContainer == null || projectilePrefab == null)
            return;

        if (gameCamera == null)
            gameCamera = Camera.main;
        if (gameCamera == null)
            return;
        if (!KeyBindings.Down(GameAction.Skill1))
            return;

        // 장애물 뒤의 적은 조준하지 않습니다.
        obstacleFilter = new ContactFilter2D();
        obstacleFilter.SetLayerMask(obstacleLayers);
        obstacleFilter.useTriggers = false;

        Vector2 origin = transform.position;
        Vector2 aim;
        if (preferMultipleTargets)
        {
            var result = SkillTargeting.Piercing(enemyContainer, owner, origin, attackRange, projectileRadius, obstacleLayers, Vector2.right);
            if (!result.Found) return;
            aim = result.Aim;
        }
        else
        {
        Transform target = null;
        float nearest = float.PositiveInfinity;
        foreach (Transform child in enemyContainer)
        {
            if (!child.gameObject.activeInHierarchy)
                continue;
            EnemyHealth health = child.GetComponent<EnemyHealth>();
            if (health == null || health.IsDead || !health.isActiveAndEnabled)
                continue;

            Vector2 offset = (Vector2)child.position - origin;
            float distance = offset.magnitude;
            if (distance > attackRange || distance >= nearest)
                continue;
            Vector2 direction = distance > 0.0001f
                ? offset / distance : Vector2.right;

            Physics2D.CircleCast(origin, Mathf.Max(0.01f, projectileRadius),
                direction, obstacleFilter, hits, distance);
            bool blocked = false;
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(owner))
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked)
                continue;

            nearest = distance;
            target = child;
        }

        if (target == null)
        {
            return;
        }

        aim = (Vector2)target.position - origin;
        aim = aim.sqrMagnitude > 0.000001f ? aim.normalized : Vector2.right;
        }
        Quaternion rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);

        // 부모를 지정하지 않아 플레이어와 독립적으로 날아갑니다.
        MovingProjectile projectile = Instantiate(
            projectilePrefab, transform.position, rotation);
        projectile.Launch(owner, aim, projectileSpeed,
            attackRange, damage, projectileRadius, obstacleLayers, gameCamera, true);

        attackVisual?.PlayAttack(aim);

        // 발사 시에는 피해를 주지 않습니다.
        nextShotTime = Time.time + Mathf.Max(0.02f, cooldown);
        if (shotSound != null)
            shotSource.PlayOneShot(shotSound, shotVolume);
    }
    private void OnGUI()
    {
        if (!showCooldown) return;
        float remaining = Mathf.Max(0f, nextShotTime - Time.time);
        string label = remaining > 0f
            ? $"Piercing skill: {remaining:F1}s"
            : $"Piercing skill: {skillKey} READY";
        GUI.Box(new Rect(10, 55, 260, 30), label);
    }
}





