// Procedural banded gas-giant / rocky planet for a sphere. Colour bands warped by noise,
// soft terminator against a fixed sun direction, and a glowing atmosphere rim.
Shader "StellarCommand/PlanetProcedural"
{
    Properties
    {
        _ColorA ("Band Colour A", Color) = (0.78, 0.55, 0.32, 1)
        _ColorB ("Band Colour B", Color) = (0.35, 0.22, 0.14, 1)
        _BandFreq ("Band Frequency", Float) = 14
        _Turbulence ("Turbulence", Range(0, 4)) = 1.6
        _Seed ("Seed", Float) = 1.3
        _SunDir ("Sun Direction (towards sun)", Vector) = (0.6, 0.35, -0.7, 0)
        _AtmoColor ("Atmosphere Colour", Color) = (0.35, 0.65, 1, 1)
        _AtmoIntensity ("Atmosphere Intensity", Range(0, 6)) = 2.2
        _Ambient ("Night Side Light", Range(0, 0.2)) = 0.025
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }

        Cull Back
        ZWrite On
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
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 objDir : TEXCOORD0;
                float3 nrm : TEXCOORD1;
                float3 wpos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _ColorA, _ColorB, _AtmoColor;
            float _BandFreq, _Turbulence, _Seed, _AtmoIntensity, _Ambient;
            float4 _SunDir;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.objDir = normalize(v.vertex.xyz);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float vnoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash13(i + float3(0, 0, 0)), hash13(i + float3(1, 0, 0)), f.x),
                         lerp(hash13(i + float3(0, 1, 0)), hash13(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(hash13(i + float3(0, 0, 1)), hash13(i + float3(1, 0, 1)), f.x),
                         lerp(hash13(i + float3(0, 1, 1)), hash13(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            float fbm(float3 p)
            {
                float a = 0.5;
                float s = 0.0;
                for (int k = 0; k < 5; k++)
                {
                    s += a * vnoise(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return s;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 d = normalize(i.objDir);

                // Latitude bands warped by stretched noise
                float warp = fbm(d * float3(2.5, 6.0, 2.5) + _Seed) - 0.5;
                float bands = sin((d.y + warp * 0.25 * _Turbulence) * _BandFreq + warp * _Turbulence * 3.0);
                float detail = fbm(d * float3(7.0, 22.0, 7.0) + _Seed * 3.0);
                float t = saturate(bands * 0.35 + 0.5 + (detail - 0.5) * 0.5);
                float3 albedo = lerp(_ColorB.rgb, _ColorA.rgb, t);

                // Lighting: soft terminator, tiny night-side light
                float3 N = normalize(i.nrm);
                float3 L = normalize(_SunDir.xyz);
                float ndl = dot(N, L);
                float lit = smoothstep(-0.08, 0.45, ndl);
                float3 col = albedo * (lit * 1.35 + _Ambient);

                // Atmosphere glow at the limb, strongest on the lit side
                float3 V = normalize(UnityWorldSpaceViewDir(i.wpos));
                float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                col += _AtmoColor.rgb * fres * _AtmoIntensity * (0.15 + lit);

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
