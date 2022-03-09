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
        // Setup code here
        // Temporary buffers
        colorMapBuffer = RTHandles.Alloc(
            Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
            useDynamicScale: true, name: "Color Map Buffer"
        );
    }

    protected override void Execute(CustomPassContext ctx)
    {

    }

    protected override void Cleanup()
    {
        colorMapBuffer.Release();
    }
}