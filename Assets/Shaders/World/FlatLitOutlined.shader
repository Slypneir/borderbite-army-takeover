// Flat Lit with a rim around the model: the units of the battle (heroes and soldiers). The side of a unit reads not only by
// its colour: a light rim for the player, a dark one for the enemy (owner, 4 November, for colour-blind players), whatever the
// number of side colours. The rim is the back faces pushed out along their normals by a constant width on the screen.
// Only units take this shader: buildings and the shore keep one pass.
Shader "Borderbite/World/Flat Lit Outlined"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Shadow ("Brightness of the side away from the light", Range(0, 1)) = 0.55
        _OutlineColor ("Rim colour", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Rim width, part of the screen height", Range(0, 0.01)) = 0.0025
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

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
                half4 _OutlineColor;
                float _OutlineWidth;
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
        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Shadow;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                float4 position = TransformObjectToHClip(input.positionOS.xyz);
                float3 normal = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(input.normalOS));
                float2 direction = normal.xy;
                float length2 = dot(direction, direction);
                if (length2 > 1e-6)
                {
                    direction *= rsqrt(length2);
                    // a constant width on the screen: clip space spans 2 over the height, the aspect keeps it round
                    direction.x *= _ScreenParams.y / _ScreenParams.x;
                    position.xy += direction * _OutlineWidth * 2.0 * position.w;
                }
                return position;
            }

            half4 Frag() : SV_Target
            {
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }
    }
}
