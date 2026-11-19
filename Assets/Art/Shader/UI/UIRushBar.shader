// UI/RushBar
// 愤怒条的填充：平时和普通 UI 图一样；快满时（_Rush 从 0 升到 1）条上出现往终点方向跑的斜纹，
// 隔一会儿有一道高光从左扫到右，整条还会一明一暗，提醒玩家「马上就满了，冲」。
// _Rush 由 AngerMeter 每帧写进来，材质上不用手调。斜纹和高光按画布坐标算，条变长时纹路不会被拉伸。
// 另外还有中毒的样子（_Poison，也由 AngerMeter 写）：有顾客的餐具中毒期间整条发紫、冒毒泡，见 frag 里「中毒」那一段。
Shader "UI/RushBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Rush ("Rush (0 = off, 1 = almost full)", Range(0, 1)) = 0

        _StripeColor ("Stripe Colour (alpha = strength)", Color) = (1, 0.95, 0.75, 0.45)
        _StripeWidth ("Stripe Spacing (canvas px)", Float) = 26
        _StripeSpeed ("Stripe Speed (canvas px / sec)", Float) = 70
        _StripeSlant ("Stripe Slant", Range(-2, 2)) = 0.7

        _ShineColor ("Shine Colour (alpha = strength)", Color) = (1, 1, 1, 0.75)
        _ShineWidth ("Shine Width (canvas px)", Float) = 34
        _ShineSpacing ("Shine Spacing (canvas px)", Float) = 620
        _ShineSpeed ("Shine Speed (canvas px / sec)", Float) = 520

        _Throb ("Brightness Throb", Range(0, 0.6)) = 0.18
        _ThrobSpeed ("Throb Speed (per sec)", Float) = 3

        [Header(Poisoned)]
        _Poison ("Poison (0 = off, 1 = a customer is poisoned)", Range(0, 1)) = 0
        _PoisonColor ("Poison Colour (alpha = how much of the bar turns)", Color) = (0.56, 0.30, 0.86, 0.85)
        _PoisonDarkColor ("Poison Sludge Colour (alpha = strength)", Color) = (0.33, 0.13, 0.55, 0.55)
        _PoisonBubbleColor ("Poison Bubble Colour (alpha = strength)", Color) = (0.74, 0.96, 0.40, 0.95)
        _PoisonBubbleSize ("Bubble Cell Size (canvas px)", Float) = 20
        _PoisonBubbleSpeed ("Bubble Rise Speed (canvas px / sec)", Float) = 26
        _PoisonWaveSize ("Sludge Wave Length (canvas px)", Float) = 90
        _PoisonWaveSpeed ("Sludge Wave Speed (canvas px / sec)", Float) = 60
        _PoisonThrob ("Poison Brightness Throb", Range(0, 0.6)) = 0.16

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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Rush;
            fixed4 _StripeColor, _ShineColor;
            float _StripeWidth, _StripeSpeed, _StripeSlant;
            float _ShineWidth, _ShineSpacing, _ShineSpeed;
            float _Throb, _ThrobSpeed;
            float _Poison;
            fixed4 _PoisonColor, _PoisonDarkColor, _PoisonBubbleColor;
            float _PoisonBubbleSize, _PoisonBubbleSpeed, _PoisonWaveSize, _PoisonWaveSpeed, _PoisonThrob;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 col = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // 画布坐标：斜着的一排亮带往右跑，越接近满跑得越快。
                float2 px = IN.worldPosition.xy;
                float along = px.x - px.y * _StripeSlant;
                float band = frac((along - _Time.y * _StripeSpeed * (1.0 + _Rush)) / max(_StripeWidth, 1.0));
                float stripe = smoothstep(0.50, 0.56, band) * (1.0 - smoothstep(0.94, 1.0, band));
                col.rgb = lerp(col.rgb, _StripeColor.rgb, stripe * _StripeColor.a * _Rush);

                // 一道窄的高光，隔一段距离一道，所以看起来是隔一会儿扫过一次。
                float shineAt = frac((along - _Time.y * _ShineSpeed) / max(_ShineSpacing, 1.0)) * _ShineSpacing;
                float shine = 1.0 - smoothstep(0.0, max(_ShineWidth, 1.0), abs(shineAt - _ShineWidth));
                col.rgb = lerp(col.rgb, _ShineColor.rgb, shine * _ShineColor.a * _Rush);

                // 整条一明一暗。
                col.rgb *= 1.0 + _Throb * _Rush * (0.5 + 0.5 * sin(_Time.y * _ThrobSpeed * 6.2831853 * (1.0 + _Rush)));

                // 中毒：有顾客的餐具中了毒（愤怒值在自己往上涨）。整条变成毒紫色，里面有一道道深色的毒液往终点方向涌，
                // 黄绿色的毒泡往上冒，整条慢慢地一明一暗。_Poison 由 AngerMeter 写进来，毒过了退回 0。
                if (_Poison > 0.001)
                {
                    fixed3 poisoned = lerp(col.rgb, _PoisonColor.rgb, _PoisonColor.a);
                    // 毒液：两道不同长短的波叠在一起，边缘是弯的。
                    float wave = sin((px.x - _Time.y * _PoisonWaveSpeed) / max(_PoisonWaveSize, 1.0) * 6.2831853 + px.y * 0.09)
                               + 0.6 * sin((px.x - _Time.y * _PoisonWaveSpeed * 0.55) / max(_PoisonWaveSize, 1.0) * 14.1 - px.y * 0.05);
                    poisoned = lerp(poisoned, _PoisonDarkColor.rgb, smoothstep(0.25, 0.7, wave) * _PoisonDarkColor.a);
                    // 毒泡：按格子摆，每格一个、大小和位置随机，四成的格子是空的；整体往上升，升的过程中变大再破掉。
                    float cell = max(_PoisonBubbleSize, 1.0);
                    float2 grid = float2(px.x, px.y - _Time.y * _PoisonBubbleSpeed) / cell;
                    float2 id = floor(grid);
                    float seed = hash21(id);
                    float2 centre = 0.5 + (float2(hash21(id + 17.0), hash21(id + 41.0)) - 0.5) * 0.4;
                    float radius = lerp(0.22, 0.40, hash21(id + 73.0)) * (0.75 + 0.25 * sin(_Time.y * 5.0 + seed * 40.0));
                    float away = length(frac(grid) - centre);
                    float bubble = (1.0 - smoothstep(radius - 0.07, radius, away)) * step(0.4, seed);
                    float hollow = smoothstep(radius * 0.35, radius * 0.8, away);      // 中间淡、边上实，看着像泡
                    poisoned = lerp(poisoned, _PoisonBubbleColor.rgb, bubble * lerp(0.45, 1.0, hollow) * _PoisonBubbleColor.a);
                    poisoned *= 1.0 + _PoisonThrob * sin(_Time.y * 7.0);
                    col.rgb = lerp(col.rgb, poisoned, _Poison);
                }

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
