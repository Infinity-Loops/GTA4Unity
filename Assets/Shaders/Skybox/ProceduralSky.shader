Shader "Skybox/ProceduralSky"
{
    Properties
    {
        [Header(Atmosphere)]
        _SunIntensity      ("Sun Intensity",        Range(1, 40))       = 20
        _AtmoDensity       ("Atmosphere Density",   Range(0.5, 3.0))    = 1.0
        _MieStrength       ("Mie (Haze)",           Range(0.001, 0.05)) = 0.021
        _MieAnisotropy     ("Mie Anisotropy",       Range(0.0, 0.99))   = 0.76
        _OzoneStrength     ("Ozone Strength",       Range(0.0, 2.0))    = 1.0


        [Header(Sun)]
        _SunDiscSize        ("Sun Disc Size",       Range(0.99, 0.9999))= 0.997
        _SunGlowIntensity   ("Sun Glow Intensity",  Range(0.0, 3.0))    = 1.5

        [Header(Moon)]
        _MoonSize           ("Moon Size",           Range(0.01, 0.15))  = 0.05
        _MoonGlow           ("Moon Glow",           Range(0.0, 2.0))    = 0.5

        [Header(Stars)]
        _StarDensity        ("Star Density",        Range(50, 500))     = 200
        _StarBrightness     ("Star Brightness",     Range(0.0, 3.0))    = 1.0
        _TwinkleSpeed       ("Twinkle Speed",       Range(0.0, 5.0))    = 1.0

        [Header(Clouds)]
        _CloudHeight        ("Cloud Height",        Range(0.1, 1.0))    = 0.4
        _CloudCoverage      ("Cloud Coverage",      Range(0.0, 1.0))    = 0.5
        _CloudSpeed         ("Cloud Speed",         Range(0.0, 0.1))    = 0.01
        _CloudScale         ("Cloud Scale",         Range(0.5, 8.0))    = 3.0
        _CloudEdge          ("Cloud Edge Softness", Range(0.01, 0.3))   = 0.06

        [Header(Exposure)]
        _Exposure           ("Exposure",            Range(0.1, 5.0))    = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Background"
            "Queue"          = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType"    = "Skybox"
        }

        Cull Off
        ZWrite Off
        Fog { Mode Off }

        Pass
        {
            Name "ProceduralSky"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "SkyboxCommon.hlsl"
            #include "SkyboxAtmosphere.hlsl"
            #include "SkyboxStars.hlsl"
            #include "SkyboxMoon.hlsl"
            #include "SkyboxClouds.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir    : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.viewDir    = mul((float3x3)UNITY_MATRIX_M, IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(IN.viewDir);
                float3 sunDir  = normalize(_MainLightPosition.xyz);
                float sunAltitude = sunDir.y;

                AtmosphereResult atmo = ComputeFullAtmosphere(viewDir, sunDir,
                    _SunIntensity, _AtmoDensity, _MieStrength,
                    _MieAnisotropy, _OzoneStrength);

                float3 skyColor = atmo.color;

                float skyFade = smoothstep(-0.05, 0.05, viewDir.y);

                // Stars + Moon (night/twilight).
                if (skyFade > 0.001 && sunAltitude < 0.1)
                {
                    float3 moonDirNorm = normalize(-sunDir);

                    float3 stars = ComputeStars(viewDir, sunAltitude,
                        _StarDensity, _StarBrightness, _TwinkleSpeed, _Time.y);
                    stars *= 1.0 - MoonMask(viewDir, moonDirNorm, _MoonSize);
                    skyColor += stars * skyFade;

                    float3 moon = ComputeMoon(viewDir, sunDir, sunAltitude,
                        moonDirNorm, _MoonSize, _MoonGlow);
                    skyColor += moon * skyFade;
                }

                // Clouds.
                if (viewDir.y > 0.005)
                {
                    float4 clouds = ComputeClouds(viewDir, sunDir, sunAltitude,
                        _CloudHeight, _CloudCoverage, _CloudSpeed, _CloudEdge,
                        _CloudScale, _Time.y);
                    skyColor = lerp(skyColor, clouds.rgb, clouds.a);
                }

                // Sun disc.
                if (skyFade > 0.001)
                {
                    float3 sunDisc = ComputeSunDisc(viewDir, sunDir,
                        _SunDiscSize, _SunGlowIntensity);
                    skyColor += sunDisc * skyFade;
                }

                // ACES filmic tonemap — proper S-curve with toe/shoulder.
                skyColor *= _Exposure;
                skyColor = (skyColor * (2.51 * skyColor + 0.03))
                         / (skyColor * (2.43 * skyColor + 0.59) + 0.14);

                return half4(saturate(skyColor), 1.0);
            }
            ENDHLSL
        }
    }

}
