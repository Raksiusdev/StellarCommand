// Additive HDR line material for hyperlanes and guide rings on the galaxy map.
// Colour comes from the vertex colour (rgb is already scaled by the builder).
Shader "StellarCommand/MapLines"
{
    Properties
    {
        _Intensity ("HDR Intensity", Range(0, 8)) = 1.5
        // Set per frame by GalaxyMap: lines fade out beyond this cylinder (world space).
        // Defaults are huge, so objects that never set them (controller rays) are unaffected.
        _ClipCenter ("Clip Centre", Vector) = (0, 0, 0, 0)
        _ClipNormal ("Clip Plane Normal", Vector) = (0, 1, 0, 0)
        _ClipRadius ("Clip Radius", Float) = 100000
        _ClipHeight ("Clip Half Height", Float) = 100000
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _Intensity;
            float4 _ClipCenter, _ClipNormal;
            float _ClipRadius, _ClipHeight;

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
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color.rgb * ClipFactor(mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return fixed4(i.color * _Intensity, 1);
            }
            ENDCG
        }
    }
}
