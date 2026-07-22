#ifndef CUSTOM_MULTI_LIGHTING_INCLUDED
#define CUSTOM_MULTI_LIGHTING_INCLUDED

// --- FLOAT PRECISION VERSION ---
void GetMultiLightData_float(float3 worldPos, float3 worldNormal, out float3 direction, out float3 color, out float shadowAtten) {
    // Default Safe Fallbacks
    direction = normalize(float3(-0.7, 0.7, -0.7));
    color = float3(1, 1, 1);
    shadowAtten = 1;

    // ONLY execute lighting math if we are in the fragment stage and URP lighting is ready
    #if !defined(SHADERGRAPH_PREVIEW) && defined(UNIVERSAL_LIGHTING_INCLUDED)
        #if defined(SHADERSTAGE_FRAGMENT)
            float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
            Light mainLight = GetMainLight(shadowCoord);
            
            direction = mainLight.direction;
            float mainNdotL = saturate(dot(worldNormal, mainLight.direction));
            float3 totalDiffuse = mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation * mainNdotL);
            float totalAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

            int pixelLightCount = GetAdditionalLightsCount();
            for (int i = 0; i < pixelLightCount; ++i) {
                Light light = GetAdditionalLight(i, worldPos);
                float additionalNdotL = saturate(dot(worldNormal, light.direction));
                totalDiffuse += light.color * (light.shadowAttenuation * light.distanceAttenuation * additionalNdotL);
                totalAttenuation += light.shadowAttenuation * light.distanceAttenuation;
            }

            color = totalDiffuse;
            shadowAtten = saturate(totalAttenuation);
        #endif
    #endif
}

// --- HALF PRECISION VERSION ---
void GetMultiLightData_half(half3 worldPos, half3 worldNormal, out half3 direction, out half3 color, out half shadowAtten) {
    // Default Safe Fallbacks
    direction = normalize(half3(-0.7, 0.7, -0.7));
    color = half3(1, 1, 1);
    shadowAtten = 1;

    #if !defined(SHADERGRAPH_PREVIEW) && defined(UNIVERSAL_LIGHTING_INCLUDED)
        #if defined(SHADERSTAGE_FRAGMENT)
            float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
            Light mainLight = GetMainLight(shadowCoord);
            
            direction = mainLight.direction;
            half mainNdotL = saturate(dot(worldNormal, mainLight.direction));
            half3 totalDiffuse = mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation * mainNdotL);
            half totalAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

            int pixelLightCount = GetAdditionalLightsCount();
            for (int i = 0; i < pixelLightCount; ++i) {
                Light light = GetAdditionalLight(i, worldPos);
                half additionalNdotL = saturate(dot(worldNormal, light.direction));
                totalDiffuse += light.color * (light.shadowAttenuation * light.distanceAttenuation * additionalNdotL);
                totalAttenuation += light.shadowAttenuation * light.distanceAttenuation;
            }

            color = totalDiffuse;
            shadowAtten = saturate(totalAttenuation);
        #endif
    #endif
}

#endif
