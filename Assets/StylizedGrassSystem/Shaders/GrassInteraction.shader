Shader "Stylized/GrassInteraction"
{
    Properties
    {
        _BendStrength("Bend Strength", Range(0, 1)) = 1.0
        _TrailIntensity("Trail Intensity", Range(0, 1)) = 1.0
        _Radius("Radius", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        // Additive blending so multiple interactors add up
        Blend One One
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            float _BendStrength;
            float _TrailIntensity;
            float _Radius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Radial gradient from center of quad
                float2 centeredUV = i.uv * 2.0 - 1.0;
                float dist = length(centeredUV);
                
                if (dist > 1.0) discard;
                
                // Gentler falloff — stronger near center, smooth at edges
                float falloff = saturate(1.0 - dist);
                falloff = pow(falloff, 0.6); // < 1 exponent = gentler falloff = stronger overall
                
                // Direction is outwards from center (normalized)
                float2 dir = normalize(centeredUV);
                
                // Output direction scaled to fit within the encodable range.
                // The RT is cleared to (0.5, 0.5, 0, 0) and we use additive blending,
                // so the final RT value is: 0.5 + output. To avoid clamping at [0,1]
                // (which causes angle-dependent distortion / bean shape), we halve the
                // direction output. The grass shader's decode (rg * 2 - 1) automatically
                // compensates, giving the correct final magnitude at all angles.
                float2 scaledDir = dir * falloff * _BendStrength * 0.5;
                
                return fixed4(scaledDir.x, 
                              scaledDir.y, 
                              falloff * _TrailIntensity, 
                              1.0);
            }
            ENDCG
        }
    }
}
