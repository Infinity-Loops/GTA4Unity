// waterTex — same as water but with an additional diffuse texture overlay.
// Engine uses this for textured water surfaces (rivers, pools).
Shader "GTA IV/waterTex"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Surface Normal", 2D) = "bump" {}
        _DiffuseTex ("Diffuse Overlay", 2D) = "white" {}
        _WaterColor ("Water Color", Color) = (0.05, 0.15, 0.2, 0.8)
        _SpecColor ("Specular Color", Color) = (0.8, 0.8, 0.8, 1)
        _Shininess ("Shininess", Range(8, 256)) = 128
        _WaveSpeed ("Wave Speed", Vector) = (0.03, 0.02, -0.02, 0.03)
        _WaveScale ("Wave Scale", Float) = 0.1
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.0
        _ReflectStrength ("Reflection Strength", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "GTA Water Forward"
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

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_DiffuseTex); SAMPLER(sampler_DiffuseTex);

            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
                half4  _WaterColor;
                half4  _SpecColor;
                float  _Shininess;
                float4 _WaveSpeed;
                float  _WaveScale;
                float  _FresnelPower;
                float  _ReflectStrength;
                float4 _MainTex_TexelSize;
                float4 _DiffuseTex_TexelSize;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                    UNITY_DOTS_INSTANCED_PROP(float4, _WaterColor)
                #define _StippleAlpha UNITY_ACCESS_DOTS_INSTANCED_PROP(float, _StippleAlpha)
                UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
                #define _WaterColor UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, _WaterColor)
            #endif

            struct WaterVaryings
            {
                float4 positionCS  : SV_POSITION;
                float4 uv          : TEXCOORD0;
                float2 uvDiffuse   : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 positionWS  : TEXCOORD3;
                float3 viewDirWS   : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            WaterVaryings vert(GTA_Attributes v)
            {
                WaterVaryings o = (WaterVaryings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs vpi = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS  = GetWorldSpaceNormalizeViewDir(vpi.positionWS);
                o.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
                o.uvDiffuse  = v.uv;

                float2 worldUV = vpi.positionWS.xz * _WaveScale;
                o.uv.xy = worldUV + _Time.y * _WaveSpeed.xy;
                o.uv.zw = worldUV * 1.7 + _Time.y * _WaveSpeed.zw;
                return o;
            }

            half4 frag(WaterVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);

                half4 diffuseTex = SAMPLE_TEXTURE2D(_DiffuseTex, sampler_DiffuseTex, i.uvDiffuse);

                half3 n0 = GTA_UnpackNormal(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy));
                half3 n1 = GTA_UnpackNormal(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.zw));
                half3 normalTS = normalize(half3(n0.xy + n1.xy, n0.z));

                float3 N = normalize(i.normalWS);
                float3 T = normalize(cross(N, float3(0, 0, 1)));
                float3 B = cross(N, T);
                float3 normalWS = GTA_NormalFromTBN(normalTS, T, B, N);

                float3 V = normalize(i.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, V)), _FresnelPower);

                float3 reflDir = reflect(-V, normalWS);
                half3 envColor = GlossyEnvironmentReflection(reflDir, 0.0, 1.0);

                half3 baseColor = _WaterColor.rgb * diffuseTex.rgb;
                half3 lit = GTA_LightingWithShadow(baseColor, normalWS, i.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 specColor = GTA_Specular(normalWS, i.positionWS, mainLight.direction,
                    mainLight.color, 1.0, _Shininess, mainLight.shadowAttenuation);

                half3 color = lit + specColor;
                color += envColor * fresnel * _ReflectStrength;
                color = GTA_ApplyFog(color, i.fogFactor);

                return half4(color, _WaterColor.a * diffuseTex.a);
            }
            ENDHLSL
        }
    }
}
