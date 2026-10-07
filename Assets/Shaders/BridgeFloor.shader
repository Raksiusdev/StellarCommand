// Glossy floor for the bridge. The scene's light strips, table and map are duplicated under the floor
// as a mirror image (see FloorReflectionRoot / MirrorProxy); this shader is a semi-transparent dark
// surface laid over that mirror world, so the copies read as reflections. Reflection gets stronger at
// grazing angles (Fresnel), like a real polished floor. Faint plate seams glow in the accent colour.
//
// It is drawn just after the opaque queue with depth writes, so the real hologram (transparent queue)
// still draws in front of it, while the mirrored copies are drawn before it (queue 2400).
Shader "StellarCommand/BridgeFloor"
{
    Properties
    {
        _BaseColor ("Floor Colour", Color) = (0.012, 0.022, 0.034, 1)
        _AccentColor ("Seam Colour", Color) = (0.20, 0.85, 1.0, 1)
        _BaseAlpha ("Opacity Looking Down", Range(0, 1)) = 0.80
        _GrazingAlpha ("Opacity At Grazing Angles", Range(0, 1)) = 0.38
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _SeamScale ("Plate Size (m)", Float) = 1.2
        _SeamWidth ("Seam Width", Range(0, 0.1)) = 0.015
        _SeamGlow ("Seam Glow", Range(0, 2)) = 0.25
    }

    SubShader
    {
        Tags { "Queue" = "Geometry+450" "RenderType" = "Transparent" }

        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
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
                float3 wpos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _BaseColor, _AccentColor;
            float _BaseAlpha, _GrazingAlpha, _FresnelPower, _SeamScale, _SeamWidth, _SeamGlow;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // The floor is horizontal, so the plate grid lies on the XZ plane
                float2 cell = i.wpos.xz / _SeamScale;
                float2 fr = abs(frac(cell) - 0.5);
                float edge = max(fr.x, fr.y);
                float seam = smoothstep(0.5 - _SeamWidth * 2.0, 0.5 - _SeamWidth * 0.5, edge);

                // Fresnel: more mirror-like the lower the viewing angle
                float3 V = normalize(UnityWorldSpaceViewDir(i.wpos));
                float fres = pow(1.0 - saturate(V.y), _FresnelPower);

                float alpha = lerp(_BaseAlpha, _GrazingAlpha, fres);
                alpha = max(alpha, seam * 0.9); // seams stay visible over the reflection

                float3 col = _BaseColor.rgb + _AccentColor.rgb * seam * _SeamGlow;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
