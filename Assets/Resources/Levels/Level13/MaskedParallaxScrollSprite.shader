Shader "Custom/2D/MaskedParallaxScrollSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture (Mask/Base)", 2D) = "white" {}
        _ScrollTex ("Scrolling Background Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _CamPosX ("Camera World X Position", Float) = 0.0
        _BaseParallax ("Base Parallax Multiplier", Range(-0.5, 0.5)) = 0

        _AutoScrollSpeed ("Auto Scroll Speed Horizontal", Float) = 0.5
        _AutoScrollSpeedY ("Auto Scroll Speed Vertical", Float) = 0.0

        _LayerCount ("Number of Layers/Segments", Range(1, 16)) = 4
        _SpeedMultiplier ("Parallax Scaling Per Layer", Float) = 1.2
        [Toggle] _ReverseSpeedOrder ("Reverse Parallax Order", Float) = 0
        [Toggle] _InvertDirection ("Invert Odd Layers", Float) = 0

        _SplitZoneCenter ("Zone Center (0 to 1)", Range(0.0, 1.0)) = 0.5
        _SplitZoneWidth ("Zone Width", Range(0.01, 1.0)) = 0.6
        _ZoneFeather ("Zone Edge Feather", Range(0.001, 0.2)) = 0.05
        _EdgeFeather ("Boundary Softness Zone", Range(0.01, 0.49)) = 0.2

        [Toggle] _UseMirrorTiling ("Mirror Repeat (Horizontal)", Float) = 1
        [Toggle] _ClampVertical ("Clamp Vertical (Fixes Vertical Breaking)", Float) = 1
        _ClampPadding ("Edge Alpha Padding", Range(0.0, 0.4)) = 0.05
        [Toggle] _FillAbsentPixels ("Fill Absent Pixels from Neighbors", Float) = 1

        _MotionBlurStrength ("Motion Blur Multiplier", Range(0.0, 0.1)) = 0.02
        _BlurSamples ("Blur Quality Samples", Range(3, 9)) = 5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            sampler2D _ScrollTex;
            float4 _ScrollTex_ST; // Automatically handles Material Inspector Tiling & Offset
            float4 _ScrollTex_TexelSize;
            fixed4 _Color;
            
            float _CamPosX;
            float _BaseParallax;
            float _AutoScrollSpeed;
            float _AutoScrollSpeedY;

            float _LayerCount;
            float _SpeedMultiplier;
            float _ReverseSpeedOrder;
            float _InvertDirection;
            float _SplitZoneCenter;
            float _SplitZoneWidth;
            float _ZoneFeather;
            float _EdgeFeather;

            float _UseMirrorTiling;
            float _ClampVertical;
            float _ClampPadding;
            float _FillAbsentPixels;

            float _MotionBlurStrength;
            int _BlurSamples;

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

            fixed4 SampleWithExpandedNeighborFill(float2 uv, float2 dx, float2 dy)
            {
                fixed4 col = tex2Dgrad(_ScrollTex, uv, dx, dy);

                if (col.a < 0.05 && _FillAbsentPixels > 0.5)
                {
                    float2 texelSize = _ScrollTex_TexelSize.xy * 2.0;

                    fixed4 n1 = tex2Dgrad(_ScrollTex, uv + float2(texelSize.x, 0.0), dx, dy);
                    fixed4 n2 = tex2Dgrad(_ScrollTex, uv + float2(-texelSize.x, 0.0), dx, dy);
                    fixed4 n3 = tex2Dgrad(_ScrollTex, uv + float2(0.0, texelSize.y), dx, dy);
                    fixed4 n4 = tex2Dgrad(_ScrollTex, uv + float2(0.0, -texelSize.y), dx, dy);
                    fixed4 n5 = tex2Dgrad(_ScrollTex, uv + float2(texelSize.x, texelSize.y), dx, dy);
                    fixed4 n6 = tex2Dgrad(_ScrollTex, uv + float2(-texelSize.x, -texelSize.y), dx, dy);

                    if (n1.a > 0.05) col = n1;
                    else if (n2.a > 0.05) col = n2;
                    else if (n3.a > 0.05) col = n3;
                    else if (n4.a > 0.05) col = n4;
                    else if (n5.a > 0.05) col = n5;
                    else if (n6.a > 0.05) col = n6;
                }

                return col;
            }

            float2 GetMappedUV(float2 unwrappedUV)
            {
                float2 mapped;

                if (_UseMirrorTiling > 0.5)
                {
                    mapped.x = abs(frac(unwrappedUV.x * 0.5) * 2.0 - 1.0);
                }
                else
                {
                    mapped.x = frac(unwrappedUV.x);
                }

                if (_ClampVertical > 0.5)
                {
                    mapped.y = saturate(unwrappedUV.y);
                }
                else
                {
                    mapped.y = frac(unwrappedUV.y);
                }

                return mapped;
            }

            fixed4 SampleTextureSeamless(float2 staticUV, float2 motionUV, float motionFactor)
            {
                float2 dx = ddx(motionUV);
                float2 dy = ddy(motionUV);

                float blurOffset = motionFactor * _MotionBlurStrength;
                float2 mappedMotionUV = GetMappedUV(motionUV);
                float2 staticMappedUV = GetMappedUV(staticUV);

                fixed4 baseCheck = tex2Dgrad(_MainTex, staticMappedUV, dx, dy);
                float2 finalSampleUV = (baseCheck.a > 0.05) ? mappedMotionUV : staticMappedUV;

                float2 edgeMask2D = smoothstep(0.0, _ClampPadding + 0.001, finalSampleUV) * 
                                    smoothstep(1.0, 1.0 - _ClampPadding - 0.001, finalSampleUV);
                float edgeFade = edgeMask2D.x * edgeMask2D.y;

                fixed4 sampledCol;
                if (abs(blurOffset) < 0.0001)
                {
                    sampledCol = SampleWithExpandedNeighborFill(finalSampleUV, dx, dy);
                }
                else
                {
                    fixed4 colorSum = fixed4(0, 0, 0, 0);
                    int samples = clamp(_BlurSamples, 3, 9);
                    float stepSize = blurOffset / (float)(samples - 1);

                    for (int i = 0; i < samples; i++)
                    {
                        float offset = ((float)i - ((float)(samples - 1) * 0.5)) * stepSize;
                        float2 sampleMotionUnwrapped = float2(motionUV.x + offset, motionUV.y);
                        float2 sampleMapped = GetMappedUV(sampleMotionUnwrapped);
                        
                        colorSum += SampleWithExpandedNeighborFill(sampleMapped, dx, dy);
                    }
                    sampledCol = colorSum / (float)samples;
                }

                sampledCol.a *= edgeFade * baseCheck.a;
                return sampledCol;
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

                // Apply material inspector Tiling & Offset to the texture coordinates
                float2 tiledUV = (uv * _ScrollTex_ST.xy) + _ScrollTex_ST.zw;

                float splitAxis = uv.y;
                float seamAxis  = uv.x;

                float halfWidth = _SplitZoneWidth * 0.5;
                float zoneMin = _SplitZoneCenter - halfWidth;
                float zoneMax = _SplitZoneCenter + halfWidth;

                float zoneMask = smoothstep(zoneMin - _ZoneFeather, zoneMin + _ZoneFeather, splitAxis) *
                                 smoothstep(zoneMax + _ZoneFeather, zoneMax - _ZoneFeather, splitAxis);

                float totalSegments = max(1.0, _LayerCount);
                float rawLayer = clamp(splitAxis, 0.0, 0.9999) * totalSegments;

                float baseIndex = floor(rawLayer);
                float fracPos = frac(rawLayer);

                float layerA = baseIndex;
                float layerB = min(baseIndex + 1.0, totalSegments - 1.0);

                float factorA = GetSafeLayerParallax(layerA);
                float factorB = GetSafeLayerParallax(layerB);

                float autoScrollX = _Time.y * _AutoScrollSpeed;
                float autoScrollY = _Time.y * _AutoScrollSpeedY;

                // Combine Tiling/Offset with parallax and auto-scrolling
                float2 continuousUVA = float2(tiledUV.x + (_CamPosX * factorA) + (autoScrollX * factorA), tiledUV.y + autoScrollY);
                float2 continuousUVB = float2(tiledUV.x + (_CamPosX * factorB) + (autoScrollX * factorB), tiledUV.y + autoScrollY);

                float effectiveSpeedA = factorA * (_AutoScrollSpeed + 1.0);
                float effectiveSpeedB = factorB * (_AutoScrollSpeed + 1.0);

                fixed4 colA = SampleTextureSeamless(uv, continuousUVA, effectiveSpeedA);
                fixed4 colB = SampleTextureSeamless(uv, continuousUVB, effectiveSpeedB);

                float feather = clamp(_EdgeFeather, 0.01, 0.49);
                float blendFactor = smoothstep(1.0 - feather, 1.0, fracPos);
                fixed4 splitLayerColor = lerp(colA, colB, blendFactor);

                float2 continuousBaseUV = float2(tiledUV.x + (_CamPosX * _BaseParallax) + (autoScrollX * _BaseParallax), tiledUV.y + autoScrollY);
                fixed4 baseColor = SampleTextureSeamless(uv, continuousBaseUV, _BaseParallax * (_AutoScrollSpeed + 1.0));

                fixed4 finalColor = lerp(baseColor, splitLayerColor, zoneMask);

                finalColor *= IN.color;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}