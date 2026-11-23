#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CarapaceMotionSetup
{
    public static void Import()
    {
        var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);
        sheet.LoadImage(File.ReadAllBytes("Assets/Art/BossPreview/Locomotion.png"));
        float scale=1;
        for(int i=0;i<12;i++)
        {
            int column=i%4,row=i/4,x0=column*sheet.width/4,x1=(column+1)*sheet.width/4;
            int y0=sheet.height-(row+1)*sheet.height/3,y1=sheet.height-row*sheet.height/3;
            int w=x1-x0,h=y1-y0;var source=sheet.GetPixels(x0,y0,w,h);
            int left=w,right=-1,bottom=h,top=-1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(source[y*w+x].a>.32f)
            {left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
            if(right<left)throw new Exception("Empty motion cell "+i);
            if(i==0)scale=224f/(top-bottom+1);
            int width=Mathf.RoundToInt((right-left+1)*scale),height=Mathf.RoundToInt((top-bottom+1)*scale);
            const int size=384;var pixels=new Color[size*size];
            if(width>size-16||height>size-16)throw new Exception("Oversized motion frame");
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                pixels[(y+16)*size+(size-width)/2+x]=source[(bottom+Math.Min(top-bottom,Mathf.FloorToInt(y/scale)))*w+left+Math.Min(right-left,Mathf.FloorToInt(x/scale))];
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels(pixels);texture.Apply();
            string folder="Assets/Resources/CarapaceDemo/"+(i<8?"Walk":"Idle");Directory.CreateDirectory(folder);
            string path=folder+"/"+(i<8?i:i-8).ToString("00")+".png";
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        }
        Object.DestroyImmediate(sheet);AssetDatabase.Refresh();
        foreach(string folder in new[]{"Assets/Resources/CarapaceDemo/Walk","Assets/Resources/CarapaceDemo/Idle"})
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{folder}))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=80;
            importer.npotScale=TextureImporterNPOTScale.None;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,16f/384);
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        Debug.Log("CARAPACE_MOTION_IMPORT_PASS");
    }
}
#endif
