Shader "GTA IV/gta_spec_reflect_decal"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Diffuse", 2D) = "white" {}
        _SpecTex ("Specular", 2D) = "black" {}
        _Shininess ("Shininess", Range(1, 128)) = 32
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-1"
        }

        Pass
        {
            Name "GTA Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Include/GTA_Common.hlsl"
            #include "Include/GTA_Stipple.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_SpecTex); SAMPLER(sampler_SpecTex);
            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
            float _Shininess;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                    UNITY_DOTS_INSTANCED_PROP(float, _Shininess)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
                #define _Shininess UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _Shininess)
            #endif

            GTA_Varyings vert(GTA_Attributes v) { return GTA_VertexSimple(v); }

            half4 frag(GTA_Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 diffuse = half4(tex.rgb * i.color.rgb, tex.a * i.color.a);
                half4 specMap = SAMPLE_TEXTURE2D(_SpecTex, sampler_SpecTex, i.uv);
                float3 N = normalize(i.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 specColor = GTA_Specular(N, i.positionWS, mainLight.direction, mainLight.color, specMap.r, _Shininess, mainLight.shadowAttenuation);

                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 reflDir = reflect(-viewDir, N);
                half3 envColor = GlossyEnvironmentReflection(reflDir, 0.3, 1.0);

                half3 color = GTA_LightingWithShadow(diffuse.rgb, N, i.positionWS);
                color += specColor;
                color += envColor * specMap.r * 0.3;
                color = GTA_ApplyFog(color, i.fogFactor);

                return half4(color, diffuse.a);
            }
            ENDHLSL
        }

    }
}
