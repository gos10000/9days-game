Shader "NineDays/Effect/BlackHole"
{
    Properties
    {
        _CoreRadius    ("核心半径", Range(0.05, 0.90)) = 0.30

        _RingRadius    ("光子环半径", Range(0.05, 1.20)) = 0.34
        _RingWidth     ("光子环宽度（约 6/像素格数）", Range(0.003, 0.30)) = 0.027
        _RingIntensity ("光子环亮度", Range(0, 6)) = 1.8
        _RingColor     ("光子环颜色", Color) = (1, 0.88, 0.62, 1)

        _DiskInner     ("吸积盘内沿", Range(0, 1)) = 0.33
        _DiskOuter     ("吸积盘外沿", Range(0, 1.5)) = 0.95
        _DiskIntensity ("吸积盘亮度", Range(0, 3)) = 1.1
        _SpinSpeed     ("角速度系数", Range(0, 6)) = 1.6
        _NoiseScale    ("噪声密度", Range(1, 30)) = 9
        _InnerColor    ("内圈颜色", Color) = (1, 0.96, 0.88, 1)
        _MidColor      ("中段颜色", Color) = (0.36, 0.78, 1, 1)
        _OuterColor    ("外圈颜色", Color) = (0.42, 0.18, 0.85, 1)

        _Alpha         ("整体不透明度", Range(0, 1)) = 1

        [Header(Pixelate)]
        _PixelGrid     ("像素格数（面片宽度上的格子数）", Range(32, 600)) = 224
        _ColorSteps    ("色阶（值越小越像像素画）", Range(2, 8)) = 4
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
                float _CoreRadius;
                float _RingRadius;
                float _RingWidth;
                float _RingIntensity;
                float4 _RingColor;
                float _DiskInner;
                float _DiskOuter;
                float _DiskIntensity;
                float _SpinSpeed;
                float _NoiseScale;
                float4 _InnerColor;
                float4 _MidColor;
                float4 _OuterColor;
                float _Alpha;
                float _PixelGrid;
                float _ColorSteps;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                // 只叠 2 层：第 3、4 层的特征尺寸小于一个像素格，会在格间跳变导致闪烁
                // amp 取 0.6667 让两层叠加上限为 1，量化后不会整片变暗
                float v = 0.0;
                float amp = 0.6667;
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    v += amp * ValueNoise(p);
                    p = p * 2.03 + 11.7;
                    amp *= 0.5;
                }
                return v;
            }

            // 2x2 有序抖动矩阵的值（row = a, col = b）
            float Bayer2(float a, float b)
            {
                return a * 3.0 + b * 2.0 - 4.0 * a * b;
            }

            // 4x4 Bayer，用递归构造的公式算，避免数组动态索引在某些平台上编译失败
            float Bayer4x4(float2 cell)
            {
                float2 f  = floor(fmod(cell, 4.0));
                float2 hi = floor(f * 0.5);
                float2 lo = f - hi * 2.0;
                return (4.0 * Bayer2(hi.y, hi.x) + Bayer2(lo.y, lo.x)) / 16.0;
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
                // 【1】先量化 uv 到格子中心，再算极坐标
                //     顺序不能反：先算 r 再量化颜色的话，边缘依然是平滑的
                float2 quv = (floor(i.uv * _PixelGrid) + 0.5) / _PixelGrid;
                float2 p   = quv - 0.5;
                float  r   = length(p) * 2.0;

                // 【2】事件视界：硬边（原来的 smoothstep 柔化在像素风里没有意义）
                float core = step(r, _CoreRadius);

                // 【3】吸积盘
                //     外缘淡出换成 4x4 抖动网点，内沿直接硬切
                float outerFade = saturate(1.0 - smoothstep(_DiskOuter - 0.12, _DiskOuter, r));
                // 完全淡出的区域必须硬性归零：step(a,b) 在 a==b 时返回 1，
                // 而 Bayer 每个 4x4 块里正好有一个 0，否则整片区域每 16 像素漏一个点
                float dith      = step(Bayer4x4(i.uv * _PixelGrid), outerFade) * step(0.0001, outerFade);
                float mask      = step(_DiskInner, r) * dith;

                float spin = min(_SpinSpeed / pow(max(r, 0.16), 1.5), 12.0);
                float sw   = fmod(_Time.y * spin, 6.28318530718);

                float s, c;
                sincos(sw, s, c);
                float2 rp = float2(c * p.x - s * p.y, s * p.x + c * p.y);

                float n    = Fbm(rp * _NoiseScale);
                float fila = pow(saturate(n), 2.0);
                fila       = floor(fila * _ColorSteps + 0.5) / _ColorSteps;

                float disk = mask * (0.25 + 0.95 * fila) * _DiskIntensity;

                float  t   = saturate((r - _DiskInner) / max(0.001, _DiskOuter - _DiskInner));
                float3 col = lerp(_InnerColor.rgb, _MidColor.rgb, saturate(t * 2.0));
                col        = lerp(col, _OuterColor.rgb, saturate((t - 0.5) * 2.0));
                disk      *= 1.0 / (1.0 + 8.0 * t * t);

                // 【4】光子环：高斯衰减保留形状，但量化成有限台阶
                float ringT = (r - _RingRadius) / max(0.001, _RingWidth);
                float ring  = floor(exp(-ringT * ringT) * _ColorSteps + 0.5) / _ColorSteps;

                // 【5】合成 + 整体色阶压缩
                float3 emit = col * disk + _RingColor.rgb * ring * _RingIntensity;
                float  cov  = saturate(max(disk, ring * _RingIntensity));

                emit = floor(emit * _ColorSteps + 0.5) / _ColorSteps;
                cov  = floor(cov  * _ColorSteps + 0.5) / _ColorSteps;

                // 【6】核心吃掉一切
                emit *= (1.0 - core);
                cov   = cov * (1.0 - core) + core;

                return half4(emit * _Alpha, cov * _Alpha);
            }
            ENDHLSL
        }
    }
}
