Shader "GTA IV/gta_glass_reflect"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Diffuse", 2D) = "white" {}
        _SpecTex ("Reflection Mask", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "GTA Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
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
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half reflMask = SAMPLE_TEXTURE2D(_SpecTex, sampler_SpecTex, i.uv).r;

                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float NdotV = saturate(dot(N, V));
                float fresnel = 0.04 + 0.96 * pow(1.0 - NdotV, 5.0);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;

                half3 ambient = max(SampleSH(N), 0.03);
                ambient *= lerp(0.5, 1.0, shadow);
                half NdotL = dot(N, mainLight.direction) * 0.5 + 0.5;
                half3 direct = mainLight.color * (NdotL * NdotL) * shadow;
                half3 lit = tex.rgb * (ambient + direct);

                float3 reflDir = reflect(-V, N);
                half3 envColor = GlossyEnvironmentReflection(reflDir, 0.0, 1.0);
                envColor *= lerp(0.6, 1.0, shadow);

                half reflAmount = max(fresnel * 0.6, reflMask * 0.5);
                half3 color = lerp(lit, envColor, reflAmount);

                float3 H = normalize(mainLight.direction + V);
                color += pow(saturate(dot(N, H)), 512.0) * 2.0 * mainLight.color * shadow;

                #ifdef _ADDITIONAL_LIGHTS
                    uint count = GetAdditionalLightsCount();
                    for (uint j = 0u; j < count; j++)
                    {
                        Light addLight = GetAdditionalLight(j, i.positionWS);
                        float addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                        float3 addH = normalize(addLight.direction + V);
                        color += pow(saturate(dot(N, addH)), 256.0) * 1.5 * addLight.color * addAtten;
                        color += tex.rgb * saturate(dot(N, addLight.direction) * 0.4 + 0.6) * addLight.color * addAtten * 0.3;
                    }
                #endif

                color = GTA_ApplyFog(color, i.fogFactor);
                float alpha = lerp(tex.a, 1.0, fresnel * 0.7);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
