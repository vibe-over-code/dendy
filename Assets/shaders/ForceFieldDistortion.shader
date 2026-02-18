Shader "Custom/HeavyShield"
{
    Properties
    {
        [HDR] _BaseColor ("Inside Color", Color) = (0, 0.2, 0.5, 0.1)
        [HDR] _LineColor ("Intersection Line", Color) = (0, 1, 2, 5)
        _LineThickness ("Line Thickness", Range(0.01, 1.0)) = 0.1
        _RimPower ("Rim Sharpness", Range(0.5, 10.0)) = 5.0
        
        [Header(Hit Effect)]
        _HitPos ("Hit World Position", Vector) = (0,0,0,0)
        _HitTime ("Hit Time", Float) = -100
        _BrokenScale ("Broken Noise Scale", Float) = 20.0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One One // Additive для максимальной яркости
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normalWS : TEXCOORD3;
            };

            float4 _BaseColor, _LineColor, _HitPos;
            float _LineThickness, _RimPower, _HitTime, _BrokenScale;

            // Функция простого шума для "эффекта поломки"
            float hash(float3 p) {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            Varyings vert (Attributes v) {
                Varyings o;
                o.worldPos = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.worldPos);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.worldPos));
                
                // 1. ЧЕТКАЯ ЛИНИЯ ГРАНИЦЫ
                float rawDepth = SampleSceneDepth(uv);
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                float thisZ = i.positionCS.w;
                float diff = sceneZ - thisZ;
                
                // Используем step для резкой линии
                float intersection = 1.0 - smoothstep(0, _LineThickness, diff);
                
                // 2. РЕЗКИЙ RIM (Края сферы)
                float rim = 1.0 - saturate(dot(i.normalWS, viewDir));
                rim = pow(rim, _RimPower);

                // 3. ЭФФЕКТ "ПОЛОМКИ" ПРИ ПОПАДАНИИ
                float timeDist = _Time.y - _HitTime;
                float hitEffect = 0;
                float noise = hash(i.worldPos * _BrokenScale + _Time.y);

                if (timeDist < 0.8) {
                    float distToHit = distance(i.worldPos, _HitPos.xyz);
                    // Расширяющееся кольцо "поломки"
                    float sphereWave = 1.0 - saturate(abs(distToHit - timeDist * 5.0));
                    hitEffect = sphereWave * noise * 10.0 * (1.0 - timeDist);
                }

                // ИТОГОВЫЙ ЦВЕТ
                half4 finalColor = _BaseColor;
                finalColor.rgb += intersection * _LineColor.rgb * 2.0; // Контур ярче
                finalColor.rgb += rim * _LineColor.rgb;
                finalColor.rgb += hitEffect * _LineColor.rgb;

                return half4(finalColor.rgb, 1.0); // В Additive Alpha работает как множитель яркости
            }
            ENDHLSL
        }
    }
}