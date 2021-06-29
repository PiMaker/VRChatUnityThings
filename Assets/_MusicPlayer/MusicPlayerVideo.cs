// counterpart to MusicPlayer.cs - attached to a video player

using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

public class MusicPlayerVideo : UdonSharpBehaviour
{
    public MusicPlayer MusicPlayer;
    public AudioSource[] audioSources;
    //public AudioSource[] audioSourcesX;
    private VRC.SDK3.Video.Components.AVPro.VRCAVProVideoPlayer vidPlayer;
    public VRC.SDK3.Video.Components.AVPro.VRCAVProVideoPlayer vidPlayer2;

    private VRC.SDK3.Video.Components.AVPro.VRCAVProVideoPlayer localVid;

    //public AudioLink audioLink;

    private float loadingSince;
    private VRCUrl currentUrl, currentStandby;
    private float setTime;
    private float retrySecondary;
    private const float TIMEOUT = 10f;
    private const float DESYNC = 15f;
    private const float AUDIO_SOURCE_FADE = 0.018f;

    void Start()
    {
        localVid = vidPlayer =
            (VRC.SDK3.Video.Components.AVPro.VRCAVProVideoPlayer)
                this.GetComponent(typeof(VRC.SDK3.Video.Components.AVPro.VRCAVProVideoPlayer));

        currentUrl = currentStandby = VRCUrl.Empty;
        loadingSince = 0f;
        retrySecondary = -1f;
        _AudioVol = 0f;
        SetVol(0f);

        vidPlayer.LoadURL(VRCUrl.Empty);
        vidPlayer2.LoadURL(VRCUrl.Empty);
    }

    void SetVol(float vol)
    {
        var isZero = Mathf.Approximately(vol, 0f);

        foreach (var src in audioSources)
        {
            src.volume = vol;
        }

        //foreach (var src in audioSourcesX)
        //{
        //    src.volume = isZero ? 0f : 0.01f;
        //}

        //audioLink.SetProgramVariable("audioSourcesIdx", isZero ? -1 : (vidPlayer == localVid ? 0 : 1));
    }

    #region Events
    private float _AudioVol;
    public void AudioVolChanged()
    {
        SetVol(_AudioVol);
    }

    public override void OnVideoReady()
    {
        if (vidPlayer == localVid)
        {
            OnVideoReady_main();
        }
        else
        {
            OnVideoReady_standby();
        }
    }

    public override void OnVideoError(VideoError videoError)
    {
        Debug.Log("[MusicPlayerVideo] OnVideoError: " + videoError);

        if (vidPlayer == localVid)
        {
            OnVideoError_main();
        }
        else
        {
            OnVideoError_standby();
        }
    }

    public void OnVideoReadySec()
    {
        if (vidPlayer != localVid)
        {
            OnVideoReady_main();
        }
        else
        {
            OnVideoReady_standby();
        }
    }

    public void OnVideoErrorSec()
    {
        if (vidPlayer != localVid)
        {
            OnVideoError_main();
        }
        else
        {
            OnVideoError_standby();
        }
    }
    #endregion

    public void OnVideoReady_main()
    {
        var took = (Time.time - loadingSince);
        Debug.Log("[MusicPlayerVideo] OnVideoReady: setTime=" + setTime + " took=" + took);
        var time = setTime < DESYNC / 2f ? 0f : setTime + took;
        loadingSince = 0f;
        // will be faded in
        SetVol(0f);
        vidPlayer.SetTime(time);
        vidPlayer.Play();

        //audioLink.SetProgramVariable("audioSourcesIdx", vidPlayer == localVid ? 0 : 1);
    }

    public void OnVideoError_main()
    {
        Debug.Log("[MusicPlayerVideo] Main video error, resetting currentUrl...");
        if (currentStandby.ToString().Equals(VRCUrl.Empty.ToString())) return; // we're not even supposed to be playing, ignore error
        // retry in 1 second
        currentUrl = VRCUrl.Empty;
        loadingSince = Time.time - TIMEOUT + 1f;
    }

