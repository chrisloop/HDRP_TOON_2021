void TriplanarTexture2DArray_float(Texture2DArray tex, float3 Normal, float Blend, SamplerState SS, float3 pos, float index, float tile, out float4 Out)
{
    float3 Node_UV = pos * tile;
    float3 Node_Blend = pow(abs(Normal), Blend);
    Node_Blend /= dot(Node_Blend, 1.0);

    float4 Node_X = SAMPLE_TEXTURE2D_ARRAY(tex, SS, Node_UV.zy, index);
    float4 Node_Y = SAMPLE_TEXTURE2D_ARRAY(tex, SS, Node_UV.xz, index);
    float4 Node_Z = SAMPLE_TEXTURE2D_ARRAY(tex, SS, Node_UV.xy, index);

    Out = Node_X * Node_Blend.x + Node_Y * Node_Blend.y + Node_Z * Node_Blend.z;
} 