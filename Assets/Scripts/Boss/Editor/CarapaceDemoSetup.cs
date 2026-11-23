#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CarapaceDemoSetup
{
    public const string ScenePath="Assets/Scenes/CarapaceDemo.unity";
    [MenuItem("Tools/Game UI/Create Carapace Demo Scene")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before creating preview scene.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        ImportFrames();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameCreateProject.unity");
        foreach(var script in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if(script==null || script.GetType().Assembly!=typeof(CarapaceDemo).Assembly)continue;
            script.enabled=script is MapChunkManager || script is WorldDepthSort || script is PlayerAttackVisual;
        }
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))canvas.gameObject.SetActive(false);
        var player=Object.FindAnyObjectByType<PlayerProgression>(FindObjectsInactive.Include);
        player.transform.position=new Vector3(-6,-3.2f,0);
        foreach(var rb in Object.FindObjectsByType<Rigidbody2D>())rb.simulated=false;
        var demo=new GameObject("Carapace Motion Showcase");demo.AddComponent<CarapaceDemo>();
        EditorSceneManager.SaveScene(scene,ScenePath);
        AssetDatabase.SaveAssets();Debug.Log("CARAPACE_DEMO_SETUP_PASS");
    }
    static RectInt Bounds(Color32[] data,int width,int height)
    {
        int left=width,right=-1,bottom=height,top=-1;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(data[y*width+x].a>80)
        {left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
        if(right<0)throw new InvalidOperationException("Empty animation frame");
        return new RectInt(left,bottom,right-left+1,top-bottom+1);
    }
    static void ImportFrames()
    {
        foreach(string name in new[]{"Charge","Slam","Volley"})
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            texture.LoadImage(File.ReadAllBytes("Assets/Art/BossPreview/"+name+".png"));
            int cw=texture.width/4,ch=texture.height/2;
            string folder="Assets/Resources/CarapaceDemo/"+name;Directory.CreateDirectory(folder);
            float scale=1;
            for(int i=0;i<8;i++)
            {
                Color[] block=texture.GetPixels((i%4)*cw,(1-i/4)*ch,cw,ch);
                var src=new Color32[block.Length];for(int j=0;j<src.Length;j++)src[j]=block[j];
                RectInt bounds=Bounds(src,cw,ch);
                if(i==0)scale=224f/bounds.height; // Stable per-clip scale: first standing pose is 2.8 units tall.
                const int size=384;var dst=new Color32[size*size];
                int width=Mathf.RoundToInt(bounds.width*scale),height=Mathf.RoundToInt(bounds.height*scale);
                if(width>size-16||height>size-16)throw new InvalidOperationException("Frame exceeds atlas cell: "+name+i);
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    int sx=bounds.x+Mathf.Min(bounds.width-1,Mathf.FloorToInt(x/scale));
                    int sy=bounds.y+Mathf.Min(bounds.height-1,Mathf.FloorToInt(y/scale));
                    dst[(y+16)*size+(size-width)/2+x]=src[sy*cw+sx];
                }
                var frame=new Texture2D(size,size,TextureFormat.RGBA32,false);frame.SetPixels32(dst);frame.Apply();
                string path=folder+"/"+i.ToString("00")+".png";File.WriteAllBytes(path,frame.EncodeToPNG());Object.DestroyImmediate(frame);
            }
            Object.DestroyImmediate(texture);
        }
        ImportEffects();
        AssetDatabase.Refresh();
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/CarapaceDemo"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            bool effect=path.Contains("/Effects/");
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=effect?224:80;
            importer.npotScale=TextureImporterNPOTScale.None;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=effect?new Vector2(.5f,.5f):new Vector2(.5f,16f/384);
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
    }
    static void ImportEffects()
    {
        var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes("Assets/Art/BossPreview/Effects.png"));
        int width=sheet.width/3,height=sheet.height;string[] names={"Rock","Shard","Dust"};
        string folder="Assets/Resources/CarapaceDemo/Effects";Directory.CreateDirectory(folder);
        for(int i=0;i<3;i++)
        {
            var block=sheet.GetPixels(i*width,0,width,height);var src=new Color32[block.Length];for(int j=0;j<src.Length;j++)src[j]=block[j];
            var bounds=Bounds(src,width,height);float scale=224f/Mathf.Max(bounds.width,bounds.height);
            int w=Mathf.RoundToInt(bounds.width*scale),h=Mathf.RoundToInt(bounds.height*scale);var dst=new Color32[256*256];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)dst[(y+(256-h)/2)*256+x+(256-w)/2]=src[(bounds.y+Mathf.Min(bounds.height-1,Mathf.FloorToInt(y/scale)))*width+bounds.x+Mathf.Min(bounds.width-1,Mathf.FloorToInt(x/scale))];
            var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.SetPixels32(dst);texture.Apply();File.WriteAllBytes(folder+"/"+names[i]+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
        }
        Object.DestroyImmediate(sheet);
    }
}
#endif
