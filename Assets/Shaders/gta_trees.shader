Shader "GTA IV/gta_trees"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Diffuse", 2D) = "white" {}
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
            Cull Off
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
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

            GTA_Varyings vert(GTA_Attributes v)
            {
                // Engine wind sway (gta_treesVS0): Rodrigues rotation around
                // cross(position, up). Amplitude scales with distance from
                // model origin (branch tips sway more than trunk).
                float3 pos = v.positionOS.xyz;
                float dist = length(pos);

                // Rotation axis = cross(pos, up) — perpendicular to branch direction
                float3 axis = normalize(cross(pos, float3(0, 0, 1)) + 1e-5);

                // Amplitude: dist * 0.005 + 0.01 (engine c4.x, c4.y)
                float amplitude = dist * 0.005 + 0.01 * 1.1;

                // Phase: each vertex oscillates at different phase based on distance
                // Engine: dist * windSpeed * (1/2pi * 0.2) + 0.5, then *2pi - pi
                float phase = frac(dist * _Time.y * 0.0318 + 0.5) * 6.2832 - 3.1416;
                float sinP, cosP;
                sincos(phase, sinP, cosP);

                // Rodrigues rotation: v' = v*cos + (axis x v)*sin + axis*(axis.v)*(1-cos)
                float3 axisXpos = cross(axis * amplitude, pos);
                float3 displaced = pos + axis * amplitude * (cosP - 1.0)
                                 + normalize(axisXpos + 1e-5) * amplitude * 0.5 * sinP;

                v.positionOS.xyz = displaced;

                GTA_Varyings o = GTA_VertexSimple(v);
                o.uv = v.uv;
                return o;
            }

            half4 frag(GTA_Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a - 0.5);

                half3 albedo = tex.rgb * i.color.rgb;
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
            Cull Off

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
            float _Cutoff;
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
                GTA_Varyings_Shadow o = GTA_VertexShadow(v, _LightDirection);
                o.uv = v.uv;
                return o;
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
