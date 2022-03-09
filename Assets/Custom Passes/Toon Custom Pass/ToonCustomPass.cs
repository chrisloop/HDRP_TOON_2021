using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

#if UNITY_EDITOR
    using UnityEditor.Rendering.HighDefinition;

    [CustomPassDrawerAttribute(typeof(ToonCustomPass))]
    class EdgeCustomPassEditor : CustomPassDrawer
    {
       protected override PassUIFlag commonPassUIFlags => PassUIFlag.Name;
    }
#endif

class ToonCustomPass : CustomPass
{
    public LayerMask    layerMask = 1;
    public float        edgeThickness = 1;
    public float        colorThreshold = 0;
    public float        colorMultiplier = 1;
    public float        colorBias = 1;
    public float        depthThreshold = 0;
    public float        depthMultiplier = 1;
    public float        depthBias = 1;
    public float        normalThreshold = 0;
    public float        normalMultiplier = 1;
    public float        normalBias = 1;

    [SerializeField, HideInInspector]
    Shader      colorMapShader;
    Material    colorMapMaterial;

    [SerializeField, HideInInspector]
    Shader      edgeShader;
    Material    edgeMaterial;

    RTHandle    colorMapBuffer;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        AssignObjectIDs(); // unique color for each object

        colorMapShader = Shader.Find("Hidden/ColorMap");
        colorMapMaterial = CoreUtils.CreateEngineMaterial(colorMapShader);

        edgeShader = Shader.Find("Hidden/Edge");
        edgeMaterial = CoreUtils.CreateEngineMaterial(edgeShader);

        // Temporary buffers
        colorMapBuffer = RTHandles.Alloc(
            Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
            useDynamicScale: true, name: "Color Map Buffer"
        );
    }

    protected override void Execute(CustomPassContext ctx)
    {
        // color map pass (vertex color and object id)
        CoreUtils.SetRenderTarget(ctx.cmd, colorMapBuffer, ctx.cameraDepthBuffer, ClearFlag.Color);
        CustomPassUtils.DrawRenderers(ctx, layerMask, CustomPass.RenderQueueType.All, colorMapMaterial);

        // Setup edge effect properties
        ctx.propertyBlock.SetTexture("_EdgeBuffer", colorMapBuffer);
        ctx.propertyBlock.SetFloat("_EdgeThickness", edgeThickness);

        ctx.propertyBlock.SetFloat("_ColorThreshold", colorThreshold);
        ctx.propertyBlock.SetFloat("_ColorMultiplier", colorMultiplier);
        ctx.propertyBlock.SetFloat("_ColorBias", colorBias);

        ctx.propertyBlock.SetFloat("_NormalThreshold", normalThreshold);
        ctx.propertyBlock.SetFloat("_NormalMultiplier", normalMultiplier);
        ctx.propertyBlock.SetFloat("_NormalBias", normalBias);

        ctx.propertyBlock.SetFloat("_DepthThreshold", depthThreshold);
        ctx.propertyBlock.SetFloat("_DepthMultiplier", depthMultiplier);
        ctx.propertyBlock.SetFloat("_DepthBias", depthBias);

        ctx.propertyBlock.SetTexture("_ColorMap", colorMapBuffer);
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.customColorBuffer.Value, ClearFlag.All);
        CoreUtils.DrawFullScreen(ctx.cmd, edgeMaterial, ctx.customColorBuffer.Value, shaderPassId: 0, properties: ctx.propertyBlock);
    }

    // Resize the render texture to match the aspect ratio of the camera (it avoid stretching issues).
    void SyncRenderTextureAspect(RenderTexture rt, Camera camera)
    {
        float aspect = rt.width / (float)rt.height;

        if (!Mathf.Approximately(aspect, camera.aspect))
        {
            rt.Release();
            rt.width = camera.pixelWidth;
            rt.height = camera.pixelHeight;
            rt.Create();
        }
    }

    public virtual void AssignObjectIDs()
    {
        var rendererList = Resources.FindObjectsOfTypeAll(typeof(Renderer));

        int index = 0;
        foreach (Renderer renderer in rendererList)
        {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            float hue = (float)index / rendererList.Length;
            propertyBlock.SetColor("_ObjectColor", Color.HSVToRGB(hue, 0.7f, 1.0f));
            renderer.SetPropertyBlock(propertyBlock);
            index++;
        }
    }

    protected override void Cleanup()
    {
        colorMapBuffer.Release();
        CoreUtils.Destroy(edgeMaterial);
        CoreUtils.Destroy(colorMapMaterial);
    }
}