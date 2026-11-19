// UI/PaperCrayon
// 给 UGUI 的 Image / RawImage 加上纸张拼贴质感：边缘抖动、蜡笔干边、蜡痕、纸张颗粒。
// 与 Figma 里的 “Paper Crayon” 着色器是同一套算法和参数，方便对照效果图调参。
// 用法：新建材质并选择本 Shader，把材质拖到 Image 的 Material 上。
// 注意：
//   1. 边缘抖动会采样到图片边界之外，图片四周请留 8-12 像素透明边；
//      打进图集（Sprite Atlas）时把 Padding 调大，否则会采到相邻图片。
//   2. Boil FPS 大于 0 时，纹理会按这个帧率跳变，做出手绘逐帧“抖动线”的感觉。
//   3. 本文件没有在 Unity 里实际编译过，第一次导入如果报错请把报错发给我。
Shader "UI/PaperCrayon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Grain ("Paper Grain", Range(0, 1)) = 0.45
        _GrainSize ("Grain Size (px)", Range(0.5, 6)) = 1.6
        _Wobble ("Edge Wobble (px)", Range(0, 12)) = 3
        _WobbleScale ("Wobble Size (px)", Range(15, 240)) = 70
        _EdgeRough ("Crayon Edge", Range(0, 1)) = 0.5
        _Streaks ("Wax Streaks", Range(0, 1)) = 0.3
        _StreakAngle ("Streak Angle (deg)", Range(0, 180)) = 35
        _Seed ("Variation", Range(0, 100)) = 7
        _BoilFps ("Boil FPS (0 = static)", Range(0, 12)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Grain, _GrainSize, _Wobble, _WobbleScale, _EdgeRough, _Streaks, _StreakAngle, _Seed, _BoilFps;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                float2 q = frac(p * float2(123.34, 456.21));
                q += dot(q, q + 45.32);
                return frac(q.x * q.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += amp * vnoise(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    amp *= 0.5;
                }
                return v;
            }

            fixed4 sampleMain(float2 uv)
            {
                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 用画布坐标做噪声坐标：九宫格拉伸的图片上纹理也不会被拉花
                float2 px = IN.worldPosition.xy;
                float seed = _Seed;
                if (_BoilFps > 0.01) seed += floor(_Time.y * _BoilFps) * 7.31;

                // 1. 边缘抖动
                float2 wp = px / max(_WobbleScale, 1.0) + float2(seed * 3.7, seed * 1.3);
                float2 shiftPx = float2(fbm(wp) - 0.5, fbm(wp + float2(31.7, 11.9)) - 0.5) * 2.0 * _Wobble;
                float2 uv = IN.texcoord + shiftPx * _MainTex_TexelSize.xy;
                fixed4 col = sampleMain(uv);

                // 2. 蜡笔干边：先估计离边缘多近，再用细噪声啃掉一部分
                float2 e = 2.5 * _MainTex_TexelSize.xy;
                float aL = sampleMain(uv - float2(e.x, 0)).a;
                float aR = sampleMain(uv + float2(e.x, 0)).a;
                float aU = sampleMain(uv - float2(0, e.y)).a;
                float aD = sampleMain(uv + float2(0, e.y)).a;
                float aLow = min(min(aL, aR), min(aU, aD));
                float edge = saturate((col.a - aLow) * 1.6);
                float fine = vnoise(px / 2.2 + float2(seed * 5.1, 0));
                float coarse = fbm(px / 9.0 + float2(0, seed * 2.9));
                float bite = smoothstep(0.42, 0.78, fine * 0.55 + coarse * 0.7);
                col.a *= 1.0 - _EdgeRough * edge * bite;

                // 3. 蜡痕
                float ang = radians(_StreakAngle);
                float2 rot = float2(px.x * cos(ang) + px.y * sin(ang), -px.x * sin(ang) + px.y * cos(ang));
                float streak = vnoise(float2(rot.x / 70.0 + seed, rot.y / 2.6));
                float streakMul = 1.0 - _Streaks * 0.22 * (streak - 0.35);

                // 4. 纸张颗粒
                float speck = hash21(floor(px / max(_GrainSize, 0.5)) + float2(seed, seed * 0.37));
                float mottle = fbm(px / 38.0 + float2(seed * 0.9, 4.0));
                float dark = step(0.965, speck);
                float grainMul = 1.0 - _Grain * (0.30 * (speck - 0.5) + 0.22 * (mottle - 0.5) + 0.22 * dark);

                col.rgb = saturate(col.rgb * streakMul * grainMul);
                col *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
