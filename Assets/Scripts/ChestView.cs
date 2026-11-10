using UnityEngine;

public sealed class ChestView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer chest;
    [SerializeField] private SpriteRenderer item;
    [SerializeField] private Sprite closedSprite, openSprite;
    [SerializeField] private Sprite healingSprite, magnetSprite, chargeSprite;
    [SerializeField, Min(.1f)] private float chestWidth = .9f;
    [SerializeField, Min(.1f)] private float itemWidth = .4f;
    public void SetAttractionVisual(bool showChest, bool attracting, Vector2 position)
    {
        chest.enabled = showChest;
        if (attracting && item.gameObject.activeSelf) item.transform.position = position;
    }
    public void Display(bool opened, bool claimed, ChestManager.Reward reward, float openingProgress)
    {
        chest.sprite = opened && openingProgress >= .5f ? openSprite : closedSprite;
        chest.transform.localScale = Vector3.one * chestWidth / chest.sprite.bounds.size.x;
        item.gameObject.SetActive(opened && !claimed);
        if (opened && !claimed)
        {
            item.sprite = reward == ChestManager.Reward.Heal ? healingSprite : reward == ChestManager.Reward.Magnet ? magnetSprite : chargeSprite;
            item.transform.localScale = Vector3.one * itemWidth / item.sprite.bounds.size.x;
            item.transform.localPosition = new Vector3(0, .25f + .25f * openingProgress, 0);
        }
    }
}
