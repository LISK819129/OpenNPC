// Flat ink / paper colour for everything in the demo. One unlit colour, no
// textures, no lighting — the look of the logo, and about as cheap as WebGL gets.
//
// _OutlinePush moves vertices away from the camera along the view ray. The
// projected silhouette is unchanged, but an inverted-hull outline pushed back
// this way sits behind the character's own limbs, so it only shows around the
// outer silhouette (same trick as the Blender logo renders, but exact for any
// camera angle because it happens after skinning).
Shader "OpenNPC/Flat"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.067, 0.067, 0.067, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _OutlinePush ("Outline Push (m)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _OutlinePush;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                float dist = max(length(positionVS), 1e-4);
                positionVS *= (dist + _OutlinePush) / dist;
                o.positionCS = TransformWViewToHClip(positionVS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
