#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴: Tools > Game UI > Setup Archer Skills (Dash + 2nd Job)
// 게임 씬의 Player에 대쉬 + 2차 전직 + 2차 스킬 3개를 붙이고 키/쿨타임/화살 이미지를 설정합니다.
public static class SecondJobSetup
{
    [MenuItem("Tools/Game UI/Setup Archer Skills (Dash + 2nd Job)")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Game UI", "플레이 모드를 끈 상태에서 실행해주세요.", "확인");
            return;
        }

        PlayerHealth player = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (player == null)
        {
            EditorUtility.DisplayDialog("Game UI", "씬에서 Player(PlayerHealth)를 찾지 못했습니다.", "확인");
            return;
        }
        GameObject go = player.gameObject;

        // 기존 관통탄 스킬에서 화살 이미지와 적 컨테이너를 가져옵니다.
        PiercingProjectileSkill piercing = go.GetComponentInChildren<PiercingProjectileSkill>(true);
        Sprite arrow = null;
        Object enemyContainer = null;
        if (piercing != null)
        {
            SerializedObject p = new SerializedObject(piercing);
            Component prefab = p.FindProperty("projectilePrefab")?.objectReferenceValue as Component;
            SpriteRenderer sr = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            arrow = sr != null ? sr.sprite : null;
            enemyContainer = p.FindProperty("enemyContainer")?.objectReferenceValue;
        }

        PlayerDash dash = Ensure<PlayerDash>(go);
        Configure(dash, KeyCode.LeftShift, 2f, arrow, enemyContainer);
        Configure(Ensure<ArcherVolleySkill>(go), KeyCode.Z, 6f, arrow, enemyContainer);
        Configure(Ensure<ArrowRainSkill>(go), KeyCode.X, 8f, arrow, enemyContainer);
        Configure(Ensure<CrystalArrowSkill>(go), KeyCode.C, 20f, arrow, enemyContainer);

        // 2차 전직 목록 연결
        SecondJobSystem system = Ensure<SecondJobSystem>(go);
        SerializedObject so = new SerializedObject(system);
        SerializedProperty disable = so.FindProperty("disableOnAdvance");
        disable.arraySize = piercing != null ? 1 : 0;
        if (piercing != null)
            disable.GetArrayElementAtIndex(0).objectReferenceValue = piercing;

        SerializedProperty jobs = so.FindProperty("jobs");
        if (jobs.arraySize == 0)
            jobs.arraySize = 1;
        SerializedProperty first = jobs.GetArrayElementAtIndex(0);
        if (string.IsNullOrEmpty(first.FindPropertyRelative("displayName").stringValue))
            first.FindPropertyRelative("displayName").stringValue = "ARCHER";
        SerializedProperty enable = first.FindPropertyRelative("enableSkills");
        MonoBehaviour[] skills =
        {
            go.GetComponent<ArcherVolleySkill>(), go.GetComponent<ArrowRainSkill>(), go.GetComponent<CrystalArrowSkill>()
        };
        enable.arraySize = skills.Length;
        for (int i = 0; i < skills.Length; i++)
            enable.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[SecondJobSetup] Player에 대쉬 + 2차 전직 + 2차 스킬(Z/X/C)을 설정했습니다. 씬을 저장해주세요."
            + (arrow == null ? " (화살 이미지를 못 찾아 임시 화살 모양 사용)" : ""), go);
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(go);
    }

    private static void Configure(ArcherSkillBase skill, KeyCode key, float cooldown, Sprite arrow, Object enemyContainer)
    {
        SerializedObject so = new SerializedObject(skill);
        so.FindProperty("key").intValue = (int)key;
        so.FindProperty("cooldown").floatValue = cooldown;
        if (arrow != null && so.FindProperty("arrowSprite").objectReferenceValue == null)
            so.FindProperty("arrowSprite").objectReferenceValue = arrow;
        if (enemyContainer != null && so.FindProperty("enemyContainer").objectReferenceValue == null)
            so.FindProperty("enemyContainer").objectReferenceValue = enemyContainer;
        so.ApplyModifiedProperties();
    }
}
#endif
