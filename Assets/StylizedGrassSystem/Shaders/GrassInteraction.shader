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

            float4 frag (v2f i) : SV_Target
            {
                // Radial gradient from center of quad
                float2 centeredUV = i.uv * 2.0 - 1.0;
                float dist = length(centeredUV);
                
                if (dist > 1.0) discard;
                
                // Gentler falloff — stronger near center, smooth at edges
                float falloff = saturate(1.0 - dist);
                falloff = pow(falloff, 0.6);
                
                // Direction is outwards from center (normalized)
                float2 dir = normalize(centeredUV);
                
                // Output raw signed direction directly. ARGBHalf supports negative values.
                float2 bend = dir * falloff * _BendStrength;
                
                return float4(bend.x, 
                              bend.y, 
                              falloff * _TrailIntensity, 
                              1.0);
            }
            ENDCG
        }
    }
}
