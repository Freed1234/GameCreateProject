using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 4차 강화 칸: X 칸 위 = 어센트, C 칸 위 = 오리진
//  - 충전 칸(점) / 충전 중 어두운 덮개 + %
//  - 쓸 수 있으면 금색 테두리가 반짝임
//  - 클릭하거나 Ctrl+X / Ctrl+C 로 사용, 마우스를 올리면 툴팁
// 4차 스킬이 켜질 때 자동으로 만들어집니다. (씬에 붙일 필요 없음)
public class FourthJobHUD : MonoBehaviour
{
    private static FourthJobHUD instance;

    private class Slot
    {
        public FourthJobSkill skill;
        public RectTransform root;
        public Image border, icon, cover;
        public Text percent, keyLabel;
        public Image[] dots;
        public UITooltipTrigger tooltip;
        public SkillSlotUI anchor;
        public int lastPercent = -2, lastCharges = -1; // 글자를 바뀔 때만 갱신하려고 기억
    }

    private readonly List<Slot> slots = new List<Slot>();
    private Font font;
    private float nextScan;

    private const float Size = 70f;
    private static readonly Color Gold = new Color(1f, 0.85f, 0.45f);

    public static void Ensure()
    {
        if (instance != null) return;
        GameObject obj = new GameObject("FourthJobHUD");
        instance = obj.AddComponent<FourthJobHUD>();
    }

