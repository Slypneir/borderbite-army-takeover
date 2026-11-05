// The surface of the map: one quad. Grass of the 3D artist underneath (_GrassTex, the whole island once, a texel a voxel), and
// over it the land of the sides as borders of «Civilization V» (task 4.21, the artist's surface README «Земля сторон», numbers
// of territory.json): a crisp line of the side's colour just inside the border, a thin dark edge just outside, and a saturated
// but see-through fill, strongest at the border and fading inward, never to nothing. _FieldTex holds for each side the signed
// distance to its border in cells (red the player, green the enemy; + inside), smooth, so the borders have no steps of cells.
// _CellTex alpha is the white flash of a cell that changed, red 0 a cell of water (layer 5): the surface is cut away there. The formulas work in sRGB 0..1, as the artist's reference.
// _GridStrength draws the grid for the developer.
Shader "Borderbite/Map/Cells"
{
    Properties
    {
        _CellTex ("Cells: alpha the flash of a change (a texel per cell, point filter)", 2D) = "black" {}
        _FieldTex ("Distance to the borders, cells: r the player, g the enemy (bilinear)", 2D) = "black" {}
        _GrassTex ("Grass (point filter)", 2D) = "white" {}
        _GrassTiles ("Grass tiles over the map, across and up", Vector) = (1, 1, 0, 0)
        _Cells ("Cells across and up", Vector) = (64, 64, 0, 0)
        _CellSize ("Side of a cell, meters", Float) = 0.5
        _FieldRange ("Distance kept in the field, cells", Float) = 4
        _PlayerColor ("Land of the player", Color) = (0.25, 0.5, 0.95, 1)
        _EnemyColor ("Land of the enemy", Color) = (0.9, 0.3, 0.27, 1)
        _LumMean ("Mean brightness of the grass", Range(0.05, 1)) = 0.491
        _LineWidth ("Line inside the border, meters", Float) = 0.14
        _LineSoft ("Softness of the edges, meters", Float) = 0.025
        _LineLighten ("Line lightened to white", Range(0, 1)) = 0.04
        _LineAlpha ("Line opacity", Range(0, 1)) = 1
        _EdgeWidth ("Dark edge outside the border, meters", Float) = 0.035
        _EdgeDarken ("Dark edge darkened", Range(0, 1)) = 0.5
        _EdgeAlpha ("Dark edge opacity", Range(0, 1)) = 0.7
        _FillEdgeAlpha ("Fill at the border", Range(0, 1)) = 0.7
        _FillInnerAlpha ("Fill deep inside", Range(0, 1)) = 0.26
        _FillFade ("Fill fades over, meters", Float) = 2
        _FillLighten ("Fill lightened to white", Range(0, 1)) = 0.14
        _FillKeepTexture ("Grass pattern kept in the fill", Range(0, 1)) = 0.4
        _FlashStrength ("White of the flash of a change", Range(0, 1)) = 0.6
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
            TEXTURE2D(_FieldTex);
            SAMPLER(sampler_FieldTex);
            TEXTURE2D(_GrassTex);
            SAMPLER(sampler_GrassTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CellTex_ST;
                float4 _FieldTex_ST;
                float4 _GrassTex_ST;
                float4 _Cells;
                float _CellSize;
                float _FieldRange;
                float _GridStrength;
                float4 _GrassTiles;
                float4 _PlayerColor;
                float4 _EnemyColor;
                float _LumMean;
                float _LineWidth;
                float _LineSoft;
                float _LineLighten;
                float _LineAlpha;
                float _EdgeWidth;
                float _EdgeDarken;
                float _EdgeAlpha;
                float _FillEdgeAlpha;
                float _FillInnerAlpha;
                float _FillFade;
                float _FillLighten;
                float _FillKeepTexture;
                float _FlashStrength;
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

            // the land of one side over the colour so far; d is the distance to its border in meters, + inside
            half3 Land(half3 color, half3 grass, half lum, half3 side, float d)
            {
                float soft = _LineSoft;
                if (d > 0)
                {
                    half3 fill = lerp(side, half3(1, 1, 1), _FillLighten) * (1 + (lum / _LumMean - 1) * _FillKeepTexture);
                    half a = lerp(_FillInnerAlpha, _FillEdgeAlpha, 1 - smoothstep(0, _FillFade, d));
                    color = lerp(color, min(fill, 1), a * smoothstep(-soft, soft, d));
                }

                half edge = smoothstep(-_EdgeWidth - soft, -_EdgeWidth + soft, d) * (1 - smoothstep(-soft, soft, d));
                color = lerp(color, side * (1 - _EdgeDarken), edge * _EdgeAlpha);
                half border = smoothstep(-soft, soft, d) * (1 - smoothstep(_LineWidth - soft, _LineWidth + soft, d));
                color = lerp(color, lerp(side, half3(1, 1, 1), _LineLighten), border * _LineAlpha);
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 grid = input.uv * _Cells.xy;
                float2 cell = floor(grid);
                float2 f = frac(grid);
                // the middle of cell i of the map is texel i + 1 of the field (a neutral ring around the map)
                half2 field = SAMPLE_TEXTURE2D(_FieldTex, sampler_FieldTex, (grid + 1) / (_Cells.xy + 2)).rg;
                float2 d = (field * 2 - 1) * _FieldRange * _CellSize;

                half3 grass = LinearToSRGB(SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, input.uv * _GrassTiles.xy).rgb);
                half lum = dot(grass, half3(0.2126, 0.7152, 0.0722));
                half3 color = grass;
                color = Land(color, grass, lum, LinearToSRGB(_EnemyColor.rgb), d.y);
                color = Land(color, grass, lum, LinearToSRGB(_PlayerColor.rgb), d.x);

                // a cell that changed flashes white for a moment, on its land
                half4 texel = SAMPLE_TEXTURE2D(_CellTex, sampler_CellTex, (cell + 0.5) / _Cells.xy);
                clip(texel.r - 0.5);
                half flash = texel.a;
                color = lerp(color, half3(1, 1, 1), flash * _FlashStrength * step(0, max(d.x, d.y)));
                color = SRGBToLinear(color);

                float rim = min(min(f.x, f.y), min(1 - f.x, 1 - f.y));
                color = lerp(color, half3(0, 0, 0), _GridStrength * step(rim, 0.04));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
