using UnityEngine;

public sealed class ChestView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer chest;
    [SerializeField] private SpriteRenderer item;
    [SerializeField] private Sprite closedSprite, openSprite;
    [SerializeField] private Sprite healingSprite, magnetSprite;
    [SerializeField, Min(.1f)] private float chestWidth = 1.08f;
    [SerializeField, Min(.1f)] private float itemWidth = .48f;
    [SerializeField] private Sprite shadowSprite, glintSprite;
    [SerializeField] private Material shadowMaterial;
    private SpriteRenderer shadow, glint;
    public const float RewardHeight = .6f;

    private void Awake()
    {
        shadow = CreateDetail("Ground shadow", shadowSprite, shadowMaterial, -1);
        shadow.color = new Color(0f, 0f, 0f, .24f);
        Vector2 bounds = shadowSprite.bounds.size;
        shadow.transform.localScale = new Vector3(.9f / bounds.x, .2f / bounds.y, 1f);
        glint = CreateDetail("Lock glint", glintSprite, chest.sharedMaterial, 1);
        glint.transform.localScale = Vector3.one * (.14f / glintSprite.bounds.size.x);
    }

    private SpriteRenderer CreateDetail(string label, Sprite sprite, Material material, int order)
    {
        var detail = new GameObject(label).AddComponent<SpriteRenderer>();
        detail.transform.SetParent(transform, false);
        detail.sprite = sprite;
        detail.sharedMaterial = material;
        detail.sortingLayerID = chest.sortingLayerID;
        detail.sortingOrder = chest.sortingOrder + order;
        return detail;
    }

    public void SetAttractionVisual(bool showChest, bool attracting, Vector2 position)
    {
        chest.enabled = showChest;
        shadow.enabled = showChest;
        item.enabled = showChest || attracting;
        if (!showChest) glint.enabled = false;
        if (attracting && item.gameObject.activeSelf) item.transform.position = position;
    }
    public void Display(bool opened, bool claimed, ChestManager.Reward reward, float openingProgress, float opacity = 1f)
    {
        Color tint = chest.color;
        tint.a = opacity;
        chest.color = tint;
        if (shadow != null) shadow.color = new Color(0f, 0f, 0f, .24f * opacity);
        chest.sprite = opened && openingProgress >= .5f ? openSprite : closedSprite;
        chest.transform.localScale = Vector3.one * chestWidth / chest.sprite.bounds.size.x;
        if (glint != null)
        {
            Vector3 location = transform.position;
            float phase = Mathf.Repeat(Time.time + location.x * .713f + location.y * .371f, 2.5f);
            glint.enabled = !opened && phase < .25f;
            if (glint.enabled)
            {
                // Lock center is (65, 114) from the top left of the 128 x 160 artwork.
                Vector2 lockPixel = new Vector2(65f, 46f);
                glint.transform.position = chest.transform.TransformPoint((lockPixel - closedSprite.pivot) / closedSprite.pixelsPerUnit);
                glint.color = new Color(1f, .87f, .45f, Mathf.Sin(Mathf.PI * phase / .25f));
            }
        }
        item.gameObject.SetActive(opened && !claimed);
        if (opened && !claimed)
        {
            item.sprite = reward == ChestManager.Reward.Heal ? healingSprite : magnetSprite;
            item.transform.localScale = Vector3.one * itemWidth / item.sprite.bounds.size.x;
            item.transform.localPosition = new Vector3(0, Mathf.Lerp(RewardHeight * .5f, RewardHeight, openingProgress), 0);
        }
    }
}

