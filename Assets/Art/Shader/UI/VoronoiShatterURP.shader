
Shader "Custom/UI/VoronoiShatterURP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Progress ("Progress", Range(0,1)) = 0
        _Scatter ("Scatter", Range(0,1)) = 0.45
        _Gravity ("Gravity", Range(0,1)) = 0.35
        _Rotation ("Rotation", Range(0,180)) = 65
        _CrackWidth ("Crack Width", Range(0,0.03)) = 0.004
        _Seed ("Random Seed", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)]
        _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "VoronoiShatter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Progress;
                float _Scatter;
                float _Gravity;
                float _Rotation;
                float _CrackWidth;
                float _Seed;
            CBUFFER_END

            float4 _ClipRect;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 positionOS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS =
                    TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS = v.positionOS.xy;
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float Hash(float2 p)
            {
                return frac(
                    sin(dot(p, float2(127.1, 311.7)))
                    * 43758.5453
                );
            }

            float2 Hash2(float2 p)
            {
                return float2(
                    Hash(p + 13.17),
                    Hash(p + 71.91)
                );
            }

            // Nine jittered Voronoi seed points.
            float2 GetSeed(int index)
            {
                float2 cell = float2(
                    index % 3,
                    index / 3
                );

                float2 randomValue =
                    Hash2(cell + _Seed * 3.17);

                return (cell + 0.2 +
                    randomValue * 0.6) / 3.0;
            }

            // Find the nearest seed and second-nearest
            // distance for drawing Voronoi boundaries.
            void GetVoronoi(
                float2 uv,
                out int nearestId,
                out float edgeDistance)
            {
                float best = 1000.0;
                float second = 1000.0;
                nearestId = 0;

                [unroll]
                for (int j = 0; j < 9; j++)
                {
                    float2 seed = GetSeed(j);
                    float d = distance(uv, seed);

                    if (d < best)
                    {
                        second = best;
                        best = d;
                        nearestId = j;
                    }
                    else if (d < second)
                    {
                        second = d;
                    }
                }

                // Approximate boundary proximity.
                edgeDistance = second - best;
            }

            float2 Rotate(float2 v, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);

                return float2(
                    c * v.x - s * v.y,
                    s * v.x + c * v.y
                );
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float progress = saturate(_Progress);

                // Crack first, then scatter.
                float crackT = smoothstep(
                    0.0, 0.16, progress);

                float moveT = smoothstep(
                    0.16, 0.9, progress);

                float fade = 1.0 -
                    smoothstep(0.65, 1.0, progress);

                float2 bestUV = 0;
                float bestScore = 1000.0;
                float bestEdge = 0;
                bool found = false;

                // Reverse-map all nine moving fragments.
                [unroll]
                for (int k = 0; k < 9; k++)
                {
                    float2 center = GetSeed(k);

                    float2 randomValue =
                        Hash2(float2(k, _Seed));

                    float angle =
                        randomValue.x * 6.2831853;

                    float2 direction =
                        float2(cos(angle), sin(angle));

                    float2 offset =
                        direction * _Scatter *
                        0.22 * moveT;

                    // Downward movement in UV space.
                    offset.y -=
                        _Gravity * 0.3 *
                        moveT * moveT;

                    float spinSign =
                        randomValue.y > 0.5 ? 1.0 : -1.0;

                    float spin =
                        radians(_Rotation) *
                        (0.35 + randomValue.x * 0.65) *
                        spinSign * moveT;

                    // Inverse rotation and translation.
                    float2 candidate =
                        center + Rotate(
                            i.uv - center - offset,
                            -spin
                        );

                    if (any(candidate < 0.0) ||
                        any(candidate > 1.0))
                        continue;

                    int owner;
                    float edge;
                    GetVoronoi(candidate, owner, edge);

                    if (owner != k)
                        continue;

                    // Deterministic choice if pieces overlap.
                    float score =
                        distance(i.uv, center + offset);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestUV = candidate;
                        bestEdge = edge;
                        found = true;
                    }
                }

                if (!found)
                    discard;

                // Cracks are transparent gaps.
                float crackMask = smoothstep(
                    _CrackWidth * crackT,
                    _CrackWidth * crackT + 0.003,
                    bestEdge
                );

                half4 col =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        bestUV
                    ) * i.color;

                col.a *= crackMask * fade;

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside =
                    step(_ClipRect.xy, i.positionOS) *
                    step(i.positionOS, _ClipRect.zw);

                col.a *= inside.x * inside.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDHLSL
        }
    }
}
