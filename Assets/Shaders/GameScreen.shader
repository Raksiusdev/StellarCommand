// Plain unlit display for the captured game window. Stereo-aware (single-pass instanced VR),
// with a flip option because capture textures can arrive upside down depending on the source.
Shader "StellarCommand/GameScreen"
{
    Properties
    {
        _MainTex ("Window Texture", 2D) = "black" {}
        _Brightness ("Brightness", Range(0.2, 2)) = 1
        _Sharpness ("Sharpness", Range(0, 1.5)) = 0.5
        _Supersample ("Anti-shimmer (filter footprint)", Range(0, 1)) = 0.7
        [Toggle] _FlipY ("Flip Vertically", Float) = 0
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _Brightness;
            float _Sharpness;
            float _Supersample;
            float _FlipY;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                if (_FlipY > 0.5) o.uv.y = 1.0 - o.uv.y;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // The capture is bigger than the headset's view of it, and the texture has no mipmaps, so a
                // plain lookup shimmers on small text. Average four taps spread over the pixel's footprint
                // (an on-the-fly box filter), then add back some edge contrast so text stays crisp.
                float2 uv = i.uv;
                float2 f = fwidth(uv) * 0.5 * _Supersample;
                float3 c = (tex2D(_MainTex, uv + float2(-f.x, -f.y)).rgb +
                            tex2D(_MainTex, uv + float2( f.x, -f.y)).rgb +
                            tex2D(_MainTex, uv + float2(-f.x,  f.y)).rgb +
                            tex2D(_MainTex, uv + float2( f.x,  f.y)).rgb) * 0.25;

                // Unsharp mask against the four direct neighbours at one-texel distance
                float2 t = _MainTex_TexelSize.xy;
                float3 blur = (tex2D(_MainTex, uv + float2(t.x, 0)).rgb + tex2D(_MainTex, uv - float2(t.x, 0)).rgb +
                               tex2D(_MainTex, uv + float2(0, t.y)).rgb + tex2D(_MainTex, uv - float2(0, t.y)).rgb) * 0.25;
                c = max(0, c + (c - blur) * _Sharpness);

                return fixed4(c * _Brightness, 1);
            }
            ENDCG
        }
    }
}
