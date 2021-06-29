using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class DialDesktopInteractor : UdonSharpBehaviour
{
    public UdonBehaviour Controller;

    public override void Interact()
    {
        this.Controller.SendCustomEvent("DesktopInteract");
    }
}