Shader "GTA IV/water"
{
    Properties
    {
        [HideInInspector] _StippleAlpha ("Stipple Alpha", Float) = 1
        _MainTex ("Water Texture", 2D) = "white" {}

        [Header(Color)]
        _ShallowColor ("Shallow Color", Color) = (0.045, 0.13, 0.11, 1)
        _WaterColor ("Deep Color", Color) = (0.05, 0.15, 0.20, 1)
        _HorizonColor ("Horizon Color", Color) = (0.17, 0.34, 0.45, 1)
        _ColorBoost ("Color Vibrance", Range(1, 2)) = 1.3

        [Header(Surface)]
        _NormalScale ("Normal Strength", Range(0, 2)) = 0.25
        _WaveSpeed ("Wave Speed", Range(0, 1)) = 0.25
        _WaveScale1 ("Large Wave Scale", Float) = 0.5
        _WaveScale2 ("Small Wave Scale", Float) = 0.33

        [Header(Lighting)]
        _SpecPower ("Sun Sharpness", Float) = 512
        _SpecIntensity ("Sun Intensity", Range(0, 4)) = 2.0
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 4.0
        _ReflectStrength ("Reflection Strength", Range(0, 1)) = 0.5

        [Header(Depth)]
        _DepthFade ("Depth Fade Distance", Range(0.5, 30)) = 5.0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float _StippleAlpha;
                half4  _ShallowColor;
                half4  _WaterColor;
                half4  _HorizonColor;
                float  _ColorBoost;
                float  _NormalScale;
                float  _WaveSpeed;
                float  _WaveScale1;
                float  _WaveScale2;
                float  _SpecPower;
                float  _SpecIntensity;
                float  _FresnelPower;
                float  _ReflectStrength;
                float  _DepthFade;
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

            // Gradient noise with analytical derivatives (IQ's method).
            // Returns float3(value, dvalue/dx, dvalue/dy).
            // Quintic Hermite for C2 continuity — no visible grid artifacts.
            float3 noised(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float2 u  = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);

                float2 ga = sin(float2(127.1, 311.7) * dot(i, float2(127.1, 311.7))) * 43758.5453;
                float2 gb = sin(float2(127.1, 311.7) * dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453;
                float2 gc = sin(float2(127.1, 311.7) * dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453;
                float2 gd = sin(float2(127.1, 311.7) * dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453;
                ga = sin(ga); gb = sin(gb); gc = sin(gc); gd = sin(gd);

                float va = dot(ga, f);
                float vb = dot(gb, f - float2(1, 0));
                float vc = dot(gc, f - float2(0, 1));
                float vd = dot(gd, f - float2(1, 1));

                float value = va + (vb - va) * u.x + (vc - va) * u.y + (va - vb - vc + vd) * u.x * u.y;
                float2 deriv = ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd)
                    + du * float2(
                        (vb - va) + (va - vb - vc + vd) * u.y,
                        (vc - va) + (va - vb - vc + vd) * u.x);

                return float3(value * 0.5 + 0.5, deriv);
            }

            struct WaterVaryings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                half4  color       : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                float4 screenPos   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            WaterVaryings vert(GTA_Attributes v)
            {
                WaterVaryings o = (WaterVaryings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;

                float wave1 = sin(worldPos.x * 0.08 + t * 3.2) * 0.3;
                float wave2 = sin(worldPos.z * 0.12 + t * 4.4) * 0.2;
                float wave3 = sin((worldPos.x + worldPos.z) * 0.05 + t * 2.4) * 0.15;
                v.positionOS.y += (wave1 + wave2 + wave3) * v.color.r;

                VertexPositionInputs vpi = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.viewDirWS  = GetWorldSpaceNormalizeViewDir(vpi.positionWS);
                o.color      = v.color;
                o.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
                o.screenPos  = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(WaterVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                GTA_StippleClip(i.positionCS.xy, _StippleAlpha);

                float3 V = normalize(i.viewDirWS);
                float2 wp = i.positionWS.xz;
                float t = _Time.y * _WaveSpeed;

                // Gradient noise normals: 4 octaves flowing in different directions.
                // Large slow waves
                float3 n1 = noised(wp * _WaveScale1 + float2(0.3, 0.7) * t);
                float3 n2 = noised(wp * _WaveScale1 * 0.8 + float2(-0.5, 0.2) * t * 0.9 + 5.3);
                // Small detail ripples
                float3 n3 = noised(wp * _WaveScale2 + float2(0.2, -0.6) * t * 1.2 + 2.7);
                float3 n4 = noised(wp * _WaveScale2 * 1.5 + float2(-0.4, 0.3) * t * 0.8 + 8.1);

                float2 deriv = n1.yz * 0.35 + n2.yz * 0.35 + n3.yz * 0.18 + n4.yz * 0.12;
                float3 normalWS = normalize(float3(-deriv.x * _NormalScale, 1.0, -deriv.y * _NormalScale));

                // Depth fade
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceEyeDepth = i.screenPos.w;
                float waterDepth = max(sceneEyeDepth - surfaceEyeDepth, 0.0);
                float depthFade = 1.0 - exp(-waterDepth / _DepthFade);

                // Fresnel — Schlick with F0 = 0.04 (water IOR)
                float NdotV = saturate(dot(normalWS, V));
                float fresnel = 0.04 + 0.96 * pow(1.0 - NdotV, _FresnelPower);

                // Color: shallow → deep via depth, then → horizon via distance
                float dist = length(_WorldSpaceCameraPos.xz - wp);
                half3 baseColor = lerp(_ShallowColor.rgb, _WaterColor.rgb, depthFade);
                float horizonT = saturate((dist - 60.0) / 200.0);
                baseColor = lerp(baseColor, _HorizonColor.rgb, horizonT * horizonT);

                // Lighting: split ambient/direct so shadows stay colored
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;

                // Ambient: sky overhead tints water in shadow — push color in shadow
                half3 ambient = max(SampleSH(float3(0, 1, 0)), 0.05);
                half3 shadowTint = _HorizonColor.rgb * 0.6 + half3(0.03, 0.06, 0.12);
                ambient = lerp(ambient + shadowTint, ambient, shadow);

                // Direct: sun contribution, killed by shadow
                float diffuse = saturate(dot(normalWS, mainLight.direction) * 0.4 + 0.6);
                half3 direct = mainLight.color * diffuse * shadow;

                baseColor *= (ambient + direct) * _ColorBoost;

                // Sky reflection via fresnel — shadow darkens reflection slightly
                half3 sky = lerp(_HorizonColor.rgb * 0.9, mainLight.color * 0.3 + half3(0.4, 0.5, 0.6), 0.4);
                sky *= lerp(0.6, 1.0, shadow);
                half3 color = lerp(baseColor, sky, fresnel * _ReflectStrength);

                // Sun specular
                float3 halfDir = normalize(mainLight.direction + V);
                float spec = pow(saturate(dot(normalWS, halfDir)), _SpecPower) * _SpecIntensity;
                color += spec * mainLight.color * shadow;

                // SSS — warm backscatter when looking toward the sun through water
                float sss = pow(saturate(dot(V, -mainLight.direction)), 5.0) * 0.12;
                color += sss * _ShallowColor.rgb * mainLight.color;

                // Additional lights
                #ifdef _ADDITIONAL_LIGHTS
                    uint count = GetAdditionalLightsCount();
                    for (uint j = 0u; j < count; j++)
                    {
                        Light addLight = GetAdditionalLight(j, i.positionWS);
                        float addDiffuse = saturate(dot(normalWS, addLight.direction) * 0.4 + 0.6);
                        color += baseColor * addDiffuse * addLight.color
                               * addLight.distanceAttenuation * addLight.shadowAttenuation;
                    }
                #endif

                color = GTA_ApplyFog(color, i.fogFactor);

                float alpha = max(depthFade, 0.75);
                alpha = lerp(alpha, 1.0, fresnel * 0.4);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
