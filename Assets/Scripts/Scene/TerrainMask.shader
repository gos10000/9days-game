Shader "NineDays/TerrainMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaskTex ("Terrain Mask", 2D) = "white" {}
        _WorldRect ("World Rect", Vector) = (-10,-10,20,20)
        _Cutoff ("Mask Cutoff", Range(0,1)) = 0.5
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
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float2 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            float4 _MainTex_ST;
            float4 _WorldRect;
            half _Cutoff;
            half4 _Color;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.worldPos = world.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                float2 uv = (input.worldPos - _WorldRect.xy) / max(_WorldRect.zw, float2(0.0001, 0.0001));
                float2 insideMin = step(float2(0.0, 0.0), uv);
                float2 insideMax = step(uv, float2(1.0, 1.0));
                float inside = insideMin.x * insideMin.y * insideMax.x * insideMax.y;

                float mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, saturate(uv)).r;
                color.a *= step(_Cutoff, mask) * inside;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
