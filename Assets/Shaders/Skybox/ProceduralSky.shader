Shader "Skybox/ProceduralSky"
{
    Properties
    {
        [Header(Sun)]
        _SunDirection            ("Sun Direction Override (xyz, w>0 to enable)", Vector) = (0, 0, 0, 0)
        _SunSize                 ("Sun Angular Radius (radians)", Range(0.001, 0.2)) = 0.025
        _SunIntensity            ("Sun Intensity",                Range(1, 50))      = 22.0
        _SunDiscColor            ("Sun Disc Color",               Color)             = (1.0, 0.95, 0.85, 1.0)

        [Header(Atmosphere)]
        _RayleighStrength        ("Rayleigh Strength",            Range(0, 5))       = 1.0
        _MieStrength             ("Mie Strength",                 Range(0, 5))       = 1.0
        _MieG                    ("Mie Anisotropy g",             Range(-0.99, 0.99))= 0.758

        [Header(Cloud Shape)]
        _CloudCoverage           ("Coverage",                     Range(0, 1))       = 0.5
        _CloudDensity            ("Density",                      Range(0, 5))       = 1.2
        _CloudEdgeSoftness       ("Edge Softness",                Range(0.005, 0.4)) = 0.06
        _CloudScale              ("Base Noise Scale",             Float)             = 0.0008
        _CloudDetailScale        ("Detail Noise Scale Multiplier",Range(1, 10))      = 4.5
        _CloudDetailWeight       ("Detail Erosion Weight",        Range(0, 1))       = 0.45
        _CloudWindSpeed          ("Wind Speed (xyz m/s)",         Vector)            = (4.0, 0.0, 2.0, 0.0)

        [Header(Cloud Shape Guerrilla)]
        _CloudPerlinWorleyMix    ("Perlin to Worley Mix",         Range(0, 1))       = 0.55
        _CloudWeatherScale       ("Weather Map Scale",            Float)             = 0.00006
        _CloudWeatherStrength    ("Weather Map Strength",         Range(0, 1))       = 0.85
        _CloudCurlScale          ("Curl Distortion Scale",        Float)             = 0.00025
        _CloudCurlStrength       ("Curl Distortion Strength",     Range(0, 2000))    = 350

        [Header(Cloud Layers)]
        _CloudLayer1Altitude     ("Layer 1 Altitude (m)",         Float)             = 1500
        _CloudLayer2Altitude     ("Layer 2 Altitude (m)",         Float)             = 2500
        _CloudLayer3Altitude     ("Layer 3 Altitude (m)",         Float)             = 3500
        _CloudLayerHeightFalloff ("Layer Height Falloff",         Range(0, 1))       = 0.3

        [Header(Cloud Lighting)]
        _CloudSunIntensity       ("Sun Brightness on Clouds",     Range(0, 10))      = 1.4
        _CloudLightAbsorption    ("Light Absorption",             Range(0.05, 1))    = 0.3
        _CloudPhaseG             ("Phase Forward Scatter g",      Range(0, 0.99))    = 0.72
        _CloudPhaseGBack         ("Phase Back Scatter g",         Range(-0.99, 0))   = -0.25
        _CloudPhaseLobeMix       ("Phase Lobe Mix (Fwd to Back)", Range(0, 1))       = 0.35
        _CloudPhaseStrength      ("Phase Strength",               Range(0, 5))       = 1.6
        _CloudAmbient            ("Ambient Floor",                Range(0, 2))       = 0.55
        _CloudColor              ("Cloud Tint",                   Color)             = (1.0, 1.0, 1.0, 1.0)
        _CloudShadowColor        ("Cloud Shadow Color",           Color)             = (0.42, 0.50, 0.62, 1.0)
        _CloudAmbientSky         ("Ambient Sky (top)",            Color)             = (0.55, 0.68, 0.85, 1.0)
        _CloudAmbientGround      ("Ambient Ground (bottom)",      Color)             = (0.65, 0.55, 0.45, 1.0)
        _CloudPowderStrength     ("Beer-Powder Strength",         Range(0, 1))       = 0.6
        _CloudMultiScatterA      ("Multi-Scatter Energy (a)",     Range(0, 1))       = 0.55
        _CloudMultiScatterB      ("Multi-Scatter Extinction (b)", Range(0, 1))       = 0.55
        _CloudMultiScatterC      ("Multi-Scatter Phase (c)",      Range(0, 1))       = 0.55

        [Header(Cloud Self Shadow)]
        _CloudSelfShadowAbsorption    ("Self Shadow Absorption",       Range(0, 5))    = 1.7
        _CloudSelfShadowDistance      ("Self Shadow Step Distance (m)",Range(50, 1500))= 220
        _CloudSelfShadowSamples       ("Self Shadow Samples",          Range(1, 8))    = 6
        _CloudSelfShadowDarknessFloor ("Self Shadow Darkness Floor",   Range(0, 1))    = 0.12
        _CloudSelfShadowConeRadius    ("Self Shadow Cone Radius",      Range(0, 1))    = 0.3
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SkyboxCommon.hlsl"
            #include "SkyboxAtmosphere.hlsl"
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
                OUT.viewDir    = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(IN.viewDir);
                float3 sunDir  = GetSunDirection();
                float3 sunCol  = GetSunColor();

                // Atmosphere — single scattering through the planet's air shell.
                float3 sky = ComputeAtmosphere(viewDir, sunDir);
                // Visible sun disc.
                sky += ComputeSunDisc(viewDir, sunDir);

                // Clouds — 2D layered, projected onto altitude planes.
                CloudResult cloud = RenderClouds(viewDir, sunDir, sunCol);

                // Premultiplied composition.
                float3 color = sky * cloud.transmittance + cloud.scatteredLight;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
