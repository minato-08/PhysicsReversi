// Highlights the on-screen outline of a convex object without enlarging it.
// Used as three materials on one renderer, drawn in render-queue order:
//   Mode 0  mask : the mesh pulled inward by _Width pixels, stencil only
//   Mode 1  edge : the full mesh wherever the mask is absent -> a band just inside the silhouette
//   Mode 2  fill : the full mesh as a faint tint; also clears the stencil
Shader "PhysicsReversi/SilhouetteHighlight"
{
    Properties
    {
        _EdgeColor ("Edge color", Color) = (1, .85, .2, 1)
        _FillColor ("Fill color", Color) = (1, .85, .2, .12)
        _Width ("Edge width (pixels at 720p)", Float) = 4
        [Enum(Mask,0,Edge,1,Fill,2)] _Mode ("Mode", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil compare", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil pass", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
        _ColorMask ("Color mask", Float) = 15
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            // Wins against the stone surface it lies on.
            Offset -1, -1
            ColorMask [_ColorMask]
            Stencil { Ref 1 Comp [_StencilComp] Pass [_StencilPass] }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EdgeColor;
                half4 _FillColor;
                float _Width;
                float _Mode;
                float _StencilComp;
                float _StencilPass;
                float _ZTest;
                float _ColorMask;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                if (_Mode < .5)
                {
                    // normalOS is not a lighting normal: xz is the radial part and y the axial part of
                    // the direction "away from the object". They are normalized separately in world
                    // space so the stone's flattened scale does not skew the result.
                    float3 radial = mul((float3x3)UNITY_MATRIX_M, float3(input.normalOS.x, 0, input.normalOS.z));
                    float3 axial = mul((float3x3)UNITY_MATRIX_M, float3(0, input.normalOS.y, 0));
                    float3 outward = radial / max(length(radial), 1e-6) * step(1e-6, length(radial))
                                   + axial / max(length(axial), 1e-6) * step(1e-6, length(axial));
                    outward /= max(length(outward), 1e-6);
                    // Sideways share of the direction as seen by the camera: 1 on the silhouette, 0 facing the viewer.
                    float sideways = length(mul((float3x3)UNITY_MATRIX_V, outward).xy);
                    float2 screen = mul((float3x3)UNITY_MATRIX_VP, outward).xy * _ScreenParams.xy;
                    float2 direction = screen / max(length(screen), 1e-6) * sideways;
                    float pixels = _Width * _ScreenParams.y / 720.0;
                    output.positionCS.xy -= direction * pixels * 2.0 / _ScreenParams.xy * output.positionCS.w;
                }
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Mode > 1.5 ? _FillColor : _EdgeColor;
            }
            ENDHLSL
        }
    }
}
