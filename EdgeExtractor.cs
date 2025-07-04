using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EdgeExtractor
{
    struct Edge
    {
        public int v1;
        public int v2;
        public Edge(int a, int b)
        {
            if(a < b)
            {
                v1 = a;
                v2 = b;
            }
            else
            {
                v1 = b;
                v2 = a;
            }
        }

        public override bool Equals(object obj)
        {
            if(!(obj is Edge)) return false;

            Edge other = (Edge)obj;
            return v1 == other.v1 && v2 == other.v2;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + v1.GetHashCode();
                hash = hash * 31 + v2.GetHashCode();

                return hash;
            }
        }
    }

    private Dictionary<Edge, List<int>> edgeToTriangles = new Dictionary<Edge, List<int>>();

    private readonly float angleThreshold = 10f;
    private float thickness = 0.04f;

    /// <summary>
    /// Mesh기반으로 외곽선을 계산하여 표시하는 메소드드
    /// </summary>
    /// <param name="targetContainer">Target 모델들이 있는 Container</param>
    /// <param name="edgeContainer">CompartmentView, ComponentInput에서 면적 계산에 제외되려면 Container를 따로 관리해야함함</param>
    /// <param name="emptyObject">Line Mesh를 가지게 될 빈 오브젝트, 메소드에서 생성하지 않는 이유는 관리가 어렵기 때문</param>
    /// <param name="edgeMaterial">edge material, 매번 new Material을 생성 하는 것은 문제가 있기 때문</PARAM>
    public void CreateEdgeline(Transform targetContainer, GameObject emptyObject, Transform edgeContainer, Material edgeMaterial)
    {        
        MeshFilter[] filters = targetContainer.GetComponentsInChildren<MeshFilter>();

        if(filters == null)
        {
            return;
        }

        foreach(var f in filters)
        {
            Mesh edge = EdgeCalculate(f);

            GameObject lines = GameObject.Instantiate(emptyObject, edgeContainer);
            lines.SetActive(true);
            
            MeshFilter filter = lines.GetComponent<MeshFilter>();
            MeshRenderer renderer = lines.GetComponent<MeshRenderer>();
            
            filter.mesh = edge;
            renderer.material = edgeMaterial;

            edgeToTriangles.Clear();
        }
    }
    
        /// <summary>
    /// 마지막에 true 넣으면 tickness 자동으로 계산하도록록
    /// </summary>
    /// <param name="targetContainer"></param>
    /// <param name="emptyObject"></param>
    /// <param name="edgeContainer"></param>
    /// <param name="edgeMaterial"></param>
    /// <param name="len"></param>
    public List<GameObject> CreateEdgeline(Transform targetContainer, GameObject emptyObject, Transform edgeContainer, Material edgeMaterial, bool hasOutline)
    {
        MeshFilter[] filters = targetContainer.GetComponentsInChildren<MeshFilter>();
        Bounds bounds = new Bounds();
        foreach (var filter in filters)
        {
            if (filter == null || filter.mesh == null) continue;
            bounds.Encapsulate(filter.mesh.bounds);
        }
        float max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

        thickness = 0.004f + ((0.15f - 0.004f) * (max - 1) / (100 - 1));

        if (filters == null)
        {
            return null;
        }
        else
        {
            List<GameObject> edgeLines = new List<GameObject>(filters.Length);
            foreach (var f in filters)
            {
                Mesh edge = EdgeCalculate(f);

                GameObject lines = GameObject.Instantiate(emptyObject, edgeContainer);
                lines.SetActive(true);

                MeshFilter filter = lines.GetComponent<MeshFilter>();
                MeshRenderer renderer = lines.GetComponent<MeshRenderer>();

                filter.mesh = edge;
                renderer.material = edgeMaterial;

                edgeToTriangles.Clear();

                edgeLines.Add(lines);
            }

            if (hasOutline)
                return edgeLines;
            else
            {
                edgeLines.Clear();
                return null;
            }
        }
    }

    private Mesh EdgeCalculate(MeshFilter filter)
    {
        Mesh mesh = filter.mesh;
        mesh.Optimize();

        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        // Edge 계산
        CalculateEdge(triangles);

        // FaceNormal 계산산
        Vector3[] triangleFaceNormals = new Vector3[triangles.Length / 3];
        CalculateFaceNormal(triangleFaceNormals, triangles, vertices);

        // 최종 Edge 계산
        List<Edge> finalEdge = new List<Edge>();
        CalculateFinalEdge(finalEdge, triangleFaceNormals);

        // 계산된 Edge를 기반으로 Line을 표현하기 위한 Quad 생성
        List<Vector3> edgeVertices = new List<Vector3>();
        List<int> edgeIndices = new List<int>();
        CreateQuad(edgeVertices, edgeIndices, finalEdge, vertices);

        Mesh edgeMesh = new()
        {
            vertices = edgeVertices.ToArray(),
            triangles = edgeIndices.ToArray()
        };

        edgeMesh.RecalculateBounds();

        return edgeMesh;
    }

    private void CalculateEdge(int[] triangles)
    {
        for(int t=0; t<triangles.Length; t+=3)
        {
            int i0 = triangles[t];
            int i1 = triangles[t+1];
            int i2 = triangles[t+2];

            Edge e01 = new(i0,i1);
            Edge e12 = new(i1,i2);
            Edge e20 = new(i2,i0);

            AddEdgeTriangle(e01, t/3);
            AddEdgeTriangle(e12, t/3);
            AddEdgeTriangle(e20, t/3);
        }
    }

    private void CalculateFaceNormal(Vector3[] triangleFaceNormals, int[] triangles, Vector3[] vertices)
    {
        for(int i=0; i<triangles.Length; i+=3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i+1];
                int i2 = triangles[i+2];

                Vector3 p0 = vertices[i0];
                Vector3 p1 = vertices[i1];
                Vector3 p2 = vertices[i2];

                // (p1-p0) * (p2-p0) 로 면 노말 계산, 정규화
                Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(p1-p0, p2-p0));
                triangleFaceNormals[i/3] = faceNormal;
            }
    }

    private void AddEdgeTriangle(Edge e, int triangleIndex)
    {
        if(!edgeToTriangles.ContainsKey(e))
        {
            edgeToTriangles[e] = new List<int>();
        }
        edgeToTriangles[e].Add(triangleIndex);
    }

    private void CalculateFinalEdge(List<Edge> finalEdges, Vector3[] triangleFaceNormals)
    {
        foreach(var kvp in edgeToTriangles)
            {
                Edge e = kvp.Key;
                List<int> triList = kvp.Value;
                if(triList.Count == 1)
                {
                    //외곽 에지
                    finalEdges.Add(e);
                }
                else if(triList.Count ==2)
                {                    
                    Vector3 n1 = triangleFaceNormals[triList[0]];
                    Vector3 n2 = triangleFaceNormals[triList[1]];
                    float angle = Vector3.Angle(n1, n2);

                    if(angle > angleThreshold)
                    {
                        finalEdges.Add(e);
                    }
                }
            }
    }

    private void CreateQuad(List<Vector3> edgeVertices, List<int> edgeIndices, List<Edge> finalEdges, Vector3[] vertices)
    {
         foreach(var edge in finalEdges)
            {
                int i0 = edge.v1;
                int i1 = edge.v2;

                Vector3 p0 = vertices[i0];
                Vector3 p1 = vertices[i1];

                Vector3 dir = p1 - p0;

                // Edge에 수직인 임의의 벡터가 필요 -> Vector3.up (0,1,0)과 교차,, dir과 수평이면 다른 방향으로 해야함
                Vector3 up = Vector3.up;
                if(Mathf.Abs(Vector3.Dot(dir.normalized, up)) > 0.99f)
                {
                    // dir와 up이 수평하면 다른 축으로 변경
                    up = Vector3.forward;
                }

                Vector3 perpendicular = Vector3.Cross(dir, up).normalized * thickness;

                // p0, p1을 기준으로 양 옆으로 perpendicular 만큼 벌려서 quad 구성
                // p0Left, p0Right, p1Left, p1Right 로 2개의 삼각형 생성 = 1 Quad
                Vector3 p0L = p0 - perpendicular;
                Vector3 p0R = p0 + perpendicular;
                Vector3 p1L = p1 - perpendicular;
                Vector3 p1R = p1 + perpendicular;

                int baseindex = edgeVertices.Count;
                edgeVertices.Add(p0L); // 0
                edgeVertices.Add(p0R); // 1
                edgeVertices.Add(p1L); // 2
                edgeVertices.Add(p1R); // 3

                // index 
                edgeIndices.Add(baseindex + 0);
                edgeIndices.Add(baseindex + 2);
                edgeIndices.Add(baseindex + 1);

                edgeIndices.Add(baseindex + 2);
                edgeIndices.Add(baseindex + 3);
                edgeIndices.Add(baseindex + 1);                
            }
    }
}
