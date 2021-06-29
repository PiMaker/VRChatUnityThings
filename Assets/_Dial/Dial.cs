using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Dial : UdonSharpBehaviour
{
    [Header("Dial Behaviour")]

    [Range(2, 4)]
    public int States = 4;
    [Range(0, 3)]
    public int CurrentState = 0;

    public int SuspendedState = -1;

    // These objects will be enabled/disabled depending on the dial state,
    // i.e. the selected position will index into this array and set the
    // corresponding GameObject to active (and all others to disabled)
    public GameObject[] EnableDisable;

    // These objects will receive a "DialEnable" and "DialDisable" Udon
    // event when selected or un-selected, respectively (only if they
    // contain an UdonBehaviour of course)
    public GameObject[] Behaviours;

    [Header("Internal Settings")]

    public AudioSource ClickSource;

    public GameObject RotatorVR, RotatorDesktop;
    public GameObject Base2, Base3, Base4;
    private GameObject activeObj;

    private Vector3 baseRotation;
    private Vector3 basePos;

    private const int BOUND_NONE = 0, BOUND_LEFT = 1, BOUND_RIGHT = 2;
    private int boundHand;
    private VRCPlayerApi.TrackingData boundBase;
    private float boundStartRotation;
    private float lastTrackedRot;

    private bool initVR;

    private int suspendedFrom = -1;

    void Start()
    {
        if (States == 2)
        {
            Base2.SetActive(true);
            Base3.SetActive(false);
            Base4.SetActive(false);
        }
        else if (States == 3)
        {
            Base2.SetActive(false);
            Base3.SetActive(true);
            Base4.SetActive(false);
        }
        else if (States == 4)
        {
            Base2.SetActive(false);
            Base3.SetActive(false);
            Base4.SetActive(true);
        }
        else
        {
            Debug.LogError(
                "[Dial] Invalid number of states (must be between 2 and 4): "
                + States);
            this.gameObject.SetActive(false);
            return;
        }

        // always start out in Desktop mode, IsUserInVR() doesn't
        // work in Start() - wait a bit and try again in Update()
        this.initVR = false;
        RotatorDesktop.SetActive(true);
        RotatorVR.SetActive(false);
        this.activeObj = RotatorDesktop;

        this.CurrentState = Mathf.Clamp(this.CurrentState, 0, States - 1);

        foreach (var obj in EnableDisable)
        {
            if (obj == null) continue;
            obj.SetActive(false);
        }
        // enable in second step to handle duplicate objects in EnableDisable
        if (EnableDisable.Length > this.CurrentState && EnableDisable[this.CurrentState] != null)
        {
            EnableDisable[this.CurrentState].SetActive(true);
        }

        this.baseRotation = this.activeObj.transform.localRotation.eulerAngles;
        this.basePos = this.activeObj.transform.localPosition;
        this.boundHand = 0;

        var collider = this.GetComponent<BoxCollider>();
        if (this.SuspendedState != -1 && collider != null && !collider.bounds.Contains(Networking.LocalPlayer.GetPosition()))
        {
            // outside collider, start suspended
            var tmp = this.CurrentState;
            this.SetState(this.SuspendedState, false);
            this.suspendedFrom = tmp;
        }
        else
        {
            SetState(this.CurrentState, false);
        }
    }

    private void EnableDisableToggle(int old, int now)
    {
        if (this.EnableDisable == null)
        {
            return;
        }

        if (this.EnableDisable.Length > old)
        {
            var o = this.EnableDisable[old];
            if (o != null)
            {
                o.SetActive(false);
            }
        }
        if (this.EnableDisable.Length > now)
        {
            var o = this.EnableDisable[now];
            if (o != null)
            {
                o.SetActive(true);
            }
        }
    }

    public int NextState;
    private void BehaviourToggle(int old, int now)
    {
        if (this.Behaviours == null)
        {
            return;
        }

        NextState = now;

        if (this.Behaviours.Length > old)
        {
            var o = this.Behaviours[old];
            if (o != null)
            {
                var b = o.GetComponent(typeof(UdonBehaviour));
                if (b != null)
                {
                    o.SetActive(true);
                    ((UdonBehaviour)b).SendCustomEvent("DialDisable");
                }
            }
        }
        if (this.Behaviours.Length > now)
        {
            var o = this.Behaviours[now];
            if (o != null)
            {
                var b = o.GetComponent(typeof(UdonBehaviour));
                if (b != null)
                {
                    o.SetActive(true);
                    ((UdonBehaviour)b).SendCustomEvent("DialEnable");
                }
            }
        }
    }

    private void SetState(int state, bool transition)
    {
        if (this.suspendedFrom != -1 || state < 0)
        {
            // we're suspended, ignore
            return;
        }

        // if transition is set, also perform actions,
        // otherwise the rotation is just visual
        if (transition)
        {
            EnableDisableToggle(this.CurrentState, state);
            BehaviourToggle(this.CurrentState, state);
        }

        // Set dial to correct rotation (fix it to its "slot")
        this.activeObj.transform.localRotation =
            Quaternion.Euler(this.baseRotation.x, this.baseRotation.y + 90f * state,
                this.baseRotation.z);

        this.CurrentState = state;
    }

    // called from DialDesktopInteractor
    public void DesktopInteract()
    {
        // Called on interact with the desktop dial, simply go to next state with rollover
        SetState(this.CurrentState == States - 1 ? 0 : this.CurrentState + 1, true);
        ClickSource.Play();
    }

    public void VRInteractStart()
    {
        // User has picked up the dial in VR, see which hand it is in
        var leftPickup = Networking.LocalPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Left);
        var rightPickup = Networking.LocalPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);
        this.boundHand = leftPickup != null && leftPickup.gameObject == this.RotatorVR.gameObject ?
            BOUND_LEFT : (rightPickup != null && rightPickup.gameObject == this.RotatorVR.gameObject ?
                BOUND_RIGHT : BOUND_NONE);

        // this is the base transform right now, i.e. what we rotate off later
        this.boundBase = Networking.LocalPlayer.GetTrackingData(
            this.boundHand == BOUND_RIGHT ?
            VRCPlayerApi.TrackingDataType.RightHand :
            VRCPlayerApi.TrackingDataType.LeftHand);

        this.boundStartRotation = this.activeObj.transform.localRotation.eulerAngles.y;
        this.lastTrackedRot = 100000;
    }

    public void VRInteractEnd()
    {
        // player let go, get current rotation to determine state
        var rotation = this.activeObj.transform.localRotation.eulerAngles.y;

        // one last position reset for good measure
        this.RotatorVR.transform.localPosition = this.basePos;

        // normalize rotation
        rotation %= 360;
        if (rotation < 0)
        {
            rotation += 360;
        }

        this.boundHand = BOUND_NONE;

        // select state based on rotation
        if (rotation < 45 || rotation >= 315)
        {
            SetState(0, true);
        }
        else if (rotation < 135)
        {
            SetState(1, true);
        }
        else if (rotation < 225)
        {
            if (this.States >= 3)
            {
                SetState(2, true);
            }
            else
            {
                SetState(1, true);
            }
        }
        else if (rotation < 315)
        {
            if (this.States >= 4)
            {
                SetState(3, true);
            }
            else
            {
                SetState(0, true);
            }
        }

        ClickSource.Play();
    }

    void Update()
    {
        // Detect VR properly now
        if (!this.initVR && Networking.LocalPlayer.IsUserInVR())
        {
            Debug.Log("[Dial] User is in VR!");
            this.initVR = true;
            RotatorDesktop.SetActive(false);
            RotatorVR.SetActive(true);
            this.activeObj = RotatorVR;
            SetState(this.CurrentState, false);
        }

        if (this.initVR && boundHand != BOUND_NONE)
        {
            // player has grabbed knob, do VR rotation tracking
            var trackState = Networking.LocalPlayer.GetTrackingData(
                this.boundHand == BOUND_RIGHT ?
                VRCPlayerApi.TrackingDataType.RightHand :
                VRCPlayerApi.TrackingDataType.LeftHand);

            // now calculate hand rotation since start of grab
            // god I despi^Wlove math sometimes

            // this is actually "forward", i.e. where your extended finger points
            // var baseForward = this.boundBase.rotation * Vector3.left;
            // nevermind, I've decided that it feels better if the "plane" you rotate
            // on is actually the rotation of the object, not your initial hand position
            var baseForward = this.gameObject.transform.up;

            // these point "down" through your fist
            var baseUp = this.boundBase.rotation * Vector3.up;
            var trackUp = trackState.rotation * Vector3.up;

            // project both on an imaginary plane so we only get one axis of rotation
            var projBase = Vector3.ProjectOnPlane(baseUp, baseForward);
            var projTrack = Vector3.ProjectOnPlane(trackUp, baseForward);

            // get the angle on the "2d plane" where we projected the vectors onto
            var trackAngle = Vector3.SignedAngle(projBase, projTrack, baseForward);

            // haptic feedback
            var relRot = Mathf.Abs(this.lastTrackedRot - trackAngle);
            if (relRot > 1.8f)
            {
                Networking.LocalPlayer.PlayHapticEventInHand(
                    this.boundHand == BOUND_RIGHT ?
                        VRC_Pickup.PickupHand.Right :
                        VRC_Pickup.PickupHand.Left,
                    0.1f,
                    0.9f,
                    0.9f);
            }

            // apply rotation to dial rotator
            var newRotation = this.baseRotation.y + this.boundStartRotation + trackAngle;
            this.activeObj.transform.localRotation = Quaternion.Euler(
                this.baseRotation.x,
                newRotation,
                this.baseRotation.z);

            // don't allow breaking off the dial, i.e. keep it in place
            this.activeObj.transform.localPosition = this.basePos;

            this.lastTrackedRot = trackAngle;
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            var tmp = this.suspendedFrom;
            this.suspendedFrom = -1;
            this.SetState(tmp, true);
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player.isLocal && this.SuspendedState != -1)
        {
            var tmp = this.CurrentState;
            this.SetState(this.SuspendedState, true);
            this.suspendedFrom = tmp;
        }
    }
}