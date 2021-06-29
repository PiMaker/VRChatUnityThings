
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;
using VRC.Udon;

public class MusicPlayerVideoSecondary : UdonSharpBehaviour
{
    public MusicPlayerVideo Primary;

    public override void OnVideoReady()
    {
        Primary.SendCustomEvent("OnVideoReadySec");
    }

    public override void OnVideoError(VideoError videoError)
    {
        Debug.Log("[MusicPlayerVideoSecondary] OnVideoError: " + videoError);
        Primary.SendCustomEvent("OnVideoErrorSec");
    }
}
