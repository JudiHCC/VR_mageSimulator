Shader "MagicTraining/Foliage"
{
    Properties
    {
        _Wind ("Wind", Float) = 1
        _Glow ("Glow (vertex alpha)", Float) = 0.7
        _Front ("Growth Front", Float) = 10000
        _GrowWidth ("Growth Width", Float) = 0.45
        _Wither ("Wither", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Foliage"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Wind;
                half _Glow;
                float _Front;
                float _GrowWidth;
                half _Wither;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half flash : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 org = v.uv1.xyz;
                float birth = v.uv1.w;
                float g = saturate((_Front - birth) / max(_GrowWidth, 0.001));
                float k = g - 1.0;
                float e = (1.0 + 2.70158 * k * k * k + 1.70158 * k * k) * (1.0 - _Wither);
                float3 pos = org + (v.positionOS.xyz - org) * e;

                float h = max(0.0, pos.y - org.y);
                float3 orgWS = TransformObjectToWorld(org);
                float ph = _Time.y * 2.0 + orgWS.x * 1.7 + orgWS.z * 1.3;
                float sway = (sin(ph) + 0.35 * sin(ph * 2.3 + 1.0)) * _Wind * 0.35 * h * h;
                pos += float3(1.0, 0.0, 0.6) * sway;

                o.positionWS = TransformObjectToWorld(pos);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.flash = saturate(1.0 - (_Front - birth) / max(_GrowWidth * 3.0, 0.001)) * step(birth, _Front);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 vdir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                n = dot(n, vdir) < 0 ? -n : n;
                Light L = GetMainLight();
                half ndl = saturate(dot(n, L.direction) * 0.6 + 0.4);
                half3 amb = SampleSH(n);
                half3 albedo = i.color.rgb;
                half3 col = albedo * (L.color * ndl + amb * 0.9);
                col += albedo * _Glow * i.color.a;
                col += (albedo * 1.5 + half3(0.5, 1.0, 0.4) * 0.5) * i.flash;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
