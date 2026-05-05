Shader "GTA IV/gta_default_skinned"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        [HideInInspector] _ComputeMeshIndex ("Compute Mesh Index", Float) = 0
        _MainTex ("Diffuse", 2D) = "white" {}
        _SpecTex ("Specular", 2D) = "black" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "GTA Forward Skinned"
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #define _ALPHATEST_ON 1
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Include/GTA_Common.hlsl"
            #include "Include/GTA_Stipple.hlsl"
            #include "Include/GTA_Skinning.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_SpecTex); SAMPLER(sampler_SpecTex);
            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
            float _ComputeMeshIndex;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                    UNITY_DOTS_INSTANCED_PROP(float, _ComputeMeshIndex)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
            #endif

            struct SkinnedAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                uint   vertexID   : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            GTA_Varyings vert(SkinnedAttributes v)
            {
                GTA_Varyings o = (GTA_Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 pos = v.positionOS.xyz;
                float3 norm = v.normalOS;
                float3 tan = v.tangentOS.xyz;

                #ifdef UNITY_DOTS_INSTANCING_ENABLED
                    GTA_ApplyComputeDeformation(v.vertexID, asuint(UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _ComputeMeshIndex)), pos, norm, tan);
                #endif

                VertexPositionInputs vpi = GetVertexPositionInputs(pos);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.normalWS   = normalize(TransformObjectToWorldNormal(norm));
                o.uv         = v.uv;
                o.color      = v.color;
                o.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
                return o;
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
            #include "Include/GTA_Skinning.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
            float _ComputeMeshIndex;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                    UNITY_DOTS_INSTANCED_PROP(float, _ComputeMeshIndex)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
            #endif

            float3 _LightDirection;

            struct SkinnedAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                uint   vertexID   : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            GTA_Varyings_Shadow vert(SkinnedAttributes v)
            {
                GTA_Varyings_Shadow o = (GTA_Varyings_Shadow)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 pos = v.positionOS.xyz;
                float3 norm = v.normalOS;
                float3 tan = 0;

                #ifdef UNITY_DOTS_INSTANCING_ENABLED
                    GTA_ApplyComputeDeformation(v.vertexID, asuint(UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _ComputeMeshIndex)), pos, norm, tan);
                #endif

                float3 posWS = TransformObjectToWorld(pos);
                float3 normWS = TransformObjectToWorldNormal(norm);
                posWS = ApplyShadowBias(posWS, normWS, _LightDirection);
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = v.uv;

                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return o;
            }

            half4 frag(GTA_Varyings_Shadow i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }
}
