using System.Collections.Generic;
using UnityEngine;

// Selected pixel frames are shared assets; runtime effects own no generated textures.
public sealed class SecondArcherFx : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
    private SpriteRenderer rendererSource;
    private MonoBehaviour source;
    private PlayerHealth owner;
    private string kind;
    private float age, lifetime;
    private static Sprite dashIcon;
    public static Sprite DashIcon => dashIcon != null ? dashIcon : (dashIcon = Resources.Load<Sprite>("SecondArcher/ShiftIcon"));
    public static Sprite Frame(string name, int index)
    {
        if (!cache.TryGetValue(name, out var frames))
        {
            frames = Resources.LoadAll<Sprite>("SecondArcher/" + name);
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            cache[name] = frames;
        }
        return frames.Length == 0 ? null : frames[index % frames.Length];
    }
    public static void Animate(GameObject obj, string name, MonoBehaviour source, float lifetime = 0f)
    {
        var fx = obj.AddComponent<SecondArcherFx>();
        fx.kind = name; fx.source = source; fx.owner = source.GetComponentInParent<PlayerHealth>();
        fx.lifetime = lifetime;
        fx.rendererSource = obj.GetComponent<SpriteRenderer>();
        if (fx.rendererSource == null) fx.rendererSource = obj.AddComponent<SpriteRenderer>();
        fx.rendererSource.sprite = Frame(name, 0); fx.rendererSource.sortingOrder = 63;
    }
    public static GameObject Spawn(string name, Vector2 at, MonoBehaviour source, float lifetime)
    {
        var obj = new GameObject("Second Archer " + name); obj.transform.position = at;
        Animate(obj, name, source, lifetime); return obj;
    }
    public static void Burst(Vector2 at, MonoBehaviour source) { Spawn("Hit", at, source, .25f); }
    private void Update()
    {
        if (source == null || !source.isActiveAndEnabled || owner == null || owner.IsDead)
        { Destroy(gameObject); return; }
        if (Time.timeScale <= 0f) return;
        age += Time.deltaTime;
        if (lifetime > 0f && age >= lifetime) { Destroy(gameObject); return; }
        rendererSource.sprite = Frame(kind, Mathf.FloorToInt(age * 16f));
        if (lifetime > 0f)
            rendererSource.color = new Color(1, 1, 1, Mathf.Clamp01((lifetime - age) / .15f));
    }
}
