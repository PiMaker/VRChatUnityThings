Shader "Custom/ALInstrument"
{
    Properties
    {
        _Color ("Background Color", Color) = (0,0,0,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _Split ("Split for Instruments", Range(0, 1)) = 0.66

        _WaveBGLineWidth ("Wave Background Line Width", Range(0, 0.1)) = 0.01
        _WaveLineWidth ("Wave Line Width", Range(0, 0.1)) = 0.02
        _WaveBGLineColor ("Wave BG Line Color", Color) = (0.1, 0.1, 0.1, 0.1)
        _WaveDFTColor ("Wave DFT Line Color", Color) = (1,1,1,1)
        [ToggleUI] _WaveDFTFill ("Wave DFT Fill Bottom", Float) = 1
        _WaveFormColor ("Wave Form Line Color", Color) = (1,1,1,1)

        _BouncyBorder ("Bouncy Border", Range(0, 0.1)) = 0.02
        _BouncySteps ("Bouncy Steps Total", Int) = 20
        _BouncyRed ("Bouncy Red Steps", Int) = 4
        _BouncyYellow ("Bouncy Yellow Steps", Int) = 3
        _BouncySmoothing ("Bouncy Smoothing (somewhat expensive)", Int) = 6

        [HideInInspector] _AudioLink ("AudioLink Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        #include "Packages/com.llealloo.audiolink/Runtime/Shaders/AudioLink.cginc"

        struct Input
        {
            float2 uv_AudioLink;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _Split;

        float _WaveLineWidth, _WaveBGLineWidth;
        fixed4 _WaveBGLineColor, _WaveDFTColor, _WaveFormColor;
        float _WaveDFTFill;
        
        float _BouncyBorder;
        uint _BouncySteps, _BouncyRed, _BouncyYellow;
        uint _BouncySmoothing;

        fixed4 bouncy_color(in float2 uv) {
            float step = uv.y * _BouncySteps;
            if (abs(distance(uv.x, 0.25)) < _BouncyBorder || // middle borders
                abs(distance(uv.x, 0.50)) < _BouncyBorder ||
                abs(distance(uv.x, 0.75)) < _BouncyBorder ||
                uv.x < _BouncyBorder || uv.x > (1 - _BouncyBorder) || // side borders
                abs(distance(frac(step) - 0.5, 0.5)) < _BouncyBorder * _BouncySteps * 0.3) // y step borders
            {
                return fixed4(0, 0, 0, 0);
            }

            uint ustep = (uint)step;
            fixed3 rgb = ustep > _BouncySteps - (_BouncyYellow + (_BouncyRed + 1)) ?
                (ustep > _BouncySteps - (_BouncyRed + 1) ? fixed3(1, 0, 0) : fixed3(1, 1, 0)) :
                fixed3(0, 1, 0);

            return fixed4(rgb, 1);
        }

        float ALFormSample(in float2 uv) {
            float sam = AudioLinkLerpMultiline(ALPASS_WAVEFORM + float2(200 * uv.x, 0)).r;
            return 1 - 50 * abs(sam - uv.y * 8 + 6);
        }

        void surf (Input i, inout SurfaceOutputStandard o)
        {
            fixed4 col = 0;
            float2 absuv;
            uint j;

            if (i.uv_AudioLink.x < _Split) {
                // draw waveform to the left
                absuv = i.uv_AudioLink;
                absuv.x *= 1/_Split;

                float spectrum = AudioLinkLerpMultiline(ALPASS_DFT + float2(absuv.x * AUDIOLINK_ETOTALBINS, 0)).z * 0.8;
                if (abs(distance(spectrum, absuv.y)) < _WaveLineWidth)
                    col = fixed4(_WaveDFTColor.rgb, 1);
                if (!col.a && _WaveDFTFill && absuv.y < spectrum)
                    col = fixed4(_WaveDFTColor.rgb * 0.1, 1);

                float form = ALFormSample(absuv);
                if (length(form) < 0.99)
                    col = fixed4(_WaveFormColor.rgb, 1);

                if (!col.a) {
                    if (abs(distance(absuv.x, 0.25)) < _WaveBGLineWidth ||
                        abs(distance(absuv.x, 0.50)) < _WaveBGLineWidth ||
                        abs(distance(absuv.x, 0.75)) < _WaveBGLineWidth ||
                        absuv.x < _WaveBGLineWidth * 2 || absuv.x > (1 - _WaveBGLineWidth * 2))
                    {
                        col = _WaveBGLineColor;
                    }
                }

            } else {
                // draw VUs to the right
                absuv = i.uv_AudioLink;
                absuv.x -= _Split;
                absuv.x *= 1/(1 - _Split);

                // normal vu meter, smoothed
                float vu = 0;
                [loop]
                for (j = 0; j < min(_BouncySmoothing, AUDIOLINK_WIDTH); j++) {
                    vu += AudioLinkData(ALPASS_AUDIOLINK + uint2(j, absuv.x * 4)).r;
                }
                vu /= (float)_BouncySmoothing;

                if (vu > absuv.y)
                    col = bouncy_color(absuv);
            }

            o.Albedo = col.a ? col.rgb : _Color.rgb;

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
