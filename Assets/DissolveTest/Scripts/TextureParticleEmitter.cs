using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TextureParticleEmitter : MonoBehaviour
{
    private const string EmissionMapKey = "_EmissionMap";
    public ParticleSystem particleSystem;
    public bool createParticles = true;
    public int sample = 10;
    public int normalsDirection = 1;
    public float uvPresicion = 0.03f;

    private Vector2[] uvs;
    private Vector3[] normals;
    private Vector3[] vertices;

    List<MapParticleData> _mapParticleDatas = new List<MapParticleData>();
    //    Dictionary<int, List<MapParticleData>> _mapParticleGroupDatas = new Dictionary<int, List<MapParticleData>>();
    struct MapParticleData
    {
        public Vector2 texPosition;
        public Color color;
        public Vector2 uvPosition;
        public Vector3 position;
        public Vector3 normal;
    }

    void Start()
    {
        var mesh = GetComponent<MeshFilter>().mesh;
        uvs = mesh.uv;
        normals = mesh.normals;
        vertices = mesh.vertices;

        var mat = GetComponent<Renderer>().material;
        var emissionTexture2D = (Texture2D)mat.GetTexture(EmissionMapKey);
        if (emissionTexture2D == null)
        {
            Debug.LogError("material should have and emission map");
            return;
        }
        //CreateFromEmissiveTexture(emissionTexture2D);

        IterateUvs(emissionTexture2D);
    }

    private void IterateUvs(Texture2D emissionTexture2D)
    {
        for (var uvX = 0f; uvX <= 1f; uvX += uvPresicion)
        {
            for (var uvY = 0f; uvY <= 1f; uvY += uvPresicion)
            {
                var uvCoordinate = new Vector2(uvX, uvY);
                var x = uvX * emissionTexture2D.width;
                var y = uvY * emissionTexture2D.height;
                var color = emissionTexture2D.GetPixel((int)x, (int)y);
                if (color.Equals(Color.black))
                    continue;
                MapParticleData m = new MapParticleData();
                m.color = color;
                m.texPosition = new Vector2(x, y);
                m.uvPosition = uvCoordinate;
                m.position = UvTo3D(uvCoordinate);
                // How to??
                m.normal = FindNormal(m.position);//normals[0];

                if (_mapParticleDatas.Contains(m))
                    continue;
                _mapParticleDatas.Add(m);
            }
        }
        InstantiateFromMapParticleData();
    }


    private void CreateFromEmissiveTexture(Texture2D emissionTexture2D)
    {
        // Calculate points that are bright
        for (int x = 0; x < emissionTexture2D.width; x += sample)
        {
            for (int y = 0; y < emissionTexture2D.height; y += sample)
            {
                var color = emissionTexture2D.GetPixel(x, y);
                if (color.Equals(Color.black))
                    continue;
                var pos = new Vector2(x, y);
                var uvPos = new Vector2((float)x / emissionTexture2D.width, (float)y / emissionTexture2D.height);
                MapParticleData m = new MapParticleData();
                m.color = color;
                m.texPosition = pos;
                m.uvPosition = uvPos;
                m.position = UvTo3D(uvPos);
                // How to??
                m.normal = FindNormal(m.position);// normals[0];

                if (_mapParticleDatas.Contains(m))
                    continue;
                _mapParticleDatas.Add(m);
            }
        }
        InstantiateFromMapParticleData();
    }

    private void InstantiateFromMapParticleData()
    {
        if (!createParticles)
            return;
        //        Instantiate
        for (int index = 0; index < _mapParticleDatas.Count; index++)
        {
            var mapData = _mapParticleDatas[index];
            var particle = Instantiate(particleSystem, transform.TransformPoint(mapData.position), Quaternion.identity);
            particle.transform.SetParent(transform);
            particle.transform.rotation = Quaternion.LookRotation(transform.TransformDirection(mapData.normal * normalsDirection));

            var p = particle.main;
            p.startColor = new ParticleSystem.MinMaxGradient(Color.white, mapData.color);
        }
    }

    private Vector3 FindNormal(Vector3 pos)
    {
        int closestIndex = 0;
        float closestPoint = 100;
        for (int index = 0; index < vertices.Length; index++)
        {
            var vertice = vertices[index];
            if (Vector3.Dot(pos, vertice) < closestPoint)
            {
                closestPoint = Vector3.Dot(pos, vertice);
                closestIndex = index;
            }
        }
        return normals[closestIndex];
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        DrawData();
    }

    private void DrawData()
    {
        if (_mapParticleDatas == null)
            return;
        for (int index = 0; index < _mapParticleDatas.Count; index++)
        {
            var data = _mapParticleDatas[index];
            var pos = /*transform.TransformPoint*/ (data.position);
            Gizmos.DrawSphere(pos, 0.01f);
            var normDir = /* transform.TransformDirection*/ (data.normal / 10f * normalsDirection);
            Gizmos.DrawLine(pos, pos + normDir);
        }
    }

    /// <summary>
    /// Uvs the to3 d.
    /// Found from unity forum
    /// </summary>
    /// <param name="uv">The uv.</param>
    /// <returns></returns>
    Vector3 UvTo3D(Vector2 uv)
    {
        var mesh = GetComponent<MeshFilter>().mesh;
        var tris = mesh.triangles;
        var meshUv = mesh.uv;
        var verts = mesh.vertices;
        for (int i = 0; i < tris.Length; i += 3)
        {
            var u1 = meshUv[tris[i]]; // get the triangle UVs
            var u2 = meshUv[tris[i + 1]];
            var u3 = meshUv[tris[i + 2]];
            // calculate triangle area - if zero, skip it
            var a = Area(u1, u2, u3);
            if (a == 0f)
                continue;
            // calculate barycentric coordinates of u1, u2 and u3
            // if anyone is negative, point is outside the triangle: skip it
            var a1 = Area(u2, u3, uv) / a;
            if (a1 < 0)
                continue;
            var a2 = Area(u3, u1, uv) / a;
            if (a2 < 0)
                continue;
            var a3 = Area(u1, u2, uv) / a;
            if (a3 < 0)
                continue;
            // point inside the triangle - find mesh position by interpolation...
            var p3D = a1 * verts[tris[i]] + a2 * verts[tris[i + 1]] + a3 * verts[tris[i + 2]];
            // and return it in world coordinates:
            return (p3D);
        }
        // point outside any uv triangle: return Vector3.zero
        return Vector3.zero;
    }

    // calculate signed triangle area using a kind of "2D cross product":
    float Area(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        var v1 = p1 - p3;
        var v2 = p2 - p3;
        return (v1.x * v2.y - v1.y * v2.x) / 2;
    }

    public class MapParticleDataComparer : IComparer<MapParticleData>
    {
        int IComparer<MapParticleData>.Compare(MapParticleData a, MapParticleData b)
        {
            if (Vector3.Dot(a.position, b.position) < 0.01)
                return 1;
            return -1;
        }
    }
}
