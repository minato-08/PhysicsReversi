// Flat contact shadow under a stone. Multiplies what is already drawn by the shadow tint, the
// same way ToonLit darkens its own shadow band, so it matches the cel shadows of everything else.
Shader "PhysicsReversi/GroundShadow"
{
    Properties
    {
        _ShadowTint ("Shadow Tint", Color) = (0.6, 0.565, 0.8, 1)
        _Amount ("Amount", Range(0, 1)) = 0.8
        // World XZ rectangle (min x, min z, max x, max z) the shadow is cut to. Used for the copy
        // that lies on top of the board frame; the default lets everything through.
        _ClipRect ("Clip Rect", Vector) = (-100000, -100000, 100000, 100000)
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
                float4 _ClipRect;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; float2 positionXZ : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionXZ = TransformObjectToWorld(input.positionOS.xyz).xz;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(min(input.positionXZ - _ClipRect.xy, _ClipRect.zw - input.positionXZ));
                half3 multiply = lerp(half3(1, 1, 1), _ShadowTint.rgb, _Amount);
                // Fog fades the shadow out toward no change, not toward the fog colour.
                multiply = MixFogColor(multiply, half3(1, 1, 1), input.fogFactor);
                return half4(multiply, 1);
            }
            ENDHLSL
        }
    }
}
