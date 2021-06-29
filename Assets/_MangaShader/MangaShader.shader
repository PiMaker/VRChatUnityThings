Shader "_pi_/MangaShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [NoScaleOffset] _ShadowTex ("Shadow", 2D) = "white" {}

        [Toggle(GEOM_TYPE_MESH)] _InvertColors ("Invert Output Color", Float) = 0
        _DarkenColors ("Darken Output Color", Range(0, 1)) = 0

        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 1.0
        _ShadowOffset ("Shadow Offset", Range(0, 0.1)) = 0.02

        [NoScaleOffset] _ShadowDistortion ("Shadow Distortion", 2D) = "black" {}
        _DistortionIntensity ("Shadow Distortion Intensity", Range(0, 0.1)) = 1.0

        _ShadowNoise ("Shadow Noise", 2D) = "white" {}
        _NoiseIntensity ("Shadow Noise Intensity", Range(0, 1)) = 0.5

        _ManualShadow1 ("Manual Shadow 1", Vector) = (0, 0, 0, 0)
        _ManualShadow2 ("Manual Shadow 2", Vector) = (0, 0, 0, 0)
        _ManualShadow3 ("Manual Shadow 3", Vector) = (0, 0, 0, 0)
        _ManualShadow4 ("Manual Shadow 4", Vector) = (0, 0, 0, 0)
        _ManualShadow5 ("Manual Shadow 5", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "LightMode" = "ForwardBase"
            "PassFlags" = "OnlyDirectional"
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma shader_feature GEOM_TYPE_MESH

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 pos : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float3 worldNormal : NORMAL;
                float4 pos : SV_POSITION;
                SHADOW_COORDS(0)
                float2 mainUV : TEXCOORD1;
                float2 noiseUV : TEXCOORD2;
                float2 defaultUV : TEXCOORD3;
                float4 worldPos : TEXCOORD4;
            };

            sampler2D _MainTex;
            sampler2D _ShadowTex;
            sampler2D _ShadowDistortion;
            float4 _MainTex_ST;

            sampler2D _ShadowNoise;
            float4 _ShadowNoise_ST;

            float _ShadowIntensity;
            float _DistortionIntensity;
            float _ShadowOffset;
            float _NoiseIntensity;
            float _DarkenColors;

            float4 _ManualShadow1;
            float4 _ManualShadow2;
            float4 _ManualShadow3;
            float4 _ManualShadow4;
            float4 _ManualShadow5;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.pos);
                o.worldPos = mul(unity_ObjectToWorld, v.pos);
                o.defaultUV = v.uv;
                o.mainUV = TRANSFORM_TEX(v.uv, _MainTex);
                o.noiseUV = TRANSFORM_TEX(v.uv, _ShadowNoise);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o)
                return o;
            }

            fixed shadowLayer(v2f i, float offset) {
                offset += (tex2D(_ShadowNoise, i.worldPos.xy * 0.3) - 0.5) * 0.01;
                float2 distortion = tex2D(_ShadowDistortion, i.mainUV*0.20567 + offset).rg;
                return tex2D(_ShadowTex, i.mainUV*0.9449 + offset + distortion * _DistortionIntensity).a;
            }

            fixed calcFakeShadow(v2f i, float3 worldPos, float4 s) {
                if (s.w == 0) {
                    return 0;
                } else {
                    return 1 - smoothstep(-0.2, 0.2, distance(worldPos, s.xyz) - s.w);
                }
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_LIGHT_ATTENUATION(shadow, i, 0)
                float shadowIntensity = 1 - shadow;

                float2 ctrDist = abs(i.defaultUV - 0.5);
                float squaring = _MainTex_ST.y / _MainTex_ST.x;
                // pretty sure this is wrong, but it works for now
                ctrDist.y += 1 - squaring;
                ctrDist.y *= squaring;
                fixed fakeAOshadow =
                    smoothstep(0.59, 0.63, length(ctrDist)) +
                    smoothstep(0.482, 0.498, max(ctrDist.x, ctrDist.y));
                shadowIntensity = saturate(shadowIntensity + fakeAOshadow);

                fixed fakeShadow = 0;
                float3 worldPos = i.worldPos.xyz / i.worldPos.w;
                fakeShadow += calcFakeShadow(i, worldPos, _ManualShadow1);
                fakeShadow += calcFakeShadow(i, worldPos, _ManualShadow2);
                fakeShadow += calcFakeShadow(i, worldPos, _ManualShadow3);
                fakeShadow += calcFakeShadow(i, worldPos, _ManualShadow4);
                fakeShadow += calcFakeShadow(i, worldPos, _ManualShadow5);
                shadowIntensity = saturate(shadowIntensity + fakeShadow);

                float2 distortion = tex2D(_ShadowDistortion, i.mainUV*0.13567).rg;
                fixed4 col = tex2D(_MainTex, i.mainUV + distortion * _DistortionIntensity * (1 - smoothstep(0.375, 0.5, length(ctrDist))));

                fixed shadowOverlay = shadowLayer(i, 0) * smoothstep(0, 0.1, shadowIntensity);
                shadowOverlay += shadowLayer(i, _ShadowOffset*0.9891) * smoothstep(0.8, 1.0, shadowIntensity);
                shadowOverlay += shadowLayer(i, _ShadowOffset*1.83781) * smoothstep(0.4, 0.55, shadowIntensity);
                shadowOverlay = saturate(shadowOverlay);

                fixed shadowNoise = 1 - (1 - pow(tex2D(_ShadowNoise, i.noiseUV).r, 0.75)) * _NoiseIntensity;
                shadowOverlay *= shadowNoise;
                shadowNoise = 1 - (1 - tex2D(_ShadowNoise, i.noiseUV * 6.722).r) * _NoiseIntensity * 0.6;
                shadowOverlay *= shadowNoise;

                col = lerp(col, fixed4(0, 0, 0, 0), saturate(shadowOverlay) * _ShadowIntensity);

                col.rgb *= (1 - _DarkenColors);

                #ifdef GEOM_TYPE_MESH // _InvertColors
                return fixed4(1 - col.rgb, col.a);
                #else
                return col;
                #endif
            }
            ENDCG
        }
    }

    Fallback "Standard"
}
