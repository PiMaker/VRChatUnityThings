using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class DialVRInteractor : UdonSharpBehaviour
{
    public UdonBehaviour Controller;

    public override void OnPickup()
    {
        this.Controller.SendCustomEvent("VRInteractStart");
    }

    public override void OnDrop()
    {
        this.Controller.SendCustomEvent("VRInteractEnd");
    }
}