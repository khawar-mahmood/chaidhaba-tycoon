// Low-poly toon shader: 3 flat tones (light, mid, shadow) from the main light,
// vertex colours as albedo, plus an inverted-hull ink outline of constant pixel width.
Shader "Chai/Toon"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.17,0.118,0.184,1)
        _OutlinePx ("Outline Pixels at 720p", Float) = 2.0
        _Emission ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _OutlineColor;
            float _OutlinePx;
            half4 _Emission;
        CBUFFER_END
        float4 _ChaiShadeColor;
        ENDHLSL

        Pass
        {
            Name "Toon"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; half4 color : COLOR; float fogFactor : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                Light l = GetMainLight();
                float ndl = dot(normalize(i.normalWS), l.direction);
                half3 shade = _ChaiShadeColor.rgb;
                half3 lightc = l.color;
                half3 tone = ndl > 0.42 ? lightc : (ndl > -0.05 ? lerp(shade, lightc, 0.55) : shade);
                half3 albedo = _BaseColor.rgb * i.color.rgb;
                half3 col = albedo * tone + _Emission.rgb;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float3 smoothN : TEXCOORD3; };
            struct Varyings { float4 positionCS : SV_POSITION; float fogFactor : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 n = dot(v.smoothN, v.smoothN) > 0.01 ? v.smoothN : v.normalOS;
                float4 pos = TransformObjectToHClip(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(n);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = length(nCS);
                float2 dir = len > 1e-5 ? nCS / len : float2(0, 0);
                float px = _OutlinePx * (_ScreenParams.y / 720.0);
                pos.xy += dir * px * 2.0 / _ScreenParams.xy * pos.w;
                o.positionCS = pos;
                o.fogFactor = ComputeFogFactor(pos.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }
    }
}
