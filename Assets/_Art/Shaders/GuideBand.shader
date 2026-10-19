// Aim guide bands on the cloth (gameplay redesign G9): unlit, transparent quad strips built by GuideBandRenderer.
// uv.y runs across the band: _BandTex gives the fill (r = 1) and the dark contour (r = 0) so the band reads on
// a white ball and on light cloth. uv.x runs along it in chevron tiles; _MainTex_ST scrolls them towards the
// target. uv2.x = 1 shows chevrons on that quad. Vertex colour = fill colour and fill alpha.
// Drawn after the opaque cloth and balls (depth decides what covers it), lifted a little with a depth offset
// against z-fighting. Where a ball is in front, the band still shows, faded, so the path never disappears.
Shader "Billiards/GuideBand"
{
    Properties
    {
        _MainTex ("Chevrons", 2D) = "black" {}
        _BandTex ("Band profile", 2D) = "white" {}
        _ContourColor ("Contour", Color) = (0, 0, 0, 0.55)
        _ChevronAlpha ("Chevron alpha", Range(0, 1)) = 0.85
        _OccludedAlpha ("Occluded alpha", Range(0, 1)) = 0.35
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
        float2 uv2 : TEXCOORD1;
        half4 color : COLOR;
    };

    struct Varyings
    {
        float4 positionHCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 band : TEXCOORD1;   // x: across, y: chevrons on, z: unused
        half4 color : COLOR;
    };

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    TEXTURE2D(_BandTex);
    SAMPLER(sampler_BandTex);

    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4 _BandTex_ST;
        half4 _ContourColor;
        half _ChevronAlpha;
        half _OccludedAlpha;
    CBUFFER_END

    Varyings vert(Attributes input)
    {
        Varyings output;
        output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = TRANSFORM_TEX(input.uv, _MainTex);
        output.band = float3(input.uv.y, input.uv2.x, 0);
        output.color = input.color;
        return output;
    }

    // Straight-alpha "over".
    half4 Over(half4 under, half3 color, half alpha)
    {
        half outAlpha = alpha + under.a * (1 - alpha);
        half3 outColor = (color * alpha + under.rgb * under.a * (1 - alpha)) / max(outAlpha, 1e-4);
        return half4(outColor, outAlpha);
    }

    half4 Band(Varyings input)
    {
        half4 profile = SAMPLE_TEXTURE2D(_BandTex, sampler_BandTex, float2(0.5, input.band.x));
        half4 color;
        color.rgb = lerp(_ContourColor.rgb, input.color.rgb, profile.r);
        color.a = profile.a * lerp(_ContourColor.a, input.color.a, profile.r);
        half4 chevron = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(input.uv.x, input.band.x));
        return Over(color, chevron.rgb, chevron.a * _ChevronAlpha * input.band.y * profile.a);
    }

    half4 fragVisible(Varyings input) : SV_Target
    {
        return Band(input);
    }

    half4 fragOccluded(Varyings input) : SV_Target
    {
        half4 color = Band(input);
        color.a *= _OccludedAlpha;
        return color;
    }
    ENDHLSL

    SubShader
    {
        // After the cloth (Geometry), in the opaque range so it is done before the transparent ghost ball.
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+450" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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
