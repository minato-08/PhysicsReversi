// Pop toy-like cel shader for URP 17 (Unity 6). Main light only.
Shader "PhysicsReversi/ToonLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _ShadowTint ("Shadow Tint", Color) = (0.722, 0.690, 0.878, 1)
        _Bands ("Bands", Range(1, 4)) = 2
        _BandSoftness ("Band Softness", Range(0.001, 0.2)) = 0.03
        _RimColor ("Rim Color (A = strength)", Color) = (1, 1, 1, 0.25)
        _RimPower ("Rim Power", Range(1, 8)) = 3
        _HighlightColor ("Highlight Color (A = strength)", Color) = (1, 1, 1, 0)
        _HighlightSize ("Highlight Size", Range(0.001, 0.5)) = 0.06
        _OutlineColor ("Outline Color", Color) = (0.2, 0.157, 0.29, 1)
        _OutlineWidth ("Outline Width (px)", Range(0, 10)) = 2
        // Draws the outline this far nearer the camera (world units), along the line of sight, so it
        // stays on screen where it is. Without it, the near rim of a thin thing lying on a surface
        // (a stone on the board) loses its outline: the hull there is behind the surface in front.
        _OutlineDepthBias ("Outline Pull Toward Camera (world)", Range(0, 2)) = 0
        _ReceiveAmbient ("Receive Ambient", Range(0, 1)) = 0.6
        // A faint overlay on the caps of a stone that is not confirmed yet. Off at alpha 0.
        _LooseColor ("Loose Overlay Color (A = strength)", Color) = (1, 1, 1, 0)
        [Enum(Plain,0,Stripes,1,Dots,2)] _LoosePattern ("Loose Overlay Pattern", Float) = 1
        _LooseScale ("Loose Overlay Density (repeats per unit)", Range(1, 20)) = 7
        _LoosePulse ("Loose Overlay Blink (per second, 0 = steady)", Range(0, 4)) = 0
        // Per-renderer feedback, driven from script through a MaterialPropertyBlock (ToonFx).
        [HideInInspector] _Pop ("Pop Scale", Vector) = (1, 1, 1, 0)
        [HideInInspector] _FlashColor ("Flash Color (A = amount)", Color) = (1, 1, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Shared by every pass so the SRP Batcher sees one layout.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowTint;
            half _Bands;
            half _BandSoftness;
            half4 _RimColor;
            half _RimPower;
            half4 _HighlightColor;
            half _HighlightSize;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineDepthBias;
            half _ReceiveAmbient;
            float4 _Pop;
            half4 _FlashColor;
            half4 _LooseColor;
            float _LoosePattern;
            float _LooseScale;
            float _LoosePulse;
        CBUFFER_END

        // Squash and stretch for the look only: every pass scales the drawn vertices about the
        // object origin, so the outline and shadow follow while the collider never changes.
        float3 PopOS(float3 positionOS) { return positionOS * _Pop.xyz; }

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        // ---------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                half   capOS      : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(PopOS(input.positionOS.xyz));
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = half3(TransformObjectToWorldNormal(input.normalOS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                output.positionOS = input.positionOS.xyz;
                output.capOS = half(abs(input.normalOS.y));
                return output;
            }

            // Quantize light into _Bands levels (0..1). Thresholds spread over 0.1..0.7.
            half ToonBands(half light)
            {
                half bands = max(round(_Bands), 1.0h);
                if (bands < 1.5h)
                    return 1.0h;

                // At least one screen pixel wide, so a band edge on a small curve (a stone's rim)
                // blends instead of stair-stepping.
                half s = max(_BandSoftness, fwidth(light));
                half denom = max(bands - 2.0h, 1.0h);
                half q = 0.0h;
                UNITY_UNROLL
                for (int k = 1; k <= 3; k++)
                {
                    if (k < bands)
                    {
                        half t = lerp(0.1h, 0.7h, (k - 1) / denom);
                        q += smoothstep(t - s, t + s, light);
                    }
                }
                return q / (bands - 1.0h);
            }

            half4 ToonFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 n = normalize(input.normalWS);
                half3 v = half3(GetWorldSpaceNormalizeViewDir(input.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndotl = saturate(dot(n, mainLight.direction));
                half light = ndotl * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half q = ToonBands(light);

                // Shadow band is base * tint, never black.
                half3 color = albedo.rgb * lerp(_ShadowTint.rgb, half3(1, 1, 1), q) * mainLight.color;

                // Ambient from SH probes.
                color += albedo.rgb * SampleSH(n) * _ReceiveAmbient;

                // Subtle rim, lit side only.
                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower) * _RimColor.a * q;
                color += _RimColor.rgb * rim;

                // A hard-edged glint where the surface mirrors the sun, like glossy plastic.
                // Flat faces (a resting stone's cap) would flash whole, so it stays on curved parts.
                half3 h = normalize(mainLight.direction + v);
                half edge = 1.0h - _HighlightSize;
                half nh = saturate(dot(n, h));
                half aa = max(fwidth(nh), 0.002h);
                half glint = smoothstep(edge - aa, edge + aa, nh);
                glint *= 1.0h - smoothstep(0.996h, 0.9995h, abs(n.y));
                glint *= _HighlightColor.a * mainLight.shadowAttenuation;
                color = lerp(color, _HighlightColor.rgb, glint);

                // The loose overlay, on the flat caps only: plain, diagonal stripes or dots, laid in
                // the stone's own space so it turns with the stone, and optionally blinking.
                if (_LooseColor.a > 0.0h)
                {
                    float2 p = input.positionOS.xz * _LooseScale;
                    half cover = 1.0h;
                    if (_LoosePattern > 0.5 && _LoosePattern < 1.5)
                    {
                        // Half-width stripes: a triangle wave is 0 mid-stripe and 1 mid-gap.
                        float q = (p.x + p.y) * 0.7071;
                        float t = abs(frac(q) - 0.5) * 2.0;
                        float w = max(fwidth(q) * 2.0, 1e-4);
                        cover = half(1.0 - smoothstep(0.5 - w, 0.5 + w, t));
                    }
                    else if (_LoosePattern > 1.5)
                    {
                        float2 c = frac(p) - 0.5;
                        float d = length(c) - 0.24;
                        float w = max(fwidth(d), 1e-4);
                        cover = half(1.0 - smoothstep(-w, w, d));
                    }
                    half blink = _LoosePulse > 0.0 ? half(0.55 + 0.45 * sin(_Time.y * 6.2832 * _LoosePulse)) : 1.0h;
                    color = lerp(color, _LooseColor.rgb, cover * blink * _LooseColor.a * step(0.9h, input.capOS));
                }

                color = lerp(color, _FlashColor.rgb, _FlashColor.a);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Inverted hull outline, constant pixel width.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite On
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // Smoothed normal for the hull (ToonPolishSetup.SmoothOutlines). Boxes and
                // cylinders split their normals at hard edges, so pushing each face along its own
                // normal tore the hull apart and the line floated off the edge. Zero on meshes
                // without it (stones, spheres), which use their own normal.
                float3 outlineOS  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half   fogFactor  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(PopOS(input.positionOS.xyz));
                positionWS += normalize(GetCameraPositionWS() - positionWS) * _OutlineDepthBias;
                float4 positionCS = TransformWorldToHClip(positionWS);
                float3 normalOS = dot(input.outlineOS, input.outlineOS) > .25 ? input.outlineOS : input.normalOS;
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float len = length(normalCS);
                float2 dir = len > 1e-5 ? normalCS / len : float2(0, 0);

                // 1 px = 2 / screen size in NDC; times w to undo the perspective divide.
                float2 pixelToNdc = 2.0 / _ScreenParams.xy;
                positionCS.xy += dir * _OutlineWidth * pixelToNdc * positionCS.w;

                // Width 0: collapse to a degenerate point so nothing rasterizes.
                if (_OutlineWidth <= 0.0)
                    positionCS = float4(0, 0, 0, 0);

                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = MixFog(_OutlineColor.rgb, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            // Directional vs punctual normal bias.
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by URP ShadowUtils.SetupShadowCasterConstantBuffer.
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(PopOS(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(PopOS(input.positionOS.xyz));
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(PopOS(input.positionOS.xyz));
                output.normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(_GBUFFER_NORMALS_OCT)
                float3 normalWS = normalize(input.normalWS);
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
