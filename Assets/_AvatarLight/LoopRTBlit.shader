Shader "Unlit/LoopRTBlit"
{
    Properties
    {
        _MainTex ("Render Texture", 2D) = "white" {}
        _Mip ("Mip Level", Float) = 9
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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
            float _Mip;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            static float uvx[3] = { 0.01, 0.5, 0.99 };
            static float uvy[3] = { 0.05, 0.5, 0.95 };

            float4 frag (v2f i) : SV_Target
            {
                float4 col = fixed4(0, 0, 0, 0);
                uint row = clamp(0, 2, (uint)(i.uv.y*3));
                for (uint j = 0; j < 3; j++) {
                    col += tex2Dlod(_MainTex, float4(float2(uvx[j], uvy[row]), _Mip, _Mip));
                }
                col /= 2.55;
                col *= 1.5 - col * col * 0.5;
                col = pow(col, 1.15);
                return col;
            }
            ENDCG
        }
    }
}
