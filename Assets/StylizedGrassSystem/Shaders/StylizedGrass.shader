Shader "Stylized/ProceduralGrass"
{
    Properties
    {
        [Header(Colors)]
        _TopColor("Top Color", Color) = (0.5, 1.0, 0.5, 1)
        _BottomColor("Bottom Color", Color) = (0.1, 0.3, 0.1, 1)
        _AmbientColor("Ambient Color", Color) = (0.2, 0.4, 0.2, 1)
        
        [Header(Lighting)]
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Translucency("Translucency", Range(0,1)) = 0.5
        _AOStrength("AO Strength", Range(0,1)) = 0.8
        
        [Header(Wind)]
        _WindSpeed("Wind Speed", Float) = 1.0
        _WindStrength("Wind Strength", Float) = 1.0
        _WindScale("Wind Scale", Float) = 1.0
        _WindDirection("Wind Direction", Vector) = (1, 0, 1, 0)
        
        [Header(Interaction)]
        _BendStrength("Bend Strength", Float) = 3.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
            "DisableBatching"="True" // Instancing handled manually
        }
        LOD 100
        Cull Back // Single-sided to halve fragment shader work

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            // URP keywords — only include shadow keywords if actually casting
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // Manual indirect instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GrassIncludes.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 colorVar : TEXCOORD2;
                half fogCoord : TEXCOORD3;
            };

            // Uniforms
            float4 _TopColor;
            float4 _BottomColor;
            float4 _AmbientColor;
            float _AOStrength;
            
            float _WindSpeed;
            float _WindStrength;
            float _WindScale;
            float4 _WindDirection;

            float _BendStrength;
            
            // Interaction Map
            sampler2D _InteractionMap;
            float4 _InteractionMapParams; // x: ortho size, y: unused, zw: center pos

            // Buffer provided by Compute Shader (only contains VISIBLE instances)
            StructuredBuffer<GrassInstanceData> _VisibleInstances;

            void setup() { }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                GrassInstanceData data = _VisibleInstances[input.instanceID];
                float4x4 instanceMatrix = data.objectToWorld;
                float3 colorVar = data.colorVariation;

                // Transform position to World Space
                float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));
                
                // Get UVs (assume V axis is height, from 0 to 1)
                float heightPercent = input.uv.y;

                // Skip all displacement for root vertices (they're anchored to ground)
                if (heightPercent > 0.01)
                {
                    // --- WIND ---
                    float3 windDir = normalize(_WindDirection.xyz);
                    float windPhase = dot(positionWS.xz, _WindScale) + _Time.y * _WindSpeed;
                    float windInfluence = (sin(windPhase) * 0.5 + 0.5) * _WindStrength * heightPercent * heightPercent;
                    positionWS.xyz += windDir * windInfluence;

                    // --- INTERACTION ---
                    float2 interUV = (positionWS.xz - _InteractionMapParams.zw) / (_InteractionMapParams.x * 2.0) + 0.5;
                    if (interUV.x >= 0 && interUV.x <= 1 && interUV.y >= 0 && interUV.y <= 1)
                    {
                        float4 interData = tex2Dlod(_InteractionMap, float4(interUV, 0, 0));
                        float2 bendDir = interData.rg; // Raw signed direction
                        float bendMag = length(bendDir);
                        
                        if (bendMag > 0.01)
                        {
                            // bendDir is already scaled by the interactor's falloff and strength.
                            // We multiply by the material's _BendStrength to allow global tuning.
                            // DO NOT multiply by interData.b (trail) here, otherwise it squashes the horizontal bend!
                            float3 bendWorld = float3(bendDir.x, 0, bendDir.y) * _BendStrength;
                            positionWS.xyz += bendWorld * heightPercent;
                            
                            // Flattening down: combination of natural bend arc + explicit trail push
                            float flattenAmount = (bendMag * _BendStrength * 0.3) + interData.b;
                            positionWS.y -= flattenAmount * heightPercent;
                        }
                    }
                }

                // Transform to clip space
                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                
                // Transform normal
                float3x3 w2oRotation;
                w2oRotation[0] = instanceMatrix[0].xyz;
                w2oRotation[1] = instanceMatrix[1].xyz;
                w2oRotation[2] = instanceMatrix[2].xyz;
                
                output.normalWS = normalize(mul(w2oRotation, input.normalOS));
                
                output.uv = input.uv;
                output.colorVar = colorVar;
                
                // Calculate Fog
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Colors — simple height-based gradient
                float heightPercent = input.uv.y;
                half3 baseColor = lerp(_BottomColor.rgb, _TopColor.rgb, heightPercent) * input.colorVar;
                
                // AO (darken roots)
                baseColor *= lerp(1.0 - _AOStrength, 1.0, heightPercent);
                
                // Simple stylized lighting — no per-pixel shadow cascade lookup
                Light mainLight = GetMainLight();
                float NdotL = dot(normalize(input.normalWS), mainLight.direction);
                float halfLambert = NdotL * 0.5 + 0.5;
                
                half3 finalColor = baseColor * (mainLight.color * halfLambert + _AmbientColor.rgb);

                // Apply Fog
                finalColor = MixFog(finalColor, input.fogCoord);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma instancing_options procedural:setup
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "GrassIncludes.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float _WindSpeed;
            float _WindStrength;
            float _WindScale;
            float4 _WindDirection;
            
            sampler2D _InteractionMap;
            float4 _InteractionMapParams;
            float _BendStrength;

            StructuredBuffer<GrassInstanceData> _VisibleInstances;

            void setup() { }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                GrassInstanceData data = _VisibleInstances[input.instanceID];
                float4x4 instanceMatrix = data.objectToWorld;

                float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));
                float heightPercent = input.uv.y;

                // Apply same vertex displacement as Forward pass so shadows match
                float3 windDir = normalize(_WindDirection.xyz);
                float windPhase = dot(positionWS.xz, _WindScale) + _Time.y * _WindSpeed;
                float windInfluence = (sin(windPhase) * 0.5 + 0.5) * _WindStrength * heightPercent * heightPercent;
                positionWS.xyz += windDir * windInfluence;

                float2 interUV = (positionWS.xz - _InteractionMapParams.zw) / (_InteractionMapParams.x * 2.0) + 0.5;
                if (interUV.x >= 0 && interUV.x <= 1 && interUV.y >= 0 && interUV.y <= 1)
                {
                    float4 interData = tex2Dlod(_InteractionMap, float4(interUV, 0, 0));
                    float2 bendDir = interData.rg; 
                    float bendMag = length(bendDir);
                    if (bendMag > 0.01)
                    {
                        float3 bendWorld = float3(bendDir.x, 0, bendDir.y) * _BendStrength;
                        positionWS.xyz += bendWorld * heightPercent;
                        
                        float flattenAmount = (bendMag * _BendStrength * 0.3) + interData.b;
                        positionWS.y -= flattenAmount * heightPercent;
                    }
                }

                // Normal for shadow bias calculation
                float3x3 w2oRotation;
                w2oRotation[0] = instanceMatrix[0].xyz;
                w2oRotation[1] = instanceMatrix[1].xyz;
                w2oRotation[2] = instanceMatrix[2].xyz;
                float3 normalWS = normalize(mul(w2oRotation, input.normalOS));

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS.xyz, normalWS, 0.0)); // Could add directional light direction parameter

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
