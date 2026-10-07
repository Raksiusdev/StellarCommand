// Sci-fi hull plating for the bridge room. Dark metal with faked lighting, plate seams that glow
// faintly in the accent colour, and a rim light. With _Emission = 1 the surface becomes a pure
// HDR light strip (feeds bloom). Self-contained: does not depend on HDRP lights or exposure.
Shader "StellarCommand/BridgeSurface"
{
    Properties
    {
        _BaseColor ("Base Colour", Color) = (0.05, 0.07, 0.10, 1)
        _AccentColor ("Accent Colour", Color) = (0.20, 0.85, 1.0, 1)
        _Emission ("Light Strip (0 = hull, 1 = emissive)", Range(0, 1)) = 0
        _EmissionIntensity ("Strip HDR Intensity", Range(0, 8)) = 3
        _SeamScale ("Plate Size (m)", Float) = 1.2
        _SeamWidth ("Seam Width", Range(0, 0.1)) = 0.018
        _SeamGlow ("Seam Glow", Range(0, 3)) = 0.5
        _Rim ("Rim Light", Range(0, 2)) = 0.6
        _LightDir ("Fake Light Direction", Vector) = (0.3, 0.8, -0.4, 0)
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
                float3 nrm : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _BaseColor, _AccentColor;
            float _Emission, _EmissionIntensity, _SeamScale, _SeamWidth, _SeamGlow, _Rim;
            float4 _LightDir;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 N = normalize(i.nrm);
                float3 wp = i.wpos;

                // Project the plate grid along the dominant axis of the surface
                float3 an = abs(N);
                float2 g = (an.y > an.x && an.y > an.z) ? wp.xz : ((an.x > an.z) ? wp.zy : wp.xy);
                float2 cell = g / _SeamScale;
                float2 fr = abs(frac(cell) - 0.5);
                float edge = max(fr.x, fr.y);
                float seam = smoothstep(0.5 - _SeamWidth * 2.0, 0.5 - _SeamWidth * 0.5, edge);

                // Per-plate tone variation so large walls do not look flat
                float tone = 0.88 + 0.24 * hash12(floor(cell) + floor(wp.y * 0.3));

                float3 L = normalize(_LightDir.xyz);
                float wrap = saturate(dot(N, L) * 0.5 + 0.5);
                float3 hull = _BaseColor.rgb * tone * (0.35 + 0.9 * wrap);

                float3 V = normalize(UnityWorldSpaceViewDir(wp));
                float rim = pow(1.0 - saturate(dot(N, V)), 3.0);

                hull = lerp(hull, hull * 0.35, seam);
                hull += _AccentColor.rgb * seam * _SeamGlow;
                hull += _AccentColor.rgb * rim * _Rim * 0.5;

                float3 strip = _AccentColor.rgb * _EmissionIntensity;
                return fixed4(lerp(hull, strip, _Emission), 1);
            }
            ENDCG
        }
    }
}
