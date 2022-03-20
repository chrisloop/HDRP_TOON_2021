Shader "Hidden/Edge"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

    TEXTURE2D_X(_NoiseBuffer);
    TEXTURE2D_X(_ColorMap);

    float _EdgeThickness;

    float _DepthThreshold;
    float _DepthMultiplier;
    float _DepthBias;

    float _NormalThreshold;
    float _NormalMultiplier;
    float _NormalBias;

    float _ColorThreshold;
    float _ColorMultiplier;
    float _ColorBias;

    float _NoiseMinMultiplier;
    float _NoiseMaxMultiplier;

    float remap(float value, float low1, float high1, float low2, float high2)
    {
        return low2 + (value - low1) * (high2 - low2) / (high1 - low1);
    }

    void GetDepthNormal_float(float2 ScreenPosition, out float Depth, out float3 Normal)
    {
        Depth = SampleCameraDepth(ScreenPosition);
        NormalData normalData;
        DecodeFromNormalBuffer(_ScreenSize.xy * ScreenPosition, normalData);
        Normal = normalData.normalWS;     
    }

    float4 EdgePass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;

        float thicknessMultiplier = SAMPLE_TEXTURE2D_X_LOD(_NoiseBuffer, s_linear_clamp_sampler, uv * _RTHandleScale.xy, 0).x;
        thicknessMultiplier = remap(thicknessMultiplier, 0, 1, _NoiseMinMultiplier, _NoiseMaxMultiplier);

        _EdgeThickness = _EdgeThickness * thicknessMultiplier;

        if (_EdgeThickness == 0)
            return float4(0,0,0,1);

        #define MAX_SAMPLES 8

        // Neighbour pixel positions
        static float2 samplingPositions[MAX_SAMPLES] =
        {
            float2( 0,  1),
            float2(-1,  0),
            float2( 0, -1),
            float2( 1,  0),
            float2( 1,  1),
            float2(-1,  1),
            float2(-1, -1),
            float2( 1, -1),
        };


        float3  Normal;
        float   Depth;
        GetDepthNormal_float(uv, Depth, Normal);

        float3 ColorMap = SAMPLE_TEXTURE2D_X_LOD(_ColorMap, s_linear_clamp_sampler, uv * _RTHandleScale.xy, 0).xyz;

        float3  colorSample;
        float3  normalSample;
        float   depthSample;
        float2 uvN;

        float colorDifference = 0;
        float depthDifference = 0;
        float normalDifference = 0;

        for (int i = 0; i < 8; i++)
        {
            // render texture position
            uvN = uv + samplingPositions[i] * _ScreenSize.zw * _EdgeThickness;

            // vertex color sample
            float3 colorSample = SAMPLE_TEXTURE2D_X_LOD(_ColorMap, s_linear_clamp_sampler, uvN * _RTHandleScale.xy, 0).xyz;
            colorDifference = colorDifference + distance(ColorMap, colorSample);

            // depth normal sample
            GetDepthNormal_float(uvN, depthSample, normalSample);
            depthDifference = depthDifference + Depth - depthSample;
            normalDifference = normalDifference + distance(Normal, normalSample);
        
            // black mask disables edges
            if ((colorSample.x == 0 && colorSample.y == 0 && colorSample.z == 0) || (ColorMap.x == 0 && ColorMap.y == 0 && ColorMap.z == 0))
            {
              //  colorDifference = 0;
              //  break;
            }
        } 
        

        // Depth edges sensitivity
        float EdgeDepth;
        
        if (_DepthThreshold == 0) 
        {
            // soft sensitivity 
            depthDifference = depthDifference * _DepthMultiplier;
            depthDifference = saturate(depthDifference);
            depthDifference = pow(depthDifference, _DepthBias);
            EdgeDepth = 1 - depthDifference;
        }
        else
        {
            // hard sensitivity
            EdgeDepth = step(depthDifference, _DepthThreshold);
        }

        // Normal edges sensitivity
        float EdgeNormal;

        if (_NormalThreshold == 0)
        {
            // soft sensitivity
            normalDifference = normalDifference * _NormalMultiplier;
            normalDifference = saturate(normalDifference);
            normalDifference = pow(normalDifference, _NormalBias);
            EdgeNormal = 1 - normalDifference;    
        }
        else
        {
            // hard sensitivity
            EdgeNormal = step(normalDifference, _NormalThreshold);      
        }

        // Color edges sensitivity
        float EdgeColor;

        if (_ColorThreshold == 0)
        {
            // soft sensitivity
            colorDifference = colorDifference * _ColorMultiplier;
            colorDifference = saturate(colorDifference);
            colorDifference = pow(colorDifference, _ColorBias);
            EdgeColor = 1 - colorDifference;
        }
        else
        {
            // hard color sensitivity
            EdgeColor = step(colorDifference, _ColorThreshold);
        }

        float Edges = min(min(EdgeColor, EdgeDepth), EdgeNormal);

        float4 Result = float4(ColorMap,Edges);
        
        return Result;
    }

    // Used to copy from temporary buffer to the camera buffer or render texture
    float4 Copy(Varyings varyings) : SV_Target
    {
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        float4 color;

        //color = float4(CustomPassLoadCameraColor(varyings.positionCS.xy, 0), 1);
        color = LoadCustomColor(posInput.positionSS);

        return color;
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "Edge Pass"

            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
                #pragma fragment EdgePass
            ENDHLSL
        }
        Pass
        {
            Name "Copy Pass"

            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
                #pragma fragment Copy
            ENDHLSL
        }
    }
    Fallback Off
}
