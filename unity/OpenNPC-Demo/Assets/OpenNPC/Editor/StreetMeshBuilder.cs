using System.Collections.Generic;
using UnityEngine;

namespace OpenNPC.EditorTools
{
    /// <summary>
    /// Accumulates ink lines, rectangles and circles into ONE mesh, so the entire
    /// line-drawn street is a single draw call. Facade shapes are drawn in the XY
    /// plane at a given depth; ground shapes lie flat in XZ.
    /// </summary>
    public sealed class StreetMeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<int> _t = new List<int>();

        public int QuadCount => _t.Count / 6;

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
        }

        /// <summary>A line segment in the facade plane (constant z).</summary>
        public void Line(float x0, float y0, float x1, float y1, float z, float width)
        {
            Vector2 dir = new Vector2(x1 - x0, y1 - y0);
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            // Extend by half a width so corners meet without notches.
            Vector2 ext = dir * (width * 0.5f);
            Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            Vector2 a = new Vector2(x0, y0) - ext, b = new Vector2(x1, y1) + ext;
            Quad(new Vector3(a.x - n.x, a.y - n.y, z), new Vector3(a.x + n.x, a.y + n.y, z),
                 new Vector3(b.x + n.x, b.y + n.y, z), new Vector3(b.x - n.x, b.y - n.y, z));
        }

        public void Rect(float x0, float y0, float x1, float y1, float z, float width)
        {
            Line(x0, y0, x1, y0, z, width);
            Line(x1, y0, x1, y1, z, width);
            Line(x1, y1, x0, y1, z, width);
            Line(x0, y1, x0, y0, z, width);
        }

        public void FilledRect(float x0, float y0, float x1, float y1, float z) =>
            Quad(new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z));

        public void Circle(float cx, float cy, float radius, float z, float width, int segments = 32)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 o0 = new Vector3(cx + Mathf.Cos(a0) * (radius + width * 0.5f), cy + Mathf.Sin(a0) * (radius + width * 0.5f), z);
                Vector3 o1 = new Vector3(cx + Mathf.Cos(a1) * (radius + width * 0.5f), cy + Mathf.Sin(a1) * (radius + width * 0.5f), z);
                Vector3 i0 = new Vector3(cx + Mathf.Cos(a0) * (radius - width * 0.5f), cy + Mathf.Sin(a0) * (radius - width * 0.5f), z);
                Vector3 i1 = new Vector3(cx + Mathf.Cos(a1) * (radius - width * 0.5f), cy + Mathf.Sin(a1) * (radius - width * 0.5f), z);
                Quad(i0, o0, o1, i1);
            }
        }

        /// <summary>A line lying on the ground (y = 0) from (x0, z0) to (x1, z1).</summary>
        public void GroundLine(float x0, float z0, float x1, float z1, float width, float y = 0.004f)
        {
            Vector2 dir = new Vector2(x1 - x0, z1 - z0).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            Quad(new Vector3(x0 - n.x, y, z0 - n.y), new Vector3(x0 + n.x, y, z0 + n.y),
                 new Vector3(x1 + n.x, y, z1 + n.y), new Vector3(x1 - n.x, y, z1 - n.y));
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetTriangles(_t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
