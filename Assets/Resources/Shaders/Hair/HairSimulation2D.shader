Shader "Custom/HairSimulation2D"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Base Tint", Color) = (1, 1, 1, 1)

        [Header(Hair Strand Orientation and Root Pinning)]
        _GrowthAngle ("Hair Growth Angle Deg", Range(0.0, 360.0)) = 90.0
        _RootPinOffset ("Root Pin Start", Range(0.0, 1.0)) = 0.05
        _RootPinPower ("Root Stiffness Power", Range(0.5, 5.0)) = 1.8

        [Header(Procedural Hair Fiber Texture)]
        _FiberStrength ("Procedural Fiber Visibility", Range(0.0, 1.0)) = 0.45
        _FiberDensity ("Hair Strand Density", Float) = 85.0
        _FiberMicroDetail ("Fine Sub Strand Multiplier", Float) = 3.2
        _FiberWaviness ("Strand Curl Waviness", Range(0.0, 2.0)) = 0.35
        _FiberWaveFreq ("Strand Curl Frequency", Float) = 12.0
        _FiberShadowColor ("Fiber Depth Shadow Tint", Color) = (0.15, 0.1, 0.12, 1.0)

        [Header(Root to Tip Hair Gradient)]
        _GradientBlend ("Root Tip Gradient Blend", Range(0.0, 1.0)) = 0.35
        _RootColor ("Hair Root Color", Color) = (0.25, 0.18, 0.22, 1.0)
        _TipColor ("Hair Tip Color", Color) = (0.95, 0.85, 0.75, 1.0)
        _GradientPower ("Gradient Falloff Power", Range(0.2, 4.0)) = 1.2

        [Header(Anisotropic Hair Sheen Highlight)]
        _SheenIntensity ("Sheen Highlight Intensity", Range(0.0, 2.0)) = 0.55
        _SheenColor ("Sheen Highlight Color", Color) = (1.0, 0.95, 0.88, 1.0)
        _SheenPosition ("Sheen Band Position", Range(0.0, 1.0)) = 0.55
        _SheenWidth ("Sheen Band Width", Range(0.02, 0.5)) = 0.14
        _SheenJitter ("Per Strand Sheen Shift", Range(0.0, 0.5)) = 0.12
        _SheenShimmerSpeed ("Sheen Shimmer Speed", Float) = 1.5

        [Header(Hair Wind and Flow Motion)]
        _WindSpeed ("Primary Sway Speed", Float) = 3.0
        _WindFrequency ("Primary Sway Frequency", Float) = 6.0
        _WindAmplitude ("Primary Vertex Sway Amplitude", Float) = 0.06
        _FlutterSpeed ("Tip Flutter Speed", Float) = 7.5
        _FlutterFrequency ("Tip Flutter Frequency", Float) = 16.0
        _FlutterAmplitude ("Tip Vertex Flutter Amplitude", Float) = 0.025
        _UVStrandRipple ("UV Strand Ripple Amount", Range(0.0, 0.08)) = 0.015

        [Header(Fur and Hair Edge Fraying)]
        _EdgeFrayStrength ("Edge Wisp Fraying", Range(0.0, 1.0)) = 0.35
        _AlphaClipSmoothness ("Alpha Edge Softness", Range(0.1, 1.0)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float2 hairCoords   : TEXCOORD1;
                float  tipWeight    : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;

                float _GrowthAngle;
                float _RootPinOffset;
                float _RootPinPower;

                float _FiberStrength;
                float _FiberDensity;
                float _FiberMicroDetail;
                float _FiberWaviness;
                float _FiberWaveFreq;
                float4 _FiberShadowColor;

                float _GradientBlend;
                float4 _RootColor;
                float4 _TipColor;
                float _GradientPower;

                float _SheenIntensity;
                float4 _SheenColor;
                float _SheenPosition;
                float _SheenWidth;
                float _SheenJitter;
                float _SheenShimmerSpeed;

                float _WindSpeed;
                float _WindFrequency;
                float _WindAmplitude;
                float _FlutterSpeed;
                float _FlutterFrequency;
                float _FlutterAmplitude;
                float _UVStrandRipple;

                float _EdgeFrayStrength;
                float _AlphaClipSmoothness;
            CBUFFER_END

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float SmoothNoise1D(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), u);
            }

            float2 ComputeHairSpace(float2 uv, float angleDeg, out float2 growthDir, out float2 perpDir)
            {
                float rad = radians(angleDeg);
                float s = sin(rad);
                float c = cos(rad);

                growthDir = float2(c, s);
                perpDir = float2(-s, c);

                float2 centeredUV = uv - 0.5;
                float along = dot(centeredUV, growthDir) + 0.5;
                float across = dot(centeredUV, perpDir) + 0.5;
                return float2(across, saturate(along));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float2 uv = input.uv;
                float2 growthDir;
                float2 perpDir;
                float2 hairSpace = ComputeHairSpace(uv, _GrowthAngle, growthDir, perpDir);

                float alongNormalized = saturate((hairSpace.y - _RootPinOffset) / max(0.001, 1.0 - _RootPinOffset));
                float tipMask = pow(alongNormalized, _RootPinPower);

                float time = _Time.y;
                float strandPhase = hairSpace.x * 12.566;

                float primaryWave = sin(time * _WindSpeed - hairSpace.y * _WindFrequency + sin(strandPhase) * 0.6) * _WindAmplitude;

                float flutterMask = tipMask * alongNormalized;
                float tipFlutter = cos(time * _FlutterSpeed - hairSpace.y * _FlutterFrequency + strandPhase * 1.7) * _FlutterAmplitude * flutterMask;

                float totalSway = (primaryWave + tipFlutter) * tipMask;
                float2 offset2D = perpDir * totalSway + growthDir * (abs(totalSway) * -0.15);

                float3 displacedOS = input.positionOS.xyz + float3(offset2D.x, offset2D.y, 0.0);

                output.positionCS = TransformObjectToHClip(displacedOS);
                output.uv = uv;
                output.color = input.color * _Color;
                output.hairCoords = hairSpace;
                output.tipWeight = tipMask;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;

                float2 growthDir;
                float2 perpDir;
                float2 hairSpace = ComputeHairSpace(input.uv, _GrowthAngle, growthDir, perpDir);
                float across = hairSpace.x;
                float along = hairSpace.y;
                float tipMask = input.tipWeight;

                // 1. Wavy / curly coordinate modulation along the hair shaft
                float waveCurve = sin(along * _FiberWaveFreq - time * _WindSpeed) * (_FiberWaviness * 0.025) * tipMask;
                float warpedAcross = across + waveCurve;

                // 2. Multi-scale procedural hair fibers (coarse clumps + fine silky strands)
                float coarseCoordinate = warpedAcross * _FiberDensity;
                float fineCoordinate = warpedAcross * (_FiberDensity * _FiberMicroDetail);

                float coarseStrand = SmoothNoise1D(coarseCoordinate);
                float fineStrand = SmoothNoise1D(fineCoordinate + coarseStrand * 2.5);
                float strandPattern = lerp(coarseStrand, fineStrand, 0.55);

                float streakVariation = SmoothNoise1D(coarseCoordinate * 0.7 + along * 2.0);
                float fiberValue = saturate(lerp(strandPattern, streakVariation, 0.3));

                // 3. Micro UV ripple per hair clump
                float strandWaveUV = sin(time * _FlutterSpeed - along * _WindFrequency + coarseStrand * 6.2831) * _UVStrandRipple * tipMask;
                float2 rippledUV = input.uv + perpDir * strandWaveUV;

                half4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, rippledUV);

                // 4. Root-to-Tip color gradient blended with sprite texture
                float gradFactor = pow(along, _GradientPower);
                half3 rootToTipCol = lerp(_RootColor.rgb, _TipColor.rgb, gradFactor);
                half3 hairColor = lerp(baseTex.rgb, baseTex.rgb * rootToTipCol * 1.35, _GradientBlend);

                // 5. Apply procedural hair fiber grooves & highlights
                half3 shadowedHair = lerp(hairColor, hairColor * _FiberShadowColor.rgb, 1.0 - fiberValue);
                half3 highlightedHair = hairColor * (0.85 + 0.35 * fiberValue);
                half3 texturedHair = lerp(shadowedHair, highlightedHair, fiberValue);
                hairColor = lerp(hairColor, texturedHair, _FiberStrength);

                // 6. Anisotropic Hair Sheen Band
                float sheenJitterOffset = (coarseStrand - 0.5) * _SheenJitter + sin(time * _SheenShimmerSpeed + across * 4.0) * 0.025;
                float sheenCenter = _SheenPosition + sheenJitterOffset;
                float distFromSheen = abs(along - sheenCenter);
                float sheenBand = smoothstep(_SheenWidth, 0.0, distFromSheen);
                sheenBand *= sheenBand * (0.4 + 0.6 * fineStrand);

                hairColor += _SheenColor.rgb * (sheenBand * _SheenIntensity);

                // 7. Silky frayed edge wisps
                float baseAlpha = baseTex.a;
                float wispMask = smoothstep(0.0, 0.95, baseAlpha);
                float edgeZone = baseAlpha * (1.0 - baseAlpha) * 4.0;
                float frayCutoff = lerp(0.0, (1.0 - fiberValue) * _EdgeFrayStrength, edgeZone + tipMask * 0.35);
                float finalAlpha = smoothstep(frayCutoff, min(1.0, frayCutoff + _AlphaClipSmoothness), wispMask) * baseAlpha;

                return half4(hairColor * input.color.rgb, saturate(finalAlpha * input.color.a));
            }
            ENDHLSL
        }
    }
}
