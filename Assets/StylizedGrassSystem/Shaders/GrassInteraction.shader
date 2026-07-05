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
                // Simple radial gradient based on UV (assuming a quad centered at 0.5,0.5)
                float2 centeredUV = i.uv * 2.0 - 1.0;
                float dist = length(centeredUV);
                
                if (dist > 1.0) discard;
                
                float falloff = 1.0 - dist;
                falloff = smoothstep(0.0, 1.0, falloff); // Smooth edges
                
                // Direction is outwards from center
                float2 dir = normalize(centeredUV);
                
                // R, G: Direction vector (mapped 0-1) * strength
                // B: Trail intensity
                // Map direction (-1 to 1) to (0 to 1)
                float2 mappedDir = dir * 0.5 + 0.5;
                
                return fixed4(mappedDir.x * falloff * _BendStrength, 
                              mappedDir.y * falloff * _BendStrength, 
                              falloff * _TrailIntensity, 
                              1.0);
            }
            ENDCG
        }
    }
}
