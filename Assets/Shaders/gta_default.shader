Shader "GTA IV/gta_default"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Diffuse", 2D) = "white" {}
        _SpecTex ("Specular", 2D) = "black" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "GTA Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #define _ALPHATEST_ON 1
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Include/GTA_Common.hlsl"
            #include "Include/GTA_Stipple.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_SpecTex); SAMPLER(sampler_SpecTex);
            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
            #endif

            GTA_Varyings vert(GTA_Attributes v)
            {
                return GTA_VertexSimple(v);
            }

            half4 frag(GTA_Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a - 0.5);

                half3 albedo = GTA_DiffuseColor(tex, i.color);
                half3 lit = GTA_LightingWithShadow(albedo, normalize(i.normalWS), i.positionWS);
                lit = GTA_ApplyFog(lit, i.fogFactor);

                return half4(lit, tex.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #define _ALPHATEST_ON 1
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Include/GTA_Common.hlsl"
            #include "Include/GTA_Stipple.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
            #endif

            float3 _LightDirection;

            GTA_Varyings_Shadow vert(GTA_Attributes v)
            {
                return GTA_VertexShadow(v, _LightDirection);
            }

            half4 frag(GTA_Varyings_Shadow i) : SV_Target
            {
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }
}
