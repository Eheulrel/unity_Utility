using System.Collections.Generic;
using UnityEngine;

public static class NormalSupport
{
    private struct VertexKey
    {
        private readonly long _x;
        private readonly long _y;
        private readonly long _z;

        private const int Tolerance = 100000;

        private const long FNV32Init = 0x811c9dc5;
        private const long FNV32Prime = 0x01000193;

        public VertexKey(Vector3 position)
        {
            _x = (long) (Mathf.Round(position.x * Tolerance));
            _y = (long) (Mathf.Round(position.y * Tolerance));
            _z = (long) (Mathf.Round(position.z * Tolerance));
        }

        public override bool Equals(object obj)
        {
            VertexKey key = (VertexKey)obj;
            return _x == key._x && _y == key._y && _z == key._z;
        }

        public override int GetHashCode()
        {
            long rv = FNV32Init;
            rv ^= _x;
            rv *= FNV32Prime;
            rv ^= _y;
            rv *= FNV32Prime;
            rv ^= _z;
            rv *= FNV32Prime;

            return rv.GetHashCode();
        }
    }

    private struct VertexEntry
    {
        public int MeshIndex;
        public int TriangleIndex;
        public int VertexIndex;

        public VertexEntry(int meshIndex, int triIndex, int vertIndex)
        {
            MeshIndex = meshIndex;
            TriangleIndex = triIndex;
            VertexIndex = vertIndex;
        }
    }

    public static void UnweldVertices(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;

        List<Vector3> unweldedVerticesList = new List<Vector3>();
        int[][] unweldedSubTriangles = new int[mesh.subMeshCount][];
        List<Vector2> unweldedUvsList = new List<Vector2>();
        int currVertex = 0;

        for(int i=0; i<mesh.subMeshCount; i++)
        {
            int[] triangles = mesh.GetTriangles(i);
            Vector3[] unweldedVertices = new Vector3[triangles.Length];
            int[] unweldedTriangels = new int[triangles.Length];
            Vector2[] unweldedUVs = new Vector2[unweldedVertices.Length];

            for(int j=0; j<triangles.Length; j++)
            {
                unweldedVertices[j] = vertices[triangles[j]];
                if(uvs.Length > triangles[j])
                {
                    unweldedUVs[j] = uvs[triangles[j]];
                }

                unweldedTriangels[j] = currVertex;
                currVertex++;                
            }

            unweldedVerticesList.AddRange(unweldedVertices);
            unweldedSubTriangles[i] = unweldedTriangels;
            unweldedUvsList.AddRange(unweldedUVs);            
        }

        mesh.vertices = unweldedVerticesList.ToArray();
        mesh.uv = unweldedUvsList.ToArray();

        for(int i=0; i<mesh.subMeshCount; i++)
        {
            mesh.SetTriangles(unweldedSubTriangles[i], i, false);
        }

        RecalculateTangents(mesh);
    }

