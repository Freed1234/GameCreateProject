#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorldVisibilitySetup
{
    [MenuItem("Tools/Game UI/Setup World Visibility")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("m_SortingLayers");
        string[] names = { "WorldGround", "WorldActors", "WorldSilhouettes" };
        for (int i = 0; i < names.Length; i++)
        {
            bool exists = false;
            for (int j = 0; j < layers.arraySize; j++)
                exists |= layers.GetArrayElementAtIndex(j).FindPropertyRelative("name").stringValue == names[i];
            if (exists) continue;
            layers.InsertArrayElementAtIndex(i);
            var layer = layers.GetArrayElementAtIndex(i);
            layer.FindPropertyRelative("name").stringValue = names[i];
            layer.FindPropertyRelative("uniqueID").intValue = 17911001 + i;
            layer.FindPropertyRelative("locked").boolValue = false;
        }
        tags.ApplyModifiedPropertiesWithoutUndo();
        var mask = Material("ObstacleMask", "Game/ObstacleMask");
        var silhouette = Material("CharacterSilhouette", "Game/CharacterOcclusion");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var renderer in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<SpriteRenderer>(true)))
            if (renderer.sortingOrder < 0) renderer.sortingLayerName = "WorldGround";
        foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include))
            Character(player.gameObject, silhouette, true);
        foreach (string path in new[] { "Assets/Perfeb/Enemy.prefab", "Assets/Perfeb/SlingshotGoblin.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            Character(root, silhouette, false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        const string mapPath = "Assets/Perfeb/MapChunk.prefab";
        var map = PrefabUtility.LoadPrefabContents(mapPath);
        foreach (var renderer in map.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.sortingLayerName = "WorldGround";
        var slots = new SerializedObject(map.GetComponent<ChunkObstacleLayout>()).FindProperty("slots");
        for (int i = 0; i < slots.arraySize; i++)
        {
            foreach (string kind in new[] { "tree", "rock" })
            {
                var owner = slots.GetArrayElementAtIndex(i).FindPropertyRelative(kind).objectReferenceValue as GameObject;
                if (owner == null) continue;
                var bodies = owner.GetComponentsInChildren<SpriteRenderer>(true).Where(r => !r.name.Contains("그림자")).ToArray();
                var baseRenderer = kind == "tree" ? bodies.FirstOrDefault(r => r.name.Contains("기둥")) : bodies.FirstOrDefault(r => r.enabled && r.name.Contains("본체"));
                if (baseRenderer == null) throw new InvalidOperationException("Missing obstacle base " + owner.name);
                float bottom = baseRenderer.transform.TransformPoint(new Vector3(0, SpriteBottom(baseRenderer.sprite), 0)).y;
                Vector2 foot = owner.transform.InverseTransformPoint(new Vector3(owner.transform.position.x, bottom, 0));
                Depth(owner, bodies, foot, true);
            }
        }
        PrefabUtility.SaveAsPrefabAsset(map, mapPath);
        PrefabUtility.UnloadPrefabContents(map);

        // Ground pickups remain above tiles and below the depth-sorted bodies.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("Chest") && !path.Contains("Experience")) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sortingLayerName = "WorldGround";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        var camera = GameCamera.Get();
        if (camera == null) throw new InvalidOperationException("Missing game camera");
        var visibility = camera.GetComponent<WorldOcclusionCamera>();
        if (visibility == null) visibility = camera.gameObject.AddComponent<WorldOcclusionCamera>();
        var settings = new SerializedObject(visibility);
        settings.FindProperty("maskMaterial").objectReferenceValue = mask;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
    }

    private static float SpriteBottom(Sprite sprite)
    {
        // Imported canvases include transparent padding; sorting follows the visible base.
        Texture2D source = sprite.texture;
        var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Texture2D readable = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            Color32[] pixels = readable.GetPixels32();
            Rect rect = sprite.rect;
            for (int y = 0; y < (int)rect.height; y++)
                for (int x = 0; x < (int)rect.width; x++)
                    if (pixels[((int)rect.y + y) * source.width + (int)rect.x + x].a > 25)
                        return (y + .5f - sprite.pivot.y) / sprite.pixelsPerUnit;
            throw new InvalidOperationException("Empty obstacle sprite " + sprite.name);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    private static Material Material(string name, string shader)
    {
        string path = "Assets/Art/Occlusion/" + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result == null)
        {
            result = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(result, path);
        }
        return result;
    }

    private static void Character(GameObject owner, Material material, bool player)
    {
        var shadow = owner.GetComponent<CharacterGroundShadow>();
        if (shadow == null) throw new InvalidOperationException("Missing foot reference " + owner.name);
        Vector2 foot = new SerializedObject(shadow).FindProperty("footOffset").vector2Value;
        Depth(owner, new[] { owner.GetComponent<SpriteRenderer>() }, foot, false);
        var component = owner.GetComponent<CharacterOcclusion>();
        bool first = component == null;
        if (first) component = owner.AddComponent<CharacterOcclusion>();
        var data = new SerializedObject(component);
        data.FindProperty("silhouetteMaterial").objectReferenceValue = material;
        if (first && !player)
        {
            data.FindProperty("fillColor").colorValue = new Color(.65f, .08f, .1f, .4f);
            data.FindProperty("outlineColor").colorValue = new Color(1f, .55f, .5f, .75f);
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Depth(GameObject owner, SpriteRenderer[] bodies, Vector2 foot, bool obstacle)
    {
        var component = owner.GetComponent<WorldDepthSort>();
        bool first = component == null;
        if (first) component = owner.AddComponent<WorldDepthSort>();
        var data = new SerializedObject(component);
        if (first) data.FindProperty("footOffset").vector2Value = foot;
        data.FindProperty("obstacle").boolValue = obstacle;
        var renderers = data.FindProperty("renderers");
        renderers.arraySize = bodies.Length;
        for (int i = 0; i < bodies.Length; i++)
        {
            renderers.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            bodies[i].sortingLayerName = "WorldActors";
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void BatchSetup()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        Setup();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("WORLD_VISIBILITY_SETUP_PASS");
    }
}
#endif
