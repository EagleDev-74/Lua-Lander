using System;
using UnityEngine;

public static class MeshUtility
{
    private static Quaternion [] _cachedQuaternionEulerArr;

    [RuntimeInitializeOnLoadMethod (RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CacheQuaternionEuler ()
    {
        if (_cachedQuaternionEulerArr != null) return;
            
        _cachedQuaternionEulerArr = new Quaternion [360];
        for (int i = 0; i < 360; i++)
        {
            _cachedQuaternionEulerArr [i] = Quaternion.Euler (0, 0, i);
        }
    }

    private static Quaternion GetQuaternionEuler (float rotationValue)
    {
        int rotation = Mathf.RoundToInt (rotationValue);
        rotation %= 360;
        if (rotation < 0) rotation += 360;
        // if (rot >= 360) rot -= 360;
        if (_cachedQuaternionEulerArr == null) CacheQuaternionEuler ();
        return _cachedQuaternionEulerArr != null ? _cachedQuaternionEulerArr [rotation] : Quaternion.identity;
    }

    private static Mesh CreateEmptyMesh ()
    {
        return new Mesh
        {
            vertices = Array.Empty <Vector3> (),
            uv = Array.Empty <Vector2> (),
            triangles = Array.Empty <int> ()
        };
    }

    public static void CreateEmptyMeshArray (int quadCount,
        out Vector3 [] vertices,
        out Vector2 [] uv,
        out int [] triangles)
    {
        vertices = new Vector3 [4 * quadCount];
        uv = new Vector2 [4 * quadCount];
        triangles = new int [6 * quadCount];
    }

    public static Mesh AddToMesh (Mesh mesh, Vector3 position, float rotation, Vector3 baseSize, Vector2 uv00, Vector2 uv11)
    {
        if (!mesh)
        {
            mesh = CreateEmptyMesh ();
        }

        Vector3 [] vertices = new Vector3 [4 + mesh.vertices.Length];
        Vector2 [] uvs = new Vector2 [4 + mesh.uv.Length];
        int [] triangles = new int [6 + mesh.triangles.Length];

        mesh.vertices.CopyTo (vertices, 0);
        mesh.uv.CopyTo (uvs, 0);
        mesh.triangles.CopyTo (triangles, 0);

        int index = vertices.Length / 4 - 1;
            
        // Relocate vertices
        int vIndex0 = index * 4;
        int vIndex1 = vIndex0 + 1;
        int vIndex2 = vIndex0 + 2;
        int vIndex3 = vIndex0 + 3;

        baseSize *= 0.5f;

        bool skewed = !Mathf.Approximately (baseSize.x, baseSize.y);
        if (skewed)
        {
            vertices [vIndex0] = position + GetQuaternionEuler (rotation) * new Vector3 (-baseSize.x, baseSize.y);
            vertices [vIndex1] = position + GetQuaternionEuler (rotation) * new Vector3 (-baseSize.x, -baseSize.y);
            vertices [vIndex2] = position + GetQuaternionEuler (rotation) * new Vector3 (baseSize.x, -baseSize.y);
            vertices [vIndex3] = position + GetQuaternionEuler (rotation) * baseSize;
        }
        else
        {
            vertices [vIndex0] = position + GetQuaternionEuler (rotation - 270) * baseSize;
            vertices [vIndex1] = position + GetQuaternionEuler (rotation - 180) * baseSize;
            vertices [vIndex2] = position + GetQuaternionEuler (rotation - 90) * baseSize;
            vertices [vIndex3] = position + GetQuaternionEuler (rotation - 0) * baseSize;
        }

        // Relocate UVs
        uvs [vIndex0] = new Vector2 (uv00.x, uv11.y);
        uvs [vIndex1] = new Vector2 (uv00.x, uv00.y);
        uvs [vIndex2] = new Vector2 (uv11.x, uv00.y);
        uvs [vIndex3] = new Vector2 (uv11.x, uv11.y);

        // Create triangles
        int tIndex = index * 6;

        triangles [tIndex + 0] = vIndex0;
        triangles [tIndex + 1] = vIndex3;
        triangles [tIndex + 2] = vIndex1;

        triangles [tIndex + 3] = vIndex1;
        triangles [tIndex + 4] = vIndex3;
        triangles [tIndex + 5] = vIndex2;

        mesh.SetVertices (vertices);
        mesh.SetTriangles (triangles, 0);
        mesh.SetUVs (0, uvs);

        // mesh.bounds = bounds;

        return mesh;
    }

    public static void AddToMeshArrays (Vector3 [] vertices, Vector2 [] uvs, int [] triangles, int index,
        Vector3 pos, float rot, Vector3 baseSize, Vector2 uv00, Vector2 uv11)
    {
        // Relocate vertices
        int vIndex0 = index * 4;
        int vIndex1 = vIndex0 + 1;
        int vIndex2 = vIndex0 + 2;
        int vIndex3 = vIndex0 + 3;

        baseSize *= 0.5f;

        bool skewed = !Mathf.Approximately (baseSize.x, baseSize.y);
        if (skewed)
        {
            vertices [vIndex0] = pos + GetQuaternionEuler (rot) * new Vector3 (-baseSize.x, baseSize.y);
            vertices [vIndex1] = pos + GetQuaternionEuler (rot) * new Vector3 (-baseSize.x, -baseSize.y);
            vertices [vIndex2] = pos + GetQuaternionEuler (rot) * new Vector3 (baseSize.x, -baseSize.y);
            vertices [vIndex3] = pos + GetQuaternionEuler (rot) * baseSize;
        }
        else
        {
            vertices [vIndex0] = pos + GetQuaternionEuler (rot - 270) * baseSize;
            vertices [vIndex1] = pos + GetQuaternionEuler (rot - 180) * baseSize;
            vertices [vIndex2] = pos + GetQuaternionEuler (rot - 90) * baseSize;
            vertices [vIndex3] = pos + GetQuaternionEuler (rot - 0) * baseSize;
        }

        // Relocate UVs
        uvs [vIndex0] = new Vector2 (uv00.x, uv11.y);
        uvs [vIndex1] = new Vector2 (uv00.x, uv00.y);
        uvs [vIndex2] = new Vector2 (uv11.x, uv00.y);
        uvs [vIndex3] = new Vector2 (uv11.x, uv11.y);

        // Create triangles
        int tIndex = index * 6;

        triangles [tIndex + 0] = vIndex0;
        triangles [tIndex + 1] = vIndex3;
        triangles [tIndex + 2] = vIndex1;

        triangles [tIndex + 3] = vIndex1;
        triangles [tIndex + 4] = vIndex3;
        triangles [tIndex + 5] = vIndex2;
    }
}
