using System.Collections.Generic;
using UnityEngine;

public sealed class GoblinStone : MonoBehaviour
{
    public const float Diameter = 0.18f;
    private const float Radius = Diameter * 0.5f;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
    private Vector2 direction;
    private float remaining, speed;
    private int damage;
    private static readonly int ObstacleLayer = LayerMask.NameToLayer("Obstacle");

    public static void Launch(Vector2 origin, Vector2 direction, float distance, float speed, int damage)
    {
        GameObject obj = new GameObject("Goblin stone");
        obj.transform.position = origin;
        obj.transform.localScale = Vector3.one * Diameter;
        SpriteRenderer art = obj.AddComponent<SpriteRenderer>();
        art.sprite = SkillUtil.CircleSprite();
        art.color = new Color(0.63f, 0.58f, 0.55f);
        art.sortingOrder = 4;
        GoblinStone stone = obj.AddComponent<GoblinStone>();
        stone.direction = direction;
        stone.remaining = distance;
        stone.speed = speed;
        stone.damage = damage;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;
        float step = Mathf.Min(remaining, speed * Time.deltaTime);
        Vector2 start = transform.position;
        Physics2D.CircleCast(start, Radius, direction, ContactFilter2D.noFilter, hits, step);
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D collider = hit.collider;
            if (collider.gameObject.layer == ObstacleLayer && !collider.isTrigger)
            {
                Destroy(gameObject);
                return;
            }
            PlayerHealth player = collider.GetComponentInParent<PlayerHealth>();
            if (player == null || collider.isTrigger) continue;
            player.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        transform.position = start + direction * step;
        remaining -= step;
        if (remaining <= 0f) Destroy(gameObject);
    }
}

