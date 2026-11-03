// Placeholders of the world (the hero, the island, the water) with a fixed light baked into the shader: the game has no
// realtime lights (Editor/RenderPipelineSetup), yet a cube must still read as a cube.
Shader "Borderbite/Placeholder/Flat Shaded"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Shadow ("Brightness of the side away from the light", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Shadow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half shade : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                float3 light = normalize(float3(-0.45, 1.0, -0.35));
                output.shade = lerp(_Shadow, 1.0, saturate(dot(normal, light)));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_BaseColor.rgb * input.shade, 1);
            }
            ENDHLSL
        }
    }
}
