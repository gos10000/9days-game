Shader "NineDays/SpriteRainbowOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [PerRendererData] _Flip ("Flip", Float) = 1
        _OutlineColor ("Outline Color", Color) = (1,0.5,0.2,1)
        _OutlineWidth ("Outline Width (px)", Range(0,10)) = 2
        _GlowStrength ("Glow Strength", Range(0,4)) = 1.6
        _InnerFade ("Inner Fade", Range(0,1)) = 0.55
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _RendererColor;
            float _Flip;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _GlowStrength;
            float _InnerFade;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                IN.vertex.x *= _Flip;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, IN.texcoord) * IN.color;

                float2 step1 = _MainTex_TexelSize.xy * _OutlineWidth;
                float2 step2 = step1 * 2.0;

                float outer = 0.0;
                outer = max(outer, tex2D(_MainTex, IN.texcoord + float2( step1.x,  0)).a);
                outer = max(outer, tex2D(_MainTex, IN.texcoord + float2(-step1.x,  0)).a);
                outer = max(outer, tex2D(_MainTex, IN.texcoord + float2( 0,  step1.y)).a);
                outer = max(outer, tex2D(_MainTex, IN.texcoord + float2( 0, -step1.y)).a);
                outer = max(outer, tex2D(_MainTex, IN.texcoord + float2( step1.x,  step1.y)).a);
                outer = max(outer, tex2D(_MainTex, IN.texcoord + float2(-step1.x,  step1.y)).a);
                outer = max(outer, tex2D(_MainTex, IN.texcoord + float2( step1.x, -step1.y)).a);
                outer = max(outer, tex2D(_MainTex, IN.texcoord + float2(-step1.x, -step1.y)).a);

                float halo = 0.0;
                halo = max(halo, tex2D(_MainTex, IN.texcoord + float2( step2.x,  0)).a);
                halo = max(halo, tex2D(_MainTex, IN.texcoord + float2(-step2.x,  0)).a);
                halo = max(halo, tex2D(_MainTex, IN.texcoord + float2( 0,  step2.y)).a);
                halo = max(halo, tex2D(_MainTex, IN.texcoord + float2( 0, -step2.y)).a);

                float outline = saturate(outer - sprite.a);
                float glow = saturate(halo - sprite.a) * _InnerFade;

                float intensity = saturate(outline + glow) * _OutlineColor.a * _GlowStrength;

                fixed3 rgb = sprite.rgb + _OutlineColor.rgb * intensity;
                float alpha = saturate(max(sprite.a, intensity));

                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
