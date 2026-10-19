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

        [Header(PSX)]
        _ColorLevels    ("Color Levels (PSX)", Range(2, 64)) = 16
        _DitherStrength ("Dither Strength", Range(0, 1)) = 1.0
        _PixelSize      ("Pixel Size (PSX)", Range(1, 8)) = 2
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
                float2 uv          : TEXCOORD0;
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
                float  _ColorLevels;
                float  _DitherStrength;
                float  _PixelSize;
            CBUFFER_END

            // 4x4 Bayer matrix, нормализованная в 0..1
            static const float bayer4[16] = {
                0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
               12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
               15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

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

                // Пикселизация (PSX) — квантуем UV до сетки
                float2 screenSize = _ScreenParams.xy;
                float2 pixelUV = floor(uv * screenSize / _PixelSize) * _PixelSize / screenSize;
                // Используем сглаженный UV для волны, но пиксельный для финальной сетки/шума
                float2 waveUV = uv;

                if (pixelUV.x < 0.0 || pixelUV.x > 1.0 || pixelUV.y < 0.0 || pixelUV.y > 1.0)
                    return half4(0, 0, 0, 1);

                float phase = _PhaseSpeed * time * 6.2831;

                // Призрак — целевая волна
                float ghost = DrawWave(
                    waveUV, _BaseFrequency, _BaseAmplitude, phase,
                    _LineThickness * _GhostThickness);

                // Реальная волна
                float waveReal = DrawWave(
                    waveUV, _WaveFrequency, _WaveAmplitude, phase,
                    _LineThickness);

                // Сетка (на пиксельном UV — для чёткого PSX-вида)
                float gridX = smoothstep(0.9, 1.0, abs(sin(pixelUV.x * 40.0)));
                float gridY = smoothstep(0.9, 1.0, abs(sin(pixelUV.y * 20.0)));
                float gridPattern = saturate(gridX + gridY) * _GridIntensity;

                // Скан-линии, шум, мерцание
                float scan = sin(pixelUV.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                float scanMask = 1.0 - _ScanlineIntensity + _ScanlineIntensity * scan;
                float noise = (hash21(pixelUV * 500.0 + time) - 0.5) * _NoiseAmount;
                float flicker = 1.0 - _FlickerAmount * hash21(float2(time, 0));

                // Сборка
                float3 col = float3(0, 0, 0);
                col += _GridColor.rgb  * gridPattern;
                col += _GhostColor.rgb * ghost * _GhostIntensity;
                col += _WaveColor.rgb  * waveReal * 3.0;
                col *= scanMask;
                col *= flicker;
                col += noise * _WaveColor.rgb * 0.3;

                // Виньетка
                float2 centered = pixelUV - 0.5;
                float vig = pow(saturate(1.0 - dot(centered, centered) * _VignettePower * 2.0), 1.5);
                col *= vig;

                float afterglow = smoothstep(0.05, 0.0, abs(pixelUV.y - 0.5)) * 0.1;
                col += _WaveColor.rgb * afterglow;

                // ============ PSX DITHER + QUANTIZE ============
                // Определяем позицию пикселя в матрице Байера
                int2 px = int2(pixelUV * screenSize / _PixelSize);
                int bx = px.x & 3;  // % 4
                int by = px.y & 3;
                float dither = bayer4[by * 4 + bx] - 0.5;

                // Добавляем дизеринг перед квантованием
                col += dither * _DitherStrength / _ColorLevels;

                // Квантование до _ColorLevels уровней
                col = floor(col * _ColorLevels + 0.5) / _ColorLevels;

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}