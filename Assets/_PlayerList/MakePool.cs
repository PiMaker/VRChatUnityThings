using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;

[ExecuteInEditMode]
public class MakePool : MonoBehaviour
{
    public bool doWork = false;
    public int instances = 80;

    void Update()
    {
        if (doWork)
        {
            Debug.Log("[MakePool] Executing!");
            doWork = false;

            GameObject template = null;

            var first = true;
            var toDestroy = new List<GameObject>();
            foreach (var childTransform in this.gameObject.transform)
            {
                var obj = ((Transform)childTransform).gameObject;
                if (first)
                {
                    first = false;
                    template = obj;
                }
                else
                {
                    toDestroy.Add(obj);
                }
            }

            toDestroy.ForEach(DestroyImmediate);

            //var pool = this.GetComponent<VRCObjectPool>();

            //pool.Pool = new GameObject[instances];
            //pool.Pool[0] = template;

            for (int i = 1; i < instances; i++)
            {
                var clone = GameObject.Instantiate(template);
                clone.name = i.ToString();
                clone.transform.SetParent(this.transform, false);

                // pool.Pool[i] = clone;
            }
        }
    }
}
