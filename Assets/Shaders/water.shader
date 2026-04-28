Shader "GTA IV/water"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Water Texture", 2D) = "white" {}
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

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
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                half4  color       : TEXCOORD4;
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

                // Engine (waterVS0): samples HeightMapSampler for wave displacement.
                // We approximate with multi-octave sine waves.
                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y;

                // 3 wave octaves at different directions and frequencies
                float wave1 = sin(worldPos.x * 0.08 + t * 0.8) * 0.3;
                float wave2 = sin(worldPos.z * 0.12 + t * 1.1) * 0.2;
                float wave3 = sin((worldPos.x + worldPos.z) * 0.05 + t * 0.6) * 0.15;
                float height = (wave1 + wave2 + wave3) * v.color.r;

                v.positionOS.y += height;

                // Compute perturbed normal from wave derivatives
                float dx = cos(worldPos.x * 0.08 + t * 0.8) * 0.08 * 0.3
                         + cos((worldPos.x + worldPos.z) * 0.05 + t * 0.6) * 0.05 * 0.15;
                float dz = cos(worldPos.z * 0.12 + t * 1.1) * 0.12 * 0.2
                         + cos((worldPos.x + worldPos.z) * 0.05 + t * 0.6) * 0.05 * 0.15;
                dx *= v.color.r;
                dz *= v.color.r;

                VertexPositionInputs vpi = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.normalWS   = float3(0, 1, 0); // computed per-pixel in fragment
                o.viewDirWS  = GetWorldSpaceNormalizeViewDir(vpi.positionWS);
                o.color      = v.color;
                o.fogFactor  = ComputeFogFactor(vpi.positionCS.z);

                float2 worldUV = vpi.positionWS.xz * _WaveScale;
                o.uv.xy = worldUV + t * _WaveSpeed.xy;
                o.uv.zw = worldUV * 1.7 + t * _WaveSpeed.zw;
                return o;
            }

            half4 frag(WaterVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);

                // Surface detail from waterclear.dds (two scrolling layers)
                half4 t0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                half4 t1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.zw);
                half3 surfaceDetail = (t0.rgb + t1.rgb) * 0.5;

                // Compute normal from analytical wave derivatives.
                // Same wave function as vertex shader but computed per-pixel for
                // higher detail than the grid resolution allows.
                float time = _Time.y;
                float3 wp = i.positionWS;
                float amp = saturate(i.color.r * 2.0);

                // Exact derivatives of: wave1 + wave2 + wave3 from vertex shader
                // wave1 = sin(x * 0.08 + t * 0.8) * 0.3
                // wave2 = sin(z * 0.12 + t * 1.1) * 0.2
                // wave3 = sin((x+z) * 0.05 + t * 0.6) * 0.15
                float dhdx = cos(wp.x * 0.08 + time * 0.8) * 0.08 * 0.3
                           + cos((wp.x + wp.z) * 0.05 + time * 0.6) * 0.05 * 0.15;
                float dhdz = cos(wp.z * 0.12 + time * 1.1) * 0.12 * 0.2
                           + cos((wp.x + wp.z) * 0.05 + time * 0.6) * 0.05 * 0.15;

                // Add high-frequency detail waves (pixel-only, not in vertex shader)
                dhdx += cos(wp.x * 0.5 + time * 2.0) * 0.5 * 0.04
                      + cos((wp.x - wp.z) * 0.3 + time * 1.5) * 0.3 * 0.03;
                dhdz += cos(wp.z * 0.7 + time * 2.3) * 0.7 * 0.04
                      + cos((wp.x - wp.z) * 0.3 + time * 1.5) * -0.3 * 0.03;

                dhdx *= amp;
                dhdz *= amp;

                // Add texture-based micro detail
                float2 texBump = (t0.rg + t1.rg - 1.0) * 0.08;
                dhdx += texBump.x;
                dhdz += texBump.y;

                // Normal from height derivatives: N = normalize(-dh/dx, 1, -dh/dz)
                float3 normalWS = normalize(float3(-dhdx, 1.0, -dhdz));

                // Fresnel
                float3 V = normalize(i.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, V)), _FresnelPower);

                // Reflection
                float3 reflDir = reflect(-V, normalWS);
                half3 envColor = GlossyEnvironmentReflection(reflDir, 0.0, 1.0);

                // Lighting
                half3 waterColor = _WaterColor.rgb * surfaceDetail;
                half3 lit = GTA_LightingWithShadow(waterColor, normalWS, i.positionWS);

                // Specular
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 specColor = GTA_Specular(normalWS, i.positionWS, mainLight.direction,
                    mainLight.color, 1.0, _Shininess, mainLight.shadowAttenuation);

                half3 color = lit + specColor;
                color += envColor * fresnel * _ReflectStrength;
                color = GTA_ApplyFog(color, i.fogFactor);

                return half4(color, _WaterColor.a);
            }
            ENDHLSL
        }
    }
}
