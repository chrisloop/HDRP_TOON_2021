Shader "Hidden/ColorMapTrans"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _DetailTexture("_DetailTexture", 2D) = "red" {}
        _DetailStep("_DetailStep", float) = 1
        _DetailScale("_DetailScale", Vector) = (1,1,1,1)

        _ColorMapOpaquePass("_ColorMapOpaquePass", 2DArray) = "blue" {}


        // Transparency
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _BlendMode("_BlendMode", Range(0.0, 1.0)) = 0.5

         _ObjectColor ("_ObjectColor", Color) = (0.000000,0.000000,0.000000,0.000000)
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    // #pragma enable_d3d11_debug_symbols

    //enable GPU instancing support
    #pragma multi_compile_instancing
    #pragma multi_compile _ DOTS_INSTANCING_ON

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" 
        
                    "RenderType"="HDUnlitShader"
        
        }
        Pass
        {
            Name "FirstPass"
            Tags { "LightMode" = "FirstPass" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual

            Cull Back

            HLSLPROGRAM

            // Toggle the alpha test
            #define _ALPHATEST_ON

            // Toggle transparency
            #define _SURFACE_TYPE_TRANSPARENT

            // Toggle fog on transparent
            #define _ENABLE_FOG_ON_TRANSPARENT
            
            // List all the attributes needed in your shader (will be passed to the vertex shader)
            // you can see the complete list of these attributes in VaryingMesh.hlsl
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_COLOR

            // List all the varyings needed in your fragment shader
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_TANGENT_TO_WORLD
            #define VARYINGS_NEED_COLOR

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            

            TEXTURE2D(_DetailTexture);

            TEXTURE2D(_ColorMapOpaquePass);

            float4 _ObjectColor;
            float _DetailStep;
            float2 _DetailScale;

            // Declare properties in the UnityPerMaterial cbuffer to make the shader compatible with SRP Batcher.
CBUFFER_START(UnityPerMaterial)
            float4 _DetailTexture_ST;
            float4 _ColorMapOpaquePass_ST;

            float4 _Color;

            float _AlphaCutoff;
            float _BlendMode;
CBUFFER_END

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassRenderersV2.hlsl"

            // If you need to modify the vertex datas, you can uncomment this code
            // Note: all the transformations here are done in object space
            // #define HAVE_MESH_MODIFICATION
            // AttributesMesh ApplyMeshModification(AttributesMesh input, float3 timeParameters)
            // {
            //     input.positionOS += input.normalOS * 0.0001; // inflate a bit the mesh to avoid z-fight
            //     return input;
            // }

            // Put the code to render the objects in your custom pass in this function
            void GetSurfaceAndBuiltinData(FragInputs fragInputs, float3 viewDirection, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
            {
                // Write back the data to the output structures
                ZERO_BUILTIN_INITIALIZE(builtinData); // No call to InitBuiltinData as we don't have any lighting
                ZERO_INITIALIZE(SurfaceData, surfaceData);
                builtinData.emissiveColor = float3(0, 0, 0);

                float3 color;

                // black masks out edges, otherwise average vertex color and object id color
                if (fragInputs.color.x == 0 && fragInputs.color.y == 0 && fragInputs.color.z == 0)
                {
                    color = fragInputs.color.xyz;
                }
                else
                {
                    color = (fragInputs.color.xyz + _ObjectColor.xyz) / 2; // combine vertex color and object id color
                } 

                // extra detail
                float2 detailUV = TRANSFORM_TEX(fragInputs.texCoord0.xy, _DetailTexture).xy;
                float detailAlpha = SAMPLE_TEXTURE2D(_DetailTexture, s_linear_repeat_sampler, detailUV * _DetailScale).x;

                if (detailAlpha > 0)
                    color = color * clamp(step(_DetailStep, detailAlpha),.05,.95);

                builtinData.opacity = .8;
                surfaceData.color = color;
            }

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }
}
