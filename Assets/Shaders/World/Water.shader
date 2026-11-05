// The sea around the island (stage 1 spec 6; owner's look of 4 November, task 4.10): calm low-poly water — a mesh of uneven
// triangles a meter apart, its vertices raised by three slow trains of waves (periods 6, 4 and 3 s, the loop is 12 s) up to
// 0.2 m, quieter and rougher in patches of ~10 m and flat at the shore and the rocks. The light and the tint of crests go per
// facet (flat shading from the screen derivatives, the height of the facet from its first vertex); the depth, the foam and the
// translucency per voxel of 1/8 m, so the shallows round the rocks stay round. No depth texture: the depth is the distance to
// the square of the island (0.._MapSize) and to the circles of the rocks. After the 3D artist's reference (voxel-forge
// models/borderbite/_water.js, README of export/borderbite/surface); the numbers come from water.json (ProjectSetup).
// _Waves 0 lays the sea flat: the far water to the horizon, one quad.
Shader "Borderbite/World/Water"
{
    Properties
    {
        _FoamTex ("Foam (R shape, G phase, B noise; linear, point)", 2D) = "black" {}
        _Shallow ("Shallow", Color) = (0.31, 0.663, 0.627, 1)
        _Deep ("Deep", Color) = (0.184, 0.518, 0.529, 1)
        _Abyss ("Abyss", Color) = (0.145, 0.412, 0.427, 1)
        _Crest ("Crest", Color) = (0.424, 0.737, 0.69, 1)
        _Trough ("Trough", Color) = (0.141, 0.404, 0.42, 1)
        _Foam ("Foam", Color) = (0.863, 0.922, 0.894, 1)
        _FoamRim ("Rim of the foam", Color) = (0.663, 0.839, 0.8, 1)
        _MapSize ("Island across and up, m", Vector) = (32, 32, 0, 0)
        _PixelsPerMeter ("Voxels in a meter", Float) = 8
        _AbyssFrom ("Abyss from, m off the shore", Float) = 3
        _AbyssTo ("Abyss to, m", Float) = 16
        _AbyssWarp ("Abyss border warp, m", Float) = 6
        _PatchTint ("Greener or bluer patches", Float) = 0.05
        _ShoreLip ("Shore lip, m", Float) = 0.45
        _Slope ("Depth per meter off the shore", Float) = 1
        _RockSlope ("Depth per meter off a rock", Float) = 2.2
        _FullDepth ("Opaque from this depth, m", Float) = 1.2
        _AlphaEdge ("Alpha at the shore", Range(0, 1)) = 0.35
        _Waves ("Waves on (1) or flat (0)", Float) = 1
        _Train0 ("Wave train 1: dir x, dir z, length m, period s", Vector) = (0.94, 0.33, 7, 6)
        _Train1 ("Wave train 2", Vector) = (-0.37, 0.93, 5, 4)
        _Train2 ("Wave train 3", Vector) = (0.6, -0.8, 3.5, 3)
        _TrainWeight ("Weights of the trains", Vector) = (0.5, 0.32, 0.18, 0)
        _TrainPhase ("Phases of the trains", Vector) = (0, 1.7, 4.1, 0)
        _Amp ("Height: min m, max m, patch m", Vector) = (0.05, 0.2, 10, 0)
        _Calm ("Flat at the shore: from depth, to depth", Vector) = (0.15, 1.4, 0, 0)
        _Tint ("Crest tint, trough tint", Vector) = (0.22, 0.18, 0, 0)
        _Caps ("Caps: tile px, period s, life, strength", Vector) = (64, 6, 0.4, 0.5)
        _CapsHeight ("Caps only on crests from this height, m", Float) = 0.1
        _ShoreFoam ("Shore foam: min m, max m, period s, lace m", Vector) = (0.08, 0.3, 6, 0.14)
        _ShoreFoamAlpha ("Shore foam alpha", Range(0, 1)) = 0.85
        _Shadow ("Light of a facet turned away", Range(0, 1)) = 0.55
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
                half4 _Trough;
                half4 _Foam;
                half4 _FoamRim;
                float4 _MapSize;
                float _PixelsPerMeter;
                float _AbyssFrom;
                float _AbyssTo;
                float _AbyssWarp;
                float _PatchTint;
                float _ShoreLip;
                float _Slope;
                float _RockSlope;
                float _FullDepth;
                float _AlphaEdge;
                float _Waves;
                float4 _Train0;
                float4 _Train1;
                float4 _Train2;
                float4 _TrainWeight;
                float4 _TrainPhase;
                float4 _Amp;
                float4 _Calm;
                float4 _Tint;
                float4 _Caps;
                float _CapsHeight;
                float4 _ShoreFoam;
                float _ShoreFoamAlpha;
                float _Shadow;
                float4 _RockRadii;
            CBUFFER_END

            // set per battle by MapView: not in the material's buffer
            float4 _Rocks[MAX_ROCKS];
            float _RockCount;
            // a map with water (layer 5): the distance to the land in a texture, meters up to _LandDistRange, over the rectangle
            // from (x, y) of sides z, w; off it, and on the plain island, the distance to the rectangle of the map
            TEXTURE2D(_LandDist);
            SAMPLER(sampler_LandDist);
            float4 _LandDistRect;
            float _LandDistRange;
            float _LandDistOn;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 world : TEXCOORD0;
                // the height of the facet: its first vertex, the same for the whole triangle
                nointerpolation float height : TEXCOORD1;
            };

            // an integer hash, the same on every pixel of a voxel: 0..1
            float Hash(int x, int y, int salt)
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 982451653u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (float)(h ^ (h >> 16)) / 4294967296.0;
            }

            // value noise over cells of <cell> units, smooth: patches of the sea
            float Noise(float2 p, float cell, int salt)
            {
                float2 f = p / cell;
                int2 i = (int2)floor(f);
                float2 t = f - floor(f);
                t = t * t * (3 - 2 * t);
                float a = Hash(i.x, i.y, salt);
                float b = Hash(i.x + 1, i.y, salt);
                float c = Hash(i.x, i.y + 1, salt);
                float d = Hash(i.x + 1, i.y + 1, salt);
                return lerp(lerp(a, b, t.x), lerp(c, d, t.x), t.y);
            }

            float EdgeDistance(float2 p)
            {
                float square = length(max(max(-p, p - _MapSize.xy), 0));
                float2 uv = (p - _LandDistRect.xy) / _LandDistRect.zw;
                if (_LandDistOn < 0.5 || any(uv < 0) || any(uv > 1))
                    return square;

                // the lip under the shore of the 3D artist is not there at a cliff of cells: the foam keeps to the wall
                return SAMPLE_TEXTURE2D_LOD(_LandDist, sampler_LandDist, uv, 0).r * _LandDistRange + max(0, _ShoreLip - 0.06);
            }

            float Depth(float2 p, float dEdge)
            {
                float depth = max(0, dEdge - _ShoreLip) * _Slope;
                for (int i = 0; i < MAX_ROCKS; i++)
                {
                    if (i >= (int)_RockCount)
                        break;
                    depth = min(depth, max(0, distance(p, _Rocks[i].xy) - _Rocks[i].z) * _RockSlope);
                }
                return depth;
            }

            float Train(float4 train, float weight, float phase, float2 p, float t)
            {
                return weight * sin(6.28318530718 * (dot(train.xy, p) / train.z - t / train.w) + phase);
            }

            float WaveHeight(float2 p, float t)
            {
                float h = Train(_Train0, _TrainWeight.x, _TrainPhase.x, p, t)
                        + Train(_Train1, _TrainWeight.y, _TrainPhase.y, p, t)
                        + Train(_Train2, _TrainWeight.z, _TrainPhase.z, p, t);
                float amp = lerp(_Amp.x, _Amp.y, smoothstep(0.25, 0.75, Noise(p * 8, _Amp.z * 8, 61)));
                return h * amp * smoothstep(_Calm.x, _Calm.y, Depth(p, EdgeDistance(p))) * _Waves;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float h = WaveHeight(world.xz, _Time.y);
                world.y += h;
                output.positionCS = TransformWorldToHClip(world);
                output.world = world;
                output.height = h;
                return output;
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
                int2 px = (int2)floor(input.world.xz * _PixelsPerMeter);
                float2 p = ((float2)px + 0.5) / _PixelsPerMeter;
                float dEdge = EdgeDistance(p);
                float depth = Depth(p, dEdge);
                float k = smoothstep(0, _FullDepth, depth);

                // the depth: a border not round the island, patches a little greener or bluer
                float warp = dEdge + _AbyssWarp * (Noise(p * 8, 96, 3) - 0.5);
                half3 sea = lerp(_Deep.rgb, _Abyss.rgb, smoothstep(_AbyssFrom, _AbyssTo, warp));
                float q = Noise(p * 8, 56, 5) - 0.5;
                sea.g *= 1 + _PatchTint * q;
                sea.b *= 1 - _PatchTint * q;
                half3 color = lerp(_Shallow.rgb, sea, k);

                // the facet: crests lighter, troughs darker, flat light by its normal
                float crest = input.height / max(0.001, _Amp.y);
                color = crest > 0 ? lerp(color, _Crest.rgb, _Tint.x * min(1, crest)) : lerp(color, _Trough.rgb, _Tint.y * min(1, -crest));
                float3 normal = normalize(cross(ddy(input.world), ddx(input.world)));
                normal *= sign(normal.y + 1e-5);
                float3 light = normalize(float3(-0.45, 1.0, -0.35));
                float lit = lerp(_Shadow, 1.0, saturate(dot(normal, light)));
                float flat = lerp(_Shadow, 1.0, light.y);
                color *= lit / flat;

                // soft caps, only on high crests: two reads of the foam turned against each other, a phase per tile
                half4 f = Foam(px);
                if (input.height >= _CapsHeight)
                {
                    for (int layer = 0; layer < 2; layer++)
                    {
                        int2 r = layer == 0 ? px : int2(-px.y + 23, px.x + 41);
                        half4 c = layer == 0 ? f : Foam(r);
                        int2 tileOf = (int2)floor((float2)r / _Caps.x);
                        float life = frac(t / _Caps.y + c.g + Hash(tileOf.x, tileOf.y, 31 + layer)) / _Caps.z;
                        float grow = sin(PI * saturate(life));
                        if (life < 1 && c.r >= 1.05 - 0.75 * grow)
                            color = lerp(color, (c.r > 0.8 && grow > 0.5) ? _Foam.rgb : _FoamRim.rgb, _Caps.w);
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
