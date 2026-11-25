// Contact shadow projected onto whatever lies inside a box (a screen-space decal, ART.md "接地影").
// The box is a unit cube placed by ContactShadows. For every pixel the box covers, the shader
// reads the scene depth, rebuilds the world point already drawn there, and darkens it when that
// point is inside the box, faces up, and falls in the disc. So one shadow lies on the table, on
// the top of the board frame and on anything else low enough, with no copies to place or cut.
// It multiplies what is drawn by the shadow tint, the same way ToonLit darkens its own shadow band.
// Needs the camera depth texture (the toon setup turns it on for the game camera).
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
            // The back faces with no depth test: the box still draws when the camera or the
            // scene surface is inside it. The depth texture decides what is shaded.
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowTint;
                half _Amount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The world point already drawn under this pixel.
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(uv);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                // Inside the box: object space -0.5..0.5 on every axis.
                float3 local = TransformWorldToObject(world);
                clip(0.5 - abs(local));

                // Up-facing surfaces only (table, frame top, cell marks), so the sides of the
                // frame and of stones and the figures' shoes standing in the box stay lit.
                // abs: the sign of the screen derivatives flips between platforms.
                float3 normal = normalize(cross(ddy(world), ddx(world)));
                clip(abs(normal.y) - 0.7);

                // The disc, with an edge one pixel soft so it does not stair-step.
                float r = length(local.xz) * 2;
                float edge = max(fwidth(r), 1e-4);
                half inside = 1 - smoothstep(1 - edge, 1, r);
                clip(inside - 0.001);

                half3 multiply = lerp(half3(1, 1, 1), _ShadowTint.rgb, _Amount * inside);
                // Fog fades the shadow out toward no change, not toward the fog colour.
                half fogFactor = ComputeFogFactor(TransformWorldToHClip(world).z);
                multiply = MixFogColor(multiply, half3(1, 1, 1), fogFactor);
                return half4(multiply, 1);
            }
            ENDHLSL
        }
    }
}
