Shader "Custom/Tactics/ForegroundSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,0.94,0.52,0.95)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.28
        _OutlineThickness ("Outline Thickness", Range(0.5, 4)) = 1.5
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
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #include "UnitySprites.cginc"

            fixed4 _OutlineColor;
            float _FillAlpha;
            float _OutlineThickness;
            float4 _MainTex_TexelSize;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseSample = SampleSpriteTexture(IN.texcoord) * IN.color;
                float baseAlpha = baseSample.a;
                float2 texelOffset = _MainTex_TexelSize.xy * _OutlineThickness;

                float surroundingAlpha = 0.0;
                surroundingAlpha = max(surroundingAlpha, SampleSpriteTexture(IN.texcoord + float2(texelOffset.x, 0.0)).a);
                surroundingAlpha = max(surroundingAlpha, SampleSpriteTexture(IN.texcoord + float2(-texelOffset.x, 0.0)).a);
                surroundingAlpha = max(surroundingAlpha, SampleSpriteTexture(IN.texcoord + float2(0.0, texelOffset.y)).a);
                surroundingAlpha = max(surroundingAlpha, SampleSpriteTexture(IN.texcoord + float2(0.0, -texelOffset.y)).a);
                surroundingAlpha = max(surroundingAlpha, SampleSpriteTexture(IN.texcoord + texelOffset).a);
                surroundingAlpha = max(surroundingAlpha, SampleSpriteTexture(IN.texcoord - texelOffset).a);
                surroundingAlpha = max(surroundingAlpha, SampleSpriteTexture(IN.texcoord + float2(texelOffset.x, -texelOffset.y)).a);
                surroundingAlpha = max(surroundingAlpha, SampleSpriteTexture(IN.texcoord + float2(-texelOffset.x, texelOffset.y)).a);

                float outlineAlpha = saturate(surroundingAlpha - baseAlpha) * _OutlineColor.a;
                float fillAlpha = baseAlpha * _FillAlpha * _OutlineColor.a;
                float finalAlpha = saturate(outlineAlpha + fillAlpha);

                return fixed4(_OutlineColor.rgb, finalAlpha);
            }
            ENDCG
        }
    }
}
