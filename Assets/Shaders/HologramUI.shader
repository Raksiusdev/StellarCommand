// Holographic panel surface for world-space uGUI (use on a RawImage).
// Draws a translucent tinted body with scanlines, a faint grid, a glowing border,
// corner brackets, a slow sweep band and a reveal wipe. Output is HDR so HDRP bloom picks it up.
Shader "StellarCommand/HologramUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Body Tint", Color) = (0.02, 0.22, 0.32, 1)
        _EdgeColor ("Edge Color", Color) = (0.25, 0.9, 1, 1)
        _BaseAlpha ("Body Alpha", Range(0, 1)) = 0.55
        _Intensity ("HDR Intensity", Range(0.5, 6)) = 2.2
        _Aspect ("Aspect (width / height)", Float) = 1.5
        _EdgeWidth ("Edge Width", Range(0.002, 0.05)) = 0.008
        _BracketSize ("Corner Bracket Size", Range(0.02, 0.3)) = 0.09
        _ScanDensity ("Scanline Density", Float) = 90
        _ScanSpeed ("Scanline Speed", Float) = 0.35
        _ScanStrength ("Scanline Strength", Range(0, 1)) = 0.35
        _GridCells ("Grid Cells (per height)", Float) = 14
        _GridStrength ("Grid Strength", Range(0, 1)) = 0.12
        _Reveal ("Reveal", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _EdgeColor;
            float _BaseAlpha, _Intensity, _Aspect, _EdgeWidth, _BracketSize;
            float _ScanDensity, _ScanSpeed, _ScanStrength, _GridCells, _GridStrength, _Reveal;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uv;
                float t = _Time.y;

                // Distance (in height units) from each edge, so the border has even thickness.
                float2 p = uv * float2(_Aspect, 1.0);
                float2 d = min(p, float2(_Aspect, 1.0) - p);
                float edgeDist = min(d.x, d.y);

                float border = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth * 1.6, edgeDist);
                float glow = exp(-edgeDist * 22.0) * 0.45;

                // Thicker L-shaped brackets in each corner.
                float bracket = step(d.x, _BracketSize) * step(d.y, _BracketSize)
                              * step(min(d.x, d.y), _EdgeWidth * 3.2);

                // Scanlines drifting upward.
                float scan = 0.5 + 0.5 * sin((uv.y * _ScanDensity - t * _ScanSpeed * _ScanDensity * 0.1) * 6.28318);
                scan = lerp(1.0, scan, _ScanStrength);

                // Faint grid.
                float2 g = abs(frac(p * _GridCells) - 0.5);
                float grid = smoothstep(0.47, 0.5, max(g.x, g.y)) * _GridStrength;

                // Slow bright band travelling up the panel.
                float bandPos = frac(t * 0.12);
                float band = exp(-pow((uv.y - bandPos) * 9.0, 2.0)) * 0.16;

                // Subtle flicker and grain.
                float flicker = 0.96 + 0.04 * sin(t * 41.0) * sin(t * 7.3);
                float grain = (hash21(floor(uv * float2(220.0, 140.0)) + floor(t * 14.0)) - 0.5) * 0.06;

                // Reveal wipe from bottom to top with a bright leading line.
                float visible = step(uv.y, _Reveal);
                float lead = exp(-pow((uv.y - _Reveal) * 40.0, 2.0)) * step(_Reveal, 0.999) * step(0.001, _Reveal);

                float edgeMask = saturate(border + bracket);
                float3 body = _Color.rgb * (0.7 + 0.3 * scan) + grain;
                float3 lines = _EdgeColor.rgb * (edgeMask * 1.4 + glow + band + grid + lead * 2.0);

                float3 rgb = (body + lines) * _Intensity * flicker;
                float a = saturate(_BaseAlpha * scan + edgeMask + glow * 0.6 + band + grid * 0.5 + lead);
                a *= saturate(visible + lead) * i.color.a;

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
