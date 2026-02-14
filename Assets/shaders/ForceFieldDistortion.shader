Shader "Custom/ForceFieldDistortion"
{
    Properties
    {
        _Color ("Tint Color", Color) = (0, 0.5, 1, 0.5)
        _RimColor ("Rim Color", Color) = (0, 0.8, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _Distortion ("Distortion Strength", Range(0, 1)) = 0.1
        _Speed ("Animation Speed", Range(0, 5)) = 1
        _BumpMap ("Normal Map (Distortion)", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        
        // GrabPass захватывает то, что за объектом, для искажения
        GrabPass { "_GrabTexture" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 grabPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normal : NORMAL;
                float3 viewDir : TEXCOORD3;
            };

            sampler2D _GrabTexture;
            sampler2D _BumpMap;
            float4 _BumpMap_ST;
            float4 _Color;
            float4 _RimColor;
            float _RimPower;
            float _Distortion;
            float _Speed;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BumpMap);
                
                // Данные для Rim Effect
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Анимация UV
                float2 animatedUV = i.uv + _Time.y * _Speed * 0.1;
                
                // Искажение на основе нормал мапы
                half3 bump = UnpackNormal(tex2D(_BumpMap, animatedUV));
                float2 offset = bump.xy * _Distortion;
                
                // Сэмплим текстуру фона со смещением
                float4 screenColor = tex2Dproj(_GrabTexture, i.grabPos + float4(offset, 0, 0));
                
                // Эффект Френеля (светящиеся края)
                float NdotV = 1.0 - saturate(dot(i.normal, i.viewDir));
                float rim = pow(NdotV, _RimPower);
                float4 rimGlow = _RimColor * rim;

                // Смешиваем фон, тинт и рим-лайт
                return screenColor * (1 - _Color.a) + _Color * _Color.a + rimGlow;
            }
            ENDCG
        }
    }
}