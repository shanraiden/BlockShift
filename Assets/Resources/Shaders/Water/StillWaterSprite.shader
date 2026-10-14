Shader "Custom/2D/StillWaterSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Water Tint / Deep Color", Color) = (0.1, 0.4, 0.6, 0.8)
        _ShallowColor ("Shallow / Surface Color", Color) = (0.3, 0.7, 0.9, 0.9)

        [Header(Ripples and Distortion)]
        _RippleSpeed ("Ripple Animation Speed", Range(0.1, 5.0)) = 0.8
        _RippleScale ("Ripple Scale / Density", Range(5.0, 80.0)) = 25.0
        _DistortionStrength ("Surface Distortion Amount", Range(0.0, 0.05)) = 0.008

        [Header(Surface Specular Highlight)]
        _HighlightColor ("Surface Highlight Color", Color) = (1, 1, 1, 0.6)
        _HighlightThreshold ("Highlight Band Thickness", Range(0.5, 0.99)) = 0.85

        [Header(Water Surface Level)]
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
            float _RippleSpeed;
            float _RippleScale;
            float _DistortionStrength;
            float _HighlightThreshold;
            float _SurfaceFeather;

            // Pseudo-random hash
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth 2D Value Noise for calm water motion
            float ValueNoise2D(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                // Smoothstep cubic interpolation
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Dual-layer layered wave animation
            float WaterCausticRipples(float2 uv)
            {
                float time = _Time.y * _RippleSpeed;

                // Slow horizontal and vertical floating vectors
                float noise1 = ValueNoise2D(uv * _RippleScale + float2(time * 0.5, time * 0.3));
                float noise2 = ValueNoise2D(uv * (_RippleScale * 1.3) - float2(time * 0.4, time * 0.6));

                return (noise1 + noise2) * 0.5;
            }

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

                // 1. Calculate dual-layer ripple noise
                float ripple = WaterCausticRipples(uv);

                // 2. Apply calm, horizontal-biased distortion to the sprite texture UVs
                float2 distortedUV = uv;
                distortedUV.x += (ripple - 0.5) * _DistortionStrength;
                distortedUV.y += (ripple - 0.5) * (_DistortionStrength * 0.5);

                // 3. Sample main sprite texture with distorted coordinates
                fixed4 spriteTex = tex2D(_MainTex, distortedUV);

                // 4. Vertical depth color gradient (Shallow at top Y=1, Deep at bottom Y=0)
                float depthFactor = 1.0 - uv.y;
                fixed4 waterGradient = lerp(_ShallowColor, _Color, depthFactor);

                // 5. Combine texture color with depth gradient & caustics
                fixed4 finalColor = spriteTex * waterGradient;

                // Add gentle caustic highlights where noise overlaps
                if (ripple > _HighlightThreshold)
                {
                    float highlightMask = smoothstep(_HighlightThreshold, 1.0, ripple);
                    finalColor.rgb = lerp(finalColor.rgb, _HighlightColor.rgb, highlightMask * _HighlightColor.a);
                }

                // 6. Feather top surface edge to prevent razor-sharp cuts
                float surfaceAlpha = smoothstep(1.0, 1.0 - _SurfaceFeather, uv.y);
                finalColor.a *= surfaceAlpha * IN.color.a;

                // Apply sprite tint & premultiplied alpha for Unity Sprites
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}