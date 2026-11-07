Shader "NineDays/Effect/GravityField"
{
    Properties
    {
        [Header(Frame)]
        _Edge          ("边界位置（1 = 碰撞边界）", Range(0.50, 1.00)) = 0.97
        _EdgeW         ("边框宽度", Range(0.004, 0.120)) = 0.030
        _EdgeColor     ("边框亮色", Color) = (0.25, 1, 0.55, 1)
        _EdgeIntensity ("边框亮度", Range(0, 4)) = 1.6
        _Corner        ("圆角（0 = 纯方角）", Range(0, 0.30)) = 0.06

        [Header(Interior)]
        _CoreColor     ("向内渐隐色", Color) = (0.06, 0.32, 0.14, 1)
        _Falloff       ("向内衰减", Range(0.5, 8)) = 3.0
        _InteriorTint  ("场内底色", Range(0, 0.30)) = 0.06

        [Header(Ticks)]
        _Segments      ("每边刻度数", Range(0, 60)) = 12
        _SegmentDepth  ("刻度凹陷（0 = 实线）", Range(0, 1)) = 0.60

        [Header(Pulse)]
        _PulseSpeed    ("呼吸速度", Range(0, 6)) = 1.4
        _PulseDepth    ("呼吸幅度", Range(0, 0.60)) = 0.12

        [Header(Speedlines)]
        _LineCount     ("速度线数量", Range(2, 60)) = 14
        _LineWidth     ("速度线粗细", Range(0.02, 0.50)) = 0.12
        _LineTiling    ("竖向密度", Range(0.5, 8)) = 2.5
        _LineSpeed     ("流速", Range(0, 6)) = 1.2
        _LineIntensity ("速度线亮度", Range(0, 2)) = 0.55
        _LineColor     ("速度线颜色", Color) = (0.55, 1, 0.70, 1)
        _Upward        ("方向（0 = 向下, 1 = 向上）", Range(0, 1)) = 0

        _Alpha         ("整体透明度", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Edge;
                float _EdgeW;
                float4 _EdgeColor;
                float _EdgeIntensity;
                float _Corner;
                float4 _CoreColor;
                float _Falloff;
                float _InteriorTint;
                float _Segments;
                float _SegmentDepth;
                float _PulseSpeed;
                float _PulseDepth;
                float _LineCount;
                float _LineWidth;
                float _LineTiling;
                float _LineSpeed;
                float _LineIntensity;
                float4 _LineColor;
                float _Upward;
                float _Alpha;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 q = i.uv;
                float2 d = abs(q - 0.5) * 2.0;          // 0 在中心，1 在四边中点

                // 【1】方框距离（可圆角）
                //     _Corner = 0 时退化成切比雪夫距离 max(d.x, d.y)，等值线就是正方形
                //     绝对不能用 d.x + d.y，那是菱形
                float2 dd = d - (1.0 - _Corner);
                float  sd = length(max(dd, 0.0)) + min(max(dd.x, dd.y), 0.0) - _Corner;
                float  box = 1.0 + sd;                  // 0 = 中心，1 = 碰撞边界

                // 【2】边框：内沿升、外沿落，形成一条带
                float frame = smoothstep(_Edge - _EdgeW, _Edge, box)
                            * (1.0 - smoothstep(_Edge, _Edge + _EdgeW * 0.5, box));

                // 【3】沿边刻度：判断离哪条边近，用沿边坐标分格
                float along = (d.x > d.y) ? q.y : q.x;
                float seg   = frac(along * _Segments);
                float tick  = 1.0 - smoothstep(0.30, 0.50, abs(seg - 0.5));
                frame *= lerp(1.0, tick, saturate(_SegmentDepth));

                // 【4】向内渐隐：贴边最亮，往里迅速衰减
                float glow = pow(saturate(box / max(0.001, _Edge)), _Falloff);
                glow = lerp(_InteriorTint, 1.0, glow);

                // 【5】呼吸
                float pulse = 1.0 - _PulseDepth + _PulseDepth * sin(_Time.y * _PulseSpeed);

                // 【6】竖向速度线：每条线独立相位，减号决定方向
                float dir  = step(0.5, _Upward) * 2.0 - 1.0;
                float id   = floor(q.x * _LineCount);
                float h    = Hash21(float2(id, 3.7));
                float cx   = frac(q.x * _LineCount);
                float core = 1.0 - smoothstep(0.0, max(0.001, _LineWidth), abs(cx - 0.5));
                float flow = frac(q.y * _LineTiling - _Time.y * _LineSpeed * dir + h);
                // 拖尾必须指向运动的“后方”：向下时头在底部（亮度随 y 递减），
                // 向上时头在顶部（亮度随 y 递增）。写成固定的一边就会有一条方向是反的
                float head = lerp(1.0 - flow, flow, step(0.0, dir));
                float tail = pow(head, 3.0);
                float lines = core * tail * (1.0 - smoothstep(0.75, 1.0, box)) * _LineIntensity;

                // 【7】合成
                float3 emit = _EdgeColor.rgb * (frame * _EdgeIntensity * pulse)
                            + _CoreColor.rgb * (glow * 0.55)
                            + _LineColor.rgb * lines;

                float cov = saturate(frame * 0.95 + glow * 0.10 + lines * 0.50);

                return half4(emit * _Alpha, cov * _Alpha);
            }
            ENDHLSL
        }
    }
}
