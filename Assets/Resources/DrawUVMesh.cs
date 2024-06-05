using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawUVMesh : MonoBehaviour
{
    Mesh mesh = null;
    public Texture2D tex;

    private void OnDrawGizmos()
    {
        if (mesh == null)
        {
            mesh = ConservativeRaster.GenerateUVMesh(GetComponent<MeshFilter>().sharedMesh);
        }

        var mat = Resources.Load<Material>("DebugView");
        mat.mainTexture = null;
        mat.color = Color.red;
        mat.SetPass(0);
        Graphics.DrawMeshNow(mesh, Matrix4x4.identity, 0);

        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        mat.mainTexture = tex;
        mat.color = Color.white;
        mat.SetPass(0);
        Graphics.DrawMeshNow(quad, Matrix4x4.Translate(new Vector3(0.5f, 0.5f, -0.00001f)), 0);
    }
}
