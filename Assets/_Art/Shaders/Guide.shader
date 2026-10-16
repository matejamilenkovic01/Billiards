// Aim guide lines and rings: unlit, transparent, vertex-coloured, pulled towards the camera a little so
// they never z-fight with the cloth. Two passes: where a ball or cushion is in front of the line it is still
// drawn, faded (x-ray), so the hit ball's path never disappears; elsewhere it is drawn normally.
Shader "Billiards/Guide"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _OccludedAlpha ("Occluded Alpha", Range(0, 1)) = 0.4
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
        half4 color : COLOR;
    };

    struct Varyings
    {
        float4 positionHCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        half4 color : COLOR;
    };

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        half4 _Color;
        half _OccludedAlpha;
    CBUFFER_END

    Varyings vert(Attributes input)
    {
        Varyings output;
        output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = TRANSFORM_TEX(input.uv, _MainTex);
        output.color = input.color;
        return output;
    }

    half4 fragVisible(Varyings input) : SV_Target
    {
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color * input.color;
    }

    // Behind something: flat (no dots), faded.
    half4 fragOccluded(Varyings input) : SV_Target
    {
        half4 color = _Color * input.color;
        color.a *= _OccludedAlpha;
        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "Occluded"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Greater
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragOccluded
            ENDHLSL
        }

        Pass
        {
            Name "Visible"
            Tags { "LightMode" = "UniversalForward" }
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragVisible
            ENDHLSL
        }
    }
}
