// The surface of the map: one quad, a texel of _CellTex per cell. Each cell reads as a voxel by a bevel lit from the upper left
// and a little brightness noise of its own; the cells touch without gaps. _GridStrength draws the grid for the developer.
Shader "Borderbite/Map/Cells"
{
    Properties
    {
        _CellTex ("Cells (a texel per cell, point filter)", 2D) = "white" {}
        _Cells ("Cells per side", Float) = 64
        _Bevel ("Bevel width, share of a cell", Range(0, 0.5)) = 0.14
        _BevelShade ("Bevel shade", Range(0, 1)) = 0.28
        _Noise ("Brightness noise of a cell", Range(0, 0.2)) = 0.04
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
                float _Bevel;
                float _BevelShade;
                float _Noise;
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

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 grid = input.uv * _Cells;
                float2 cell = floor(grid);
                float2 f = frac(grid);
                half3 color = SAMPLE_TEXTURE2D(_CellTex, sampler_CellTex, (cell + 0.5) / _Cells).rgb;

                // lit from the upper left: the top and left rims are lighter, the bottom and right ones darker
                float top = saturate((f.y - (1 - _Bevel)) / _Bevel);
                float left = saturate((_Bevel - f.x) / _Bevel);
                float bottom = saturate((_Bevel - f.y) / _Bevel);
                float right = saturate((f.x - (1 - _Bevel)) / _Bevel);
                float shade = 1 + _BevelShade * (0.6 * top + 0.35 * left - bottom - 0.6 * right);
                shade += (Hash(cell) - 0.5) * 2 * _Noise;
                color *= shade;

                float rim = min(min(f.x, f.y), min(1 - f.x, 1 - f.y));
                color = lerp(color, half3(0, 0, 0), _GridStrength * step(rim, 0.04));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
