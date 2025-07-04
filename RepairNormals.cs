using System;
using UnityEngine;

public class RepairNormals
{
    /// <summary>
    /// Soft Edge Normal => Hard Edge Normal
    /// </summary>
    /// <param name="filter">target Meshfilter</param>
    /// <param name="angle">default angle = 30deg</param>
    public static void NormalSmooth(ref MeshFilter filter, float angle = 30f)
    {
        Mesh mesh = new Mesh(){indexFormat = UnityEngine.Rendering.IndexFormat.UInt32};

        CopyMesh(filter.mesh, mesh);

        mesh.RecalculateNormals(angle);

        filter.mesh = mesh;
    }

    private static void CopyMesh(Mesh source, Mesh destination)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        Vector3[] v = new Vector3[source.vertices.Length];
        int[][] t = new int[source.subMeshCount][];
        Vector2[] u = new Vector2[source.uv.Length];
        Vector2[] u2 = new Vector2[source.uv2.Length];
        Vector4[] tan = new Vector4[source.tangents.Length];
        Vector3[] n = new Vector3[source.normals.Length];
        Color32[] c = new Color32[source.colors32.Length];

        Array.Copy(source.vertices, v, v.Length);

        for (int i = 0; i < t.Length; i++)
            t[i] = source.GetTriangles(i);

        Array.Copy(source.uv, u, u.Length);
        Array.Copy(source.uv2, u2, u2.Length);
        Array.Copy(source.normals, n, n.Length);
        Array.Copy(source.tangents, tan, tan.Length);
        Array.Copy(source.colors32, c, c.Length);

        destination.Clear();
        destination.name = source.name;

        destination.vertices = v;

        destination.subMeshCount = t.Length;

        for (int i = 0; i < t.Length; i++)
            destination.SetTriangles(t[i], i);

        destination.uv = u;
        destination.uv2 = u2;
        destination.tangents = tan;
        destination.normals = n;
        destination.colors32 = c;
    }
}
