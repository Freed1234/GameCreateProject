using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float invincibleTime = 1f;

    private int currentHealth;
    private float nextDamageTime;
    private bool isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
        Time.timeScale = 1f;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isDead)
            return;

        // 마지막 피해 이후 무적 시간이 남아 있으면 무시합니다.
        if (Time.time < nextDamageTime)
            return;

        // 적에게 닿았을 때만 피해를 받습니다.
        EnemyMovement enemy =
            other.GetComponent<EnemyMovement>();

        if (enemy == null)
            return;

        nextDamageTime = Time.time + invincibleTime;

        currentHealth = Mathf.Max(
            0, currentHealth - contactDamage
        );

        if (currentHealth == 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        PlayerMovement movement =
            GetComponent<PlayerMovement>();

        AutoAttack attack =
            GetComponent<AutoAttack>();

        if (movement != null)
            movement.enabled = false;

        if (attack != null)
            attack.enabled = false;

        Time.timeScale = 0f;
    }

    private void OnGUI()
    {
        GUI.Box(
            new Rect(10, 10, 180, 35),
            $"HP: {currentHealth} / {maxHealth}"
        );

        if (isDead)
        {
            GUI.Box(
                new Rect(
                    Screen.width / 2f - 140,
                    Screen.height / 2f - 50,
                    280,
                    100
                ),
                "GAME OVER\n\nStop Play to retry"
            );
        }
    }

    private void OnDisable()
    {
        // 테스트 종료 시 정지된 시간을 되돌립니다.
        Time.timeScale = 1f;
    }
}