    public void OnVideoReady_standby()
    {
        Debug.Log("[MusicPlayerVideo] Standby video ready!");
    }

    public void OnVideoError_standby()
    {
        Debug.Log("[MusicPlayerVideo] Standby video error, scheduling retry...");
        if (currentStandby.ToString().Equals(VRCUrl.Empty.ToString())) return;
        // retry, for better or worse
        retrySecondary = 2.55f;
    }

    void Update()
    {
        // update audio source volume (fade)
        SetVol(Mathf.Lerp(audioSources[0].volume, _AudioVol, AUDIO_SOURCE_FADE));

        // if video is loading (and not for too long) we skip the update cycle
        if (!(loadingSince > 0f && (Time.time - loadingSince) < TIMEOUT))
        {
            var shouldPlay = (VRCUrl)MusicPlayer.GetProgramVariable("currentUrl") ?? VRCUrl.Empty;
            if (shouldPlay == null || shouldPlay == VRCUrl.Empty) return;
            var shouldTime = (float)MusicPlayer.GetProgramVariable("currentTime");
            var hasTime = vidPlayer.GetTime();

            if (!currentUrl.ToString().Equals(shouldPlay.ToString()))
            {
                // video desync
                Debug.Log($"[MusicPlayerVideo] Video desync: local={currentUrl} vs remote={shouldPlay} ({shouldTime})");
                loadingSince = Time.time;
                setTime = shouldTime;
                currentUrl = shouldPlay;
                vidPlayer.Stop();
                vidPlayer.Pause();

                if (currentStandby.ToString().Equals(shouldPlay.ToString()) && !currentUrl.ToString().Equals(VRCUrl.Empty.ToString()) && vidPlayer2.IsReady)
                {
                    // good path
                    Debug.Log("[MusicPlayerVideo] standby URL ok - switching to load");
                    var tmp = vidPlayer;
                    vidPlayer = vidPlayer2;
                    vidPlayer2 = tmp;
                    OnVideoReady_main();
                }
                else
                {
                    // bad path
                    Debug.Log("[MusicPlayerVideo] standby URL stale - reloading main");
                    Debug.Log($"[MusicPlayerVideo] Reasoning: would use if '{currentStandby}' == '{shouldPlay}' && !'{currentUrl}'.is.VRCUrl.Empty && ready={vidPlayer2.IsReady}");
                    vidPlayer.LoadURL(currentUrl);
                }
                return; // exit now
            }
            else if (Mathf.Abs(shouldTime - hasTime) > DESYNC && !currentUrl.ToString().Equals(VRCUrl.Empty.ToString()) && vidPlayer.IsReady)
            {
                // time desync
                Debug.Log($"[MusicPlayerVideo] Time desync: local={hasTime} vs remote={shouldTime}");
                if (vidPlayer.IsPlaying)
                {
                    vidPlayer.SetTime(shouldTime);
                }
            }
        }

        // sync standby player
        var standby = (VRCUrl)MusicPlayer.GetProgramVariable("standbyUrl") ?? VRCUrl.Empty;
        if (retrySecondary > 0f)
        {
            var tmp = retrySecondary;
            retrySecondary -= Time.deltaTime;
            if (retrySecondary <= 0f && tmp > 0f && standby.ToString().Equals(currentStandby.ToString()))
            {
                // timer expired
                Debug.Log("[MusicPlayerVideo] Retrying standby load...");
                vidPlayer2.LoadURL(currentStandby);
            }
        }
        else if (!standby.ToString().Equals(currentStandby.ToString()))
        {
            Debug.Log($"[MusicPlayerVideo] standby out of sync: local={currentStandby} vs remote={standby}");
            vidPlayer2.Pause();
            retrySecondary = -1f;
            currentStandby = standby;
            vidPlayer2.LoadURL(standby);
        }
    }
}
