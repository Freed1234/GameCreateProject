using System;
using UnityEngine;

// Visuals only: never modifies movement, health, colliders, transform or tint.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
public sealed class EnemyAppearance : MonoBehaviour
{
    [Serializable]
    public class Variant
    {
        public string name;
        public Sprite[] frames = Array.Empty<Sprite>();
        public bool IsValid {
            get {
                if (frames == null || frames.Length == 0) return false;
                foreach (var frame in frames) if (frame == null) return false;
                return true;
            }
        }
    }

    [SerializeField] private Variant[] slimes = Array.Empty<Variant>();
    [SerializeField] private Variant[] mushrooms = Array.Empty<Variant>();
    [SerializeField, Range(0f,1f)] private float slimeChance = 0.5f;
    [SerializeField, Min(0.1f)] private float slimeFramesPerSecond = 10f;
    [SerializeField, Min(0.1f)] private float mushroomFramesPerSecond = 10f;
    [Header("표시 크기 — 프리팹 저장 후 Setup Random Enemy Appearance 재실행")]
    [SerializeField, Min(0.1f)] private float slimeWidth = 1f;
    [SerializeField, Min(0.1f)] private float mushroomWidth = 1f;
    [SerializeField, Min(0f)] private float movementThreshold = 0.01f;

    public bool IsSlime { get; private set; }
    public int VariantIndex { get; private set; } = -1;
    public int FrameIndex { get; private set; }
    public float SlimeWidth => slimeWidth;
    public float MushroomWidth => mushroomWidth;
    private SpriteRenderer target;
    private Rigidbody2D body;
    private EnemyMovement movement;
    private EnemyHealth health;
    private Variant selected;
    private float elapsed;

    private void Awake() {
        target = GetComponent<SpriteRenderer>(); body = GetComponent<Rigidbody2D>();
        movement = GetComponent<EnemyMovement>(); health = GetComponent<EnemyHealth>();
        // Once per instantiated enemy. Re-enabling a stunned/hidden object never rerolls it.
        bool slime;
        VariantIndex = SelectVariant(slimes,mushrooms,slimeChance,UnityEngine.Random.value,UnityEngine.Random.value,out slime);
        IsSlime = slime;
        if (VariantIndex < 0) return; // Preserve the existing sprite if all assets are missing.
        selected = (IsSlime ? slimes : mushrooms)[VariantIndex];
        target.sprite = selected.frames[0];
    }

    public static int SelectVariant(Variant[] slimes, Variant[] mushrooms, float chance, float typeRoll, float colorRoll, out bool slime) {
        int a = CountValid(slimes), b = CountValid(mushrooms);
        slime = a > 0 && (b == 0 || chance >= 1f || typeRoll < Mathf.Clamp01(chance));
        var candidates = slime ? slimes : mushrooms;
        int count = slime ? a : b;
        if (count == 0) return -1;
        int pick = Mathf.Min(count-1,Mathf.FloorToInt(Mathf.Clamp01(colorRoll)*count));
        for (int i=0;i<candidates.Length;i++) if (candidates[i]!=null && candidates[i].IsValid && pick-- == 0) return i;
        return -1;
    }

    private static int CountValid(Variant[] items) {
        int count=0;
        if (items != null) foreach(var item in items) if (item!=null && item.IsValid) count++;
        return count;
    }

    private void LateUpdate() {
        if (selected == null || Time.timeScale <= 0f || (health != null && health.IsDead)
            || (movement != null && !movement.isActiveAndEnabled)) return;
        Vector2 velocity = body.linearVelocity;
        float threshold = Mathf.Max(0.001f,movementThreshold);
        if (velocity.sqrMagnitude <= threshold*threshold) return;
        if (Mathf.Abs(velocity.x) > threshold) target.flipX = velocity.x < 0f;
        elapsed += Time.deltaTime * Mathf.Max(0.1f,IsSlime ? slimeFramesPerSecond : mushroomFramesPerSecond);
        int advance = Mathf.FloorToInt(elapsed);
        if (advance == 0) return;
        elapsed -= advance;
        FrameIndex = (FrameIndex+advance)%selected.frames.Length;
        if (selected.frames[FrameIndex] != null) target.sprite = selected.frames[FrameIndex];
    }
}
