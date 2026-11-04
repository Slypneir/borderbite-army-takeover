// Models and placeholders of the world (heroes, soldiers, buildings, piles, the shore, the water) with a fixed light baked
// into the shader: the game has no realtime lights (Editor/RenderPipelineSetup), yet a voxel must still read as a voxel.
// _BaseMap is the texture of a model (point filter, a few texels per voxel face); _BaseColor tints it — the colour of a side
// on a `paint` part, white elsewhere.
Shader "Borderbite/World/Flat Lit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Shadow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half shade : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                float3 light = normalize(float3(-0.45, 1.0, -0.35));
                output.shade = lerp(_Shadow, 1.0, saturate(dot(normal, light)));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                return half4(color * input.shade, 1);
            }
            ENDHLSL
        }
    }
}
