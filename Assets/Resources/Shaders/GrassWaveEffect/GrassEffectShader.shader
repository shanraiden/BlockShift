Shader "Custom/GrassEffectShader"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        _SpriteHeight ("Sprite Height", Float) = 1.0
        _SpritePivotY ("Sprite Pivot Y", Float) = 0.5

        [Header(Strand Frequency Settings)]
        _StrandFrequency ("Strand Density", Float) = 25.0
        _StrandPhaseOffset ("Individual Strand Offset", Float) = 1.5

        [Header(Layer 1 Base Trunk Sway)]
        _L1_Speed ("L1 Speed", Float) = 2.0
        _L1_Amplitude ("L1 Amplitude", Float) = 0.1
        _L1_Direction ("L1 Dir", Float) = 1.0
        _L1_Power ("L1 Gradient Power", Range(0.5, 4.0)) = 1.0

        [Header(Layer 2 Mid Branch Shear)]
        _L2_Speed ("L2 Speed", Float) = 4.5
        _L2_Amplitude ("L2 Amplitude", Float) = 0.05
        _L2_Direction ("L2 Dir", Float) = -1.0
        _L2_Power ("L2 Gradient Power", Range(1.0, 6.0)) = 2.0
        _L2_StartHeight ("L2 Start Height", Range(0.0, 0.8)) = 0.25

        [Header(Layer 3 Top Tip Swirl)]
        _L3_Speed ("L3 Speed", Float) = 7.0
        _L3_Amplitude ("L3 Amplitude", Float) = 0.03
        _L3_Direction ("L3 Dir", Float) = 1.0
        _L3_Power ("L3 Gradient Power", Range(2.0, 8.0)) = 4.0
        _L3_StartHeight ("L3 Start Height", Range(0.0, 0.9)) = 0.55
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _SpriteHeight;
                float _SpritePivotY;
                
                float _StrandFrequency;
                float _StrandPhaseOffset;

                float _L1_Speed;
                float _L1_Amplitude;
                float _L1_Direction;
                float _L1_Power;

                float _L2_Speed;
                float _L2_Amplitude;
                float _L2_Direction;
                float _L2_Power;
                float _L2_StartHeight;

                float _L3_Speed;
                float _L3_Amplitude;
                float _L3_Direction;
                float _L3_Power;
                float _L3_StartHeight;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                float rawNormalizedY = (input.positionOS.y / _SpriteHeight) + _SpritePivotY;
                float baseHeight = saturate(rawNormalizedY);

                float time = _Time.y;

                // --- STRAND-SPECIFIC PHASE CALCULATION ---
                float strandX = input.positionOS.x * _StrandFrequency;
                float strandPhase = sin(strandX) * _StrandPhaseOffset;

                // LAYER 1: Base Trunk/Stem Wave
                float l1_mask = pow(baseHeight, _L1_Power);
                float l1_wave = sin(time * _L1_Speed + input.positionOS.x) * _L1_Amplitude * _L1_Direction * l1_mask;

                // LAYER 2: Mid-Layer Shear with Per-Strand Variance
                float l2_raw = saturate((baseHeight - _L2_StartHeight) / max(0.001, (1.0 - _L2_StartHeight)));
                float l2_mask = pow(l2_raw, _L2_Power);
                float l2_wave = cos(time * _L2_Speed + strandPhase + input.positionOS.y * 2.0) * _L2_Amplitude * _L2_Direction * l2_mask;

                // LAYER 3: Top Tip Swirl with Independent Strand Flutter
                float l3_raw = saturate((baseHeight - _L3_StartHeight) / max(0.001, (1.0 - _L3_StartHeight)));
                float l3_mask = pow(l3_raw, _L3_Power);
                float l3_wave_x = sin(time * _L3_Speed + (strandX * 2.0) + strandPhase) * _L3_Amplitude * _L3_Direction * l3_mask;
                float l3_wave_y = cos(time * _L3_Speed * 0.8 + strandPhase) * (_L3_Amplitude * 0.5) * l3_mask;

                // Combine total displacement
                float3 totalOffset = float3(0, 0, 0);
                totalOffset.x = l1_wave + l2_wave + l3_wave_x;
                totalOffset.y = l3_wave_y;

                float3 displacedPosition = input.positionOS.xyz + totalOffset;

                output.positionCS = TransformObjectToHClip(displacedPosition);
                output.uv = input.uv;
                output.color = input.color * _Color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return texColor * input.color;
            }
            ENDHLSL
        }
    }
}