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
    public LayerMask        layerMask = 1;

    RTHandle    colorMapBuffer;

    [SerializeField, HideInInspector]
    Shader      colorMapShader;
    Material    colorMapMaterial;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        AssignObjectIDs(); // unique color for each object

        colorMapShader = Shader.Find("Hidden/ColorMap");
        colorMapMaterial = CoreUtils.CreateEngineMaterial(colorMapShader);


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
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.customColorBuffer.Value, ctx.cameraDepthBuffer, ClearFlag.Color);
        CustomPassUtils.DrawRenderers(ctx, layerMask, CustomPass.RenderQueueType.All, colorMapMaterial);
    }

    protected override void Cleanup()
    {
        colorMapBuffer.Release();
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
}