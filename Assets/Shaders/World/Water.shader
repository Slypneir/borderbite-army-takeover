// The sea around the island (stage 1 spec 6, owner's decision of 4 November): see-through over the shore, no bottom in sight,
// swell rolling to the shore, white caps and foam breathing at the shore and the rocks. After the reference of the 3D artist
// (voxel-forge models/borderbite/_water.js, README of export/borderbite/surface); the numbers come from water.json into this
// material (ProjectSetup). No depth texture (it costs a pass on weak phones): the depth is the distance to the square of the
// island (0.._MapSize on x and z) and to the circles of the rocks (_Rocks: x, z, radius), steeper at the rocks. Everything is
// snapped to the voxel, 1/8 m, so foam and stripes stay blocky; the loop is 6 s.
Shader "Borderbite/World/Water"
{
    Properties
    {
        _FoamTex ("Foam (R shape, G phase, B noise; linear, point)", 2D) = "black" {}
        _Shallow ("Shallow", Color) = (0.31, 0.663, 0.627, 1)
        _Deep ("Deep", Color) = (0.184, 0.518, 0.529, 1)
        _Abyss ("Abyss", Color) = (0.145, 0.412, 0.427, 1)
        _Crest ("Crest of the swell", Color) = (0.373, 0.702, 0.659, 1)
        _Foam ("Foam", Color) = (0.863, 0.922, 0.894, 1)
        _FoamRim ("Rim of the foam", Color) = (0.663, 0.839, 0.8, 1)
        _MapSize ("Island side, m", Float) = 32
        _PixelsPerMeter ("Voxels in a meter", Float) = 8
        _AbyssFrom ("Abyss from, m off the shore", Float) = 2
        _AbyssTo ("Abyss to, m", Float) = 14
        _ShoreLip ("Shore lip, m", Float) = 0.45
        _Slope ("Depth per meter off the shore", Float) = 1
        _RockSlope ("Depth per meter off a rock", Float) = 2.2
        _FullDepth ("Opaque from this depth, m", Float) = 1.2
        _AlphaEdge ("Alpha at the shore", Range(0, 1)) = 0.35
        _Swell ("Swell: length m, period s, warp, strength", Vector) = (2.5, 3, 0.9, 0.32)
        _SwellRange ("Swell from, to (m off the shore)", Vector) = (0.8, 2.6, 0, 0)
        _Caps ("Caps: tile px, period s, life, strength", Vector) = (64, 3, 0.5, 0.85)
        _CapsFrom ("Caps from, m off the shore", Float) = 1.6
        _ShoreFoam ("Shore foam: min m, max m, period s, lace m", Vector) = (0.08, 0.3, 3, 0.14)
        _ShoreFoamAlpha ("Shore foam alpha", Range(0, 1)) = 0.9
        _RockRadii ("Rock radius at the water: small, large, tall", Vector) = (0.315, 0.773, 0.635, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_ROCKS 16

            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FoamTex_ST;
                half4 _Shallow;
                half4 _Deep;
                half4 _Abyss;
                half4 _Crest;
                half4 _Foam;
                half4 _FoamRim;
                float _MapSize;
                float _PixelsPerMeter;
                float _AbyssFrom;
                float _AbyssTo;
                float _ShoreLip;
                float _Slope;
                float _RockSlope;
                float _FullDepth;
                float _AlphaEdge;
                float4 _Swell;
                float4 _SwellRange;
                float4 _Caps;
                float _CapsFrom;
                float4 _ShoreFoam;
                float _ShoreFoamAlpha;
                float4 _RockRadii;
            CBUFFER_END

            // set per battle by MapView: not in the material's buffer
            float4 _Rocks[MAX_ROCKS];
            float _RockCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.world = world.xz;
                return output;
            }

            // an integer hash, the same on every pixel of a voxel: 0..1
            float Hash(int x, int y, int salt)
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 982451653u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (float)(h ^ (h >> 16)) / 4294967296.0;
            }

            half4 Foam(int2 px)
            {
                float tile = _Caps.x;
                float2 uv = (fmod(fmod((float2)px, tile) + tile, tile) + 0.5) / tile;
                return SAMPLE_TEXTURE2D_LOD(_FoamTex, sampler_FoamTex, uv, 0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const float tau = 6.28318530718;
                float t = _Time.y;
                int2 px = (int2)floor(input.world * _PixelsPerMeter);
                float2 p = ((float2)px + 0.5) / _PixelsPerMeter;

                float dEdge = length(max(max(-p, p - _MapSize), 0));
                float depth = max(0, dEdge - _ShoreLip) * _Slope;
                for (int i = 0; i < MAX_ROCKS; i++)
                {
                    if (i >= (int)_RockCount)
                        break;
                    depth = min(depth, max(0, distance(p, _Rocks[i].xy) - _Rocks[i].z) * _RockSlope);
                }

                half4 f = Foam(px);
                half3 sea = lerp(_Deep.rgb, _Abyss.rgb, smoothstep(_AbyssFrom, _AbyssTo, dEdge));
                float k = smoothstep(0, _FullDepth, depth);
                half3 color = lerp(_Shallow.rgb, sea, k);

                // swell: stripes rolling to the shore
                float s = sin(tau * (dEdge / _Swell.x + t / _Swell.y + _Swell.z * (f.b - 0.5)));
                color = lerp(color, _Crest.rgb,
                    smoothstep(0.55, 1, s) * _Swell.w * smoothstep(_SwellRange.x, _SwellRange.y, min(dEdge, depth + _ShoreLip)));

                // white caps: two reads of the foam turned against each other, a phase per tile, so the 8 m repeat does not show
                if (dEdge > _CapsFrom)
                {
                    float fade = _Caps.w * smoothstep(_CapsFrom, _CapsFrom + 1.5, dEdge);
                    for (int layer = 0; layer < 2; layer++)
                    {
                        int2 q = layer == 0 ? px : int2(-px.y + 23, px.x + 41);
                        half4 c = layer == 0 ? f : Foam(q);
                        int2 tileOf = (int2)floor((float2)q / _Caps.x);
                        float life = frac(t / _Caps.y + c.g + Hash(tileOf.x, tileOf.y, 31 + layer)) / _Caps.z;
                        float grow = sin(PI * saturate(life));
                        float threshold = 1.05 - 0.75 * grow + 0.25 * (0.5 - 0.5 * s);
                        if (life < 1 && c.r >= threshold)
                            color = lerp(color, (c.r > 0.8 && grow > 0.5) ? _Foam.rgb : _FoamRim.rgb, fade);
                    }
                }

                float alpha = lerp(_AlphaEdge, 1, k);

                // foam at the shore and the rocks: the edge breathes
                float breath = 0.5 + 0.5 * sin(tau * t / _ShoreFoam.z);
                float edge = lerp(_ShoreFoam.x, _ShoreFoam.y, breath) * (0.6 + 0.8 * f.b);
                if (depth < edge)
                {
                    color = lerp(color, _Foam.rgb, _ShoreFoamAlpha);
                    alpha = 1;
                }
                else if (depth < edge + _ShoreFoam.w && Hash(px.x, px.y, 50 + (int)floor(t / _ShoreFoam.z * 2)) > 0.55)
                {
                    color = lerp(color, _FoamRim.rgb, 0.7 * _ShoreFoamAlpha);
                }

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
