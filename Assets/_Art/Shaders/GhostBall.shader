// Ghost ball (gameplay redesign G9): where the cue ball will be at contact. Unlit, transparent sphere:
// a faint body plus a brighter fresnel rim, so in the first-person view it reads as a ball, not a ring.
Shader "Billiards/GhostBall"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BodyAlpha ("Body alpha", Range(0, 1)) = 0.28
        _RimAlpha ("Rim alpha", Range(0, 1)) = 0.7
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _BodyAlpha;
                half _RimAlpha;
                half _RimPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half rim = pow(1 - facing, _RimPower);
                return half4(_Color.rgb, lerp(_BodyAlpha, _RimAlpha, rim) * _Color.a);
            }
            ENDHLSL
        }
    }
}
