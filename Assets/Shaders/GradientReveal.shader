Shader "Custom/GradientReveal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Progress ("Reveal Progress", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Progress;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample texture
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // Logic: Reveal from Y=1 down to Y=0
                // If _Progress = 0, threshold = 1. Only Y>=1 visible.
                // If _Progress = 1, threshold = 0. Y>=0 visible.
                float threshold = 1.0 - _Progress;

                // Soft edge or hard cut? Doc implies "Gradient render", maybe alpha fade?
                // "Gradient render present... replace direct display abrupt effect"
                // Let's add a small smoothstep for gradient edge
                float edgeWidth = 0.1;
                float alpha = smoothstep(threshold, threshold + edgeWidth, i.uv.y);
                
                col.a *= alpha;

                return col;
            }
            ENDCG
        }
    }
}
