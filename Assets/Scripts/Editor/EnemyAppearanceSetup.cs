#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyAppearanceSetup
{
    const string Root="Assets/Art/Enemies/RandomAppearance";
    static readonly string[] Colors={"green","blue","purple","red","yellow"};
    [Serializable] class Metrics { public float slimeWidthPixels; public float mushroomWidthPixels; }

    [MenuItem("Tools/Game UI/Setup Random Enemy Appearance")]
    public static void Setup() {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Play 모드를 종료해주세요.");
        var spawner=UnityEngine.Object.FindAnyObjectByType<EnemySpawner>();
        if(spawner==null) throw new InvalidOperationException("EnemySpawner가 있는 게임 씬을 먼저 열어주세요.");
        var spawnData=new SerializedObject(spawner);
        var prefab=spawnData.FindProperty("enemyPrefab").objectReferenceValue;
        string prefabPath=AssetDatabase.GetAssetPath(prefab);
        if(string.IsNullOrEmpty(prefabPath)) throw new InvalidOperationException("스포너의 Enemy Prefab 연결을 확인해주세요.");
        var metrics=JsonUtility.FromJson<Metrics>(File.ReadAllText(Root+"/metrics.json"));
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try {
            var visual=root.GetComponent<EnemyAppearance>();bool first=visual==null;
            if(first) visual=root.AddComponent<EnemyAppearance>();
            var renderer=root.GetComponent<SpriteRenderer>();
            var collider=root.GetComponent<CircleCollider2D>();
            if(renderer==null || collider==null) throw new InvalidOperationException("기존 루트 SpriteRenderer와 CircleCollider2D가 필요합니다.");
            float foot=collider.offset.y-collider.radius;
            var so=new SerializedObject(visual);
            Connect(so,"slimes","Slime",16,metrics.slimeWidthPixels/Mathf.Max(.1f,visual.SlimeWidth),foot);
            Connect(so,"mushrooms","Mushroom",8,metrics.mushroomWidthPixels/Mathf.Max(.1f,visual.MushroomWidth),foot);
            so.ApplyModifiedPropertiesWithoutUndo();
            // The old placeholder is tinted red. Set a neutral base once; runtime tint is never modified.
            if(first) { renderer.color=new Color(1,1,1,renderer.color.a); renderer.flipX=false; }
            // Keep the original fallback sprite, renderer material/order and all physical properties.
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        } finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Debug.Log("ENEMY_APPEARANCE_SETUP_OK: "+prefabPath);
    }

    static void Connect(SerializedObject so,string field,string species,int count,float ppu,float foot) {
        var variants=so.FindProperty(field);variants.arraySize=Colors.Length;
        for(int c=0;c<Colors.Length;c++) {
            var variant=variants.GetArrayElementAtIndex(c);variant.FindPropertyRelative("name").stringValue=Colors[c];
            var frames=variant.FindPropertyRelative("frames");frames.arraySize=count;
            for(int f=0;f<count;f++) {
                string path=$"{Root}/{species}/{Colors[c]}/walk_{f+1:00}.png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null) throw new FileNotFoundException("프레임이 없습니다.",path);
                Vector2 pivot=new Vector2(.5f,(20f-foot*ppu)/256f);
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                bool dirty=importer.textureType!=TextureImporterType.Sprite || importer.spriteImportMode!=SpriteImportMode.Single
                    || importer.filterMode!=FilterMode.Point || importer.textureCompression!=TextureImporterCompression.Uncompressed
                    || importer.mipmapEnabled || importer.isReadable || !Mathf.Approximately(importer.spritePixelsPerUnit,ppu)
                    || settings.spritePivot!=pivot || settings.spriteAlignment!=(int)SpriteAlignment.Custom || settings.spriteMeshType!=SpriteMeshType.FullRect;
                if(dirty) {
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.spritePixelsPerUnit=ppu;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.isReadable=false;
                    importer.wrapMode=TextureWrapMode.Clamp;
                    importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;settings.spriteMeshType=SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);importer.SaveAndReimport();
                }
                frames.GetArrayElementAtIndex(f).objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
    }
    public static void BatchSetup() {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");Setup();
        string prefabPath=AssetDatabase.GetAssetPath(new SerializedObject(UnityEngine.Object.FindAnyObjectByType<EnemySpawner>()).FindProperty("enemyPrefab").objectReferenceValue);
        string before=File.ReadAllText(prefabPath);Setup();
        if(before!=File.ReadAllText(prefabPath))throw new Exception("Repeated setup changed prefab");
        Debug.Log("ENEMY_APPEARANCE_IDEMPOTENT_OK");
    }
}
#endif

