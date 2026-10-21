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

        [Header(Dust and Grime)]
        _DustColor        ("Dust Color", Color) = (0.55, 0.5, 0.45, 1)
        _DustIntensity    ("Dust Intensity", Range(0, 2)) = 0.7
        _DustScale        ("Dust Scale", Range(20, 400)) = 180
        _DustEdgePower    ("Edge Concentration", Range(0.5, 4)) = 2.0
        _SpotIntensity    ("Smudge Intensity", Range(0, 1)) = 0.5
        _ScratchIntensity ("Scratch Intensity", Range(0, 1)) = 0.3

        [Header(Geometry)]
        _BulgeAmount    ("Bulge Amount (neg = dent)", Range(-0.2, 0.2)) = 0.03
        _BulgeCenter    ("Bulge Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _BulgeRadius    ("Bulge Radius (0.1..2)", Range(0.1, 2.0)) = 1.0
        _BulgeSharpness ("Bulge Sharpness (1=linear 2=soft 4=sharp)", Range(0.5, 6.0)) = 2.0
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
                float3 normalOS   : NORMAL;
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
                float4 _DustColor;
                float  _DustIntensity;
                float  _DustScale;
                float  _DustEdgePower;
                float  _SpotIntensity;
                float  _ScratchIntensity;
                float  _BulgeAmount;
                float4 _BulgeCenter;
                float  _BulgeRadius;
                float  _BulgeSharpness;
            CBUFFER_END

            static const float bayer4[16] = {
                0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
               12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
               15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float2 delta = (IN.uv - _BulgeCenter.xy) / max(0.001, _BulgeRadius);
                float r2 = saturate(dot(delta, delta));
                float bulge = pow(saturate(1.0 - r2), _BulgeSharpness);

                float3 posOS = IN.positionOS.xyz;
                posOS += IN.normalOS * bulge * _BulgeAmount;

                OUT.positionHCS = TransformObjectToHClip(posOS);
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

            float fbm3(float2 p)
            {
                float v = 0.0;
                v += hash21(p * 1.0)         * 0.5;
                v += hash21(p * 2.0 + 13.0)  * 0.3;
                v += hash21(p * 4.0 + 37.0)  * 0.2;
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;
                float2 uv = CurveUV(IN.uv, _Curvature);

                float2 screenSize = _ScreenParams.xy;
                float2 pixelUV = floor(uv * screenSize / _PixelSize) * _PixelSize / screenSize;
                float2 waveUV = uv;

                if (pixelUV.x < 0.0 || pixelUV.x > 1.0 || pixelUV.y < 0.0 || pixelUV.y > 1.0)
                    return half4(0, 0, 0, 1);

                float phase = _PhaseSpeed * time * 6.2831;

                float ghost = DrawWave(
                    waveUV, _BaseFrequency, _BaseAmplitude, phase,
                    _LineThickness * _GhostThickness);

                float waveReal = DrawWave(
                    waveUV, _WaveFrequency, _WaveAmplitude, phase,
                    _LineThickness);

                float gridX = smoothstep(0.9, 1.0, abs(sin(pixelUV.x * 40.0)));
                float gridY = smoothstep(0.9, 1.0, abs(sin(pixelUV.y * 20.0)));
                float gridPattern = saturate(gridX + gridY) * _GridIntensity;

                float scan = sin(pixelUV.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                float scanMask = 1.0 - _ScanlineIntensity + _ScanlineIntensity * scan;
                float noise = (hash21(pixelUV * 500.0 + time) - 0.5) * _NoiseAmount;
                float flicker = 1.0 - _FlickerAmount * hash21(float2(time, 0));

                float3 col = float3(0, 0, 0);
                col += _GridColor.rgb  * gridPattern;
                col += _GhostColor.rgb * ghost * _GhostIntensity;
                col += _WaveColor.rgb  * waveReal * 3.0;
                col *= scanMask;
                col *= flicker;
                col += noise * _WaveColor.rgb * 0.3;

                float2 centered = pixelUV - 0.5;
                float vig = pow(saturate(1.0 - dot(centered, centered) * _VignettePower * 2.0), 1.5);
                col *= vig;

                float afterglow = smoothstep(0.05, 0.0, abs(pixelUV.y - 0.5)) * 0.1;
                col += _WaveColor.rgb * afterglow;

                if (_DustIntensity > 0.001)
                {
                    float2 cv = pixelUV - 0.5;
                    float edgeRaw = saturate(dot(cv, cv) * 2.5);
                    float edgeFactor = pow(edgeRaw, _DustEdgePower);

                    float2 dustUV = pixelUV * _DustScale + float2(time * 0.03, time * 0.02);
                    float fineDustNoise = hash21(dustUV);
                    float fineDust = smoothstep(0.72, 0.95, fineDustNoise);
                    float microDust = smoothstep(0.85, 0.98, hash21(dustUV * 2.3 + 7.0));
                    float dustMask = (fineDust * 0.7 + microDust * 0.3);

                    float2 spotUV = pixelUV * _DustScale * 0.15;
                    float spotNoise = fbm3(spotUV);
                    float spots = smoothstep(0.62, 0.88, spotNoise) * _SpotIntensity;

                    float colID = floor(pixelUV.x * 80.0);
                    float colRandom = hash21(float2(colID, 7.0));
                    float isScratchColumn = step(0.92, colRandom);
                    float withinColumn = frac(pixelUV.x * 80.0);
                    float scratchLine = smoothstep(0.45, 0.55, withinColumn) * (1.0 - smoothstep(0.55, 0.65, withinColumn));
                    float scratchNoise = hash21(float2(colID, floor(pixelUV.y * 40.0)));
                    float scratchMask = isScratchColumn * scratchLine * step(0.6, scratchNoise) * _ScratchIntensity;

                    float totalGrime = saturate(
                        dustMask  * _DustIntensity * edgeFactor
                        + spots   * _DustIntensity * edgeFactor
                        + scratchMask * _DustIntensity);

                    col = lerp(col, _DustColor.rgb, totalGrime * 0.65);
                }

                int2 px = int2(pixelUV * screenSize / _PixelSize);
                int bx = px.x & 3;
                int by = px.y & 3;
                float dither = bayer4[by * 4 + bx] - 0.5;

                col += dither * _DitherStrength / _ColorLevels;
                col = floor(col * _ColorLevels + 0.5) / _ColorLevels;

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}