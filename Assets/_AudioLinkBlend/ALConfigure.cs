using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class ALConfigure : UdonSharpBehaviour
{
    void Start()
    {
        var rend = this.GetComponent<MeshRenderer>();

        var bandSplit = this.gameObject.name.Split(';');
        var band = int.Parse(bandSplit[bandSplit.Length - 1]);

        var mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        mpb.SetInt("_ALBand", band);
        rend.SetPropertyBlock(mpb);

        // disable ourselves so we don't use any performance (hopefully)
        this.enabled = false;
    }
}
