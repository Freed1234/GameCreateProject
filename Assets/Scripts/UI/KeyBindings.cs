using System;
using System.Collections.Generic;
using UnityEngine;

// 키 설정 (설정 > KEY SETTINGS 에서 변경, PlayerPrefs에 저장)
//   Dash / Skill1(Z) / Skill2(X) / Skill3(C) / Character(Tab)
// 4차 스킬 Ctrl+X / Ctrl+C 는 Skill2 / Skill3 키를 따라갑니다. (예: X를 E로 바꾸면 Ctrl+E)
// Tab 창 방식(누르고 있기 / 눌러서 열고 닫기)도 여기 저장합니다.
public enum GameAction { Dash, Skill1, Skill2, Skill3, Character }

public static class KeyBindings
{
    public static readonly GameAction[] All =
        { GameAction.Dash, GameAction.Skill1, GameAction.Skill2, GameAction.Skill3, GameAction.Character };

    public static event Action Changed;
    public static KeyCode Normalize(KeyCode key) => key == KeyCode.RightShift ? KeyCode.LeftShift : key;
    public static bool IsAllowed(KeyCode key) {
        key = Normalize(key);
        return System.Enum.IsDefined(typeof(KeyCode), key) && key > KeyCode.None && key < KeyCode.Mouse0
            && key != KeyCode.W && key != KeyCode.A && key != KeyCode.S && key != KeyCode.D
            && key != KeyCode.UpArrow && key != KeyCode.DownArrow && key != KeyCode.LeftArrow && key != KeyCode.RightArrow
            && key != KeyCode.Escape && key != KeyCode.R && key != KeyCode.F11
            && key != KeyCode.Return && key != KeyCode.KeypadEnter
            && key != KeyCode.LeftControl && key != KeyCode.RightControl && key != KeyCode.LeftAlt && key != KeyCode.RightAlt;
    }
    public static bool Down(GameAction action) => !SettingsExtras.IsCapturing && RawDown(Get(action));
    public static bool Held(GameAction action) => !SettingsExtras.IsCapturing &&
        (Input.GetKey(Get(action)) || Get(action) == KeyCode.LeftShift && Input.GetKey(KeyCode.RightShift));
    public static bool RawDown(KeyCode key) => Input.GetKeyDown(key) || key == KeyCode.LeftShift && Input.GetKeyDown(KeyCode.RightShift);
    public static bool SkillDown(KeyCode key) => ActionForDefault(key, out GameAction action) ? Down(action) : !SettingsExtras.IsCapturing && Input.GetKeyDown(key);


    private const string TabModeKey = "TabToggleMode";
    private static readonly Dictionary<GameAction, KeyCode> cache = new Dictionary<GameAction, KeyCode>();

    public static KeyCode Default(GameAction action)
    {
        switch (action)
        {
            case GameAction.Dash: return KeyCode.LeftShift;
            case GameAction.Skill1: return KeyCode.Z;
            case GameAction.Skill2: return KeyCode.X;
            case GameAction.Skill3: return KeyCode.C;
            default: return KeyCode.Tab;
        }
    }

    public static string DisplayName(GameAction action)
    {
        switch (action)
        {
            case GameAction.Dash: return "DASH";
            case GameAction.Skill1: return "SKILL 1";
            case GameAction.Skill2: return "SKILL 2";
            case GameAction.Skill3: return "SKILL 3";
            default: return "CHARACTER";
        }
    }

    public static KeyCode Get(GameAction action)
    {
        if (!cache.TryGetValue(action, out KeyCode key))
        {
            key = (KeyCode)PlayerPrefs.GetInt("Key_" + action, (int)Default(action));
            key = Normalize(key);
            if (!IsAllowed(key)) key = Default(action);
            cache[action] = key;
        }
        return key;
    }

    // 다른 동작이 이미 쓰는 키면 서로 바꿉니다.
    public static void Set(GameAction action, KeyCode key)
    {
        key = Normalize(key);
        if (!IsAllowed(key)) return;
        KeyCode old = Get(action);
        if (old == key) return;
        foreach (GameAction other in All)
        {
            if (other != action && Get(other) == key)
                Store(other, old);
        }
        Store(action, key);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        foreach (GameAction a in All) Store(a, Default(a));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static void Store(GameAction action, KeyCode key)
    {
        cache[action] = key;
        PlayerPrefs.SetInt("Key_" + action, (int)key);
    }

    // true = Tab 눌러서 열고 다시 눌러서 닫기 / false = 누르고 있는 동안만 (기본)
    public static bool TabToggle
    {
        get => PlayerPrefs.GetInt(TabModeKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(TabModeKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    // 원래 키(기본값)로 어떤 동작인지 찾기. 해당 없으면 false
    public static bool ActionForDefault(KeyCode original, out GameAction action)
    {
        foreach (GameAction a in All)
        {
            if (a != GameAction.Character && Default(a) == original)
            {
                action = a;
                return true;
            }
        }
        if (original == KeyCode.RightShift) { action = GameAction.Dash; return true; }
        action = GameAction.Dash;
        return false;
    }

    // 스킬 칸 아래 글자 (예: LeftShift → SHIFT, Alpha1 → 1)
    public static string Pretty(KeyCode key) => Pretty(key.ToString());

    public static string Pretty(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        switch (key)
        {
            case "LeftShift":
            case "RightShift": return "SHIFT";
            case "LeftControl":
            case "RightControl": return "CTRL";
            case "LeftAlt":
            case "RightAlt": return "ALT";
            case "Space": return "SPACE";
            case "Tab": return "TAB";
            case "Mouse0": return "L-CLICK";
            case "Mouse1": return "R-CLICK";
            case "Mouse2": return "M-CLICK";
        }
        if (key.StartsWith("Alpha") && key.Length == 6) return key.Substring(5);
        if (key.StartsWith("Keypad")) return "NUM" + key.Substring(6);
        return key.ToUpperInvariant();
    }

    // 스킬 칸의 기본 글자(SHIFT/Z/X/C)가 어떤 동작 칸인지
    public static bool ActionForSlotLabel(string label, out GameAction action)
    {
        switch (label)
        {
            case "SHIFT": action = GameAction.Dash; return true;
            case "Z": action = GameAction.Skill1; return true;
            case "X": action = GameAction.Skill2; return true;
            case "C": action = GameAction.Skill3; return true;
        }
        action = GameAction.Dash;
        return false;
    }
}