    public static void RecalculateTangents(Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        Vector3[] normals = mesh.normals;

        int triangleCount = triangles.Length;
        int vertexCount = vertices.Length;

        Vector3[] tan1 = new Vector3[vertexCount];
        Vector3[] tan2 = new Vector3[vertexCount];

        Vector4[] tangents = new Vector4[vertexCount];
        
        for(int a = 0; a < triangleCount; a+=3)
        {
            int i1 = triangles[a + 0];
            int i2 = triangles[a + 1];
            int i3 = triangles[a + 2];

            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 v3 = vertices[i3];

            Vector2 w1 = uv[i1];
            Vector2 w2 = uv[i2];
            Vector2 w3 = uv[i3];

            float x1 = v2.x - v1.x;
            float x2 = v3.x - v1.x;

            float y1 = v2.y - v1.y;
            float y2 = v3.y - v1.y;
            
            float z1 = v2.z - v1.z;
            float z2 = v3.z - v1.z;

            float s1 = w2.x - w1.x;
            float s2 = w3.x - w1.x;

            float t1 = w2.y - w1.y;
            float t2 = w3.y - w1.y;

            float div = s1 * t2 - s2 * t1;
            float r = (div == 0.0f) ? 0.0f : (1.0f / div);

            Vector3 sDir = new Vector3((t2 * x1 - t1 * x2)*r, (t2*y1 - t1*y2)*r, (t2*z1 - t1*z2)*r);
            Vector3 tDir = new Vector3((s1*x2-s2*x1)*r, (s1*y2-s2*y1)*r, (s1*z2-s2*z1)*r);

            tan1[i1] += sDir;
            tan1[i2] += sDir;
            tan1[i3] += sDir;

            tan2[i1] += tDir;
            tan2[i2] += tDir;
            tan2[i3] += tDir;
        }

        for(int a = 0; a < vertexCount; ++a)
        {
            Vector3 n = normals[a];
            Vector3 t = tan1[a];

            Vector3.OrthoNormalize(ref n, ref t);
            tangents[a].x = t.x;
            tangents[a].y = t.y;
            tangents[a].z = t.z;

            tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a])<0f) ? -1f : 1f;
        }

        mesh.tangents = tangents;
    }

    public static void RecalculateNormals(this Mesh mesh, float angle)
    {
        UnweldVertices(mesh);

        float cosineThreshold = Mathf.Cos(angle * Mathf.Deg2Rad);

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = new Vector3[vertices.Length];

        Vector3[][] triNormals = new Vector3[mesh.subMeshCount][];

        Dictionary<VertexKey, List<VertexEntry>> dictionary = new Dictionary<VertexKey, List<VertexEntry>>(vertices.Length);

        for(int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; ++subMeshIndex)
        {
            int[] tiriangles = mesh.GetTriangles(subMeshIndex);

            triNormals[subMeshIndex] = new Vector3[tiriangles.Length/3];

            for (int i=0; i<tiriangles.Length; i+=3)
            {
                int i1 = tiriangles[i];
                int i2 = tiriangles[i+1];
                int i3 = tiriangles[i+2];

                Vector3 p1 = vertices[i2] - vertices[i1];
                Vector3 p2 = vertices[i3] - vertices[i1];
                Vector3 normal = Vector3.Cross(p1, p2);
                float magnitude = normal.magnitude;
                if(magnitude > 0)
                {
                    normal /= magnitude;
                }

                int triIndex = i / 3;
                triNormals[subMeshIndex][triIndex] = normal;

                List<VertexEntry> entry;
                VertexKey key;

                if(!dictionary.TryGetValue(key = new VertexKey(vertices[i1]), out entry))
                {
                    entry = new List<VertexEntry>(4);
                    dictionary.Add(key, entry);
                }

                entry.Add(new VertexEntry(subMeshIndex, triIndex, i1));

                if(!dictionary.TryGetValue(key = new VertexKey(vertices[i2]), out entry))
                {
                    entry = new List<VertexEntry>();
                    dictionary.Add(key, entry);
                }

                entry.Add(new VertexEntry(subMeshIndex, triIndex, i2));

                if(!dictionary.TryGetValue(key = new VertexKey(vertices[i3]), out entry))
                {
                    entry = new List<VertexEntry>();
                    dictionary.Add(key, entry);                    
                }

                entry.Add(new VertexEntry(subMeshIndex, triIndex, i3));
            }

            foreach(List<VertexEntry> vertList in dictionary.Values)
            {
                for(int i=0; i<vertList.Count; ++i)
                {
                    Vector3 sum = new Vector3();
                    VertexEntry lhsEntry = vertList[i];

                    for(int j=0; j<vertList.Count; ++j)
                    {
                        VertexEntry rhsEntry = vertList[j];

                        if(lhsEntry.VertexIndex == rhsEntry.VertexIndex)
                        {
                            sum += triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex];
                        }
                        else
                        {
                            float dot = Vector3.Dot(
                                triNormals[lhsEntry.MeshIndex][lhsEntry.TriangleIndex],
                                triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex]
                            );

                            if(dot >= cosineThreshold)
                            {
                                sum += triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex];
                            }
                        }
                    }

                    normals[lhsEntry.VertexIndex] = sum.normalized;
                }                
            }

            mesh.normals = normals;
        }
    }
}
