Shader "Custom/2D/CameraParallaxScrollSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Camera Parallax Settings)]
        _CamPosX ("Camera World X Position", Float) = 0.0
        _BaseParallax ("Base Parallax Multiplier", Range(-2.0, 2.0)) = 0.5

        [Header(Layer Settings)]
        _LayerCount ("Number of Layers/Segments", Range(1, 16)) = 4
        _SpeedMultiplier ("Parallax Scaling Per Layer", Float) = 1.2
        [Toggle] _ReverseSpeedOrder ("Reverse Parallax Order", Float) = 0
        [Toggle] _InvertDirection ("Invert Odd Layers", Float) = 0
        [Toggle] _VerticalSplit ("Split Vertically Instead of Horizontally", Float) = 0

        [Header(Split Zone Settings)]
        _SplitZoneCenter ("Zone Center (0 to 1)", Range(0.0, 1.0)) = 0.5
        _SplitZoneWidth ("Zone Width", Range(0.01, 1.0)) = 0.6
        _ZoneFeather ("Zone Edge Feather", Range(0.001, 0.2)) = 0.05
        _EdgeFeather ("Boundary Softness Zone", Range(0.01, 0.49)) = 0.2

        [Header(Fog and Wind Overlay)]
        _FogColor ("Fog Color & Opacity", Color) = (0.8, 0.85, 0.95, 0.35)
        _FogDensity ("Fog Overall Density", Range(0.0, 2.0)) = 0.6
        _FogScale ("Fog Pattern Scale", Range(1.0, 30.0)) = 6.0
        _FogSpeed ("Wind Travel Speed", Float) = 0.8
        _FogWindAngle ("Wind Angle (Degrees)", Range(0.0, 360.0)) = 25.0

        [Header(Dynamic Smooth Bump Settings)]
        _BumpAmount ("Bump Distortion Strength", Range(0.0, 0.2)) = 0.03
        _BumpFrequency ("Bump Frequency / Scale", Range(1.0, 50.0)) = 10.0
        _BumpSpeed ("Bump Wave Speed", Float) = 1.0

        [Header(Velocity Motion Blur)]
        _MotionBlurStrength ("Motion Blur Multiplier", Range(0.0, 0.1)) = 0.02
        [IntRange] _BlurSamples ("Blur Quality Samples", Range(3, 9)) = 5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            
            float _CamPosX;
            float _BaseParallax;
            float _LayerCount;
            float _SpeedMultiplier;
            float _ReverseSpeedOrder;
            float _InvertDirection;
            float _VerticalSplit;
            float _SplitZoneCenter;
            float _SplitZoneWidth;
            float _ZoneFeather;
            float _EdgeFeather;

            fixed4 _FogColor;
            float _FogDensity;
            float _FogScale;
            float _FogSpeed;
            float _FogWindAngle;

            float _BumpAmount;
            float _BumpFrequency;
            float _BumpSpeed;
            float _MotionBlurStrength;
            int _BlurSamples;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise2D(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FractalFogNoise(float2 uv)
            {
                float total = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    total += ValueNoise2D(uv) * amplitude;
                    uv *= 2.03;
                    amplitude *= 0.5;
                }
                return total;
            }

            float GetSmoothDynamicBump(float seamPos, float time)
            {
                float phase = seamPos * _BumpFrequency + (time * _BumpSpeed);
                float wave1 = sin(phase);
                float wave2 = sin(phase * 2.3 + 1.5) * 0.5;
                float wave3 = sin(phase * 4.1 + 3.1) * 0.25;
                return (wave1 + wave2 + wave3) * 0.57;
            }

            float GetSafeLayerParallax(float layerIdx)
            {
                int safeIdx = clamp((int)layerIdx, 0, max(0, (int)_LayerCount - 1));
                float effectiveIdx = (float)safeIdx;

                if (_ReverseSpeedOrder > 0.5)
                {
                    effectiveIdx = max(0.0, _LayerCount - 1.0 - (float)safeIdx);
                }

                float factor = _BaseParallax * pow(max(0.001, _SpeedMultiplier), effectiveIdx);
                
                if (_InvertDirection > 0.5 && (safeIdx % 2) != 0)
                {
                    factor *= -1.0;
                }
                
                return factor;
            }

            fixed4 SampleTextureSeamless(float2 unwrappedUV, float parallaxFactor)
            {
                float2 dx = ddx(unwrappedUV);
                float2 dy = ddy(unwrappedUV);

                float blurOffset = parallaxFactor * _MotionBlurStrength;

                if (abs(blurOffset) < 0.0001)
                {
                    float2 wrappedUV = float2(frac(unwrappedUV.x), unwrappedUV.y);
                    return tex2Dgrad(_MainTex, wrappedUV, dx, dy);
                }

                fixed4 colorSum = fixed4(0, 0, 0, 0);
                int samples = clamp(_BlurSamples, 3, 9);
                float stepSize = blurOffset / (float)(samples - 1);

                for (int i = 0; i < samples; i++)
                {
                    float offset = ((float)i - ((float)(samples - 1) * 0.5)) * stepSize;
                    float2 sampleUV = float2(frac(unwrappedUV.x + offset), unwrappedUV.y);
                    colorSum += tex2Dgrad(_MainTex, sampleUV, dx, dy);
                }

                return colorSum / (float)samples;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                float splitAxis = _VerticalSplit > 0.5 ? uv.x : uv.y;
                float seamAxis  = _VerticalSplit > 0.5 ? uv.y : uv.x;

                float halfWidth = _SplitZoneWidth * 0.5;
                float zoneMin = _SplitZoneCenter - halfWidth;
                float zoneMax = _SplitZoneCenter + halfWidth;

                float zoneMask = smoothstep(zoneMin - _ZoneFeather, zoneMin + _ZoneFeather, splitAxis) *
                                 smoothstep(zoneMax + _ZoneFeather, zoneMax - _ZoneFeather, splitAxis);

                float noiseVal = GetSmoothDynamicBump(seamAxis, _Time.y);
                float edgeMask = smoothstep(0.0, 0.1, splitAxis) * smoothstep(1.0, 0.9, splitAxis);
                float bumpySplitAxis = splitAxis + (noiseVal * _BumpAmount * edgeMask);

                float totalSegments = max(1.0, _LayerCount);
                float rawLayer = clamp(bumpySplitAxis, 0.0, 0.9999) * totalSegments;

                float baseIndex = floor(rawLayer);
                float fracPos = frac(rawLayer);

                float layerA = baseIndex;
                float layerB = min(baseIndex + 1.0, totalSegments - 1.0);

                // Multipliers per layer
                float factorA = GetSafeLayerParallax(layerA);
                float factorB = GetSafeLayerParallax(layerB);

                // Scrolling UVs driven directly by Camera X Position
                float2 continuousUVA = float2(uv.x + (_CamPosX * factorA), uv.y);
                float2 continuousUVB = float2(uv.x + (_CamPosX * factorB), uv.y);

                fixed4 colA = SampleTextureSeamless(continuousUVA, factorA);
                fixed4 colB = SampleTextureSeamless(continuousUVB, factorB);

                float feather = clamp(_EdgeFeather, 0.01, 0.49);
                float blendFactor = smoothstep(1.0 - feather, 1.0, fracPos);
                fixed4 splitLayerColor = lerp(colA, colB, blendFactor);

                // Unsplit Base layer UV driven by base camera position
                float2 continuousBaseUV = float2(uv.x + (_CamPosX * _BaseParallax), uv.y);
                fixed4 baseColor = SampleTextureSeamless(continuousBaseUV, _BaseParallax);

                fixed4 finalColor = lerp(baseColor, splitLayerColor, zoneMask);

                // Volumetric wind fog stays independent for ambient atmosphere
                float rad = radians(_FogWindAngle);
                float2 windDir = float2(cos(rad), sin(rad));
                float2 fogUV = (uv * _FogScale) + (windDir * (_Time.y * _FogSpeed));
                
                float fogPattern = FractalFogNoise(fogUV);
                float fogAlpha = saturate(fogPattern * _FogDensity * _FogColor.a);

                finalColor.rgb = lerp(finalColor.rgb, _FogColor.rgb, fogAlpha * finalColor.a);

                finalColor *= IN.color;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}