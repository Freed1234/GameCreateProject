using System;
using UnityEngine;
using UnityEngine.UI;

// 동그란 스킬 슬롯 하나를 표시합니다.
// 빈 슬롯 / 잠김(현재 직업에서 꺼진 스킬) / 사용 가능 / 쿨타임 상태를 구분합니다.
// skillSource가 비어 있으면 플레이어의 ISkillCooldown 스크립트 중
// 키가 fallbackKeyLabel과 같거나(예: X) 클래스 이름에 autoBindKeywords가 들어간 것을 자동 연결합니다.
[DisallowMultipleComponent]
public class SkillSlotUI : MonoBehaviour
{
    [Tooltip("ISkillCooldown을 구현한 스킬 컴포넌트입니다. 비우면 아래 키워드로 자동 연결을 시도합니다.")]
    [SerializeField] private MonoBehaviour skillSource;

    [Tooltip("클래스 이름에 이 단어가 들어간 스킬을 자동 연결합니다. (예: Dash)")]
    [SerializeField] private string[] autoBindKeywords = new string[0];

    [Tooltip("스킬이 아직 없을 때 슬롯 아래 표시할 키 (예: SHIFT)")]
    [SerializeField] private string fallbackKeyLabel = "";

    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private Text cooldownText;
    [SerializeField] private Text keyText;

    [Tooltip("쓸 수 없는 스킬(없음/전직 전)은 숨기고, 쓸 수 있게 되면 그 자리에 나타납니다.")]
    [SerializeField] private bool hideWhenUnavailable = true;
    [Tooltip("켜면 스킬이 아직 없어도 전직한 순간부터 칸을 보여줍니다. (대쉬 칸)")]
    [SerializeField] private bool showAfterJob = false;
    [SerializeField, Min(0f)] private float fadeSpeed = 6f;

    [Header("상태별 배경색")]
    [SerializeField] private Color readyColor = new Color(0.22f, 0.27f, 0.38f, 0.95f);
    [SerializeField] private Color lockedColor = new Color(0.12f, 0.12f, 0.14f, 0.75f);
    [SerializeField] private Color emptyColor = new Color(0.08f, 0.08f, 0.1f, 0.45f);

    private ISkillCooldown skill;
    private float nextBindTry;
    private float nextSwapCheck;
    private CanvasGroup group;

    // 지금 슬롯에 표시 중인 키 (예: "X") — 4차 강화 칸이 X/C 칸 위치를 찾을 때 사용
    public string DisplayKey => keyText != null && !string.IsNullOrEmpty(keyText.text) ? keyText.text : fallbackKeyLabel;

    public void SetSkill(MonoBehaviour source)
    {
        skillSource = source;
        skill = source as ISkillCooldown;
    }

    private void Awake()
    {
        skill = skillSource as ISkillCooldown;

        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        if (hideWhenUnavailable)
            group.alpha = 0f;
    }

    private void OnValidate()
    {
        if (skillSource != null && !(skillSource is ISkillCooldown))
        {
            Debug.LogWarning(
                $"{skillSource.GetType().Name}은(는) ISkillCooldown을 구현하지 않아 연결할 수 없습니다.", this);
            skillSource = null;
        }
        skill = skillSource as ISkillCooldown;
    }

