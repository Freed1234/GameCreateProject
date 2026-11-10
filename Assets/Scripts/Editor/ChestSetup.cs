#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChestSetup
{
    const string Root="Assets/Art/Chests";
    [MenuItem("Tools/Game UI/Setup Chests")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Play 모드를 종료해주세요.");
        var health=UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
        var movement=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        var map=UnityEngine.Object.FindAnyObjectByType<MapChunkManager>();
        var navigation=UnityEngine.Object.FindAnyObjectByType<EnemyFlowField>();
        var experience=UnityEngine.Object.FindAnyObjectByType<ExperienceDropManager>();
        Scene scene=SceneManager.GetActiveScene();
        if(health==null || movement==null || map==null || navigation==null)throw new InvalidOperationException("플레이어와 맵이 있는 게임 씬을 먼저 여세요.");
        if(experience==null) { ExperienceDropSetup.Setup(); experience=UnityEngine.Object.FindAnyObjectByType<ExperienceDropManager>(); }
        foreach(var component in new Component[]{health,movement,map,navigation,experience})if(component.gameObject.scene!=scene)throw new InvalidOperationException("게임 씬 하나를 활성화해주세요.");
        var managers=UnityEngine.Object.FindObjectsByType<ChestManager>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(managers.Length>1 || (managers.Length==1 && managers[0].gameObject.scene!=scene))throw new InvalidOperationException("ChestManager 중복 또는 다른 씬 연결을 확인해주세요.");
        foreach(string name in new[]{"chest-closed","chest-open","healing","magnet","dash-charge"})
        {
            string path=Root+"/"+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("상자 이미지가 없습니다: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=128;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=name.StartsWith("chest-")?new Vector2(.5f,.025f):new Vector2(.5f,.5f);
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        string prefabPath=Root+"/Chest.prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(prefab==null)
        {
            var root=new GameObject("Chest");var view=root.AddComponent<ChestView>();
            var chest=new GameObject("Body").AddComponent<SpriteRenderer>();chest.transform.SetParent(root.transform,false);
            var item=new GameObject("Reward").AddComponent<SpriteRenderer>();item.transform.SetParent(root.transform,false);
            var playerRenderer=health.GetComponent<SpriteRenderer>();
            if(playerRenderer!=null)foreach(var renderer in new[]{chest,item}){renderer.sortingLayerID=playerRenderer.sortingLayerID;renderer.sortingOrder=playerRenderer.sortingOrder-1;renderer.sharedMaterial=playerRenderer.sharedMaterial;}
            item.sortingOrder=chest.sortingOrder+1;
            var data=new SerializedObject(view);
            data.FindProperty("chest").objectReferenceValue=chest;data.FindProperty("item").objectReferenceValue=item;
            string[] fields={"closedSprite","openSprite","healingSprite","magnetSprite","chargeSprite"};
            string[] files={"chest-closed","chest-open","healing","magnet","dash-charge"};
            for(int i=0;i<5;i++)data.FindProperty(fields[i]).objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/"+files[i]+".png");
            data.ApplyModifiedPropertiesWithoutUndo();view.Display(false,false,ChestManager.Reward.Heal,0);
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);UnityEngine.Object.DestroyImmediate(root);
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        if(prefab.GetComponent<ChestView>()==null)throw new InvalidOperationException("Chest 프리팹의 ChestView를 확인해주세요.");
        var manager=managers.Length==1?managers[0]:new GameObject("Chests").AddComponent<ChestManager>();
        Undo.RecordObject(manager,"Setup Chests");var so=new SerializedObject(manager);
        string[] keys={"health","movement","map","navigation","experience","viewPrefab"};
        UnityEngine.Object[] refs={health,movement,map,navigation,experience,prefab.GetComponent<ChestView>()};
        for(int i=0;i<keys.Length;i++)so.FindProperty(keys[i]).objectReferenceValue=refs[i];
        so.ApplyModifiedProperties();EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        Debug.Log("CHEST_SETUP_OK: 게임 씬을 저장해주세요.");
    }
    public static void BatchSetup()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");Setup();EditorSceneManager.SaveOpenScenes();
        string scene=File.ReadAllText("Assets/Scenes/GameCreateProject.unity"),prefab=File.ReadAllText(Root+"/Chest.prefab");
        Setup();EditorSceneManager.SaveOpenScenes();
        if(scene!=File.ReadAllText("Assets/Scenes/GameCreateProject.unity") || prefab!=File.ReadAllText(Root+"/Chest.prefab"))throw new Exception("Chest setup is not idempotent");
        Debug.Log("CHEST_SETUP_IDEMPOTENT");
    }
}
#endif

