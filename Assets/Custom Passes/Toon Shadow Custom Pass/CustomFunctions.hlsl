void ShadowEdges_float(float2 ScreenPosition, float Thickness, float Multiplier, float Bias, float Threshold, out float ShadowEdges)
{
    ShadowEdges = 1;

    #ifndef SHADERGRAPH_PREVIEW

       float Shadow = step(Threshold, SampleCustomColor(ScreenPosition));

        if (Thickness <= 0)
            return;

        // Neighbour pixel positions
        static float2 samplingPositions[8] =
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

        float shadowDifference = 0;
        float shadowSample;

        for (int i = 0; i < 8; i++)
        {
            shadowSample = step(Threshold, SampleCustomColor(ScreenPosition + samplingPositions[i] * Thickness * _ScreenSize.zw)).x;
            shadowDifference = shadowDifference + Shadow - shadowSample.r;
        }

        // shadow sensitivity
        shadowDifference = shadowDifference * Multiplier;
        shadowDifference = pow(shadowDifference, Bias); 
        ShadowEdges = 1.0 - clamp(shadowDifference,0,1);
        
    #endif
}