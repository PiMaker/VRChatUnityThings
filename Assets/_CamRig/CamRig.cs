using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class CamRig : UdonSharpBehaviour
{
    public GameObject Camera;
    public UnityEngine.Camera CamCam;
    public Text TextPos1, TextPos2, TextPosCur;
    public Text ButtonMoveText;
    public Slider SliderSpeed;
    public Slider SliderSmooth;
    public Slider SliderFOV;
    public Toggle ToggleReverse;
    public Button SetPos1Button, SetPos2Button, ButtonMove, ButtonBring;
    public Text SetPos1Text, SetPos2Text, OwnerInfoText;

    public Material GreenMat, RedMat;
    private Renderer renderer2;

    public GameObject S1, S2, S3, S4;
    public GameObject[] SInter;

    [UdonSynced]
    private float speed, smoothing;

    private Rigidbody camBody;
    private VRC.SDK3.Components.VRCPickup camPickup;

    [UdonSynced]
    private Vector3 pos1, pos2;

    [UdonSynced]
    private Quaternion rot1, rot2;
    
    [UdonSynced]
    private bool running;

    private int setting;

    void Start()
    {
        running = false;
        setting = -1;

        pos1 = Camera.transform.position;
        pos2 = Camera.transform.position;
        rot1 = Camera.transform.rotation;
        rot2 = Camera.transform.rotation;

        camBody = (Rigidbody)Camera.GetComponent(typeof(Rigidbody));
        camPickup = (VRC.SDK3.Components.VRCPickup)Camera.GetComponent(typeof(VRC.SDK3.Components.VRCPickup));

        renderer2 = (Renderer)this.GetComponent(typeof(Renderer));
    }

    public void UpdateFOV()
    {
        CamCam.focalLength = SliderFOV.value;
    }

    public void SetPos1()
    {
        if (running) return;

        if (setting == 1)
        {
            SetPos1Text.text = "Set Pos 1";
            setting = -1;
            return;
        }

        SetPos1Text.text = "STOP";
        SetPos2Text.text = "Set Pos 2";
        setting = 1;
    }

    public void SetPos2()
    {
        if (running) return;

        if (setting == 2)
        {
            SetPos2Text.text = "Set Pos 2";
            setting = -1;
            return;
        }

        SetPos1Text.text = "Set Pos 1";
        SetPos2Text.text = "STOP";
        setting = 2;
    }

    public void DoIt()
    {
        setting = -1;
        SetPos1Text.text = "Set Pos 1";
        SetPos2Text.text = "Set Pos 2";

        running = !running;
        ButtonMoveText.text = running ? "STOP" : "Start Move";
        setRunning();
    }

    public void Bring()
    {
        this.Camera.transform.position = this.gameObject.transform.position + this.gameObject.transform.right.normalized * 0.4f;
    }

    private void setRunning()
    {
        camPickup.pickupable = !running;

        S1.SetActive(!running);
        S2.SetActive(!running);
        S3.SetActive(!running);
        S4.SetActive(!running);

        foreach (var s in SInter)
        {
            s.SetActive(!running);
        }
    }

    public void Update()
    {
        var isOwner = Networking.IsOwner(this.gameObject);
        SetPos1Button.enabled = isOwner;
        SetPos2Button.enabled = isOwner;
        ButtonMove.enabled = isOwner;
        SliderSmooth.enabled = isOwner;
        SliderSpeed.enabled = isOwner;
        SliderFOV.enabled = isOwner;
        ButtonBring.enabled = isOwner;
        OwnerInfoText.enabled = !isOwner;

        if (isOwner)
        {
            if (!Networking.IsOwner(Camera))
            {
                Networking.SetOwner(Networking.LocalPlayer, Camera);
            }

            speed = SliderSpeed.value;
            smoothing = SliderSmooth.value;

            if (setting == 1)
            {
                pos1 = Camera.transform.position;
                rot1 = Camera.transform.rotation;
            }
            else if (setting == 2)
            {
                pos2 = Camera.transform.position;
                rot2 = Camera.transform.rotation;
            }

            renderer2.material = GreenMat;
        }
        else
        {
            SliderSpeed.value = speed;
            SliderSmooth.value = smoothing;

            renderer2.material = RedMat;
        }

        if (!running)
        {
            S1.transform.position = pos1 - getLeftVector(rot1).normalized * 0.2f;
            S2.transform.position = pos1;
            S3.transform.position = pos2;
            S4.transform.position = pos2 + getLeftVector(rot2).normalized * 0.2f;

            for (var i = 0; i < SInter.Length; i++)
            {
                SInter[i].transform.position = posInterp(easeInOutSine((float)(i+1) / (SInter.Length+1)));
            }
        }

        if (isOwner && running)
        {
            float progress = easeInOutSine((Time.time % speed) / speed);
            if (ToggleReverse.isOn)
            {
                progress = 1 - progress;
            }
            Camera.transform.position = posInterp(progress);
            Camera.transform.rotation = Quaternion.Slerp(rot1, rot2, progress);
        }

        TextPos1.text = pos1.ToString();
        TextPos2.text = pos2.ToString();
        TextPosCur.text = Camera.transform.position.ToString();
    }

    public override void OnDeserialization()
    {
        setRunning();
    }

    private Vector3 posInterp(float t)
    {
        return getCatmullRomPosition(t, pos1 - getLeftVector(rot1).normalized * smoothing, pos1, pos2, pos2 + getLeftVector(rot2).normalized * smoothing);
    }

    // Returns a position between 4 Vector3 with Catmull-Rom spline algorithm
    // http://www.iquilezles.org/www/articles/minispline/minispline.htm
    private Vector3 getCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        // The coefficients of the cubic polynomial (except the 0.5f * which I added later for performance)
        Vector3 a = 2f * p1;
        Vector3 b = p2 - p0;
        Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

        // The cubic polynomial: a + b * t + c * t^2 + d * t^3
        Vector3 pos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));

        return pos;
    }

    private Vector3 getLeftVector(Quaternion q)
    {
        float x = 1 - 2 * (q.y * q.y + q.z * q.z);
        float y = 2 * (q.x * q.y + q.w * q.z);
        float z = 2 * (q.x * q.z - q.w * q.y);
        return new Vector3(x, y, z);
    }

    private float easeInOutSine(float x) {
        return -(Mathf.Cos(Mathf.PI * x) - 1) / 2;
    }
}
