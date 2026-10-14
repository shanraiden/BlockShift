Shader "Custom/2D/SineWaveWaterSprite"
{
Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Water Tint / Deep Color", Color) = (0.1, 0.4, 0.6, 0.8)
        _ShallowColor ("Shallow / Surface Color", Color) = (0.3, 0.7, 0.9, 0.9)

        _WaveSpeed ("Wave Speed", Float) = 2.0
        _WaveScale ("Surface Wave Frequency", Range(1.0, 50.0)) = 15.0
        _WaveAmplitude ("Surface Wave Strength", Range(0.0, 0.05)) = 0.015
        _WaveDirection ("Wave Direction (XY)", Vector) = (1.0, 0.0, 0, 0)
        _SecondaryWaveWeight ("Secondary Cross-Wave Blend", Range(0.0, 1.0)) = 0.5

        _WaterLayers ("Number of Depth Layers", Range(1.0, 8.0)) = 4.0
        _FrequencyDecay ("Deeper Layer Frequency Decay", Range(0.1, 1.0)) = 0.65
        _StrengthDecay ("Deeper Layer Strength Decay", Range(0.1, 1.0)) = 0.50
        _LayerSmoothness ("Layer Edge Smoothing / Blur", Range(0.0, 1.0)) = 0.4

        _HighlightColor ("Surface Highlight Color", Color) = (1, 1, 1, 0.6)
        _HighlightThreshold ("Highlight Band Thickness", Range(0.5, 0.99)) = 0.82

        _SurfaceFeather ("Top Edge Softness", Range(0.001, 0.1)) = 0.02
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
            fixed4 _ShallowColor;
            fixed4 _HighlightColor;
            float _WaveSpeed;
            float _WaveScale;
            float _WaveAmplitude;
            float4 _WaveDirection;
            float _SecondaryWaveWeight;
            float _WaterLayers;
            float _FrequencyDecay;
            float _StrengthDecay;
            float _LayerSmoothness;
            float _HighlightThreshold;
            float _SurfaceFeather;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 1. Calculate depth factor (0 at top, 1 at bottom)
                float depthFactor = 1.0 - uv.y;

                // 2. Smooth layer boundary transition instead of a hard block cutoff
                float scaledDepth = depthFactor * _WaterLayers;
                float layerBase = floor(scaledDepth);
                float layerFrac = frac(scaledDepth);
                
                // Use smoothstep to feather/blur the boundary transition between adjacent layers
                float smoothBlend = smoothstep(0.5 - (_LayerSmoothness * 0.5), 0.5 + (_LayerSmoothness * 0.5), layerFrac);
                float continuousLayer = layerBase + smoothBlend;
                float normalizedLayer = saturate(continuousLayer / max(1.0, _WaterLayers - 1.0));

                // 3. Compute continuous layer-specific frequency and amplitude
                float currentScale = _WaveScale * pow(_FrequencyDecay, normalizedLayer * _WaterLayers * 0.5);
                float currentAmplitude = _WaveAmplitude * pow(_StrengthDecay, normalizedLayer * _WaterLayers * 0.5);

                // 4. Wave directions and phase generation
                float2 dir = normalize(_WaveDirection.xy + float2(0.0001, 0.0001));
                float2 orthoDir = float2(-dir.y, dir.x);

                float phase1 = dot(uv, dir) * currentScale + (_Time.y * _WaveSpeed);
                float phase2 = dot(uv, orthoDir) * (currentScale * 1.2) - (_Time.y * _WaveSpeed * 0.8);

                float sineWave = (sin(phase1) + (sin(phase2) * _SecondaryWaveWeight)) / (1.0 + _SecondaryWaveWeight);

                // 5. Apply smooth distortion to texture coordinates
                float2 distortedUV = uv;
                distortedUV += dir * sineWave * currentAmplitude;

                // 6. Sample sprite texture safely within bounds
                fixed4 spriteTex = tex2D(_MainTex, distortedUV);

                // 7. Vertical depth color gradient
                fixed4 waterGradient = lerp(_ShallowColor, _Color, depthFactor);
                fixed4 finalColor = spriteTex * waterGradient;

                // 8. Surface specular highlight sheen
                float normalizedWave = (sineWave + 1.0) * 0.5;
                float topLayerMask = 1.0 - saturate(normalizedLayer * 1.5);
                
                if (normalizedWave > _HighlightThreshold && topLayerMask > 0.1)
                {
                    float highlightMask = smoothstep(_HighlightThreshold, 1.0, normalizedWave) * topLayerMask;
                    finalColor.rgb = lerp(finalColor.rgb, _HighlightColor.rgb, highlightMask * _HighlightColor.a);
                }

                // 9. Surface edge feathering and alpha blending
                float surfaceAlpha = smoothstep(1.0, 1.0 - _SurfaceFeather, uv.y);
                finalColor.a *= surfaceAlpha * IN.color.a;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}