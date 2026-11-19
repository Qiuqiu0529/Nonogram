// UI/RainStreaks
// 一层往下落的雨丝，铺在背景和界面内容之间。全部是算出来的，不需要贴图。
// 画法：把画面切成很多竖条，每条里有一根圆头的短雨丝按自己的速度往下落；两层叠在一起，
// 近的一层粗一点、快一点，远的一层细一点、慢一点、更淡。
// 用在一张铺满界面的 Image 上（图本身用什么都行，只取它的矩形）。
Shader "UI/RainStreaks"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Rain Colour (alpha = strength)", Color) = (0.86, 0.9, 0.95, 0.55)

        _Columns ("Streak Columns Across The Screen", Range(8, 120)) = 34
        _Density ("How Many Columns Have Rain", Range(0, 1)) = 0.7
        _Length ("Streak Length (fraction of screen height)", Range(0.01, 0.3)) = 0.07
        _Width ("Streak Width (fraction of a column)", Range(0.02, 0.5)) = 0.09
        _Speed ("Fall Speed (screens per second)", Range(0, 3)) = 0.55
        _Slant ("Slant (positive leans right as it falls)", Range(-0.6, 0.6)) = 0.12
        _FarLayer ("Far Layer Strength", Range(0, 1)) = 0.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

            fixed4 _Color;
            float4 _ClipRect;
            float _Columns, _Density, _Length, _Width, _Speed, _Slant, _FarLayer;

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

            float hash(float n)
            {
                return frac(sin(n * 127.1 + 311.7) * 43758.5453);
            }

            // 一层雨。columns 越大雨丝越细越密；返回 0 到 1 的遮罩。
            float rainLayer(float2 uv, float aspect, float columns, float speed, float seed)
            {
                // 往下落的同时往一边斜：越往下（uv.y 越小）横向偏得越多。
                float x = (uv.x + (1.0 - uv.y) * _Slant / aspect) * columns;
                float column = floor(x);
                float inColumn = frac(x) - 0.5;

                float lane = hash(column + seed);
                if (lane > _Density) return 0.0;

                // 每条竖道有自己的速度和起点，一屏高度里落两根。
                float laneSpeed = speed * lerp(0.75, 1.25, hash(column + seed + 17.0));
                float y = frac(uv.y * 2.0 + _Time.y * laneSpeed * 2.0 + lane * 7.0) * 0.5;

                // 圆头的短线：在竖直方向上是一段，在两头收成半圆。距离都换算成「列宽」为单位。
                float columnHeight = aspect / columns;              // 一列的宽度相当于多少屏高
                float halfLength = _Length * 0.5;
                float along = max(abs(y - 0.25) - halfLength, 0.0) / columnHeight;
                float across = inColumn + (hash(column + seed + 5.0) - 0.5) * 0.5;
                float dist = sqrt(along * along + across * across);
                float edge = max(fwidth(x), 1e-4);
                return 1.0 - smoothstep(_Width * 0.5 - edge, _Width * 0.5 + edge, dist);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                // 界面的宽高比：一个像素在 uv 里竖着走的距离除以横着走的距离。
                float aspect = max(fwidth(uv.y), 1e-5) / max(fwidth(uv.x), 1e-5);

                float near = rainLayer(uv, aspect, _Columns, _Speed, 0.0);
                float far = rainLayer(uv, aspect, _Columns * 1.7, _Speed * 0.6, 91.0) * _FarLayer;
                float mask = saturate(max(near, far));

                fixed4 col = fixed4(IN.color.rgb, IN.color.a * mask);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
