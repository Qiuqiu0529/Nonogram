// UI/ScrollingTile
// 让一张四边无缝的背景图块在 UI 上平铺并循环滚动（菜单背景用）。
// 用法：
//   1. 从 Figma 导出 Asset/BG Tile/* 的 PNG（1024×1024），导入 Unity。
//   2. 贴图设置里把 Wrap Mode 设为 Repeat。
//   3. 在 Canvas 最底层放一个铺满屏幕的 RawImage，Texture 选这张图块，Material 选用本 Shader 的材质。
//   4. Tile Size 是一块图块在屏幕上占多少画布像素；Scroll Speed 是每秒滚动多少块。
// 本文件没有在 Unity 里实际编译过，第一次导入如果报错请把报错发给我。
Shader "UI/ScrollingTile"
{
    Properties
    {
        [PerRendererData] _MainTex ("Tile Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TileSize ("Tile Size (canvas px)", Float) = 512
        _ScrollSpeed ("Scroll Speed (tiles/sec, xy)", Vector) = (0.03, 0.02, 0, 0)
        _Angle ("Pattern Angle (deg)", Range(-45, 45)) = 0
        _Grain ("Paper Grain", Range(0, 1)) = 0.3
        [Toggle] _SeamlessStripes ("Seamless Diagonal Stripes", Float) = 0
        _StripeLight ("Stripe Light", Color) = (0.980, 0.929, 0.867, 1)
        _StripeDark ("Stripe Dark", Color) = (0.980, 0.871, 0.796, 1)

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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float4 worldPosition : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _StripeLight, _StripeDark;
            float _SeamlessStripes;
            float _TileSize, _Angle, _Grain;
            float4 _ScrollSpeed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                float2 q = frac(p * float2(123.34, 456.21));
                q += dot(q, q + 45.32);
                return frac(q.x * q.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 用画布坐标平铺，图块大小不随 RawImage 的尺寸变化
                float2 px = IN.worldPosition.xy;
                float a = radians(_Angle);
                float2 r = float2(px.x * cos(a) - px.y * sin(a), px.x * sin(a) + px.y * cos(a));
                float2 uv = r / max(_TileSize, 1.0) + _Time.y * _ScrollSpeed.xy;
                fixed4 col = tex2D(_MainTex, uv) * IN.color;
                if (_SeamlessStripes > 0.5)
                {
                    // A continuous phase avoids seams regardless of the panel size.
                    float phase = dot(uv, float2(3.58, 4.42));
                    float wave = cos(phase * 6.2831853) - 0.18;
                    float edge = max(fwidth(wave) * 0.5, 0.001);
                    col = lerp(_StripeLight, _StripeDark, smoothstep(-edge, edge, wave)) * IN.color;
                }

                // 颗粒固定在屏幕上，不跟着背景滚动，像是印在纸上的图案在移动
                float speck = hash21(floor(px / 1.6));
                col.rgb = saturate(col.rgb * (1.0 - _Grain * 0.3 * (speck - 0.5)));
                return col;
            }
            ENDCG
        }
    }
}
