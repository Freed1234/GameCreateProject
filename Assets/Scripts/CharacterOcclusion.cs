using UnityEngine;

[DefaultExecutionOrder(450), DisallowMultipleComponent, RequireComponent(typeof(WorldDepthSort))]
public sealed class CharacterOcclusion : MonoBehaviour
{
    [SerializeField] private Material silhouetteMaterial;
    [SerializeField] private Color fillColor = new Color(.65f, .9f, 1f, .6f);
    [SerializeField] private Color outlineColor = new Color(.9f, 1f, 1f, .85f);
    [SerializeField, Range(0f, 3f)] private float outlinePixels = 1f;
    [SerializeField, Min(.001f)] private float transitionTime = .1f;
    private SpriteRenderer body, overlay;
    private WorldDepthSort depth;
    private PlayerHealth player;
    private EnemyHealth enemy;
    private MaterialPropertyBlock properties;
    private float visibility;

    private void Start()
    {
        body = GetComponent<SpriteRenderer>();
        depth = GetComponent<WorldDepthSort>();
        player = GetComponent<PlayerHealth>();
        enemy = GetComponent<EnemyHealth>();
        properties = new MaterialPropertyBlock();
        // A separate object avoids the player's sprite/blink cache and portrait discovery.
        var go = new GameObject(name + " Occlusion (Runtime)");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        overlay = go.AddComponent<SpriteRenderer>();
        overlay.sharedMaterial = silhouetteMaterial;
        overlay.sortingLayerName = "WorldSilhouettes";
    }

    private void LateUpdate()
    {
        if (overlay == null) return;
        bool alive = body.enabled && body.gameObject.activeInHierarchy
            && (player == null || !player.IsDead) && (enemy == null || !enemy.IsDead);
        overlay.enabled = alive;
        if (!alive) return;
        bool covered = false;
        foreach (var obstacle in WorldDepthSort.Obstacles)
        {
            if (obstacle.Order <= depth.Order) continue;
            foreach (var renderer in obstacle.Renderers)
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
                    && renderer.bounds.Intersects(body.bounds)) { covered = true; break; }
            if (covered) break;
        }
        visibility = Mathf.MoveTowards(visibility, covered ? 1f : 0f, Time.deltaTime / transitionTime);
        overlay.enabled = visibility > 0f;
        overlay.sprite = body.sprite;
        overlay.flipX = body.flipX;
        overlay.flipY = body.flipY;
        overlay.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
        overlay.transform.localScale = body.transform.lossyScale;
        overlay.color = new Color(1f, 1f, 1f, body.color.a);
        overlay.sortingOrder = depth.Order;
        properties.SetColor("_FillColor", fillColor);
        properties.SetColor("_OutlineColor", outlineColor);
        properties.SetFloat("_OutlinePixels", outlinePixels);
        properties.SetFloat("_Visibility", visibility);
        properties.SetFloat("_ActorOrder", depth.Order);
        overlay.SetPropertyBlock(properties);
    }

    private void OnDisable() { visibility = 0f; if (overlay != null) overlay.enabled = false; }
    private void OnDestroy() { if (overlay != null) Destroy(overlay.gameObject); }
}
