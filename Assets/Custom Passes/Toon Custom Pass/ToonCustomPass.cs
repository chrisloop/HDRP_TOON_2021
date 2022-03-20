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

    public float            edgeThickness = 1;
    public float            colorThreshold = 0;
    public float            colorMultiplier = 1;
    public float            colorBias = 1;
    public float            depthThreshold = 0;
    public float            depthMultiplier = 1;
    public float            depthBias = 1;
    public float            normalThreshold = 0;
    public float            normalMultiplier = 1;
    public float            normalBias = 1;
    public float            noiseScale = 1;
    public float            noiseMinMultiplier = 1;
    public float            noiseMaxMultiplier = 1;
    public Vector2          noiseStep = new Vector2(0,1);
    public RenderTexture    renderTexture = null;


    [SerializeField, HideInInspector] Material    colorMapMaterial;
    [SerializeField, HideInInspector] Material    colorMapTransMaterial;
    [SerializeField, HideInInspector] Material    colorMapCompositeMaterial;
    [SerializeField, HideInInspector] Material    edgeMaterial;
    [SerializeField, HideInInspector] Material    noiseMaterial;    

    RTHandle    colorMapBufferComposite;
    RTHandle    colorMapBufferOpaque;
    RTHandle    noiseBuffer;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    { 
        AssignObjectIDs(); // unique color for each object

        colorMapMaterial            = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/ColorMapOpaque"));
        colorMapTransMaterial       = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/ColorMapTrans"));
        colorMapCompositeMaterial   = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/ColorMapComposite"));
        edgeMaterial                = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Edge"));
        noiseMaterial               = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Noise"));

        //
        // Temporary buffers
        //
        colorMapBufferComposite = RTHandles.Alloc(
            Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R16G16B16A16_UNorm,
            useDynamicScale: true, name: "Color Map Buffer Composite"
        );

        colorMapBufferOpaque = RTHandles.Alloc(
            Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R16G16B16A16_UNorm,
            useDynamicScale: true, name: "Color Map Buffer Opaque"
        );

        noiseBuffer = RTHandles.Alloc(
            Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R16G16B16A16_UNorm,
            useDynamicScale: true, name: "Noise Buffer"
        );
    }

    protected override void Execute(CustomPassContext ctx)
    {
        // opaque color map pass (vertex color and object id)
        CoreUtils.SetRenderTarget(ctx.cmd, colorMapBufferOpaque, ctx.cameraDepthBuffer, ClearFlag.Color);
        CustomPassUtils.DrawRenderers(ctx, layerMask, CustomPass.RenderQueueType.AllOpaque, colorMapMaterial);

        // transparent color map pass (vertex color and object id)
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.customColorBuffer.Value, ctx.cameraDepthBuffer, ClearFlag.Color);
        CustomPassUtils.DrawRenderers(ctx, layerMask, CustomPass.RenderQueueType.AllTransparent, colorMapTransMaterial);

        // composite color map full screen pass
        ctx.propertyBlock.SetTexture("_ColorMapBufferOpaque", colorMapBufferOpaque);
        ctx.propertyBlock.SetTexture("_ColorMapBufferTransparent", ctx.customColorBuffer.Value);
        CoreUtils.SetRenderTarget(ctx.cmd, colorMapBufferComposite, ClearFlag.All);
        CoreUtils.DrawFullScreen(ctx.cmd, colorMapCompositeMaterial, colorMapBufferComposite, shaderPassId: 0, properties: ctx.propertyBlock); 

        // world space noise pass
        noiseMaterial.SetFloat("_NoiseScale", noiseScale);
        noiseMaterial.SetVector("_NoiseStep", noiseStep);
        CoreUtils.SetRenderTarget(ctx.cmd, noiseBuffer, ctx.cameraDepthBuffer, ClearFlag.Color);
        CustomPassUtils.DrawRenderers(ctx, layerMask, CustomPass.RenderQueueType.All, noiseMaterial);

        // Setup edge pass properties
        ctx.propertyBlock.Clear();

        ctx.propertyBlock.SetTexture("_NoiseBuffer", noiseBuffer);
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

        ctx.propertyBlock.SetFloat("_NoiseMinMultiplier", noiseMinMultiplier);
        ctx.propertyBlock.SetFloat("_NoiseMaxMultiplier", noiseMaxMultiplier);

        ctx.propertyBlock.SetTexture("_ColorMap", colorMapBufferComposite);

        // Write final edges to the included customColorBuffer
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.customColorBuffer.Value, ClearFlag.All);
        CoreUtils.DrawFullScreen(ctx.cmd, edgeMaterial, ctx.customColorBuffer.Value, shaderPassId: edgeMaterial.FindPass("Edge Pass"), properties: ctx.propertyBlock);

        // Optionally write the final edges to a render texture for use in the shadow custom pass
        if (renderTexture != null)
        {
            SyncRenderTextureAspect(renderTexture, ctx.hdCamera.camera);
            CoreUtils.SetRenderTarget(ctx.cmd, renderTexture, renderTexture.depthBuffer, ClearFlag.All);
            CoreUtils.DrawFullScreen(ctx.cmd, edgeMaterial, renderTexture, shaderPassId:  edgeMaterial.FindPass("Copy Pass"), properties: ctx.propertyBlock);        
        }
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
        colorMapBufferOpaque.Release();
        colorMapBufferComposite.Release();
        noiseBuffer.Release();

        CoreUtils.Destroy(edgeMaterial);
        CoreUtils.Destroy(colorMapMaterial);
        CoreUtils.Destroy(colorMapTransMaterial);
        CoreUtils.Destroy(colorMapCompositeMaterial);
        CoreUtils.Destroy(noiseMaterial);
    }
}