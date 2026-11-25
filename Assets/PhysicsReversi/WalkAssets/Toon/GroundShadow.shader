// Flat contact shadow under a stone. Multiplies what is already drawn by the shadow tint, the
// same way ToonLit darkens its own shadow band, so it matches the cel shadows of everything else.
Shader "PhysicsReversi/GroundShadow"
{
    Properties
    {
        _ShadowTint ("Shadow Tint", Color) = (0.6, 0.565, 0.8, 1)
        _Amount ("Amount", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GroundShadow"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowTint;
                half _Amount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 multiply = lerp(half3(1, 1, 1), _ShadowTint.rgb, _Amount);
                // Fog fades the shadow out toward no change, not toward the fog colour.
                multiply = MixFogColor(multiply, half3(1, 1, 1), input.fogFactor);
                return half4(multiply, 1);
            }
            ENDHLSL
        }
    }
}
