Shader "MagicTraining/Water"
{
    Properties
    {
        _Color ("Shallow (A = opacidad)", Color) = (0.15, 0.62, 0.98, 0.78)
        _DeepColor ("Deep (A = opacidad)", Color) = (0.02, 0.16, 0.52, 0.92)
        _FoamColor ("Foam", Color) = (0.95, 0.99, 1, 1)
        _RimColor ("Rim", Color) = (0.55, 0.95, 1, 1)
        _RimPower ("Rim Power", Float) = 2.5
        _FoamStart ("Foam Start (v)", Float) = 0.62
        _ScrollSpeed ("Scroll Speed", Float) = 1.5
        _WaveAmp ("Surface Wobble (m)", Float) = 0.02
        _Spec ("Sun Glints", Float) = 1.6
        _Fade ("Fade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "MagicWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _RimColor;
                half _RimPower;
                half _FoamStart;
                half _ScrollSpeed;
                half _WaveAmp;
                half _Spec;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float ph = dot(posWS.xz, float2(2.7, 3.3)) + posWS.y * 2.1 + _Time.y * 4.0;
                posWS += nWS * (sin(ph) + 0.5 * sin(ph * 2.3 + 1.7)) * _WaveAmp;
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = nWS;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y;
                float2 uv = i.uv;

                float w1 = sin(uv.x * 23.0 + t * 2.1 + sin(uv.y * 9.0 - t * _ScrollSpeed * 3.0) * 1.5);
                float w2 = sin(-uv.x * 17.0 + uv.y * 31.0 - t * _ScrollSpeed * 4.0);
                float w3 = sin(uv.x * 41.0 - uv.y * 13.0 + t * 1.3);
                half caustic = saturate(abs(w1 * w2) * 0.8 + w3 * 0.1);

                float3 vdir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 n = normalize(i.normalWS);
                n = dot(n, vdir) < 0 ? -n : n;
                float3 np = normalize(n + float3(w1 * 0.12, w3 * 0.08, w2 * 0.12));

                Light L = GetMainLight();
                half diff = saturate(dot(np, L.direction) * 0.5 + 0.5);
                float3 hv = normalize(L.direction + vdir);
                half spec = pow(saturate(dot(np, hv)), 96.0) * _Spec;
                half rim = pow(1.0h - saturate(dot(np, vdir)), _RimPower);

                half depthK = saturate(uv.y);
                half3 col = lerp(_DeepColor.rgb, _Color.rgb, depthK) * (0.55h + 0.6h * diff);
                col += caustic * 0.25h * _RimColor.rgb + rim * _RimColor.rgb * 0.9h + spec * L.color;
                half a = lerp(_DeepColor.a, _Color.a, depthK) + rim * 0.25h + spec;

                half foam = smoothstep(_FoamStart, _FoamStart + 0.08, uv.y + (w1 * 0.5 + 0.5) * 0.07 + w3 * 0.02);
                foam = max(foam, smoothstep(0.93, 1.0, caustic) * 0.3h);
                col = lerp(col, _FoamColor.rgb * (0.75h + 0.35h * diff), foam);
                a = lerp(a, _FoamColor.a, foam);

                a *= smoothstep(0.0, 0.06, uv.y);
                a = saturate(a) * _Fade * i.color.a;
                return half4(col * i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}
