// The surface of the map: one quad, a texel of _CellTex per cell. The painted ground reads as one surface without a grid; the
// border of two colours goes in steps of cells (stage 1 spec 2.1). _GridStrength draws the grid for the developer.
Shader "Borderbite/Map/Cells"
{
    Properties
    {
        _CellTex ("Cells (a texel per cell, point filter)", 2D) = "white" {}
        _Cells ("Cells per side", Float) = 64
        _GridStrength ("Grid of the developer", Range(0, 1)) = 0
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

            TEXTURE2D(_CellTex);
            SAMPLER(sampler_CellTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CellTex_ST;
                float _Cells;
                float _GridStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 grid = input.uv * _Cells;
                float2 cell = floor(grid);
                float2 f = frac(grid);
                half3 color = SAMPLE_TEXTURE2D(_CellTex, sampler_CellTex, (cell + 0.5) / _Cells).rgb;

                float rim = min(min(f.x, f.y), min(1 - f.x, 1 - f.y));
                color = lerp(color, half3(0, 0, 0), _GridStrength * step(rim, 0.04));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
