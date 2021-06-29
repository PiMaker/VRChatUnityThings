// interacts with MusicPlayerVideo.cs
// provides a synchronized music listening experience :)

using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class MusicPlayer : UdonSharpBehaviour
{
    [Header("Track List")]

    // Data set
    [Tooltip("YouTube links or similar")]
    public VRCUrl[] clips;
    [Tooltip("Track names in same order as links above (*must* have same count and order!)")]
    public string[] names;

    [Header("Other Configuration (optional)")]

    // Public UI
    [Tooltip("If set, this will print the tracklist in a sorted order to this text element on startup")]
    public Text tracklist;

    [Header("Internal Settings")]

    // UI set
    public Text[] nowPlaying;
    public Text[] playBtnText;
    //public Text debug;
    public Text[] timeText;
    public Button[] skipButton;

    public AudioSource audioSource;
    public MusicPlayerVideo MPV;
    void SetAudioVol(float vol) { MPV.SetProgramVariable("_AudioVol", vol); MPV.SendCustomEvent("AudioVolChanged"); }

    // Volume slider and skip button rendering
    public Slider volSliderMaster;
    public Slider[] volSliders;
    private bool inMasterSliderUpdate;
    private bool muted;
    private bool notStarted;
    private float skipTimeout;
    private Vector3 skipDefScale, skipOffScale;

    // Synced stuff
    [UdonSynced]
    private float currentTime; // accessed externally
    [UdonSynced]
    private float startTime;
    [UdonSynced]
    private int curClip;

    [UdonSynced]
    private VRCUrl standbyUrl;

    private VRCUrl currentUrl; // accessed externally

    private float startTimeLocal;
    private float startTimePrev;

    // Master stuff
    private int curIndex;
    private int[] indices;
    private bool wasMaster;
    private bool shouldSkip;

    void Start()
    {
        inMasterSliderUpdate = false;
        VolumeSliderMasterChanged();

        indices = new int[clips.Length];
        GenerateIndices();
        curIndex = 0;
        wasMaster = Networking.IsMaster;
        shouldSkip = false;

        muted = true;
        notStarted = true;
        skipTimeout = 0;

        skipDefScale = skipButton[0].transform.localScale;
        skipOffScale = new Vector3(0, 0, 0);

        currentUrl = VRCUrl.Empty;

        // render tracklist, sorted
        if (tracklist != null)
        {
            tracklist.text = "Tracklist:\n";
            var namesSorted = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                // insertion sort-ish
                var name = names[i];
                string moving = null;
                for (int j = 0; j < namesSorted.Length; j++)
                {
                    if (namesSorted[j] == null)
                    {
                        namesSorted[j] = moving == null ? name : moving;
                        break;
                    }
                    else if (moving != null)
                    {
                        var tmp = moving;
                        moving = namesSorted[j];
                        namesSorted[j] = tmp;
                    }
                    else if (namesSorted[j].CompareTo(name) > 0)
                    {
                        moving = namesSorted[j];
                        namesSorted[j] = name;
                    }
                }
            }
            foreach (var name in namesSorted)
            {
                var space = UnityEngine.Random.value > 0.5f ? " " : "";
                tracklist.text += "\n" + space + name;
            }
        }

#if UNITY_EDITOR
#else
        if (Networking.IsMaster)
        {
            // initialize immediately if we're master,
            // this should happen only once per instance
            Mute();
            Mute();
        }
#endif
    }

    public void Mute()
    {
        if (notStarted)
        {
            Debug.Log("[MusicPlayer] Initializing!");
            notStarted = false;
            if (Networking.IsMaster)
            {
                startTime = Time.time + 1f;
            }
        }

        Debug.Log("[MusicPlayer] Mute/Unmute");
        if (muted)
        {
            SetAudioVol(volSliderMaster.value);
        }
        else
        {
            SetAudioVol(0f);
        }
        muted = !muted;
    }

    public void Skip()
    {
        if (skipTimeout > 0f) return;
        if (Networking.IsMaster)
        {
            SkipMaster();
        }
        else
        {
            skipTimeout = 10f;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "SkipMaster");
        }
    }

    public void SkipMaster()
    {
        if (skipTimeout > 0f) return;
        shouldSkip = true;
        skipTimeout = 5f;
    }

#region VolumeSlider
    public void VolumeSliderChanged()
    {
        if (inMasterSliderUpdate)
        {
            return;
        }

        var newVol = -1f;
        foreach (var slider in volSliders)
        {
            if (slider.value != volSliderMaster.value)
            {
                newVol = slider.value;
                break;
            }
        }

        if (newVol != -1f)
        {
            volSliderMaster.value = newVol;
        }
    }

    public void VolumeSliderMasterChanged()
    {
        if (!muted)
        {
            SetAudioVol(volSliderMaster.value);
        }

        inMasterSliderUpdate = true;
        foreach (var slider in volSliders)
        {
            slider.value = volSliderMaster.value;
        }
        inMasterSliderUpdate = false;
    }
