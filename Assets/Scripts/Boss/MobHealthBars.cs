using System.Collections.Generic;
using UnityEngine;

// 일반 몹 머리 위에 작은 HP바를 붙입니다. (보스는 화면 위쪽 큰 체력바를 쓰므로 제외)
// 팀 코드(EnemyHealth, EnemySpawner)는 수정하지 않고, 적 목록을 보고 바를 따로 만들어 따라다니게 합니다.
[DisallowMultipleComponent]
public class MobHealthBars : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(0.7f, 0.09f);
    [Tooltip("몹 그림 위로 띄우는 간격")]
    [SerializeField, Min(0f)] private float gap = 0.12f;
    [Tooltip("체력이 가득 찬 몹도 표시할지")]
    [SerializeField] private bool showWhenFull = true;
    [SerializeField] private Color fillColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.7f);
    [SerializeField] private int sortingOrder = 80;

    private class Bar
    {
        public Transform root;
        public Transform fill;
        public float height;
    }

    private readonly Dictionary<EnemyHealth, Bar> bars = new Dictionary<EnemyHealth, Bar>();
    private readonly List<EnemyHealth> removeBuffer = new List<EnemyHealth>();
    private readonly HashSet<EnemyHealth> seen = new HashSet<EnemyHealth>();

    private void LateUpdate()
    {
        if (!BossUtil.CanReadEnemyHp)
            return;

        seen.Clear();
        foreach (EnemyHealth enemy in SkillUtil.Enemies(SkillUtil.EnemyContainer(null)))
        {
            if (enemy.GetComponent<BossController>() != null || enemy.GetComponent<BossHitProxy>() != null)
                continue;
            seen.Add(enemy);

            if (!bars.TryGetValue(enemy, out Bar bar))
            {
                bar = CreateBar(enemy);
                bars.Add(enemy, bar);
            }

            BossUtil.TryGetHp(enemy, out int hp, out int max);
            float ratio = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;
            bool visible = showWhenFull || ratio < 1f;
            if (bar.root.gameObject.activeSelf != visible)
                bar.root.gameObject.SetActive(visible);

            bar.root.position = (Vector2)enemy.transform.position + Vector2.up * bar.height;
            bar.fill.localScale = new Vector3(size.x * ratio, size.y, 1f);
        }

        // 죽은 몹의 바 정리
        removeBuffer.Clear();
        foreach (KeyValuePair<EnemyHealth, Bar> pair in bars)
        {
            if (pair.Key == null || !seen.Contains(pair.Key))
                removeBuffer.Add(pair.Key);
        }
        foreach (EnemyHealth key in removeBuffer)
        {
            if (bars[key].root != null)
                Destroy(bars[key].root.gameObject);
            bars.Remove(key);
        }
    }

    private Bar CreateBar(EnemyHealth enemy)
    {
        // 몹 그림 높이 기준으로 머리 위 위치 계산
        float top = 0.45f;
        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            top = sr.bounds.max.y - enemy.transform.position.y;

        GameObject root = new GameObject("MobHpBar");
        root.transform.SetParent(transform, false);

        SpriteRenderer back = new GameObject("Back").AddComponent<SpriteRenderer>();
        back.transform.SetParent(root.transform, false);
        back.sprite = BossUtil.Square();
        back.color = backColor;
        back.sortingOrder = sortingOrder;
        back.transform.localScale = new Vector3(size.x + 0.04f, size.y + 0.04f, 1f);

        SpriteRenderer fill = new GameObject("Fill").AddComponent<SpriteRenderer>();
        fill.transform.SetParent(root.transform, false);
        fill.sprite = BossUtil.SquareLeft();
        fill.color = fillColor;
        fill.sortingOrder = sortingOrder + 1;
        fill.transform.localPosition = new Vector3(-size.x * 0.5f, 0f, 0f);

        return new Bar { root = root.transform, fill = fill.transform, height = top + gap };
    }

    private void OnDisable()
    {
        foreach (Bar bar in bars.Values)
        {
            if (bar.root != null)
                Destroy(bar.root.gameObject);
        }
        bars.Clear();
    }
}
