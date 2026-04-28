Shader "GTA IV/gta_glass_emissivenight"
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
                float fresnel = 0.04 + 0.96 * pow(1.0 - NdotV, 5.0);

                // Emissive base — self-lit, no shadow dependency
                half3 color = tex.rgb;

                // Fresnel reflection on top of emissive
                float3 reflDir = reflect(-V, N);
                half3 envColor = GlossyEnvironmentReflection(reflDir, 0.0, 1.0);
                color = lerp(color, color + envColor, fresnel * 0.3);

                // Subtle gloss specular from sun
                Light mainLight = GetMainLight();
                float3 H = normalize(mainLight.direction + V);
                color += pow(saturate(dot(N, H)), 512.0) * 0.8 * mainLight.color;

                color = GTA_ApplyFog(color, i.fogFactor);
                float alpha = lerp(tex.a, 1.0, fresnel * 0.5);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
