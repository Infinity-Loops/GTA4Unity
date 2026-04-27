Shader "GTA IV/gta_grass"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Diffuse", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
            float _Cutoff;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                    UNITY_DOTS_INSTANCED_PROP(float, _Cutoff)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
                #define _Cutoff UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _Cutoff)
            #endif

            GTA_Varyings vert(GTA_Attributes v)
            {
                float3 pos = v.positionOS.xyz;
                float dist = length(pos);
                float3 axis = normalize(cross(pos, float3(0, 0, 1)) + 1e-5);
                float amplitude = dist * 0.005 + 0.01;
                float phase = frac(dist * _Time.y * 0.0318 + 0.5) * 6.2832 - 3.1416;
                float sinP, cosP;
                sincos(phase, sinP, cosP);
                float3 axisXpos = cross(axis * amplitude, pos);
                v.positionOS.xyz = pos + axis * amplitude * (cosP - 1.0)
                                 + normalize(axisXpos + 1e-5) * amplitude * 0.5 * sinP;
                GTA_Varyings o = GTA_VertexSimple(v);
                o.uv = v.uv;
                return o;
            }

            half4 frag(GTA_Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 diffuse = half4(tex.rgb * i.color.rgb, tex.a);
                clip(diffuse.a - _Cutoff);
                half3 lit = GTA_LightingWithShadow(diffuse.rgb, normalize(i.normalWS), i.positionWS);
                lit = GTA_ApplyFog(lit, i.fogFactor);
                return half4(lit, 1);
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
                    UNITY_DOTS_INSTANCED_PROP(float, _Cutoff)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
                #define _Cutoff UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _Cutoff)
            #endif

            float3 _LightDirection;
            GTA_Varyings_Shadow vert(GTA_Attributes v) { return GTA_VertexShadow(v, _LightDirection); }
            half4 frag(GTA_Varyings_Shadow i) : SV_Target
            {
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
