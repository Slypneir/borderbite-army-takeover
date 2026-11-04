// The surface of the map: one quad. Grass of the 3D artist underneath (_GrassTex, the whole island once, a texel a voxel), and
// a texel of _CellTex per cell over it: rgb the colour of the side, a how much it is painted (0 neutral grass, 1 a side; the
// flash and the wave go between). The paint is the side lightened to white and scaled by the brightness of the grass, so the
// grass shows through and the land is lighter than the army of the same side (stage 1 spec 2.1, owner's numbers of 4 November).
// The formula works in sRGB 0..1, as the artist's reference. _GridStrength draws the grid for the developer.
Shader "Borderbite/Map/Cells"
{
    Properties
    {
        _CellTex ("Cells (a texel per cell, point filter)", 2D) = "white" {}
        _GrassTex ("Grass (point filter)", 2D) = "white" {}
        _GrassTiles ("Grass tiles over the map", Float) = 1
        _Lighten ("Side lightened to white", Range(0, 1)) = 0.55
        _PaintAlpha ("Paint over the grass", Range(0, 1)) = 0.7
        _LumMean ("Mean brightness of the grass", Range(0.05, 1)) = 0.491
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_CellTex);
            SAMPLER(sampler_CellTex);
            TEXTURE2D(_GrassTex);
            SAMPLER(sampler_GrassTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CellTex_ST;
                float4 _GrassTex_ST;
                float _Cells;
                float _GridStrength;
                float _GrassTiles;
                float _Lighten;
                float _PaintAlpha;
                float _LumMean;
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
                half4 paint = SAMPLE_TEXTURE2D(_CellTex, sampler_CellTex, (cell + 0.5) / _Cells);
                half3 grass = LinearToSRGB(SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, input.uv * _GrassTiles).rgb);
                half3 side = lerp(LinearToSRGB(paint.rgb), half3(1, 1, 1), _Lighten);
                half lum = dot(grass, half3(0.2126, 0.7152, 0.0722));
                half3 painted = lerp(grass, min(1, side * lum / _LumMean), _PaintAlpha);
                half3 color = SRGBToLinear(lerp(grass, painted, paint.a));

                float rim = min(min(f.x, f.y), min(1 - f.x, 1 - f.y));
                color = lerp(color, half3(0, 0, 0), _GridStrength * step(rim, 0.04));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