#endregion

    void Update()
    {
        var vidPlayer = (VRC.SDK3.Video.Components.AVPro.VRCAVProVideoPlayer)MPV.GetProgramVariable("vidPlayer");
        if (vidPlayer == null) return; // not initialized yet
        var dur = vidPlayer.GetDuration();

        // MASTER UPDATE
        if (Networking.IsMaster && Networking.GetOwner(this.gameObject).isLocal)
        {
            if (!wasMaster)
            {
                // we just became master
                wasMaster = true;
                startTime = startTimeLocal;
                notStarted = false;
            }

            if (shouldSkip || (dur > 0f && currentTime > (dur + 1f)))
            {
                // song ended or skip requested, go to next one
                curIndex++;
                shouldSkip = false;

                if (curIndex == indices.Length)
                {
                    // index overflow, generate new playlist
                    GenerateIndices();
                    curIndex = 0;
                }
                
                startTime = Time.time;
            }

            currentTime = Mathf.Max(0f, Time.time - startTime);
            curClip = indices[curIndex];
            standbyUrl = notStarted || curClip < 0 || curIndex >= clips.Length - 1 ? VRCUrl.Empty : clips[indices[curIndex + 1]];
        }

        // ALL UPDATE
        currentUrl = notStarted || curClip < 0 ? VRCUrl.Empty : clips[curClip];

        if (startTimePrev != startTime)
        {
            // prepare if we become master at some point
            Debug.Log("[MusicPlayer] startTime changed");
            startTimeLocal = Time.time - currentTime;
            startTimePrev = startTime;

            skipTimeout = 5f;
        }

        // UI STATE

        // "now playing"
        var nowPlayingText = notStarted ? "click play!" : (curClip < 0 ? "syncing..." : names[curClip]);
        foreach (var t in nowPlaying)
        {
            t.text = nowPlayingText;
        }

        // "current time"
        string timeTextText;
        if (notStarted)
        {
            timeTextText = "";
        }
        else
        {
            int cu = (int)vidPlayer.GetTime();
            int to = (int)dur;
            if (cu == to)
            {
                timeTextText = "loading...";
            }
            else
            {
                string cuS = (cu / 60).ToString() + ":" + (cu % 60).ToString("00");
                string toS = (to / 60).ToString() + ":" + (to % 60).ToString("00");
                timeTextText = cuS + " / " + toS;
            }
        }
        foreach (var t in timeText)
        {
            t.text = timeTextText;
        }

        // "mute / unmute"
        var playBtnTextText = notStarted ? "play" : (muted ? "unmute" : "mute");
        foreach (var t in playBtnText)
        {
            t.text = playBtnTextText;
        }

        // "skip"
        foreach (var s in skipButton)
        {
            if (notStarted || skipTimeout > 0f)
            {
                s.enabled = false;
                s.transform.localScale = skipOffScale;
            }
            else
            {
                s.enabled = true;
                s.transform.localScale = skipDefScale;
            }
        }

        if (skipTimeout > 0f)
        {
            skipTimeout -= Time.deltaTime;
        }

        // "debug"
        //debug.text = "master=" + (Networking.IsMaster ? "y" : "n") +
        //    " time=" + string.Format("{0:0.00}", currentTime) +
        //    " vol=" + string.Format("{0:0.00}", audioSource.volume) +
        //    " retry_sec=" + string.Format("{0:0.00}", (float)MPV.GetProgramVariable("retrySecondary")) +
        //    "\r\nvid=" + (vidPlayer.IsPlaying ? "play" : "stop") +
        //    " idx=" + curIndex +
        //    " clip=" + curClip +
        //    (skipTimeout > 0 ? string.Format(" skip={0:0.00}", skipTimeout) : "");
    }

    private void GenerateIndices()
    {
        // generate local playlist
        Debug.Log("[MusicPlayer] Generating indices list...");
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = -1;
        }

        // primitive (but deterministic time!) shuffle
        for (int i = 0; i < indices.Length; i++)
        {
            int next = UnityEngine.Random.Range(0, indices.Length - i);
            for (int j = 0; j < clips.Length; j++)
            {
                bool contained = false; // = indices.contains(j)
                for (int k = 0; k < indices.Length; k++)
                {
                    if (indices[k] == j)
                    {
                        contained = true;
                        break;
                    }
                }

                if (!contained)
                {
                    next--;
                    if (next == -1)
                    {
                        indices[i] = j;
                        break;
                    }
                }
            }
        }

        // prevent double play on regen at end of playlist
        if (indices[0] == curIndex)
        {
            int sw = indices.Length / 2;
            int tmp = indices[0];
            indices[0] = indices[sw];
            indices[sw] = tmp;
        }
    }
}
