using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using UdonSharp;

#if UNITY_EDITOR

public class ALBlendGenerator : MonoBehaviour
{
    [Header("Internal Stuff")]
    public Material AudioLinkBlendMaterial;
    public UdonSharpProgramAsset ALConfigure;

    [Header("User Configuration")]
    [Tooltip("Which AudioLink Band to react to (0=bass, 1=mid_low, 2=mid_high, 3=treble) - this is encoded into the name of the created GameObject")]
    [Range(0, 3)]
    public int AudioLinkBand = 0;

    [HideInInspector]
    public float min = 0.0f, max = 1.0f;
    [HideInInspector]
    public int shape = 0;

    public void DoWork()
    {
        var mesh = this.GetComponent<SkinnedMeshRenderer>();

        var initialVal = mesh.GetBlendShapeWeight(shape);

        var newMesh = new Mesh();

        var scale = this.transform.localScale;
        this.transform.localScale = Vector3.one;

        mesh.SetBlendShapeWeight(shape, max);
        mesh.BakeMesh(newMesh);

        var bounds1 = CalculateBounds(newMesh);

        var vertexPositions = new List<Vector3>(newMesh.vertices);
        var normalPositions = new List<Vector3>(newMesh.normals);
        var uvPositions = new List<Vector2>(newMesh.uv);

        mesh.SetBlendShapeWeight(shape, min);
        mesh.BakeMesh(newMesh);

        var bounds2 = CalculateBounds(newMesh);

        this.transform.localScale = scale;

        newMesh.SetUVs(1, vertexPositions);
        newMesh.SetUVs(2, normalPositions);
        newMesh.SetUVs(3, uvPositions);

        var meshName = this.gameObject.transform.GetHierarchyPath().Replace("\\", "_").Replace("/", "_");
        SaveMesh(newMesh, meshName);

        mesh.SetBlendShapeWeight(shape, initialVal);

        // disable parent renderer, children will render instead
        mesh.enabled = false;

        foreach (var existingChild in this.transform)
        {
            var subFilter = (existingChild as Transform)?.GetComponent<MeshFilter>();
            if (subFilter != null)
            {
                subFilter.sharedMesh = newMesh;
            }
        }

        var newObj = new GameObject(this.gameObject.name + "_AL;" + this.AudioLinkBand,
            typeof(MeshRenderer), typeof(MeshFilter), typeof(UdonBehaviour));

        newObj.transform.SetParent(this.gameObject.transform);
        newObj.transform.localPosition = Vector3.zero;
        newObj.transform.localRotation = Quaternion.identity;
        newObj.transform.localScale = Vector3.one;

        var filter = newObj.GetComponent<MeshFilter>();
        filter.sharedMesh = newMesh;
        filter.sharedMesh.bounds = new Bounds(Vector3.zero, new Vector3(
            Mathf.Max(Mathf.Abs(bounds1.extents.x), Mathf.Abs(bounds2.extents.x)) * 0.5f,
            Mathf.Max(Mathf.Abs(bounds1.extents.y), Mathf.Abs(bounds2.extents.y)) * 0.5f,
            Mathf.Max(Mathf.Abs(bounds1.extents.z), Mathf.Abs(bounds2.extents.z)) * 0.5f
        ));

        var rend = newObj.GetComponent<MeshRenderer>();
        this.AudioLinkBlendMaterial.enableInstancing = true;
        rend.sharedMaterials = Enumerable.Repeat(this.AudioLinkBlendMaterial, mesh.sharedMaterials.Length).ToArray();

        var udon = newObj.GetComponent<UdonBehaviour>();
        udon.programSource = ALConfigure;
    }

    void SaveMesh(Mesh mesh, string name)
    {
        var path = "Assets\\_pi_\\_AudioLinkBlend\\generated\\";
        System.IO.Directory.CreateDirectory(path);
        path += name + ".asset";
        MeshUtility.Optimize(mesh);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
    }

    Bounds CalculateBounds(Mesh mesh)
    {
        var min = Vector3.one * float.MaxValue;
        var max = Vector3.one * float.MinValue;
        foreach (var v in mesh.vertices)
        {
            // please don't ask me why we need to multiply by 500 here...
            min = Vector3.Min(v * 500.0f, min);
            max = Vector3.Max(v * 500.0f, max);
        }
        var ret = new Bounds(Vector3.zero, new Vector3(
            Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)) * 0.5f,
            Mathf.Max(Mathf.Abs(min.y), Mathf.Abs(max.y)) * 0.5f,
            Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z)) * 0.5f
        ));
        return ret;
    }

    public List<string> GetBlendShapes()
    {
        var mesh = this.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        var ret = new List<string>();
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            ret.Add(mesh.GetBlendShapeName(i));
        }
        return ret;
    }
}

[CustomEditor(typeof(ALBlendGenerator))]
public class ALBlendGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ALBlendGenerator gen = (ALBlendGenerator)target;

        EditorGUILayout.LabelField("Blend Shape:");
        var shapes = gen.GetBlendShapes();
        gen.shape = EditorGUILayout.Popup(gen.shape, shapes.ToArray());

        EditorGUILayout.LabelField("Blend Range:");
        EditorGUILayout.MinMaxSlider(ref gen.min, ref gen.max, 0.0f, 1.0f);

        if (GUILayout.Button("Generate"))
        {
            gen.DoWork();
        }
    }

}

#endif