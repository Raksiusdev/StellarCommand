// Glowing billboard point for the galaxy map. The mesh stores one quad per star:
//   vertex   = star centre (object space)
//   uv       = quad corner in -1..1
//   uv1.x    = world-space radius of the quad (float, vertex colours are only 8-bit)
//   color    = rgb tint
// The quad is expanded in view space, so it always faces the viewer. Additive and HDR (feeds bloom).
Shader "StellarCommand/MapStar"
{
    Properties
    {
        _Intensity ("HDR Intensity", Range(0, 12)) = 3
        // Set per frame by GalaxyMap: stars fade out beyond this cylinder (world space)
        _ClipCenter ("Clip Centre", Vector) = (0, 0, 0, 0)
        _ClipNormal ("Clip Plane Normal", Vector) = (0, 1, 0, 0)
        _ClipRadius ("Clip Radius", Float) = 1000
        _ClipHeight ("Clip Half Height", Float) = 1000
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Blend One One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 size : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _Intensity;
            float4 _ClipCenter, _ClipNormal;
            float _ClipRadius, _ClipHeight;

            // 1 inside the clip cylinder, fading to 0 at its edge and above/below it
            float ClipFactor(float3 worldPos)
            {
                float3 n = normalize(_ClipNormal.xyz);
                float3 d = worldPos - _ClipCenter.xyz;
                float height = dot(d, n);
                float radial = length(d - n * height);
                float edge = 1.0 - smoothstep(_ClipRadius * 0.9, _ClipRadius, radial);
                float cap = 1.0 - smoothstep(_ClipHeight * 0.8, _ClipHeight, abs(height));
                return edge * cap;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // keep the star size correct if the map object is ever scaled
                float objScale = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));

                float4 viewPos = mul(UNITY_MATRIX_MV, float4(v.vertex.xyz, 1.0));
                viewPos.xy += v.uv * v.size.x * objScale;
                o.pos = mul(UNITY_MATRIX_P, viewPos);
                o.color = v.color.rgb * ClipFactor(mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float d2 = dot(i.uv, i.uv);
                float fade = saturate(1.0 - d2);              // hard edge of the quad
                float core = exp(-d2 * 14.0);                  // bright centre
                float halo = exp(-d2 * 3.5) * 0.35;            // soft glow
                float3 col = i.color * (core + halo) * fade * _Intensity;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
