Shader "GTA IV/gta_glass"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Diffuse", 2D) = "white" {}
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

                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float NdotV = saturate(dot(N, V));

                // Schlick fresnel (F0 = 0.04, glass IOR ~1.5)
                float fresnel = 0.04 + 0.96 * pow(1.0 - NdotV, 5.0);

                // Shadow-aware split lighting
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;

                // Ambient: SH sky, persists in shadow
                half3 ambient = max(SampleSH(N), 0.03);
                ambient *= lerp(0.5, 1.0, shadow);

                // Direct: squared wrap, killed by shadow
                half NdotL = dot(N, mainLight.direction) * 0.5 + 0.5;
                half3 direct = mainLight.color * (NdotL * NdotL) * shadow;

                half3 glassTint = tex.rgb;
                half3 lit = glassTint * (ambient + direct);

                // Environment reflection
                float3 reflDir = reflect(-V, N);
                half3 envColor = GlossyEnvironmentReflection(reflDir, 0.0, 1.0);
                envColor *= lerp(0.6, 1.0, shadow);

                // Blend glass → reflection via fresnel
                half3 color = lerp(lit, envColor, fresnel * 0.6);

                // Sun specular — sharp and bright for smooth glass
                float3 H = normalize(mainLight.direction + V);
                float NdotH = saturate(dot(N, H));
                color += pow(NdotH, 512.0) * 2.0 * mainLight.color * shadow;

                // Additional lights: specular sparkle + subtle diffuse
                #ifdef _ADDITIONAL_LIGHTS
                    uint count = GetAdditionalLightsCount();
                    for (uint j = 0u; j < count; j++)
                    {
                        Light addLight = GetAdditionalLight(j, i.positionWS);
                        float addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                        float3 addH = normalize(addLight.direction + V);
                        color += pow(saturate(dot(N, addH)), 256.0) * 1.5 * addLight.color * addAtten;
                        color += glassTint * saturate(dot(N, addLight.direction) * 0.4 + 0.6) * addLight.color * addAtten * 0.3;
                    }
                #endif

                color = GTA_ApplyFog(color, i.fogFactor);

                // Alpha: fresnel-driven (transparent head-on, opaque at grazing)
                float alpha = lerp(tex.a, 1.0, fresnel * 0.7);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
