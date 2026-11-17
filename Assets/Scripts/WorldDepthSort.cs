using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(350), DisallowMultipleComponent]
public sealed class WorldDepthSort : MonoBehaviour
{
    public static readonly List<WorldDepthSort> Obstacles = new List<WorldDepthSort>();
    [SerializeField] private Vector2 footOffset;
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private bool obstacle;
    public SpriteRenderer[] Renderers => renderers;
    public int Order { get; private set; }
    public Vector3 Foot => transform.TransformPoint(footOffset);

    private void OnEnable() { if (obstacle) Obstacles.Add(this); }
    private void OnDisable() { if (obstacle) Obstacles.Remove(this); }
    private void LateUpdate()
    {
        Camera camera = GameCamera.Get();
        if (camera == null) return;
        Order = Mathf.Clamp(Mathf.RoundToInt((camera.transform.position.y - Foot.y) * 100f), -30000, 30000);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.sortingLayerName = "WorldActors";
            renderer.sortingOrder = Order;
        }
    }
}
