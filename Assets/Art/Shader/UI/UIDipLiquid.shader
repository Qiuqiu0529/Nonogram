// UI/DipLiquid
// 语言碟里的两种蘸料：左边一种颜色、右边一种颜色，中间是一条竖着的、会流动的波浪分界线。
// 和标题的液体是一套画法：分界线上有一条亮线，左边的蘸料里有同色的深色圆点，离分界线越远越大。
// 分界线的位置、波浪的相位和幅度都由 DipLiquid 脚本每帧写进来，所以悬停时加快流动不会跳帧。
// 用在一张圆形的图上（图的透明度决定形状），不要打进图集。
Shader "UI/DipLiquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ColorLeft ("Left Sauce", Color) = (0.91, 0.314, 0.227, 1)
        _ColorRight ("Right Sauce", Color) = (1, 0.906, 0.678, 1)
        _Split ("Boundary Position (0 = left edge, 1 = right edge)", Range(0, 1)) = 0.7

        _WaveAmp ("Wave Width", Range(0, 0.2)) = 0.035
        _WaveCount ("Wave Count Down The Dish", Range(0.5, 6)) = 1.4
        _WavePhase ("Wave Phase (driven by script)", Float) = 0

        _SurfaceGlow ("Boundary Line Brightness", Range(0, 1)) = 0.3
        _SurfaceWidth ("Boundary Line Width", Range(0.001, 0.05)) = 0.014

        _DotSpacing ("Dot Spacing (texture px)", Range(4, 64)) = 13
        _DotRadius ("Dot Max Radius (0-0.5)", Range(0, 0.5)) = 0.36
        _DotDepth ("Dot Fade Depth", Range(0.02, 1)) = 0.45
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

            fixed4 _ColorLeft, _ColorRight;
            float _Split, _WaveAmp, _WaveCount, _WavePhase;
            float _SurfaceGlow, _SurfaceWidth;
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
                // 图本身只提供形状（透明度），颜色全部由两种蘸料决定。
                fixed alpha = (tex2D(_MainTex, uv) + _TextureSampleAdd).a * IN.color.a;

                // 1. 竖着的波浪分界线：两层正弦叠加，朝相反方向流
                float w1 = sin(uv.y * _WaveCount * 6.2832 + _WavePhase);
                float w2 = sin(uv.y * _WaveCount * 2.3 * 6.2832 - _WavePhase * 1.7);
                float boundary = _Split + (w1 * 0.6 + w2 * 0.4) * _WaveAmp;
                float depth = boundary - uv.x;           // 大于 0 表示在左边的蘸料里
                float aa = max(fwidth(uv.x) * 1.5, 1e-4);
                float inLeft = smoothstep(-aa, aa, depth);
                float3 rgb = lerp(_ColorRight.rgb, _ColorLeft.rgb, inLeft);

                // 2. 左边蘸料里的同色圆点：交错排列，离分界线越远越大，跟着分界线一起移动
                float2 g = uv * _MainTex_TexelSize.zw / max(_DotSpacing, 1.0);
                float gaa = max(fwidth(g.x), 1e-4);
                float column = floor(g.x);
                g.y += fmod(column, 2.0) * 0.5;
                float2 cell = frac(g) - 0.5;
                float columnCenterU = (column + 0.5) * _DotSpacing * _MainTex_TexelSize.x;
                float dotDepth = saturate((_Split - columnCenterU) / max(_DotDepth, 0.02));
                float radius = _DotRadius * dotDepth;
                float dotMask = (1.0 - smoothstep(radius - gaa, radius + gaa, length(cell))) * step(0.001, radius);
                float3 dotCol = _ColorLeft.rgb * lerp(float3(1, 1, 1), float3(0.86, 0.76, 0.72), _DotDarken);
                rgb = lerp(rgb, dotCol, dotMask * inLeft);

                // 3. 分界线上一条细细的亮线
                float lineMask = 1.0 - smoothstep(0.0, _SurfaceWidth, abs(depth));
                rgb = lerp(rgb, float3(1, 1, 1), lineMask * _SurfaceGlow);

                fixed4 col = fixed4(rgb * IN.color.rgb, alpha);

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
