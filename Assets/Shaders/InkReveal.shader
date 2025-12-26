Shader "Custom/InkReveal"
{
    Properties
    {
        // Keep the original ShaderGraph property names so existing materials can be swapped with minimal rework.
        _Vector2("Vector2", Vector) = (1, 1, 0, 0)
        _Float("Float", Float) = 1.5
        _Vector2_1("Vector2 (1)", Vector) = (0, 0, 0, 0)
        _Color("Color", Color) = (0, 0, 0, 0)
        _("\u6d41\u901f", Vector) = (0.005, 0, 0, 0)

        [NoScaleOffset]_SampleTexture2D_36f8206b9c2a4194a8ef868b60ab8654_Texture_1_Texture2D("Texture2D", 2D) = "white" {}

        // Painting reveal control (driven by PaintedPath via progressProperty)
        _Progress("Reveal Progress", Range(0, 1)) = 0
        _RevealEdgeWidth("Reveal Edge Width", Range(0.001, 0.3)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float2 _Vector2;
                float _Float;
                float2 _Vector2_1;
                float4 _Color;
                float2 _;
                float _Progress;
                float _RevealEdgeWidth;
            CBUFFER_END

            TEXTURE2D(_SampleTexture2D_36f8206b9c2a4194a8ef868b60ab8654_Texture_1_Texture2D);
            SAMPLER(sampler_SampleTexture2D_36f8206b9c2a4194a8ef868b60ab8654_Texture_1_Texture2D);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
            };

            float2 RotateRadians(float2 uv, float2 center, float radians)
            {
                float s = sin(radians);
                float c = cos(radians);

                uv -= center;
                float2x2 m = float2x2(c, -s, s, c);
                uv = mul(uv, m);
                uv += center;
                return uv;
            }

            float Hash21(float2 p)
            {
                // Deterministic hash, good enough for stylized ink noise.
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                // Smooth interpolation
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i + float2(0, 0));
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                float x1 = lerp(a, b, f.x);
                float x2 = lerp(c, d, f.x);
                return lerp(x1, x2, f.y);
            }

            float SimpleNoise3Octaves(float2 uv, float scale)
            {
                // Match the generated graph's 3 octaves structure.
                float outN = 0.0;

                float freq0 = pow(2.0, 0.0);
                float amp0  = pow(0.5, 3.0 - 0.0);
                outN += ValueNoise(uv * (scale / freq0)) * amp0;

                float freq1 = pow(2.0, 1.0);
                float amp1  = pow(0.5, 3.0 - 1.0);
                outN += ValueNoise(uv * (scale / freq1)) * amp1;

                float freq2 = pow(2.0, 2.0);
                float amp2  = pow(0.5, 3.0 - 2.0);
                outN += ValueNoise(uv * (scale / freq2)) * amp2;

                return outN;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, float4(1, 0, 0, 1));

                output.positionHCS = posInputs.positionCS;
                output.uv = input.uv;
                output.normalWS = normalInputs.normalWS;
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // --- Ink technique extracted from ShaderGraph-generated code ---
                // 1) Animated noise in object space
                float t = _TimeParameters.x;
                float2 flow = t.xx * _;
                float2 flowRot = RotateRadians(flow, float2(0.5, 0.5), 1.2);
                float2 noiseUV = input.positionOS.xy + flowRot;

                float n = SimpleNoise3Octaves(noiseUV, 500.0);
                float inkBase = pow(n, 5.0);

                // 2) Texture-based alpha mask (tiling + offset + rotation)
                float2 uv2 = uv * _Vector2 + _Vector2_1;
                uv2 = RotateRadians(uv2, float2(0.5, 0.5), _Float);
                float4 tex = SAMPLE_TEXTURE2D(_SampleTexture2D_36f8206b9c2a4194a8ef868b60ab8654_Texture_1_Texture2D,
                                              sampler_SampleTexture2D_36f8206b9c2a4194a8ef868b60ab8654_Texture_1_Texture2D,
                                              uv2);

                float4 oneMinus = 1.0 - tex;
                float4 maskPow = pow(oneMinus, 2.0);
                float4 lerpBW = lerp(float4(0, 0, 0, 0), float4(1, 1, 1, 1), maskPow);

                float upFactor = saturate(dot(normalize(input.normalWS), float3(0, 1, 0)));
                float alphaInk = (lerpBW * upFactor.xxxx).x;

                // --- UV-Y reveal (PaintedPath drives _Progress) ---
                float threshold = 1.0 - saturate(_Progress);
                float reveal = smoothstep(threshold, threshold + max(_RevealEdgeWidth, 1e-5), uv.y);

                float alpha = alphaInk * reveal;

                float3 baseCol = (inkBase.xxx);
                // Allow tinting if desired; default _Color from graph is black so this preserves the look.
                baseCol *= max(_Color.rgb, 0.0.xxx);

                return half4(baseCol, alpha);
            }
            ENDHLSL
        }
    }
}
