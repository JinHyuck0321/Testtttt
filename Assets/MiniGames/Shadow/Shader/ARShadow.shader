Shader "FX/TransparentShadow"
{
    Properties
    {
        _Intensity("Shadow Intensity", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags{"Queue" = "Transparent+100" "RenderType" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" "IgnoreProjector" = "True"}
        LOD 200
		
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite On
		Cull Off

        Pass
        {
            Name "AR Shadow"
            Tags { "LightMode" = "UniversalForward" }            

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 3.0

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ALPHAPREMULTIPLY_ON

            // -------------------------------------
            // Lightweight Pipeline keywords
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING // v10+ only, renamed from "_MIXED_LIGHTING_SUBTRACTIVE"
			#pragma multi_compile_fog

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex HiddenVertex
            #pragma fragment HiddenFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _Intensity;

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 shadowCoord  : TEXCOORD0;
            };

            Varyings HiddenVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.shadowCoord = GetShadowCoord(vertexInput);
                output.positionCS = vertexInput.positionCS;

                return output;
            }

            half4 HiddenFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half s = MainLightRealtimeShadow(input.shadowCoord);

                return half4(_SubtractiveShadowColor.xyz, (1 - s) * _Intensity);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}