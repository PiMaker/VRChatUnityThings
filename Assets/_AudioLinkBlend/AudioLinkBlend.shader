Shader "_pi_/AudioLinkBlend"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _ALBand ("AudioLink Band", Int) = 0

        _Frequency ("Vibration Frequency (set to 0 to disable vibration)", Float) = 50.0

        [HideInInspector] _AudioLink ("AudioLink Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="true" }
        LOD 100

        CGPROGRAM
        #pragma surface surf Standard addshadow fullforwardshadows vertex:vert
        #pragma target 3.0

        #include "../../AudioLink/Shaders/AudioLink.cginc"

        sampler2D _MainTex;

        struct Input {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(uint, _ALBand)
            UNITY_DEFINE_INSTANCED_PROP(uint, _Frequency)
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            if (AudioLinkData(ALPASS_GENERALVU + uint2(0, 0)).x >= 0) {
                uint band = UNITY_ACCESS_INSTANCED_PROP(Props, _ALBand);
                float al = AudioLinkData(ALPASS_AUDIOLINK + int2(0, band)).r;

                uint freq = UNITY_ACCESS_INSTANCED_PROP(Props, _Frequency);

                float intens = freq ? 0.5 + 0.5*sin(_Time.w*freq)*al : al;
                intens *= 100;

                v.vertex = lerp(v.vertex, v.texcoord1, intens);
                v.normal = lerp(v.normal, v.texcoord2, intens);
                o.uv_MainTex = v.texcoord = lerp(v.texcoord, v.texcoord3, intens);
            }
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
}
