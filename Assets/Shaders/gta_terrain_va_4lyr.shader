Shader "GTA IV/gta_terrain_va_4lyr"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _Layer0Tex ("Layer 0", 2D) = "white" {}
        _Layer1Tex ("Layer 1", 2D) = "white" {}
        _Layer2Tex ("Layer 2", 2D) = "white" {}
        _Layer3Tex ("Layer 3", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry-1"
        }

        Pass
        {
            Name "GTA Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Include/GTA_Common.hlsl"
            #include "Include/GTA_Stipple.hlsl"

            TEXTURE2D(_Layer0Tex); SAMPLER(sampler_Layer0Tex);
            TEXTURE2D(_Layer1Tex); SAMPLER(sampler_Layer1Tex);
            TEXTURE2D(_Layer2Tex); SAMPLER(sampler_Layer2Tex);
            TEXTURE2D(_Layer3Tex); SAMPLER(sampler_Layer3Tex);
            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
            float4 _Layer0Tex_TexelSize;
            float4 _Layer1Tex_TexelSize;
            float4 _Layer2Tex_TexelSize;
            float4 _Layer3Tex_TexelSize;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
            #endif

            GTA_Varyings vert(GTA_Attributes v) { return GTA_VertexSimple(v); }

            half4 frag(GTA_Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);
                half3 l0 = SAMPLE_TEXTURE2D(_Layer0Tex, sampler_Layer0Tex, i.uv).rgb;
                half3 l1 = SAMPLE_TEXTURE2D(_Layer1Tex, sampler_Layer1Tex, i.uv).rgb;
                half3 l2 = SAMPLE_TEXTURE2D(_Layer2Tex, sampler_Layer2Tex, i.uv).rgb;
                half3 l3 = SAMPLE_TEXTURE2D(_Layer3Tex, sampler_Layer3Tex, i.uv).rgb;
                half3 albedo = lerp(l0, l1, i.color.r);
                albedo = lerp(albedo, l2, i.color.g);
                albedo = lerp(albedo, l3, i.color.b);
                half3 lit = GTA_LightingWithShadow(albedo, normalize(i.normalWS), i.positionWS);
                return half4(GTA_ApplyFog(lit, i.fogFactor), 1);
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Include/GTA_Common.hlsl"
            #include "Include/GTA_Stipple.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
            float4 _Layer0Tex_TexelSize;
            float4 _Layer1Tex_TexelSize;
            float4 _Layer2Tex_TexelSize;
            float4 _Layer3Tex_TexelSize;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
            #endif
            float3 _LightDirection;
            GTA_Varyings_Shadow vert(GTA_Attributes v) { return GTA_VertexShadow(v, _LightDirection); }
            half4 frag(GTA_Varyings_Shadow i) : SV_Target { GTA_StippleClip(i.positionCS.xy, _StippleAlpha); return 0; }
            ENDHLSL
        }
    }
}
