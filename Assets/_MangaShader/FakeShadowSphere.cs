using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

public class FakeShadowSphere : MonoBehaviour
{
    public string ShaderName = "_pi_/MangaShader";

    [Range(1, 5)]
    public uint Number = 1;

    [Range(0.0f, 5.0f)]
    public float Distance = 1.0f;

    public void DoWork()
    {
        var pos = this.gameObject.transform.position;
        var meshRends = FindObjectsOfType<MeshRenderer>();
        foreach (var mesh in meshRends)
        {
            if (mesh.sharedMaterial.shader.name == ShaderName)
            {
                mesh.sharedMaterial.SetVector("_ManualShadow" + Number, new Vector4(pos.x, pos.y, pos.z, Distance));
            }
        }
    }
}

[CustomEditor(typeof(FakeShadowSphere))]
public class FakeShadowSphereEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FakeShadowSphere myScript = (FakeShadowSphere)target;
        if (GUILayout.Button("Update"))
        {
            myScript.DoWork();
        }
    }

}

#endif