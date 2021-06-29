using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class SkyboxCaptureScript : MonoBehaviour
{
    public RenderTexture Texture;
    public bool Dump = false;

    void Start()
    {
    }

    void Update()
    {
        if (Dump)
        {
            var cam = this.gameObject.GetComponent<Camera>();
            Capture(cam, 0, 0, 0, 0);
            Capture(cam, 0, 90, 0, 1);
            Capture(cam, 0, 180, 0, 2);
            Capture(cam, 0, 270, 0, 3);
            Capture(cam, -90, 0, 0, 4);
            Capture(cam, 90, 0, 0, 5);
            Dump = false;
        }

        void Capture(Camera cam, float x, float y, float z, int num)
        {
            this.gameObject.transform.localRotation = Quaternion.Euler(x, y, z);
            cam.Render();
            DumpRenderTexture(Texture, "Assets\\_pi_\\_Skybox\\capture-" + num + ".png");
        }
    }

    public static void DumpRenderTexture(RenderTexture rt, string pngOutPath)
    {
        var oldRT = RenderTexture.active;

        var tex = new Texture2D(rt.width, rt.height);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        File.WriteAllBytes(pngOutPath, tex.EncodeToPNG());
        RenderTexture.active = oldRT;
    }
}
