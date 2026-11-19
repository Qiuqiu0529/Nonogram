// UI/TitleLiquid
// 标题字里的“液体”效果：字的下半部分是原色的液体，上半部分是浅色的空杯，
// 中间是会晃动的波浪液面，液体里靠下的位置有一排排比原色深一点的同色圆点。
// 打开 Animate 后，液面会慢慢涨满、停一下、再落回去，循环播放。
//
// 用法：
//   1. 标题拆成上下两张图，叠在一起：
//      下层：白色外描边 + 深色描边 + 背后的网点和星星（普通 Image，不用本 Shader）。
//      上层：只有彩色字的填充（每个字是纯色，不带浅色部分和圆点），用本 Shader 的材质。
//      Figma 里导出上层时，把每个 Letter 里的 Liquid Top 和 Dots 两个图层隐藏，再隐藏描边和装饰。
//   2. 上层这张图不要打进图集（Sprite Atlas），否则液面高度会算错；或者用 RawImage。
//   3. Level 是静态液面高度（0 在图片最下边，1 在最上边）。关掉 Animate 就是静态效果，
//      和 Figma 里的效果图一致时大约是 0.5。
//   4. 液面高度是按整张图算的。想让每个字各涨各的，就把每个字单独导出成一张图，
//      各用一个材质，把 Phase 调成不同的数。
// 本文件没有在 Unity 里实际编译过，第一次导入如果报错请把报错发给我。
Shader "UI/TitleLiquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Level ("Liquid Level (static)", Range(0, 1)) = 0.5
        [Toggle] _Animate ("Animate Fill Loop", Float) = 1
        _LevelMin ("Loop: Lowest Level", Range(0, 1)) = 0.18
        _LevelMax ("Loop: Highest Level", Range(0, 1.1)) = 0.95
        _FillTime ("Loop: Fill Seconds", Range(0.2, 20)) = 4
        _HoldTime ("Loop: Hold Seconds", Range(0, 10)) = 1
        _DrainTime ("Loop: Drain Seconds", Range(0.1, 10)) = 0.7
        _Phase ("Loop: Phase Offset (sec)", Range(0, 20)) = 0
        [Toggle] _Loop ("Loop: Drain And Refill (off = fill once and stay)", Float) = 0
        _Bob ("Idle Bob Height (when not looping)", Range(0, 0.1)) = 0.03

        _WaveAmp ("Wave Height", Range(0, 0.2)) = 0.035
        _WaveCount ("Wave Count Across Image", Range(0.5, 12)) = 3
        _WaveSpeed ("Wave Speed", Range(0, 8)) = 1.6
        _Tilt ("Surface Tilt", Range(-0.3, 0.3)) = 0

        _TopLighten ("Empty Part Lighten", Range(0, 1)) = 0.26
        _SurfaceGlow ("Surface Line Brightness", Range(0, 1)) = 0.25
        _SurfaceWidth ("Surface Line Width", Range(0.001, 0.05)) = 0.012

        _DotSpacing ("Dot Spacing (texture px)", Range(4, 64)) = 14
        _DotRadius ("Dot Max Radius (0-0.5)", Range(0, 0.5)) = 0.38
        _DotDepth ("Dot Fade Depth", Range(0.02, 1)) = 0.32
        _DotDarken ("Dot Darkness", Range(0, 1)) = 1

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
            #pragma target 3.0

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

            float _Level, _Animate, _LevelMin, _LevelMax, _FillTime, _HoldTime, _DrainTime, _Phase, _Loop, _Bob;
            float _WaveAmp, _WaveCount, _WaveSpeed, _Tilt;
            float _TopLighten, _SurfaceGlow, _SurfaceWidth;
            float _DotSpacing, _DotRadius, _DotDepth, _DotDarken;

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

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 col = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                float t = _Time.y;

                // 1. 液面高度：静态值，或者“涨满 - 停住 - 落回”的循环
                float level = _Level;
                if (_Animate > 0.5)
                {
                    float k;
                    if (_Loop > 0.5)
                    {
                        float total = _FillTime + _HoldTime + _DrainTime;
                        float ph = fmod(t + _Phase, total);
                        if (ph < _FillTime) k = smoothstep(0.0, 1.0, ph / _FillTime);
                        else if (ph < _FillTime + _HoldTime) k = 1.0;
                        else k = 1.0 - smoothstep(0.0, 1.0, (ph - _FillTime - _HoldTime) / _DrainTime);
                        level = lerp(_LevelMin, _LevelMax, k);
                    }
                    else
                    {
                        // 只涨一次：涨到高位后停住，轻轻起伏，不会掉回去重来。
                        k = smoothstep(0.0, 1.0, saturate((t + _Phase) / _FillTime));
                        level = lerp(_LevelMin, _LevelMax, k) + sin(t * 1.3) * _Bob * k;
                    }
                }

                // 2. 波浪液面：两层正弦叠加，朝相反方向移动
                float w1 = sin(uv.x * _WaveCount * 6.2832 + t * _WaveSpeed);
                float w2 = sin(uv.x * _WaveCount * 2.3 * 6.2832 - t * _WaveSpeed * 1.7);
                float surface = level + (w1 * 0.6 + w2 * 0.4) * _WaveAmp + (uv.x - 0.5) * _Tilt;
                float depth = surface - uv.y;            // 大于 0 表示在液面以下
                float aa = max(fwidth(uv.y) * 1.5, 1e-4);
                float inLiquid = smoothstep(-aa, aa, depth);

                // 3. 液面以上是浅色的“空杯”，以下是原色
                float3 emptyCol = lerp(col.rgb, float3(1, 1, 1), _TopLighten);
                float3 rgb = lerp(emptyCol, col.rgb, inLiquid);

                // 4. 液体里的同色圆点：交错排列，越靠下越大，跟着液面一起升降
                float2 g = uv * _MainTex_TexelSize.zw / max(_DotSpacing, 1.0);
                float gaa = max(fwidth(g.y), 1e-4);
                float row = floor(g.y);
                g.x += fmod(row, 2.0) * 0.5;
                float2 cell = frac(g) - 0.5;
                float rowCenterV = (row + 0.5) * _DotSpacing * _MainTex_TexelSize.y;
                float dotDepth = saturate((level - rowCenterV) / max(_DotDepth, 0.02));
                float radius = _DotRadius * dotDepth;
                float dotMask = (1.0 - smoothstep(radius - gaa, radius + gaa, length(cell))) * step(0.001, radius);
                float3 dotCol = col.rgb * lerp(float3(1, 1, 1), float3(0.86, 0.76, 0.72), _DotDarken);
                rgb = lerp(rgb, dotCol, dotMask * inLiquid);

                // 5. 液面上一条细细的亮线
                float lineMask = 1.0 - smoothstep(0.0, _SurfaceWidth, abs(depth));
                rgb = lerp(rgb, float3(1, 1, 1), lineMask * _SurfaceGlow);

                col.rgb = rgb;

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
