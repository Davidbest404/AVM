Shader "AVM/CRTOscilloscope"
{
    Properties
    {
        [Header(Wave)]
        _WaveColor       ("Wave Color (HDR)", Color) = (0.3, 1.0, 0.4, 1)
        _GhostColor      ("Ghost Color (target)", Color) = (0.3, 0.9, 1.0, 1)
        _WaveFrequency   ("Wave Frequency", Float) = 4
        _WaveAmplitude   ("Wave Amplitude", Range(0, 1)) = 0.25
        _BaseFrequency   ("Base Frequency", Float) = 4
        _BaseAmplitude   ("Base Amplitude", Range(0, 1)) = 0.25
        _PhaseSpeed      ("Phase Speed", Float) = 1
        _LineThickness   ("Line Thickness", Range(0.001, 0.1)) = 0.008
        _GlowPower       ("Glow Power", Float) = 3
        _GhostIntensity  ("Ghost Intensity", Range(0, 3)) = 1.5
        _GhostThickness  ("Ghost Thickness Mult", Range(0.2, 1.5)) = 0.6

        [Header(CRT)]
        _ScanlineIntensity ("Scanlines", Range(0, 1)) = 0.25
        _ScanlineCount     ("Scanline Count", Float) = 320
        _FlickerAmount     ("Flicker", Range(0, 1)) = 0.04
        _Curvature         ("Curvature", Range(0, 0.5)) = 0.12
        _VignettePower     ("Vignette", Range(0, 3)) = 1.4
        _NoiseAmount       ("Noise", Range(0, 1)) = 0.05

        [Header(Grid)]
        _GridColor       ("Grid Color", Color) = (0.1, 0.5, 0.2, 1)
        _GridIntensity   ("Grid Intensity", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv      : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _WaveColor;
                float4 _GhostColor;
                float  _WaveFrequency;
                float  _WaveAmplitude;
                float  _BaseFrequency;
                float  _BaseAmplitude;
                float  _PhaseSpeed;
                float  _LineThickness;
                float  _GlowPower;
                float  _GhostIntensity;
                float  _GhostThickness;
                float  _ScanlineIntensity;
                float  _ScanlineCount;
                float  _FlickerAmount;
                float  _Curvature;
                float  _VignettePower;
                float  _NoiseAmount;
                float4 _GridColor;
                float  _GridIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 CurveUV(float2 uv, float amount)
            {
                uv = uv * 2.0 - 1.0;
                float2 offset = abs(uv.yx) / float2(5.0, 4.0);
                uv = uv + uv * offset * offset * amount;
                return uv * 0.5 + 0.5;
            }

            float DrawWave(float2 uv, float frequency, float amplitude, float phase, float thickness)
            {
                float waveValue = sin(uv.x * frequency * 6.2831 + phase);
                float waveY = 0.5 + waveValue * amplitude * 0.5;
                float dist = abs(uv.y - waveY);

                float core = smoothstep(thickness, 0.0, dist);
                float glow = exp(-dist * _GlowPower * 10.0) * 0.7;
                return saturate(core + glow);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;
                float2 uv = CurveUV(IN.uv, _Curvature);

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0, 0, 0, 1);

                float phase = _PhaseSpeed * time * 6.2831;

                // Призрак — целевая волна, тоньше и другим цветом
                float ghost = DrawWave(
                    uv, _BaseFrequency, _BaseAmplitude, phase,
                    _LineThickness * _GhostThickness);

                // Реальная волна
                float waveReal = DrawWave(
                    uv, _WaveFrequency, _WaveAmplitude, phase,
                    _LineThickness);

                // Сетка
                float gridX = smoothstep(0.9, 1.0, abs(sin(uv.x * 40.0)));
                float gridY = smoothstep(0.9, 1.0, abs(sin(uv.y * 20.0)));
                float gridPattern = saturate(gridX + gridY) * _GridIntensity;

                // Скан-линии, шум, мерцание
                float scan = sin(uv.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                float scanMask = 1.0 - _ScanlineIntensity + _ScanlineIntensity * scan;
                float noise = (hash21(uv * 500.0 + time) - 0.5) * _NoiseAmount;
                float flicker = 1.0 - _FlickerAmount * hash21(float2(time, 0));

                // Сборка
                float3 col = float3(0, 0, 0);
                col += _GridColor.rgb  * gridPattern;
                col += _GhostColor.rgb * ghost * _GhostIntensity;   // ярче
                col += _WaveColor.rgb  * waveReal * 3.0;
                col *= scanMask;
                col *= flicker;
                col += noise * _WaveColor.rgb * 0.3;

                // Виньетка
                float2 centered = uv - 0.5;
                float vig = pow(saturate(1.0 - dot(centered, centered) * _VignettePower * 2.0), 1.5);
                col *= vig;

                float afterglow = smoothstep(0.05, 0.0, abs(uv.y - 0.5)) * 0.1;
                col += _WaveColor.rgb * afterglow;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}