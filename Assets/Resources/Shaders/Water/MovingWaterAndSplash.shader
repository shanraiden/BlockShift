Shader "Custom/2D/SineWaveWaterSprite"
{
   Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Color and Layers)]
        _Color ("Water Tint / Deep Color", Color) = (0.1, 0.4, 0.6, 0.8)
        _ShallowColor ("Shallow / Surface Color", Color) = (0.3, 0.7, 0.9, 0.9)
        _WaterLayers ("Number of Depth Layers", Range(1.0, 8.0)) = 4.0

        [Header(Flow Control)]
        _ScrollSpeed ("Texture Scroll Speed (XY)", Vector) = (0.05, 0.0, 0, 0)
        _Tiling ("Texture Tiling (XY)", Vector) = (2.0, 2.0, 0, 0)
        _ParallaxIntensity ("Parallax Layer Speed Scale", Range(0.0, 1.0)) = 0.2
        _LayerSmoothness ("Layer Edge Smoothing / Blur", Range(0.0, 1.0)) = 0.4

        [Header(Wave Control)]
        _BaseSpeed ("Wave Speed", Float) = 2.0
        _WaveScale ("Surface Wave Frequency", Range(1.0, 50.0)) = 15.0
        _WaveAmplitude ("Surface Wave Strength", Range(0.0, 0.05)) = 0.015
        _WaveDirection ("Wave Direction (XY)", Vector) = (1.0, 0.0, 0, 0)
        _SecondaryWaveWeight ("Secondary Cross-Wave Blend", Range(0.0, 1.0)) = 0.5
        [Toggle] _ParallaxDirection ("Invert Parallax Order", Float) = 0.0

        [Header(Highlights and Edges)]
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
            float4 _Tiling;
            float4 _ScrollSpeed;
            float _ParallaxIntensity;
            float _LayerSmoothness;
            float _BaseSpeed;
            float _WaveScale;
            float _WaveAmplitude;
            float4 _WaveDirection;
            float _SecondaryWaveWeight;
            float _WaterLayers;
            float _ParallaxDirection;
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

                // 1. Calculate depth factor (0 at top surface, 1 at deep bottom)
                float depthFactor = 1.0 - uv.y;

                // 2. Compute adjacent depth layers for smooth cross-fading (Blur)
                float scaledDepth = depthFactor * _WaterLayers;
                float layerA = floor(scaledDepth);
                float layerB = min(_WaterLayers - 1.0, layerA + 1.0);
                float layerFrac = frac(scaledDepth);

                // Smooth blend factor controlled by _LayerSmoothness
                float blurWindow = max(0.001, _LayerSmoothness * 0.5);
                float blendFactor = smoothstep(0.5 - blurWindow, 0.5 + blurWindow, layerFrac);

                // Normalized layer indices
                float normA = saturate(layerA / max(1.0, _WaterLayers - 1.0));
                float normB = saturate(layerB / max(1.0, _WaterLayers - 1.0));
                float smoothedNormLayer = lerp(normA, normB, blendFactor);

                // 3. Parallax Order Logic for both layers
                float pFactorA = lerp(normA, 1.0 - normA, step(0.5, _ParallaxDirection));
                float pFactorB = lerp(normB, 1.0 - normB, step(0.5, _ParallaxDirection));

                // 4. Uniform Layer Speeds (Distortion-free)
                float speedA = _BaseSpeed * (1.0 - (pFactorA * _ParallaxIntensity));
                float speedB = _BaseSpeed * (1.0 - (pFactorB * _ParallaxIntensity));
                float2 scrollA = _ScrollSpeed.xy * (1.0 - (pFactorA * _ParallaxIntensity));
                float2 scrollB = _ScrollSpeed.xy * (1.0 - (pFactorB * _ParallaxIntensity));

                // 5. Wave directions and phase generation for Layer A and Layer B
                float2 dir = normalize(_WaveDirection.xy + float2(0.0001, 0.0001));
                float2 orthoDir = float2(-dir.y, dir.x);

                // Layer A Waves
                float phase1A = dot(uv, dir) * _WaveScale + (_Time.y * speedA);
                float phase2A = dot(uv, orthoDir) * (_WaveScale * 1.2) - (_Time.y * speedA * 0.8);
                float sineWaveA = (sin(phase1A) + (sin(phase2A) * _SecondaryWaveWeight)) / (1.0 + _SecondaryWaveWeight);
                float2 waveOffsetA = float2(sin(phase1A) + cos(phase2A * 0.7), cos(phase1A * 0.8) + sin(phase2A)) * _WaveAmplitude;

                // Layer B Waves
                float phase1B = dot(uv, dir) * _WaveScale + (_Time.y * speedB);
                float phase2B = dot(uv, orthoDir) * (_WaveScale * 1.2) - (_Time.y * speedB * 0.8);
                float sineWaveB = (sin(phase1B) + (sin(phase2B) * _SecondaryWaveWeight)) / (1.0 + _SecondaryWaveWeight);
                float2 waveOffsetB = float2(sin(phase1B) + cos(phase2B * 0.7), cos(phase1B * 0.8) + sin(phase2B)) * _WaveAmplitude;

                // 6. Apply Tiling, Scrolling, and Wave Distortion independently for both layers
                float2 tiledUV = uv * _Tiling.xy;
                float2 flowUV_A = tiledUV + (scrollA * _Time.y) + waveOffsetA;
                float2 flowUV_B = tiledUV + (scrollB * _Time.y) + waveOffsetB;

                // 7. Sample and smooth-blend textures and waves between layers
                fixed4 spriteTexA = tex2D(_MainTex, flowUV_A);
                fixed4 spriteTexB = tex2D(_MainTex, flowUV_B);
                fixed4 spriteTex = lerp(spriteTexA, spriteTexB, blendFactor);

                float sineWave = lerp(sineWaveA, sineWaveB, blendFactor);

                // 8. Vertical depth color gradient
                float gradientFactor = lerp(smoothedNormLayer, 1.0 - smoothedNormLayer, step(0.5, _ParallaxDirection));
                fixed4 waterGradient = lerp(_ShallowColor, _Color, gradientFactor);
                fixed4 finalColor = spriteTex * waterGradient;

                // 9. Surface specular highlight sheen
                float normalizedWave = (sineWave + 1.0) * 0.5;
                float topLayerMask = 1.0 - saturate(normA * 1.5);
                
                if (normalizedWave > _HighlightThreshold && topLayerMask > 0.1)
                {
                    float highlightMask = smoothstep(_HighlightThreshold, 1.0, normalizedWave) * topLayerMask;
                    finalColor.rgb = lerp(finalColor.rgb, _HighlightColor.rgb, highlightMask * _HighlightColor.a);
                }

                // 10. Surface edge feathering and alpha blending
                float surfaceAlpha = smoothstep(1.0, 1.0 - _SurfaceFeather, uv.y);
                finalColor.a *= surfaceAlpha * IN.color.a;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}
