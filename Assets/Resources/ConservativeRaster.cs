using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ConservativeRaster
{
    [MenuItem("ConservativeRaster/Render")]
    public static void Run()
    {
        var bunny = Resources.Load<Mesh>("Bunny");
        var uvMesh = GenerateUVMesh(bunny);

        var hw = GenerateMaskHardware(uvMesh, 1024);
        File.WriteAllBytes("Assets/Hardware.png", hw.EncodeToPNG());
        var vtx = GenerateMaskVertexShader(uvMesh, 1024);
        File.WriteAllBytes("Assets/Vertex.png", vtx.EncodeToPNG());

        var hwPixels = hw.GetPixels();
        var vtxPixels = vtx.GetPixels();
        int diff = 0;
        for (int i = 0; i < hwPixels.Length; i++)
        {
            if (hwPixels[i] != vtxPixels[i])
                diff++;
        }
        Debug.Log("Diff pixels: " + diff);

        AssetDatabase.Refresh();
    }

    public static Mesh GenerateUVMesh(Mesh from)
    {
        Mesh mesh = new Mesh();
        mesh.vertices = from.uv2.Select(uv => new Vector3(uv.x, uv.y, 0)).ToArray();
        mesh.triangles = from.triangles;
        return mesh;
    }

    public static Mesh GenerateUniqueVertMesh(Mesh from)
    {
        var originalVerts = from.vertices;
        var originalIndices = from.triangles;

        Vector3[] verts = new Vector3[originalIndices.Length];
        int[] indices = new int[originalIndices.Length];

        for (int i = 0; i < originalIndices.Length; i++)
        {
            verts[i] = originalVerts[originalIndices[i]];
            indices[i] = i;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.triangles = indices;

        return mesh;
    }

    public static Texture2D RenderUVMeshToTexture(Mesh uvMesh, Material mat, int size)
    {
        RenderTexture rt = RenderTexture.GetTemporary(size, size);

        using var cmd = new CommandBuffer();
        cmd.SetRenderTarget(rt);
        cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0, 1, 0, 1, 0, 1));
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.DrawMesh(uvMesh, Matrix4x4.identity, mat);
        Graphics.ExecuteCommandBuffer(cmd);

        RenderTexture.active = rt;
        Texture2D res = new Texture2D(size, size);
        res.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        RenderTexture.active = null;

        RenderTexture.ReleaseTemporary(rt);

        return res;
    }

    public static Texture2D GenerateMaskHardware(Mesh uvMesh, int size)
    {
        var mat = Resources.Load<Material>("ConservativeRasterHardware");
        return RenderUVMeshToTexture(uvMesh, mat, size);
    }

    public static Texture2D GenerateMaskVertexShader(Mesh uvMesh, int size)
    {
        Mesh uniqueVertMesh = GenerateUniqueVertMesh(uvMesh);
        var mat = Resources.Load<Material>("ConservativeRasterVertex");
        using var vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, uniqueVertMesh.vertexCount, UnsafeUtility.SizeOf<Vector3>());
        vertexBuffer.SetData(uniqueVertMesh.vertices);
        mat.SetBuffer("_VertexBuffer", vertexBuffer);
        var res = RenderUVMeshToTexture(uniqueVertMesh, mat, size);
        Object.DestroyImmediate(uniqueVertMesh);
        return res;
    }
}
