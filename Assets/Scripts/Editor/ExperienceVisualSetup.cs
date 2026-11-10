#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ExperienceVisualSetup
{
    const string Root = "Assets/Art/ExperienceVisuals";
    [MenuItem("Tools/Game UI/Setup Experience Visuals")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 모드를 종료해주세요.");
        var scene = SceneManager.GetActiveScene();
        var players = UnityEngine.Object.FindObjectsByType<PlayerProgression>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var managers = UnityEngine.Object.FindObjectsByType<ExperienceDropManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length != 1 || managers.Length != 1 || players[0].gameObject.scene != scene || managers[0].gameObject.scene != scene)
            throw new InvalidOperationException("Player와 ExperienceDropManager가 하나씩 있는 게임 씬을 여세요. 경험치 시스템을 먼저 연결해주세요.");
        var player = players[0];
        var sr = player.GetComponent<SpriteRenderer>();
        if (sr == null) throw new InvalidOperationException("Player의 SpriteRenderer가 없습니다.");
        var managerData = new SerializedObject(managers[0]);
        var prefab = managerData.FindProperty("viewPrefab").objectReferenceValue;
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("경험치 표시 프리팹 참조가 없습니다.");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/ExperienceSilhouette.shader");
        if (shader == null) throw new InvalidOperationException("ExperienceSilhouette.shader 파일이 없습니다.");
        PrepareArt();
        string matPath = Root + "/ExperienceGlow.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view = contents.GetComponent<ExperiencePickupView>();
            if (view == null) throw new InvalidOperationException("경험치 프리팹 루트에 ExperiencePickupView가 없습니다.");
            var data = new SerializedObject(view);
            var frames = data.FindProperty("sparkleFrames");
            frames.arraySize = 4;
            for (int i = 0; i < 4; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = Sprite("orb-" + i);
            data.FindProperty("bodyWidth").floatValue = .5f; // 8 body pixels / 16 pixels per unit.
            data.ApplyModifiedPropertiesWithoutUndo();
            var renderer = contents.GetComponent<SpriteRenderer>();
            renderer.sprite = Sprite("orb-0");
            renderer.color = Color.white;
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        var effect = player.GetComponent<PlayerLevelUpEffect>();
        if (effect == null) effect = Undo.AddComponent<PlayerLevelUpEffect>(player.gameObject);
        Undo.RecordObject(effect, "Setup Experience Visuals");
        var fx = new SerializedObject(effect);
        fx.FindProperty("targetRenderer").objectReferenceValue = sr;
        fx.FindProperty("effectMaterial").objectReferenceValue = mat;
        fx.FindProperty("ringSprite").objectReferenceValue = Sprite("ring");
        fx.FindProperty("crossSprite").objectReferenceValue = Sprite("cross");
        fx.FindProperty("squareSprite").objectReferenceValue = Sprite("square");
        fx.ApplyModifiedProperties();
        EditorUtility.SetDirty(effect);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("EXPERIENCE_VISUALS_OK: 씬을 저장해주세요. 기존 흡수/성장 수치는 변경하지 않았습니다.");
    }
    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/" + name + ".png");
    static void Save(string name, int width, int height, Func<int,int,Color32> pixel)
    {
        string path = Root + "/" + name + ".png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false);
            var pixels = new Color32[width*height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) pixels[y*width+x]=pixel(x,y);
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
    }
    public static void PrepareArt()
    {
        Directory.CreateDirectory(Root);
        Color32 clear = new Color32(0,0,0,0), white = new Color32(255,255,235,255), lime = new Color32(193,244,130,255);
        for(int frame=0;frame<4;frame++)
        {
            int f=frame;
            Save("orb-"+f,24,24,(x,y)=> {
                if(x>=8 && x<=15 && y>=8 && y<=15)
                {
                    if((x==8 || x==15) && (y==8 || y==15)) return clear;
                    return y>=14 || x==15 ? lime : white;
                }
                int sx=f<2?19:5, sy=f<2?18:7;
                int d=Math.Abs(x-sx)+Math.Abs(y-sy);
                if(d<=(f%2==0?1:2)) return f%2==0?lime:white;
                return clear;
            });
        }
        Save("ring",32,16,(x,y)=> {
            float d=(x-15.5f)*(x-15.5f)/(15*15)+(y-7.5f)*(y-7.5f)/(6*6);
            return d>=.72f && d<=1.04f?white:clear;
        });
        Save("cross",5,5,(x,y)=>x==2 || y==2?white:clear);
        Save("square",2,2,(x,y)=>white);
    }
    public static void BatchSetup()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        Setup(); EditorSceneManager.SaveOpenScenes();
        string before=File.ReadAllText("Assets/Scenes/GameCreateProject.unity");
        Setup(); EditorSceneManager.SaveOpenScenes();
        if(before!=File.ReadAllText("Assets/Scenes/GameCreateProject.unity")) throw new Exception("Setup is not idempotent");
        Debug.Log("EXPERIENCE_VISUALS_IDEMPOTENT");
    }
}
#endif
