Shader "AVM/PixelateEffect"
{
    Properties
    {
        _PixelSize ("Pixel Size", Range(1, 16)) = 3
        _ColorLevels ("Color Levels", Range(2, 64)) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "PixelatePass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelSize;
            float _ColorLevels;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 screenSize = _ScreenParams.xy;

                // ===== ПИКСЕЛИЗАЦИЯ =====
                // Округляем UV до сетки, размер которой = _PixelSize пикселей экрана
                float2 blockGrid = screenSize / _PixelSize;
                float2 pixelatedUV = floor(uv * blockGrid) / blockGrid;
                // Сдвиг на пол-пикселя — чтобы блок «центрировался», а не «съезжал» в угол
                pixelatedUV += 0.5 / blockGrid;

                // Читаем цвет из центра блока
                half3 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, pixelatedUV).rgb;

                // ===== ОПЦИОНАЛЬНО: квантование цвета (banding) =====
                col = floor(col * _ColorLevels + 0.5) / _ColorLevels;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}