    private void TryAutoBind()
    {
        // 1초마다 한 번만 찾습니다.
        if (Time.unscaledTime < nextBindTry)
            return;
        nextBindTry = Time.unscaledTime + 1f;

        GameSession session = GameSession.Instance;
        PlayerHealth player = session != null ? session.Player : FindAnyObjectByType<PlayerHealth>();
        if (player == null)
            return;

        // 켜진 스킬을 먼저 찾고, 없으면 꺼진 스킬(전직 전)까지 찾습니다.
        MonoBehaviour[] all = player.GetComponentsInChildren<MonoBehaviour>(true);
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (MonoBehaviour behaviour in all)
            {
                if (!(behaviour is ISkillCooldown cd))
                    continue;
                if (pass == 0 && !behaviour.isActiveAndEnabled)
                    continue;
                if (Matches(behaviour, cd))
                {
                    SetSkill(behaviour);
                    return;
                }
            }
        }
    }

    private static readonly string[] SlotKeys = { "SHIFT", "Z", "X", "C" };

    private bool Matches(MonoBehaviour behaviour, ISkillCooldown cd)
    {
        string key = PrettyKey(cd.KeyLabel);
        if (!string.IsNullOrEmpty(fallbackKeyLabel) && key == fallbackKeyLabel)
            return true;

        // 이름으로 찾을 때, 다른 칸의 키(SHIFT/Z/X/C)를 쓰는 스킬은 제외
        // (예: Z 칸 키워드 "Piercing"이 C키 스킬 PiercingGaleSkill에 잘못 붙는 것 방지)
        if (System.Array.IndexOf(SlotKeys, key) >= 0 && key != fallbackKeyLabel)
            return false;

        string typeName = behaviour.GetType().Name;
        if (autoBindKeywords == null)
            return false;
        foreach (string keyword in autoBindKeywords)
        {
            if (!string.IsNullOrEmpty(keyword)
                && typeName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    // 연결된 스킬이 꺼졌을 때(예: 2차 전직으로 1차 Z 스킬이 꺼짐),
    // 같은 키를 쓰는 다른 '켜진' 스킬이 있으면 그쪽으로 갈아탑니다.
    private void TrySwapToActiveSameKey()
    {
        if (Time.unscaledTime < nextSwapCheck)
            return;
        nextSwapCheck = Time.unscaledTime + 0.25f;

        string key = skill != null ? PrettyKey(skill.KeyLabel) : fallbackKeyLabel;
        if (string.IsNullOrEmpty(key))
            return;

        GameSession session = GameSession.Instance;
        PlayerHealth player = session != null ? session.Player : FindAnyObjectByType<PlayerHealth>();
        if (player == null)
            return;

        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == skillSource || !behaviour.isActiveAndEnabled)
                continue;
            if (behaviour is ISkillCooldown other && PrettyKey(other.KeyLabel) == key)
            {
                SetSkill(behaviour);
                return;
            }
        }
    }

    private void LateUpdate()
    {
        if (skillSource == null)
        {
            skill = null;
            TryAutoBind();
        }
        else if (!skillSource.isActiveAndEnabled)
        {
            TrySwapToActiveSameKey();
            // 꺼진 채로 남아 있으면, 같은 칸에 맞는 켜진 스킬이 생겼는지 다시 찾아봄
            if (!skillSource.isActiveAndEnabled && skill != null && Time.unscaledTime >= nextBindTry)
            {
                MonoBehaviour before = skillSource;
                TryAutoBind();
                if (skillSource == null) SetSkill(before);
            }
        }

        bool hasSkill = skill != null && skillSource != null;
        bool usable = hasSkill && skillSource.isActiveAndEnabled;
        float remaining = usable ? Mathf.Max(0f, skill.CooldownRemaining) : 0f;
        float duration = hasSkill ? Mathf.Max(0.01f, skill.Cooldown) : 1f;

        // 쓸 수 있을 때만 보이기 (부드럽게 나타남/사라짐)
        if (group != null)
        {
            PlayerProgression progression = PlayerProgression.Instance;
            bool hasJob = progression != null && progression.CurrentJob != PlayerJob.None;
            bool visible = !hideWhenUnavailable || usable || (showAfterJob && hasJob);
            float target = visible ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        }

        if (background != null)
            background.color = !hasSkill ? emptyColor : usable ? readyColor : lockedColor;

        if (icon != null)
        {
            icon.enabled = hasSkill && icon.sprite != null;
            icon.color = usable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.enabled = remaining > 0f;
            cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / duration);
        }

        if (cooldownText != null)
        {
            cooldownText.text = remaining <= 0f ? ""
                : remaining >= 1f ? Mathf.CeilToInt(remaining).ToString()
                : remaining.ToString("0.0");
        }

        if (keyText != null)
            keyText.text = skillSource is StackDashUIAdapter dash
                ? $"SHIFT  {dash.Charges}/{dash.MaxCharges}"
                : hasSkill ? PrettyKey(skill.KeyLabel) : fallbackKeyLabel;
    }

    private static string PrettyKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "";
        switch (key)
        {
            case "LeftShift":
            case "RightShift": return "SHIFT";
            case "LeftControl":
            case "RightControl": return "CTRL";
            case "LeftAlt":
            case "RightAlt": return "ALT";
            case "Space": return "SPACE";
            case "Mouse0": return "L-CLICK";
            case "Mouse1": return "R-CLICK";
        }
        if (key.StartsWith("Alpha") && key.Length == 6)
            return key.Substring(5);
        return key.ToUpperInvariant();
    }
}
