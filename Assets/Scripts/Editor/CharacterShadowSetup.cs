#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class CharacterShadowSetup
{
    const string Root="Assets/Art/CharacterShadows";
    [MenuItem("Tools/Game UI/Setup Character Shadows")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Play 모드를 종료해주세요.");
        Scene scene=SceneManager.GetActiveScene();
        var players=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerHealth>(true)).ToArray();
        var spawners=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EnemySpawner>(true)).ToArray();
        if(players.Length!=1 || spawners.Length!=1)throw new InvalidOperationException("Player와 EnemySpawner가 하나씩 있는 게임 씬을 여세요.");
        var player=players[0];
        var playerRenderer=player.GetComponent<SpriteRenderer>();
        var motion=player.GetComponent<PlayerAttackVisual>();
        Sprite reference=motion!=null?new SerializedObject(motion).FindProperty("idleSprite").objectReferenceValue as Sprite:null;
        var spawnData=new SerializedObject(spawners[0]);
        string enemyPath=AssetDatabase.GetAssetPath(spawnData.FindProperty("enemyPrefab").objectReferenceValue);
        var enemyAsset=AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
        var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if(playerRenderer==null || reference==null || enemyAsset==null || enemyAsset.GetComponent<SpriteRenderer>()==null
            || enemyAsset.GetComponent<CircleCollider2D>()==null || shader==null)
            throw new InvalidOperationException("플레이어 모션의 Idle Sprite, 적 프리팹의 루트 Renderer/Collider, URP 2D 셰이더 연결을 확인해주세요.");
        Directory.CreateDirectory(Root);
        string path=Root+"/pixel-oval.png";
        if(!File.Exists(path))
        {
            var texture=new Texture2D(32,12,TextureFormat.RGBA32,false);
            var pixels=new Color32[32*12];
            for(int y=0;y<12;y++)for(int x=0;x<32;x++)
            {
                float dx=(x-15.5f)/16f,dy=(y-5.5f)/6f;
                pixels[y*32+x]=dx*dx+dy*dy<=1f?new Color32(255,255,255,255):new Color32(0,0,0,0);
            }
            texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        }
        AssetDatabase.Refresh();
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);importer.SaveAndReimport();
        Sprite sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        string materialPath=Root+"/GroundShadow.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        var contents=PrefabUtility.LoadPrefabContents(enemyPath);
        try
        {
            var collider=contents.GetComponent<CircleCollider2D>();
            Connect(contents,sprite,material,new Vector2(collider.offset.x,collider.offset.y-collider.radius+.025f),collider.radius*2.2f,collider.radius*.7f,false);
            PrefabUtility.SaveAsPrefabAsset(contents,enemyPath);
        }
        finally {PrefabUtility.UnloadPrefabContents(contents);}
        // CharacterMotion/NoviceMotion normalize both bodies to this reference height.
        float bodyHeight=reference.bounds.size.y;
        Connect(player.gameObject,sprite,material,new Vector2(0f,-bodyHeight*.5f+bodyHeight*.025f),bodyHeight*.47f,bodyHeight*.16f,true);
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_SHADOWS_OK: 플레이어와 현재 스포너의 적 프리팹에 연결했습니다. 씬을 저장해주세요.");
    }
    static void Connect(GameObject owner,Sprite sprite,Material material,Vector2 foot,float width,float height,bool undo)
    {
        var shadow=owner.GetComponent<CharacterGroundShadow>();bool first=shadow==null;
        if(first)shadow=undo?Undo.AddComponent<CharacterGroundShadow>(owner):owner.AddComponent<CharacterGroundShadow>();
        var so=new SerializedObject(shadow);
        so.FindProperty("targetRenderer").objectReferenceValue=owner.GetComponent<SpriteRenderer>();
        so.FindProperty("shadowSprite").objectReferenceValue=sprite;
        so.FindProperty("shadowMaterial").objectReferenceValue=material;
        if(first)
        {
            so.FindProperty("footOffset").vector2Value=foot;
            so.FindProperty("width").floatValue=width;
            so.FindProperty("height").floatValue=height;
        }
        if(undo)so.ApplyModifiedProperties();else so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shadow);
    }
    public static void BatchSetup()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");Setup();EditorSceneManager.SaveOpenScenes();
        var spawner=Object.FindAnyObjectByType<EnemySpawner>();
        string enemyPath=AssetDatabase.GetAssetPath(new SerializedObject(spawner).FindProperty("enemyPrefab").objectReferenceValue);
        string sceneBefore=File.ReadAllText("Assets/Scenes/GameCreateProject.unity"),enemyBefore=File.ReadAllText(enemyPath);
        Setup();EditorSceneManager.SaveOpenScenes();
        if(sceneBefore!=File.ReadAllText("Assets/Scenes/GameCreateProject.unity") || enemyBefore!=File.ReadAllText(enemyPath))throw new Exception("Shadow setup is not idempotent");
        Debug.Log("CHARACTER_SHADOWS_IDEMPOTENT");
    }
}
#endif
