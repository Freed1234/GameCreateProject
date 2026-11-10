using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(PlayerProgression))]
[DefaultExecutionOrder(300)]
public sealed class PlayerLevelUpEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Material effectMaterial;
    [SerializeField] private Sprite ringSprite;
    [SerializeField] private Sprite squareSprite;
    [SerializeField] private Sprite crossSprite;
    [SerializeField] private Color glowColor = new Color(.89f, 1f, .64f, .9f);
    [SerializeField] private Color particleColor = new Color(.68f, 1f, .45f, 1f);
    [SerializeField, Min(.01f)] private float duration = .65f;
    [SerializeField, Min(.01f)] private float glowDuration = .18f;
    [SerializeField, Min(.01f)] private float ringDuration = .45f;
    [SerializeField, Min(.01f)] private float ringWidth = 1.3f;
    [SerializeField, Min(.01f)] private float particleSize = .10f;
    [SerializeField, Min(0f)] private float particleTravel = .65f;
    private PlayerProgression progression;
    private GameObject root;
    private SpriteRenderer glow, ring;
    private readonly SpriteRenderer[] particles = new SpriteRenderer[10];
    private float elapsed, endTime;
    private bool playing;
    public bool IsPlaying => playing;

    private void OnEnable()
    {
        progression = GetComponent<PlayerProgression>();
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        progression.LeveledUp += OnLevelUp;
    }
    private void OnLevelUp(int levels)
    {
        if (levels <= 0 || progression.IsPlayerDead || !isActiveAndEnabled) return;
        if (playing)
        {
            endTime = Mathf.Min(1.2f, Mathf.Max(endTime, elapsed + .35f));
            return;
        }
        if (targetRenderer == null || effectMaterial == null || ringSprite == null
            || squareSprite == null || crossSprite == null) return;
        EnsureRenderers();
        elapsed = 0f;
        endTime = Mathf.Min(1.2f, Mathf.Max(.01f, duration));
        playing = true;
        root.SetActive(true);
        Render();
    }
    private void EnsureRenderers()
    {
        if (root != null) return;
        // Runtime-only, outside Player: never captured by PlayerHealth's alpha cache.
        root = new GameObject("LevelUpEffect (Runtime)");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        glow = Create("Glow", null);
        ring = Create("Ring", ringSprite);
        for (int i = 0; i < particles.Length; i++)
            particles[i] = Create("Pixel " + i, i % 2 == 0 ? crossSprite : squareSprite);
    }
    private SpriteRenderer Create(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = effectMaterial;
        return renderer;
    }
    private void LateUpdate()
    {
        if (!playing) return;
        if (progression == null || progression.IsPlayerDead || !progression.isActiveAndEnabled
            || (GameSession.Instance != null && GameSession.Instance.IsGameOver)) { Stop(); return; }
        if (Time.timeScale > 0f) elapsed += Time.deltaTime;
        if (elapsed >= endTime) { Stop(); return; }
        Render();
    }
    private void Render()
    {
        if (targetRenderer == null) { Stop(); return; }
        Bounds bounds = targetRenderer.bounds;
        Vector3 feet = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .06f, targetRenderer.transform.position.z);
        root.transform.position = feet;
        glow.sprite = targetRenderer.sprite;
        glow.flipX = targetRenderer.flipX;
        glow.flipY = targetRenderer.flipY;
        glow.transform.SetPositionAndRotation(targetRenderer.transform.position, targetRenderer.transform.rotation);
        glow.transform.localScale = targetRenderer.transform.lossyScale;
        glow.sortingLayerID = targetRenderer.sortingLayerID;
        glow.sortingOrder = targetRenderer.sortingOrder + 1;
        float alpha = targetRenderer.enabled ? targetRenderer.color.a : 0f;
        glow.color = WithAlpha(glowColor, Mathf.Clamp01(1f - elapsed / Mathf.Max(.01f, glowDuration)) * alpha);
        float r = Mathf.Clamp01(elapsed / Mathf.Max(.01f, ringDuration));
        ring.sortingLayerID = targetRenderer.sortingLayerID;
        ring.sortingOrder = targetRenderer.sortingOrder - 1;
        ring.transform.localScale = Vector3.one * (ringWidth * Mathf.Lerp(.4f, 1f, r) / ringSprite.bounds.size.x);
        ring.color = WithAlpha(particleColor, 1f - r);
        float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
        float fade = Mathf.Clamp01((endTime - elapsed) / .3f);
        for (int i = 0; i < particles.Length; i++)
        {
            float angle = (i * 137.508f + 20f) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * (.2f + t * particleTravel),
                .12f + (i % 3) * bounds.size.y * .22f + Mathf.Sin(angle) * .15f + t * particleTravel * .75f, 0);
            var p = particles[i];
            p.transform.localPosition = offset;
            p.transform.localScale = Vector3.one * (particleSize / p.sprite.bounds.size.x);
            p.sortingLayerID = targetRenderer.sortingLayerID;
            p.sortingOrder = targetRenderer.sortingOrder + 2;
            p.color = WithAlpha(particleColor, fade);
        }
    }
    private static Color WithAlpha(Color color, float factor) { color.a *= factor; return color; }
    private void Stop() { playing = false; elapsed = endTime = 0f; if (root != null) root.SetActive(false); }
    private void OnDisable() { if (progression != null) progression.LeveledUp -= OnLevelUp; Stop(); }
    private void OnDestroy() { if (root != null) Destroy(root); }
}
