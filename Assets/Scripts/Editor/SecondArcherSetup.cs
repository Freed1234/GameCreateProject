#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SecondArcherSetup
{
    [MenuItem("Tools/Game UI/Setup Level 30 Second Archer")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 모드를 종료해주세요.");
        var player = Object.FindAnyObjectByType<PlayerProgression>(FindObjectsInactive.Include);
        if (player == null) throw new InvalidOperationException("게임 씬의 Player가 필요합니다.");
        ImportArt();
        var z = Add<StormBarrageSkill>(player); var x = Add<LeafVortexSkill>(player);
        var c = Add<CompressedWindSkill>(player); var dash = Add<FeatherDashSkill>(player);
        var system = Add<SecondJobSystem>(player);
        var container = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include).transform;
        foreach (var entry in new[] { ((ArcherSkillBase)z, "Z", 7f, 10f), (x, "X", 10f, 7f), (c, "C", 12f, 16f) })
        {
            var so = new SerializedObject(entry.Item1);
            so.FindProperty("key").intValue = (int)(entry.Item2 == "Z" ? KeyCode.Z : entry.Item2 == "X" ? KeyCode.X : KeyCode.C);
            so.FindProperty("cooldown").floatValue = entry.Item3;
            so.FindProperty("aimRange").floatValue = entry.Item4;
            if (entry.Item1 == z) so.FindProperty("shotCount").intValue = 40;
            if (entry.Item1 == x) so.FindProperty("radius").floatValue = 5f;
            so.FindProperty("enemyContainer").objectReferenceValue = container;
            so.FindProperty("skillIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SecondArcher/" + entry.Item2 + "Icon.png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var data = new SerializedObject(system);
        data.FindProperty("requiredLevel").intValue = 30;
        data.FindProperty("requiredFirstJob").intValue = (int)PlayerJob.Archer;
        Assign(data.FindProperty("disableOnAdvance"), new Object[] {
            player.GetComponent<ArcherVolleySkill>(), player.GetComponent<ArrowRainSkill>(),
            player.GetComponent<CrystalArrowSkill>(), player.GetComponent<EvasiveShotSkill>() });
        var jobs = data.FindProperty("jobs"); jobs.arraySize = 1;
        var job = jobs.GetArrayElementAtIndex(0);
        job.FindPropertyRelative("displayName").stringValue = "2차 궁수";
        job.FindPropertyRelative("description").stringValue = "Z 순차 포착 연사 · X 나뭇잎 회오리\nC 압축 바람 화살 · Shift 백깃 돌진";
        job.FindPropertyRelative("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SecondArcher/ZIcon.png");
        Assign(job.FindPropertyRelative("enableSkills"), new Object[] { z, x, c, dash });
        data.ApplyModifiedPropertiesWithoutUndo();
        system.enabled = true;
        foreach (var skill in new MonoBehaviour[] {z,x,c,dash}) { skill.enabled = false; EditorUtility.SetDirty(skill); }
        EditorUtility.SetDirty(system);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
    }
    private static T Add<T>(Component player) where T : Component => player.GetComponent<T>() ?? Undo.AddComponent<T>(player.gameObject);
    private static void Assign(SerializedProperty list, Object[] items)
    {
        if (items.Any(x => x == null)) throw new InvalidOperationException("기존 1차 스킬 연결을 확인해주세요.");
        list.arraySize = items.Length;
        for (int i=0;i<items.Length;i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/SecondArcher" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            bool z = path.Contains("/Z/"), c = path.Contains("/C/");
            importer.spritePixelsPerUnit = z ? 40f : c ? 22f : path.Contains("/X/") ? 24f : path.EndsWith("Target.png") ? 40f : 48f;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = z || c ? new Vector2(.75f,.5f) : path.Contains("/X/") ? new Vector2(.5f,50f/128f) : new Vector2(.5f,.5f);
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
    public static void BatchSetup()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        Setup(); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        Debug.Log("SECOND_ARCHER_SETUP_PASS");
    }
}
#endif
