Shader "AVM/PSX_URP_Lit"
{
    Properties
    {
        [Header(Albedo)]
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color (HDR)", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1

        [Header(Metallic)]
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _ReflectionTint ("Reflection Tint", Color) = (1,1,1,1)
        _ReflectionStrength ("Reflection Strength", Range(0, 2)) = 1

        [Header(Ambient and Rim)]
        _AmbientColor ("Ambient Tint", Color) = (1,1,1,1)
        _AmbientBoost ("Ambient Boost", Range(0, 3)) = 1
        [HDR] _RimColor ("Rim Color (HDR)", Color) = (0.2, 0.3, 0.4, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.4

        [Header(PSX Effects)]
        _JitterAmount ("Vertex Jitter", Range(0, 0.05)) = 0.008
        [Toggle] _Affine ("Affine Mapping", Float) = 1
        [Toggle] _FlatShading ("Flat Shading", Float) = 1

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float4 _EmissionColor;
                float  _EmissionIntensity;
                float  _Metallic;
                float  _Smoothness;
                float4 _ReflectionTint;
                float  _ReflectionStrength;
                float4 _AmbientColor;
                float  _AmbientBoost;
                float4 _RimColor;
                float  _RimPower;
                float  _RimIntensity;
                float  _JitterAmount;
                float  _Affine;
                float  _FlatShading;
                float  _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float3 uvAffine    : TEXCOORD5;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float4 clipPos = TransformObjectToHClip(IN.positionOS.xyz);

                if (_JitterAmount > 0.00001)
                {
                    float2 snapGrid = _JitterAmount * clipPos.w;
                    clipPos.xy = floor(clipPos.xy / snapGrid + 0.5) * snapGrid;
                }

                OUT.positionCS  = clipPos;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.fogFactor   = ComputeFogFactor(clipPos.z);
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);

                float w = max(clipPos.w, 0.0001);
                OUT.uvAffine = float3(OUT.uv * w, w);
                return OUT;
            }

            half4 frag(Varyings IN, half facing : VFACE) : SV_Target
            {
                float2 uv = IN.uv;
                if (_Affine > 0.5) uv = IN.uvAffine.xy / IN.uvAffine.z;

                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb * _Color.rgb;

                // ---- NORMAL ----
                half3 normalWS = normalize(IN.normalWS);
                if (_FlatShading > 0.5)
                    normalWS = normalize(cross(ddx(IN.positionWS), ddy(IN.positionWS)) * facing);
                else
                    normalWS *= facing; // flip for back-face viewing

                half3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);

                // ---- MAIN LIGHT ----
                Light mainLight = GetMainLight(IN.shadowCoord);
                half NdotL = saturate(dot(normalWS, mainLight.direction));

                // Тень применяем ТОЛЬКО на фронт-фейсах.
                // Для вывернутых (facing=-1) поверхностей "тень от внешней стороны"
                // ничего не значит — используем полное освещение.
                half shadowFront = mainLight.shadowAttenuation;
                half shadow = facing > 0 ? shadowFront : 1.0;

                half3 directLight = mainLight.color * shadow;

                // ---- PBR ----
                half roughness = 1 - _Smoothness;
                half3 diffuseColor  = albedo * (1 - _Metallic);
                half3 specularColor = lerp(half3(0.04, 0.04, 0.04), albedo, _Metallic);

                half3 H = normalize(mainLight.direction + viewDir);
                half NdotH = saturate(dot(normalWS, H));
                half specPower = exp2(_Smoothness * 10);
                half specularTerm = pow(NdotH, specPower) * (specPower + 8) / 8;
                half3 specular = specularColor * specularTerm * NdotL * directLight;

                // ---- DIFFUSE ----
                half3 diffuse = diffuseColor * NdotL * directLight;

                // ---- AMBIENT FROM ENV (SampleSH) ----
                half3 ambientSH = SampleSH(normalWS) * _AmbientColor.rgb * _AmbientBoost;
                half3 ambient = diffuseColor * ambientSH + specularColor * ambientSH * 0.3;

                // ---- ENV REFLECTION (Reflection Probe / Skybox) ----
                half3 reflectDir = reflect(-viewDir, normalWS);
                half3 envReflection = GlossyEnvironmentReflection(reflectDir, roughness, 1.0);
                envReflection *= _ReflectionTint.rgb * _ReflectionStrength;
                // Отражение видно сильнее у металла и на малых углах
                half NdotV = saturate(dot(normalWS, viewDir));
                half fresnel = pow(1 - NdotV, 4);
                envReflection *= lerp(1.0, _Metallic, 1.0) * lerp(1.0, fresnel, 0.5);

                // ---- RIM LIGHT ----
                half rim = pow(1 - NdotV, _RimPower) * _RimIntensity;
                half3 rimColor = _RimColor.rgb * rim;

                // ---- COMBINE ----
                half3 finalColor = diffuse + specular + ambient + envReflection + rimColor;
                finalColor = MixFog(finalColor, IN.fogFactor);

                // Emission
                half3 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                half3 emission = emissionTex * _EmissionColor.rgb * _EmissionIntensity;
                finalColor += emission;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ===== ShadowCaster =====
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float4 _MainTex_ST;
                float4 _EmissionColor; float _EmissionIntensity;
                float _Metallic; float _Smoothness;
                float4 _ReflectionTint; float _ReflectionStrength;
                float4 _AmbientColor; float _AmbientBoost;
                float4 _RimColor; float _RimPower; float _RimIntensity;
                float _JitterAmount; float _Affine; float _FlatShading; float _Cull;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(IN.normalOS);
                float4 clipPos = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    clipPos.z = min(clipPos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clipPos.z = max(clipPos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = clipPos;
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ===== DepthOnly =====
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float4 _MainTex_ST;
                float4 _EmissionColor; float _EmissionIntensity;
                float _Metallic; float _Smoothness;
                float4 _ReflectionTint; float _ReflectionStrength;
                float4 _AmbientColor; float _AmbientBoost;
                float4 _RimColor; float _RimPower; float _RimIntensity;
                float _JitterAmount; float _Affine; float _FlatShading; float _Cull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float4 clipPos = TransformObjectToHClip(IN.positionOS.xyz);
                if (_JitterAmount > 0.00001)
                {
                    float2 snapGrid = _JitterAmount * clipPos.w;
                    clipPos.xy = floor(clipPos.xy / snapGrid + 0.5) * snapGrid;
                }
                OUT.positionCS = clipPos;
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ===== DepthNormals =====
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float4 _MainTex_ST;
                float4 _EmissionColor; float _EmissionIntensity;
                float _Metallic; float _Smoothness;
                float4 _ReflectionTint; float _ReflectionStrength;
                float4 _AmbientColor; float _AmbientBoost;
                float4 _RimColor; float _RimPower; float _RimIntensity;
                float _JitterAmount; float _Affine; float _FlatShading; float _Cull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float4 clipPos = TransformObjectToHClip(IN.positionOS.xyz);
                if (_JitterAmount > 0.00001)
                {
                    float2 snapGrid = _JitterAmount * clipPos.w;
                    clipPos.xy = floor(clipPos.xy / snapGrid + 0.5) * snapGrid;
                }
                OUT.positionCS = clipPos;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target
            {
                return half4(normalize(IN.normalWS), 0);
            }
            ENDHLSL
        }
    }
}