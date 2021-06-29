Shader "Custom/CamRig/CameraBox"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                uint id : SV_VertexID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 color : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float3 hue_to_rgb(float H)
            {
                float R = abs(H * 6 - 3) - 1;
                float G = 2 - abs(H * 6 - 2);
                float B = 2 - abs(H * 6 - 4);
                return saturate(float3(R,G,B));
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = hue_to_rgb(frac(v.id * 0.08f + _Time.x*2));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(i.color*0.1, 1);
            }
            ENDCG
        }

        Pass
        {
            Cull Off

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

            float3 get_camera_pos() {
                float3 worldCam;
                worldCam.x = unity_CameraToWorld[0][3];
                worldCam.y = unity_CameraToWorld[1][3];
                worldCam.z = unity_CameraToWorld[2][3];
                return worldCam;
            }
            static float3 camera_pos = get_camera_pos();
            static bool isInMirror = UNITY_MATRIX_P._31 != 0 || UNITY_MATRIX_P._32 != 0;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;

                float3 maxP = float3(1, 1, 1);
                float3 minP = float3(-1, -1, -1);
                float3 cam = mul(unity_WorldToObject, float4(camera_pos, 1)).xyz;
                bool within = cam.x < maxP.x && cam.x > minP.x &&
                              cam.y < maxP.y && cam.y > minP.y &&
                              cam.z < maxP.z && cam.z > minP.z;

                if (isInMirror || !within) {
                    o.vertex = float4(0, 0, 0, 0);
                    o.uv = float2(0, 0);
                    return o;
                }

                #ifdef UNITY_UV_STARTS_AT_TOP
                v.uv.y = 1-v.uv.y;
                #endif
                o.vertex = float4(v.uv * 2 - 1, 1, 1);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.x = 1 - o.uv.x;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
