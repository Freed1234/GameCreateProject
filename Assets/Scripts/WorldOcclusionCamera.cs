using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class WorldOcclusionCamera : MonoBehaviour
{
    [SerializeField] private Material maskMaterial;
    private Camera target;
    private RenderTexture mask;
    private CommandBuffer commands;
    private readonly List<WorldDepthSort> visible = new List<WorldDepthSort>();
    private readonly Dictionary<Sprite, Mesh> meshes = new Dictionary<Sprite, Mesh>();
    private readonly Plane[] planes = new Plane[6];
    private MaterialPropertyBlock properties;

    private void OnEnable()
    {
        target = GetComponent<Camera>();
        commands = new CommandBuffer { name = "Character occlusion mask" };
        properties = new MaterialPropertyBlock();
        RenderPipelineManager.beginCameraRendering += RenderMask;
    }

    private Mesh SpriteMesh(Sprite sprite)
    {
        if (meshes.TryGetValue(sprite, out var mesh)) return mesh;
        Vector2[] source = sprite.vertices;
        var vertices = new Vector3[source.Length];
        for (int i = 0; i < source.Length; i++) vertices[i] = source[i];
        ushort[] sourceIndices = sprite.triangles;
        var indices = new int[sourceIndices.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = sourceIndices[i];
        mesh = new Mesh { name = sprite.name + " Occlusion mask" };
        mesh.vertices = vertices;
        mesh.uv = sprite.uv;
        mesh.triangles = indices;
        mesh.UploadMeshData(true);
        meshes.Add(sprite, mesh);
        return mesh;
    }

    private void RenderMask(ScriptableRenderContext context, Camera camera)
    {
        Shader.SetGlobalFloat("_OcclusionAvailable", camera == target && maskMaterial != null ? 1f : 0f);
        if (camera != target || maskMaterial == null) return;
        int width = Mathf.Max(1, camera.pixelWidth), height = Mathf.Max(1, camera.pixelHeight);
        if (mask == null || mask.width != width || mask.height != height)
        {
            ReleaseMask();
            mask = new RenderTexture(width, height, 0, RenderTextureFormat.RGFloat)
            { name = "Obstacle depth mask", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            mask.Create();
        }
        GeometryUtility.CalculateFrustumPlanes(camera, planes);
        visible.Clear();
        foreach (var obstacle in WorldDepthSort.Obstacles)
        {
            foreach (var renderer in obstacle.Renderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
                    && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0
                    && GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                { visible.Add(obstacle); break; }
            }
        }
        visible.Sort((a, b) => a.Order.CompareTo(b.Order));
        Matrix4x4 projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
        commands.Clear();
        commands.SetRenderTarget(mask);
        commands.ClearRenderTarget(false, true, Color.clear);
        commands.SetGlobalMatrix("_OcclusionVP", projection);
        foreach (var obstacle in visible)
        {
            foreach (var renderer in obstacle.Renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null
                    || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                properties.Clear();
                properties.SetTexture("_MainTex", renderer.sprite.texture);
                properties.SetFloat("_ObstacleOrder", obstacle.Order);
                properties.SetFloat("_Opacity", renderer.color.a);
                Matrix4x4 flip = Matrix4x4.Scale(new Vector3(renderer.flipX ? -1 : 1, renderer.flipY ? -1 : 1, 1));
                commands.DrawMesh(SpriteMesh(renderer.sprite), renderer.localToWorldMatrix * flip, maskMaterial, 0, 0, properties);
            }
        }
        commands.SetGlobalTexture("_ObstacleMask", mask);
        Graphics.ExecuteCommandBuffer(commands);
    }

    private void ReleaseMask()
    {
        if (mask == null) return;
        mask.Release();
        Destroy(mask);
        mask = null;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= RenderMask;
        commands?.Release();
        commands = null;
        ReleaseMask();
        foreach (var mesh in meshes.Values) Destroy(mesh);
        meshes.Clear();
        Shader.SetGlobalTexture("_ObstacleMask", Texture2D.blackTexture);
    }
}
