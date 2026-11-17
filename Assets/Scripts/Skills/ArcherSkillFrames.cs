using System;
using System.Collections.Generic;
using UnityEngine;

// Sprites retain their authored muzzle, ground or impact pivots.
public sealed class ArcherSkillFrames : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
    private SpriteRenderer art;
    private Sprite[] frames;
    private int first, count, startOffset;
    private float started, duration;
    private bool holdLast;

    public static Sprite[] Load(string key)
    {
        if (cache.TryGetValue(key, out Sprite[] result)) return result;
        result = Resources.LoadAll<Sprite>("ArcherFx/" + key);
        Array.Sort(result, (a, b) => string.CompareOrdinal(a.name, b.name));
        cache.Add(key, result);
        return result;
    }

    public static ArcherSkillFrames Play(string key, Vector2 point, Quaternion rotation,
        float scale, int order, int first, int count, float duration, Transform parent = null, bool holdLast = false)
    {
        var obj = new GameObject("Archer FX " + key);
        obj.transform.SetPositionAndRotation(point, rotation);
        obj.transform.localScale = Vector3.one * scale;
        obj.transform.SetParent(parent, true);
        var art = obj.AddComponent<SpriteRenderer>();
        art.sortingOrder = order;
        var player = obj.AddComponent<ArcherSkillFrames>();
        player.holdLast = holdLast;
        player.Configure(art, key, first, count, duration);
        return player;
    }

    public static void AnimateFlight(SkillProjectile arrow)
    {
        var player = arrow.gameObject.AddComponent<ArcherSkillFrames>();
        player.Configure(arrow.GetComponent<SpriteRenderer>(), "C", 0, 3, .14f);
        player.holdLast = true;
    }

    private void Configure(SpriteRenderer renderer, string key, int first, int count, float duration)
    {
        art = renderer;
        frames = Load(key);
        this.first = first;
        this.count = count;
        Restart(duration);
    }

    public void Restart(float seconds, int offset = 0)
    {
        duration = Mathf.Max(.001f, seconds);
        startOffset = offset;
        started = Time.time;
        art.sprite = frames[first + offset];
        enabled = true;
    }

    public void Show(int index)
    {
        art.sprite = frames[first + (startOffset + index) % count];
        enabled = false;
    }

    private void Update()
    {
        int index = Mathf.FloorToInt((Time.time - started) * count / duration);
        if (index >= count)
        {
            if (holdLast) { Show(count - 1); }
            else Destroy(gameObject);
            return;
        }
        art.sprite = frames[first + (startOffset + index) % count];
    }
}
