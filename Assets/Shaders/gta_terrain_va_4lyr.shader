Shader "gta_terrain_va_4lyr"
{
    Properties
    {
        _MainTex ("Layer 0 (Base)", 2D) = "white" {}
        _Layer1  ("Layer 1",        2D) = "white" {}
        _Layer2  ("Layer 2",        2D) = "white" {}
        _Layer3  ("Layer 3",        2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            // BRG plumbing — see comment in gta_terrain_va_3lyr.shader.
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Layer1);  SAMPLER(sampler_Layer1);
            TEXTURE2D(_Layer2);  SAMPLER(sampler_Layer2);
            TEXTURE2D(_Layer3);  SAMPLER(sampler_Layer3);

            // BRG's per-instance buffer layout must match this CBUFFER. _TexelSize entries
            // are auto-filled by Unity for any TEXTURE2D — same pattern as gta_default.
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float4 _Layer1_TexelSize;
                float4 _Layer2_TexelSize;
                float4 _Layer3_TexelSize;
            CBUFFER_END

            static const float _AmbientFloor = 0.25;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vn = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS   = vn.normalWS;
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 c0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 c1 = SAMPLE_TEXTURE2D(_Layer1,  sampler_Layer1,  IN.uv);
                half4 c2 = SAMPLE_TEXTURE2D(_Layer2,  sampler_Layer2,  IN.uv);
                half4 c3 = SAMPLE_TEXTURE2D(_Layer3,  sampler_Layer3,  IN.uv);

                // 4-layer splat: vertex.rgb pick layers 1/2/3, vertex.a (the "VA" in
                // gta_terrain_va_4lyr) reduces the base layer's contribution. Standard
                // RAGE 4-layer pattern.
                half wR = saturate(IN.color.r);
                half wG = saturate(IN.color.g);
                half wB = saturate(IN.color.b);
                half wBase = saturate(1.0 - wR - wG - wB);

                half4 albedo = c0 * wBase + c1 * wR + c2 * wG + c3 * wB;

                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half ndotl = saturate(dot(N, mainLight.direction));
                half3 lit  = mainLight.color * ndotl * mainLight.shadowAttenuation;

                half3 final = albedo.rgb * (lit + _AmbientFloor);
                return half4(final, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ShadowVaryings   { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
