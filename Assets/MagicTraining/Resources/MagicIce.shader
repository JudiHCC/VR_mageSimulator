Shader "MagicTraining/Ice"
{
    Properties
    {
        _Color ("Body (A = opacidad)", Color) = (0.45, 0.75, 1, 0.25)
        _RimColor ("Rim", Color) = (0.75, 0.95, 1, 1)
        _RimPower ("Rim Power", Float) = 2.2
        _Intensity ("Rim Intensity", Float) = 1.6
        _Glow ("Glow", Float) = 0
        _Fade ("Fade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "MagicIce"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RimColor;
                half _RimPower;
                half _Intensity;
                half _Glow;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 vdir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half ndv = saturate(abs(dot(n, vdir)));
                half rim = pow(1.0h - ndv, _RimPower);

                Light ml = GetMainLight();
                float3 h = normalize(ml.direction + vdir);
                half spec = pow(saturate(dot(n, h)), 48.0) * 1.5h;

                half facet = frac(sin(dot(floor(n * 4.0 + 0.5), float3(12.9898, 78.233, 37.719))) * 43758.5453);
                half3 body = _Color.rgb * (0.7h + 0.6h * facet);

                half3 col = body * _Color.a + rim * _RimColor.rgb * _Intensity + spec * _RimColor.rgb + _Glow * _RimColor.rgb * 0.6h;
                half a = saturate(_Color.a + rim * 0.5h + spec * 0.5h + _Glow * 0.2h);
                half k = _Fade * i.color.a;
                col *= i.color.rgb * k;
                a *= k;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
