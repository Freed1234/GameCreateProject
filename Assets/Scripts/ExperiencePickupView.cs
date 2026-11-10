using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public sealed class ExperiencePickupView : MonoBehaviour
{
    [SerializeField] private Sprite[] sparkleFrames = new Sprite[0];
    [SerializeField, Min(.01f)] private float smallWidth = .26f;
    [SerializeField, Min(.01f)] private float mediumWidth = .32f;
    [SerializeField, Min(.01f)] private float largeWidth = .40f;
    [Tooltip("Body width in sprite-local units, excluding margin and sparkles.")]
    [SerializeField, Min(.001f)] private float bodyWidth = .5f;
    [SerializeField, Min(0f)] private float framesPerSecond = 6f;
    private SpriteRenderer target;
    private float frame;
    private static uint sequence;

    private void OnEnable()
    {
        // Cosmetic phase must not consume the gameplay random sequence.
        frame = (++sequence * .61803399f % 1f) * Mathf.Max(1, sparkleFrames.Length);
        ApplyFrame();
    }
    public void Show(int amount)
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        float width = amount >= 20 ? largeWidth : amount >= 5 ? mediumWidth : smallWidth;
        bool configured = sparkleFrames.Length > 0 && sparkleFrames[0] != null;
        float basis = configured ? bodyWidth : target.sprite != null ? target.sprite.bounds.size.x : 1f;
        transform.localScale = Vector3.one * (width / Mathf.Max(.001f, basis));
        target.color = Color.white;
        ApplyFrame(); // Merging changes size without restarting animation.
    }
    private void Update()
    {
        var player = PlayerProgression.Instance;
        if (Time.timeScale <= 0f || (player != null && (player.IsPlayerDead || !player.isActiveAndEnabled))
            || (GameSession.Instance != null && GameSession.Instance.IsGameOver)) return;
        if (sparkleFrames.Length == 0) return;
        frame = (frame + Time.deltaTime * framesPerSecond) % sparkleFrames.Length;
        ApplyFrame();
    }
    private void ApplyFrame()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (sparkleFrames.Length == 0) return;
        Sprite sprite = sparkleFrames[Mathf.FloorToInt(frame) % sparkleFrames.Length];
        if (sprite != null) target.sprite = sprite;
    }
}
