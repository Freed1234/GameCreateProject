#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ExperienceDropSetup
{
    const string Root="Assets/Art/ExperienceDrops";
    const string PrefabPath=Root+"/ExperiencePickup.prefab";
    [MenuItem("Tools/Game UI/Setup Experience Drops")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Play 모드를 종료해주세요.");
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerProgression>();
        var map=UnityEngine.Object.FindAnyObjectByType<MapChunkManager>();
        var spawner=UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
        if(player==null || map==null || spawner==null) throw new InvalidOperationException("Player와 MapChunkManager, EnemySpawner가 있는 게임 씬을 여세요.");
        Scene scene=SceneManager.GetActiveScene();
        if(player.gameObject.scene!=scene || map.gameObject.scene!=scene || spawner.gameObject.scene!=scene)
            throw new InvalidOperationException("게임 씬 하나를 활성화해 실행해주세요.");
        var existing=UnityEngine.Object.FindObjectsByType<ExperienceDropManager>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(existing.Length>1) throw new InvalidOperationException("ExperienceDropManager가 여러 개입니다. 중복을 먼저 제거해주세요.");
        var prefabRef=new SerializedObject(spawner).FindProperty("enemyPrefab").objectReferenceValue;
        string enemyPath=AssetDatabase.GetAssetPath(prefabRef);
        if(string.IsNullOrEmpty(enemyPath)) throw new InvalidOperationException("EnemySpawner의 Enemy Prefab 참조가 없습니다.");
        var enemy=AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
        if(enemy==null || enemy.GetComponent<EnemyHealth>()==null) throw new InvalidOperationException("Enemy 프리팹의 EnemyHealth가 없습니다.");
        if(new SerializedObject(enemy.GetComponent<EnemyHealth>()).FindProperty("experienceAmount")==null)
            throw new InvalidOperationException("새 EnemyHealth 스크립트를 먼저 적용해주세요.");
        // Existing serialized stats and appearances are never rewritten by this tool.
        Directory.CreateDirectory(Root);
        string texturePath=Root+"/experience-crystal.png";
        if(!File.Exists(texturePath))
        {
            var tex=new Texture2D(16,16,TextureFormat.RGBA32,false);
            var pixels=new Color32[256];
            for(int y=1;y<15;y++) for(int x=3;x<13;x++)
            {
                float d=Mathf.Abs(x-7.5f)/4.5f+Mathf.Abs(y-7.5f)/7f;
                if(d>1f) continue;
                pixels[y*16+x]=d>.75f?new Color32(23,104,119,255):x<8?new Color32(160,255,246,255):new Color32(47,211,215,255);
            }
            tex.SetPixels32(pixels); tex.Apply(); File.WriteAllBytes(texturePath,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        }
        AssetDatabase.Refresh();
        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=16; importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        var pickup=AssetDatabase.LoadAssetAtPath<ExperiencePickupView>(PrefabPath);
        if(pickup==null)
        {
            var go=new GameObject("ExperiencePickup");
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            var playerRenderer=player.GetComponent<SpriteRenderer>();
            if(playerRenderer!=null) { sr.sortingLayerID=playerRenderer.sortingLayerID; sr.sortingOrder=playerRenderer.sortingOrder-1; sr.sharedMaterial=playerRenderer.sharedMaterial; }
            go.AddComponent<ExperiencePickupView>();
            PrefabUtility.SaveAsPrefabAsset(go,PrefabPath); UnityEngine.Object.DestroyImmediate(go);
            pickup=AssetDatabase.LoadAssetAtPath<ExperiencePickupView>(PrefabPath);
        }
        var manager=existing.Length==1?existing[0]:new GameObject("ExperienceDrops").AddComponent<ExperienceDropManager>();
        if(manager.gameObject.scene!=scene) throw new InvalidOperationException("다른 씬의 경험치 관리자가 열려 있습니다.");
        Undo.RecordObject(manager,"Setup Experience Drops");
        var data=new SerializedObject(manager);
        data.FindProperty("player").objectReferenceValue=player;
        data.FindProperty("map").objectReferenceValue=map;
        data.FindProperty("viewPrefab").objectReferenceValue=pickup;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        Debug.Log("EXPERIENCE_SETUP_OK: 씬을 저장해주세요. "+enemyPath+" 의 Experience Amount 기본값은 1입니다.");
    }
    public static void BatchSetup()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        Setup(); EditorSceneManager.SaveOpenScenes();
        string before=File.ReadAllText("Assets/Scenes/GameCreateProject.unity");
        Setup(); EditorSceneManager.SaveOpenScenes();
        if(before!=File.ReadAllText("Assets/Scenes/GameCreateProject.unity")) throw new Exception("Setup is not idempotent");
        Debug.Log("EXPERIENCE_SETUP_IDEMPOTENT");
    }
}
#endif
