Shader "Custom/TerrainVertexColor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Smoothness;
                float _Metallic;
            CBUFFER_END

            sampler2D _MainTex;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample texture and multiply by vertex color
                half4 texColor = tex2D(_MainTex, input.uv);
                half4 baseColor = texColor * input.color;

                // Lighting data
                float3 normalWS = normalize(input.normalWS);
                
                // Get main light
                Light mainLight = GetMainLight();
                
                // Simple Lambertian Diffuse
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (NdotL * mainLight.shadowAttenuation);
                
                // Add Ambient (Environment)
                half3 ambient = SampleSH(normalWS);
                
                half3 finalColor = baseColor.rgb * (lighting + ambient);
                
                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }
    }
}
