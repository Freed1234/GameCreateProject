#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class NoviceCharacterMotionSetup
{
    public const string Root = "Assets/Textures/NoviceMotion";
    private const int CanvasWidth = 384, CanvasHeight = 320, BodyHeight = 256, Bottom = 16;
    private const byte AlphaCutoff = 224;

    [MenuItem("Tools/Game UI/Setup Novice Character Motions")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Play 모드를 끄고 실행해주세요."); return; }
        PlayerHealth player = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (player == null) { Debug.LogError("PlayerHealth가 있는 Player를 찾지 못했습니다."); return; }
        PlayerAttackVisual visual = player.GetComponent<PlayerAttackVisual>();
        if (visual == null) visual = Undo.AddComponent<PlayerAttackVisual>(player.gameObject);
        SerializedObject so = new SerializedObject(visual);
        SpriteRenderer sr = so.FindProperty("targetRenderer").objectReferenceValue as SpriteRenderer;
        if (sr == null) sr = player.GetComponent<SpriteRenderer>();
        if (sr == null) { Debug.LogError("Player의 SpriteRenderer가 필요합니다."); return; }
        Sprite original = so.FindProperty("idleSprite").objectReferenceValue as Sprite;
        if (original == null) original = sr.sprite;
        if (original == null) { Debug.LogError("기존 캐릭터 이미지가 필요합니다."); return; }
        // Read legacy source size every time, never compound the scale on repeated setup.
        float height = Mathf.Max(0.1f, original.bounds.size.y);
        float ppu = BodyHeight / height;
        Vector2 pivot = new Vector2(0.5f, (Bottom + BodyHeight * 0.5f) / CanvasHeight);
        Directory.CreateDirectory(Root + "/Frames");
        foreach (string direction in new[] { "right", "left", "down", "up" })
        {
            Texture2D sheet = LoadSheet(Root + "/" + direction + "-sheet.png");
            if (sheet == null) throw new InvalidOperationException(direction + " 시트가 없습니다.");
            Texture2D walkSheet = sheet;
            SerializedProperty set = so.FindProperty("novice" + char.ToUpperInvariant(direction[0]) + direction.Substring(1));
            Assign(set.FindPropertyRelative("walk"), walkSheet, direction, "walk", 0, 0, 4, 3, ppu, pivot);
            Assign(set.FindPropertyRelative("idle"), sheet, direction, "idle", 1, 0, 2, 3, ppu, pivot);
            Assign(set.FindPropertyRelative("hurt"), sheet, direction, "hurt", 1, 2, 2, 3, ppu, pivot);
            Assign(set.FindPropertyRelative("attack"), sheet, direction, "attack", 2, 0, 3, 3, ppu, pivot);
        }
        so.FindProperty("targetRenderer").objectReferenceValue = sr;
        if (so.FindProperty("idleSprite").objectReferenceValue == null)
            so.FindProperty("idleSprite").objectReferenceValue = original;
        Texture2D reference = LoadSheet(Root + "/reference.png");
        Texture2D fallback = PackFrame(reference, new RectInt(0, 0, reference.width, reference.height));
        string fallbackPath = Root + "/fallback.png";
        File.WriteAllBytes(fallbackPath, fallback.EncodeToPNG());
        Object.DestroyImmediate(fallback);
        AssetDatabase.ImportAsset(fallbackPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter fallbackImporter = (TextureImporter)AssetImporter.GetAtPath(fallbackPath);
        fallbackImporter.textureType = TextureImporterType.Sprite;
        fallbackImporter.spriteImportMode = SpriteImportMode.Single;
        fallbackImporter.spritePixelsPerUnit = ppu;
        TextureImporterSettings fallbackSettings = new TextureImporterSettings();
        fallbackImporter.ReadTextureSettings(fallbackSettings);
        fallbackSettings.spriteAlignment = (int)SpriteAlignment.Custom;
        fallbackSettings.spritePivot = pivot;
        fallbackImporter.SetTextureSettings(fallbackSettings);
        fallbackImporter.filterMode = FilterMode.Point;
        fallbackImporter.textureCompression = TextureImporterCompression.Uncompressed;
        fallbackImporter.mipmapEnabled = false;
        fallbackImporter.alphaIsTransparency = true;
        fallbackImporter.npotScale = TextureImporterNPOTScale.None;
        fallbackImporter.SaveAndReimport();
        so.FindProperty("noviceFallbackSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(visual);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Selection.activeGameObject = player.gameObject;
        AssetDatabase.SaveAssets();
        Debug.Log("[CharacterMotion] 미전직 44개 모션 프레임 연결 완료. Player에서 재생 속도를 조절하고 Ctrl+S로 씬을 저장하세요.", player);
    }

    private static Texture2D LoadSheet(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static void Assign(SerializedProperty array, Texture2D sheet, string direction, string action,
        int row, int col, int count, int rows, float ppu, Vector2 pivot)
    {
        array.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            string path = Root + "/Frames/" + direction + "-" + action + "-" + i + ".png";
            int sourceIndex = direction == "right" && action == "attack" ? (i == 0 ? 1 : i == 1 ? 0 : 2) : i;
            int x0 = (col + sourceIndex) * sheet.width / 4, x1 = (col + sourceIndex + 1) * sheet.width / 4;
            int y0 = sheet.height - (row + 1) * sheet.height / rows;
            int y1 = sheet.height - row * sheet.height / rows;
            Texture2D frame = PackFrame(sheet, new RectInt(x0, y0, x1 - x0, y1 - y0));
            File.WriteAllBytes(path, frame.EncodeToPNG());
            Object.DestroyImmediate(frame);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            array.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
    // Deterministic atlas preparation: trim alpha, align head/ground, nearest-neighbor pack.
    // It does not invent intermediate poses; all pose pixels come from the authored sheets.
    private static Texture2D PackFrame(Texture2D sheet, RectInt cell)
    {
        Color[] source = sheet.GetPixels(cell.x, cell.y, cell.width, cell.height);
        int minX = cell.width, minY = cell.height, maxX = -1, maxY = -1;
        float cutoff = AlphaCutoff / 255f;
        for (int y = 0; y < cell.height; y++) for (int x = 0; x < cell.width; x++)
            if (source[y * cell.width + x].a >= cutoff)
            { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
        if (maxY < minY) throw new InvalidOperationException("투명한 프레임이 발견되었습니다.");
        float factor = (float)BodyHeight / (maxY - minY + 1);
        int headMin = cell.width, headMax = -1;
        int bandY = minY + Mathf.RoundToInt((maxY - minY) * 0.76f);
        for (int y = bandY; y <= Mathf.Min(maxY, bandY + 4); y++) for (int x = minX; x <= maxX; x++)
            if (source[y * cell.width + x].a >= cutoff) { headMin = Mathf.Min(headMin, x); headMax = Mathf.Max(headMax, x); }
        float anchorX = headMax >= headMin ? (headMin + headMax) * 0.5f : (minX + maxX) * 0.5f;
        Color[] pixels = new Color[CanvasWidth * CanvasHeight];
        for (int y = Bottom; y < Bottom + BodyHeight; y++) for (int x = 0; x < CanvasWidth; x++)
        {
            int sx = Mathf.RoundToInt((x - CanvasWidth * 0.5f) / factor + anchorX);
            int sy = minY + Mathf.Min(maxY - minY, Mathf.FloorToInt((y - Bottom) / factor));
            if (sx < 0 || sx >= cell.width) continue;
            Color c = source[sy * cell.width + sx];
            if (c.a < cutoff) continue;
            c.a = 1f;
            pixels[y * CanvasWidth + x] = c;
        }
        Texture2D texture = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels); texture.Apply();
        return texture;
    }
}
#endif

