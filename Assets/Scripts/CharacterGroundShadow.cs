using UnityEngine;

// A flat ground decal: no colliders, sprite animation changes or combat tint changes.
[DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(400)]
public sealed class CharacterGroundShadow : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite shadowSprite;
    [SerializeField] private Material shadowMaterial;
    [Tooltip("Local foot position. Fixed across animation frames to avoid jitter.")]
    [SerializeField] private Vector2 footOffset = new Vector2(0f, -.4f);
    [SerializeField, Min(.01f)] private float width = .8f;
    [SerializeField, Min(.01f)] private float height = .25f;
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, .28f);
    [SerializeField, Min(1)] private int orderBehindCharacter = 3;
    private SpriteRenderer shadow;

    private void Start()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (shadowSprite == null || shadowMaterial == null)
        { Debug.LogError("Character shadow: run Tools > Game UI > Setup Character Shadows.", this); enabled = false; return; }
        // Separate runtime object keeps this renderer out of PlayerHealth's blink cache,
        // character portrait discovery and any character bounds calculations.
        var go = new GameObject(name + " GroundShadow (Runtime)");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        shadow = go.AddComponent<SpriteRenderer>();
        shadow.sprite = shadowSprite;
        shadow.sharedMaterial = shadowMaterial;
        Refresh();
    }
    private void LateUpdate() { Refresh(); }
    private void Refresh()
    {
        if (shadow == null) return;
        bool visible = isActiveAndEnabled && targetRenderer != null && targetRenderer.enabled && targetRenderer.gameObject.activeInHierarchy;
        shadow.enabled = visible;
        if (!visible) return;
        Vector3 scale = targetRenderer.transform.lossyScale;
        Vector3 position = targetRenderer.transform.position;
        // Negative scale/flip must not move the ground contact to the other side.
        position += new Vector3(footOffset.x * Mathf.Abs(scale.x), footOffset.y * Mathf.Abs(scale.y), 0f);
        shadow.transform.SetPositionAndRotation(position, Quaternion.identity);
        shadow.transform.localScale = new Vector3(
            Mathf.Max(.01f, width) * Mathf.Abs(scale.x) / shadowSprite.bounds.size.x,
            Mathf.Max(.01f, height) * Mathf.Abs(scale.y) / shadowSprite.bounds.size.y, 1f);
        shadow.color = shadowColor;
        shadow.sortingLayerID = targetRenderer.sortingLayerID;
        shadow.sortingOrder = targetRenderer.sortingOrder - Mathf.Max(1, orderBehindCharacter);
    }
    private void OnEnable() { Refresh(); }
    private void OnDisable() { if (shadow != null) shadow.enabled = false; }
    private void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }
}
