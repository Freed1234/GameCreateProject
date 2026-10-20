using UnityEngine;

// 적을 잠깐 멈추게 합니다 (기절). 적의 EnemyMovement를 잠시 끄는 방식이라 팀 코드를 고치지 않습니다.
public class SkillStun : MonoBehaviour
{
    private EnemyMovement movement;
    private Rigidbody2D body;
    private SpriteRenderer[] sprites;
    private float remaining;

    public static void Apply(EnemyHealth enemy, float seconds)
    {
        if (enemy == null || seconds <= 0f)
            return;
        SkillStun stun = enemy.GetComponent<SkillStun>();
        if (stun == null)
            stun = enemy.gameObject.AddComponent<SkillStun>();
        stun.remaining = Mathf.Max(stun.remaining, seconds);
    }

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        body = GetComponent<Rigidbody2D>();
        sprites = GetComponentsInChildren<SpriteRenderer>();
        if (movement != null)
            movement.enabled = false;
        Tint(new Color(0.6f, 0.85f, 1f));
    }

    private void Update()
    {
        if (body != null)
            body.linearVelocity = Vector2.zero;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
            Destroy(this);
    }

    private void OnDestroy()
    {
        if (movement != null)
            movement.enabled = true;
        Tint(Color.white);
    }

    private void Tint(Color color)
    {
        if (sprites == null)
            return;
        foreach (SpriteRenderer sr in sprites)
        {
            if (sr == null)
                continue;
            Color c = color;
            c.a = sr.color.a;
            sr.color = c;
        }
    }
}
