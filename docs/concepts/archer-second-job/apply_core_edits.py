"""One-shot source edits preserving the original BOM, encoding and newline style."""
from pathlib import Path

def edit(name, pairs):
    p=Path('Assets/Scripts')/name
    raw=p.read_bytes()
    encoding='utf-16' if raw[:2] in (b'\xfe\xff',b'\xff\xfe') else 'utf-8-sig' if raw.startswith(b'\xef\xbb\xbf') else 'utf-8'
    s=raw.decode(encoding); newline='\r\n' if '\r\n' in s else '\n'; s=s.replace('\r\n','\n')
    for old,new in pairs:
        assert old in s, (name,old[:60])
        s=s.replace(old,new,1)
    data=s.replace('\n',newline)
    if raw[:2]==b'\xfe\xff': p.write_bytes(b'\xfe\xff'+data.encode('utf-16-be'))
    else: p.write_bytes(data.encode(encoding))

edit('EnemyHealth.cs', [('    public bool IsDead => currentHealth <= 0;', '''    public bool IsDead => currentHealth <= 0;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    // Spawner calls this once after Awake, before combat begins.
    public void InitializeSpawnHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = maxHealth;
    }''')])
edit('EnemySpawner.cs', [('    private float timer;', '''    [Header("생존 시간별 체력 (2분 간격, 마지막 값 유지)")]
    [SerializeField] private int[] normalHealth = { 3, 6, 12, 18, 24, 30 };
    [SerializeField] private int[] goblinHealth = { 3, 6, 12, 24, 30, 36 };

    public int HealthAt(float seconds, bool isGoblin)
    {
        int[] values = isGoblin ? goblinHealth : normalHealth;
        if (values == null || values.Length == 0) return 3;
        return Mathf.Max(1, values[Mathf.Clamp(Mathf.FloorToInt(seconds / 120f), 0, values.Length - 1)]);
    }

    private float timer;'''), ('            enemy.SetPlayer(player);', '''            enemy.GetComponent<EnemyHealth>()?.InitializeSpawnHealth(
                HealthAt(GameSession.Instance != null ? GameSession.Instance.SurvivalTime : 0f, prefab == goblinPrefab));
            enemy.SetPlayer(player);''')])
edit('EnemyMovement.cs', [('    private float steeringJitter;', '''    private float steeringJitter;
    private Vector2 pullVelocity, knockVelocity;
    private float pullUntil, knockUntil;
    private readonly System.Collections.Generic.List<RaycastHit2D> forcedHits = new System.Collections.Generic.List<RaycastHit2D>();

    public void PullTowards(Vector2 center, float speed)
    {
        Vector2 offset = center - (Vector2)transform.position;
        pullVelocity = offset.normalized * Mathf.Min(speed, Mathf.Max(0f, offset.magnitude - .3f) / Time.fixedDeltaTime);
        pullUntil = Time.time + Time.fixedDeltaTime * 2f;
    }

    public void Push(Vector2 displacement, float duration)
    {
        knockVelocity = displacement / Mathf.Max(.01f, duration);
        knockUntil = Time.time + duration;
    }

    private Vector2 SafeForcedVelocity(Vector2 velocity)
    {
        float length = velocity.magnitude * Time.fixedDeltaTime;
        if (length < .00001f) return Vector2.zero;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(SkillUtil.ObstacleMask); filter.useTriggers = false;
        body.Cast(velocity.normalized, filter, forcedHits, length);
        foreach (var hit in forcedHits) length = Mathf.Min(length, Mathf.Max(0f, hit.distance - .01f));
        var field = EnemyFlowField.Instance;
        if (field != null && field.Ready && !field.Contains(body.position + velocity.normalized * length)) return Vector2.zero;
        return velocity.normalized * (length / Time.fixedDeltaTime);
    }'''), ('        EnemyFlowField field = EnemyFlowField.Instance;', '''        if (Time.time < knockUntil)
        {
            body.linearVelocity = SafeForcedVelocity(knockVelocity);
            return;
        }
        EnemyFlowField field = EnemyFlowField.Instance;'''), ('                : Vector2.zero;\n            return;', '''                : Vector2.zero;
            if (Time.time < pullUntil) body.linearVelocity += SafeForcedVelocity(pullVelocity);
            return;'''), ('if (!hasWaypoint) { body.linearVelocity = Vector2.zero; return; }', 'if (!hasWaypoint) { body.linearVelocity = Time.time < pullUntil ? SafeForcedVelocity(pullVelocity) : Vector2.zero; return; }'), ('        body.linearVelocity = offset.normalized * speed;', '''        body.linearVelocity = offset.normalized * speed;
        if (Time.time < pullUntil) body.linearVelocity += SafeForcedVelocity(pullVelocity);'''), ('        hasWaypoint = false; nextSteering = 0f; fieldVersion = -1;', '''        hasWaypoint = false; nextSteering = 0f; fieldVersion = -1;
        pullUntil = knockUntil = 0f;''')])