    private void Awake()
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        foreach (Slot s in slots) if (s.root != null) Destroy(s.root.gameObject);
        if (font != null) Destroy(font);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            Scan();
        }

        foreach (Slot s in slots)
        {
            if (s.root == null) continue;
            bool on = s.skill != null && s.skill.isActiveAndEnabled;
            if (s.root.gameObject.activeSelf != on) s.root.gameObject.SetActive(on);
            if (!on) continue;

            FourthJobSkill k = s.skill;
            bool ready = k.Charges > 0 && !k.Busy;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            s.border.color = ready ? new Color(Gold.r, Gold.g, Gold.b, pulse) : new Color(0.45f, 0.45f, 0.5f, 0.9f);
            s.icon.color = ready ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
            s.keyLabel.color = ready ? Gold : new Color(0.8f, 0.8f, 0.8f);

            if (k.MaxCharges > 1)
            {
                // 칸 방식: 점으로 표시, 비어 있는 동안 덮개
                for (int i = 0; i < s.dots.Length; i++)
                {
                    bool full = i < k.Charges;
                    bool filling = i == k.Charges;
                    s.dots[i].color = full ? JobColor(k) : filling ? Color.Lerp(new Color(0.25f, 0.25f, 0.3f), JobColor(k), k.ChargeProgress * 0.6f) : new Color(0.25f, 0.25f, 0.3f);
                }
                s.cover.fillAmount = k.Charges > 0 ? 0f : 1f - k.ChargeProgress;
                if (s.lastPercent != -1) { s.lastPercent = -1; s.percent.text = ""; }
            }
            else
            {
                s.cover.fillAmount = k.Charges > 0 ? 0f : 1f - k.ChargeProgress;
                int percent = k.Charges > 0 ? -1 : Mathf.FloorToInt(k.ChargeProgress * 100f);
                if (percent != s.lastPercent) // 숫자가 바뀔 때만 글자를 새로 만듦
                {
                    s.lastPercent = percent;
                    s.percent.text = percent < 0 ? "" : percent + "%";
                }
            }

            if (k.Charges != s.lastCharges)
            {
                s.lastCharges = k.Charges;
                s.tooltip.SetContent(k.DisplayName,
                    $"{k.Description}\n\n충전 {k.Charges} / {k.MaxCharges}\n{k.KeyText} 또는 클릭으로 사용");
            }
        }
    }

    private static Color JobColor(FourthJobSkill k) =>
        k.Branch == ArcherBranch.Shadow ? JobTierUtil.ShadowColor : JobTierUtil.BoltColor;

    // 켜진 4차 스킬마다 칸을 만들고, 같은 키(X/C)의 스킬 칸 위에 붙임
    private void Scan()
    {
        foreach (FourthJobSkill skill in FindObjectsByType<FourthJobSkill>(FindObjectsSortMode.None))
        {
            if (!skill.isActiveAndEnabled) continue;
            Slot existing = slots.Find(x => x.skill == skill);
            if (existing != null && existing.root != null) continue;

            SkillSlotUI anchor = null;
            foreach (SkillSlotUI ui in FindObjectsByType<SkillSlotUI>(FindObjectsSortMode.None))
                if (ui.DisplayKey == skill.Key.ToString()) { anchor = ui; break; }
            if (anchor == null) continue; // 스킬 칸이 아직 없음 → 다음에 다시

            if (existing != null) slots.Remove(existing);
            slots.Add(Build(skill, anchor));
        }
    }

    private Slot Build(FourthJobSkill skill, SkillSlotUI anchor)
    {
        Slot s = new Slot { skill = skill, anchor = anchor };
        RectTransform parent = (RectTransform)anchor.transform;

        s.root = NewRect("FourthJobSlot_" + skill.Key, parent);
        s.root.anchorMin = s.root.anchorMax = new Vector2(0.5f, 1f);
        s.root.pivot = new Vector2(0.5f, 0f);
        s.root.sizeDelta = new Vector2(Size, Size);
        s.root.anchoredPosition = new Vector2(0f, 26f);
        // 스킬 칸의 CanvasGroup(클릭 막음)을 무시해야 클릭됨
        CanvasGroup group = s.root.gameObject.AddComponent<CanvasGroup>();
        group.ignoreParentGroups = true;

        s.border = Fill(NewRect("Border", s.root), new Vector2(-4f, -4f), new Vector2(4f, 4f)).gameObject.AddComponent<Image>();
        s.border.raycastTarget = false;

        RectTransform iconRect = Fill(NewRect("Icon", s.root), Vector2.zero, Vector2.zero);
        s.icon = iconRect.gameObject.AddComponent<Image>();
        s.icon.sprite = skill.Icon;
        s.icon.preserveAspect = true;
        if (s.icon.sprite == null) s.icon.color = JobColor(skill);

        RectTransform coverRect = Fill(NewRect("Cover", s.root), Vector2.zero, Vector2.zero);
        s.cover = coverRect.gameObject.AddComponent<Image>();
        s.cover.color = new Color(0f, 0f, 0f, 0.65f);
        s.cover.type = Image.Type.Filled;
        s.cover.fillMethod = Image.FillMethod.Vertical;
        s.cover.fillOrigin = (int)Image.OriginVertical.Top;
        s.cover.raycastTarget = false;

        s.percent = Label(Fill(NewRect("Percent", s.root), Vector2.zero, Vector2.zero), "", 20, Color.white);

        RectTransform keyRect = NewRect("Key", s.root);
        keyRect.anchorMin = keyRect.anchorMax = new Vector2(0.5f, 1f);
        keyRect.pivot = new Vector2(0.5f, 0f);
        keyRect.sizeDelta = new Vector2(120f, 22f);
        keyRect.anchoredPosition = new Vector2(0f, 4f);
        s.keyLabel = Label(keyRect, skill.KeyText, 16, Gold);

        int n = skill.MaxCharges > 1 ? skill.MaxCharges : 0;
        s.dots = new Image[n];
        for (int i = 0; i < n; i++)
        {
            RectTransform d = NewRect("Dot" + i, s.root);
            d.anchorMin = d.anchorMax = new Vector2(0.5f, 0f);
            d.sizeDelta = new Vector2(11f, 11f);
            d.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * 16f, -10f);
            s.dots[i] = d.gameObject.AddComponent<Image>();
            s.dots[i].sprite = SkillUtil.CircleSprite();
            s.dots[i].raycastTarget = false;
        }

        // 클릭 + 툴팁 (아이콘이 클릭을 받음)
        s.icon.raycastTarget = true;
        Button button = iconRect.gameObject.AddComponent<Button>();
        button.targetGraphic = s.icon;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        FourthJobSkill target = skill;
        button.onClick.AddListener(() => target.TryActivate());
        s.tooltip = iconRect.gameObject.AddComponent<UITooltipTrigger>();
        return s;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        r.SetParent(parent, false);
        return r;
    }

    private static RectTransform Fill(RectTransform r, Vector2 offMin, Vector2 offMax)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = offMin; r.offsetMax = offMax;
        return r;
    }

    private Text Label(RectTransform r, string value, int size, Color color)
    {
        Text t = r.gameObject.AddComponent<Text>();
        t.font = font; t.text = value; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        Outline o = r.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.85f);
        return t;
    }
}
