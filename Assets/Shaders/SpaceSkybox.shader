// Procedural deep-space backdrop for an inverted sphere centred on the room:
// layered star field, soft nebula clouds and a faint galactic band. Everything is computed
// from the view direction, so there are no textures and no cubemap seams. HDR output feeds bloom.
Shader "StellarCommand/SpaceSkybox"
{
    Properties
    {
        _SpaceColor ("Space Colour", Color) = (0.004, 0.006, 0.016, 1)
        _NebulaColorA ("Nebula A", Color) = (0.10, 0.22, 0.65, 1)
        _NebulaColorB ("Nebula B", Color) = (0.55, 0.12, 0.45, 1)
        _NebulaIntensity ("Nebula Intensity", Range(0, 3)) = 0.9
        _StarIntensity ("Star Intensity", Range(0, 12)) = 5
        _StarDensity ("Star Density", Range(0.05, 1)) = 0.5
        _BandNormal ("Galactic Band Normal", Vector) = (0.25, 0.9, 0.35, 0)
        _Seed ("Seed", Float) = 3.7
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }

        // ZWrite On is deliberate: HDRP paints its own sky over every pixel whose depth is still
        // at the far plane, which would cover this sphere. Writing depth keeps that from happening.
        Cull Front
        ZWrite On
        ZTest LEqual
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _SpaceColor, _NebulaColorA, _NebulaColorB;
            float _NebulaIntensity, _StarIntensity, _StarDensity, _Seed;
            float4 _BandNormal;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                // sphere is centred on the world origin, so its vertex position is the view direction
                o.dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
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

            // One layer of stars: a jittered point per grid cell, projected onto the sphere.
            float3 starLayer(float3 dir, float scale, float density, float angSigma, float seed)
            {
                float3 id = floor(dir * scale + seed);
                if (hash13(id) > density) return 0;

                float3 jitter = float3(hash13(id + 1.7), hash13(id + 5.3), hash13(id + 9.1)) - 0.5;
                float3 starDir = normalize(id - seed + 0.5 + jitter * 0.8);

                float a2 = 2.0 * (1.0 - dot(dir, starDir));
                float g = exp(-a2 / (2.0 * angSigma * angSigma));

                float brightness = pow(hash13(id + 3.1), 3.0) * 0.9 + 0.1;
                float temp = hash13(id + 7.7);
                float3 tint = lerp(float3(0.65, 0.8, 1.0), float3(1.0, 0.75, 0.5), temp);
                return tint * g * brightness;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 dir = normalize(i.dir);

                // Galactic band concentrates nebula and stars around one great circle.
                float band = exp(-pow(dot(dir, normalize(_BandNormal.xyz)) * 3.2, 2.0));

                // Nebula clouds
                float n1 = fbm(dir * 2.4 + _Seed);
                float n2 = fbm(dir * 5.0 - _Seed * 1.7);
                // fbm of value noise stays in roughly 0.25..0.75, so the thresholds sit inside that range
                float cloud = smoothstep(0.34, 0.70, n1) * (0.45 + 0.55 * band);
                float3 nebula = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, smoothstep(0.30, 0.70, n2)) * cloud * _NebulaIntensity;

                // Stars: fine dense layer, medium layer, a few bright ones
                float dens = _StarDensity * (0.6 + 0.8 * band);
                float3 stars = starLayer(dir, 160.0, dens * 0.55, 0.0011, _Seed)
                             + starLayer(dir, 70.0, dens * 0.35, 0.0016, _Seed + 11.0) * 1.4
                             + starLayer(dir, 28.0, dens * 0.18, 0.0024, _Seed + 23.0) * 2.2;

                float3 col = _SpaceColor.rgb + nebula + stars * _StarIntensity;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
