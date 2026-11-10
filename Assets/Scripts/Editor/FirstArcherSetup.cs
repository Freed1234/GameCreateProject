#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class FirstArcherSetup
{
    [MenuItem("Tools/Game UI/Setup First Archer Skills")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 모드를 종료해주세요.");
        Scene scene = SceneManager.GetActiveScene();
        var players = InScene<PlayerProgression>(scene);
        if (players.Length != 1) throw new InvalidOperationException("PlayerProgression이 하나 있는 게임 씬을 활성화해주세요.");
        var player = players[0];
        var controller = player.GetComponent<JobAttackController>();
        if (controller == null || player.GetComponent<PlayerHealth>() == null || player.GetComponent<PlayerMovement>() == null)
            throw new InvalidOperationException("Player의 JobAttackController / PlayerHealth / PlayerMovement가 필요합니다.");
        var basic = player.GetComponentsInChildren<AutoAttack>(true);
        var arrows = player.GetComponentsInChildren<AutoProjectileAttack>(true);
        var volley = One<ArcherVolleySkill>(player);
        var rain = One<ArrowRainSkill>(player);
        var crystal = One<CrystalArrowSkill>(player);
        var evasion = One<EvasiveShotSkill>(player);
        var dash = One<StackDashUIAdapter>(player);
        if (basic.Length == 0 || arrows.Length == 0 || player.GetComponentInChildren<PlayerDash>(true) != null)
            throw new InvalidOperationException("기존 미전직/궁수 평타가 필요하며 PlayerDash는 연결하지 않아야 합니다.");
        MonoBehaviour[] newSkills = { volley, rain, crystal, evasion };
        foreach (var skill in newSkills.Concat(arrows.Cast<MonoBehaviour>()).Concat(basic.Cast<MonoBehaviour>()))
            if (!skill.gameObject.activeInHierarchy) throw new InvalidOperationException(skill.GetType().Name + "의 GameObject를 활성화해주세요.");
        if (evasion.GetComponent<PlayerMovement>() == null)
            throw new InvalidOperationException("회피 사격은 PlayerMovement와 같은 오브젝트에 있어야 합니다.");
        var slots = InScene<SkillSlotUI>(scene);
        string[] keys = { "SHIFT", "Z", "X", "C" };
        MonoBehaviour[] sources = { dash, volley, rain, crystal };
        var selectedSlots = new SkillSlotUI[4];
        for (int i = 0; i < keys.Length; i++)
        {
            var found = slots.Where(s => new SerializedObject(s).FindProperty("fallbackKeyLabel").stringValue == keys[i]).ToArray();
            if (found.Length != 1) throw new InvalidOperationException(keys[i] + " HUD 슬롯이 하나여야 합니다.");
            selectedSlots[i] = found[0];
            if (i > 0 && ((ISkillCooldown)sources[i]).KeyLabel != keys[i])
                throw new InvalidOperationException(keys[i] + " 스킬의 기본 Key가 다릅니다. 키 변경은 Settings에서 설정해주세요.");
        }
        var currentSets = new SerializedObject(controller).FindProperty("jobAttacks");
        int noviceCount = 0, archerCount = 0;
        for (int i = 0; i < currentSets.arraySize; i++)
        {
            int job = currentSets.GetArrayElementAtIndex(i).FindPropertyRelative("job").intValue;
            if (job == (int)PlayerJob.None) noviceCount++;
            if (job == (int)PlayerJob.Archer) archerCount++;
        }
        if (noviceCount != 1 || archerCount > 1)
            throw new InvalidOperationException("기존 직업 공격 목록을 확인해주세요. 미전직 목록 1개와 궁수 목록 최대 1개가 필요합니다.");
        var retired = new HashSet<Object>();
        foreach (var p in player.GetComponentsInChildren<PiercingProjectileSkill>(true)) retired.Add(p);
        foreach (var s in InScene<SecondJobSystem>(scene)) retired.Add(s);
        // Validate every dependency before editing the scene. No stats are copied from old outputs.
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Setup First Archer Skills");
        try
        {
            var data = new SerializedObject(controller);
            var sets = data.FindProperty("jobAttacks");
            int archerIndex = -1;
            for (int i = 0; i < sets.arraySize; i++)
            {
                var set = sets.GetArrayElementAtIndex(i);
                if (set.FindPropertyRelative("job").intValue == (int)PlayerJob.Archer)
                {
                    if (archerIndex >= 0) throw new InvalidOperationException("궁수 공격 목록이 중복되어 있습니다.");
                    archerIndex = i;
                }
                Filter(set.FindPropertyRelative("attacks"), o => o != null && !retired.Contains(o) && !newSkills.Contains(o));
            }
            if (archerIndex < 0)
            {
                archerIndex = sets.arraySize; sets.arraySize++;
                sets.GetArrayElementAtIndex(archerIndex).FindPropertyRelative("job").intValue = (int)PlayerJob.Archer;
            }
            Assign(sets.GetArrayElementAtIndex(archerIndex).FindPropertyRelative("attacks"), arrows.Cast<Object>().Concat(newSkills));
            Filter(data.FindProperty("unusedAttacks"), o => o != null && !retired.Contains(o) && !newSkills.Contains(o) && !arrows.Contains(o));
            // Keep the current novice range/list and other serialized values exactly as configured.
            data.ApplyModifiedProperties();
            for (int i = 0; i < selectedSlots.Length; i++)
            {
                var so = new SerializedObject(selectedSlots[i]);
                so.FindProperty("skillSource").objectReferenceValue = sources[i];
                so.FindProperty("hideWhenUnavailable").boolValue = false;
                var words = so.FindProperty("autoBindKeywords"); words.arraySize = 1;
                words.GetArrayElementAtIndex(0).stringValue = sources[i].GetType().Name;
                so.ApplyModifiedProperties();
            }
            foreach (var window in InScene<JobSelectWindow>(scene))
            {
                var so = new SerializedObject(window);
                so.FindProperty("title").stringValue = "1차 전직";
                so.FindProperty("archerName").stringValue = "1차 궁수";
                so.FindProperty("warriorUnlocked").boolValue = false;
                so.ApplyModifiedProperties();
            }
            // Clear remaining serialized references before removing only the retired components.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || retired.Contains(behaviour)) continue;
                    var so = new SerializedObject(behaviour); var property = so.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && retired.Contains(property.objectReferenceValue))
                            property.objectReferenceValue = null;
                    so.ApplyModifiedProperties();
                }
            foreach (var component in retired) Undo.DestroyObjectImmediate(component);
            foreach (var skill in newSkills.Concat(arrows.Cast<MonoBehaviour>()))
            { Undo.RecordObject(skill, "Lock archer skills before job selection"); skill.enabled = false; EditorUtility.SetDirty(skill); }
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("FIRST_ARCHER_SETUP_OK: 1차 궁수에 Z/X/C/회피 사격을 연결했습니다. 씬을 저장해주세요.");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
    private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static T One<T>(Component player) where T : Component
    {
        var found = player.GetComponentsInChildren<T>(true);
        if (found.Length != 1) throw new InvalidOperationException(typeof(T).Name + "이 하나 필요합니다. 기존 스킬 설정이 있는 씬을 사용해주세요.");
        return found[0];
    }
    private static void Assign(SerializedProperty list, IEnumerable<Object> values)
    {
        Object[] items = values.ToArray(); list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
    private static void Filter(SerializedProperty list, Func<Object, bool> keep)
    {
        var values = new List<Object>();
        for (int i = 0; i < list.arraySize; i++) { var o = list.GetArrayElementAtIndex(i).objectReferenceValue; if (keep(o)) values.Add(o); }
        Assign(list, values);
    }
    public static void BatchSetup()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        Setup(); EditorSceneManager.SaveOpenScenes();
        string first = System.IO.File.ReadAllText("Assets/Scenes/GameCreateProject.unity");
        SecondJobSetup.Setup(); EditorSceneManager.SaveOpenScenes();
        if (first != System.IO.File.ReadAllText("Assets/Scenes/GameCreateProject.unity")) throw new Exception("First archer setup is not idempotent");
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0) throw new Exception("Missing script: " + transform.name);
        Debug.Log("FIRST_ARCHER_IDEMPOTENT_AND_NO_MISSING_SCRIPTS");
    }
}
#endif

