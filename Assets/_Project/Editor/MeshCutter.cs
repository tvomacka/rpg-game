using System.Collections.Generic;
using UnityEngine;

namespace RpgGame.Editor
{
    /// <summary>
    /// Cuts a mesh at a height: keeps what is below, splits the triangles that cross the cut, and closes the
    /// top with a flat cap so the cut wall looks solid from the isometric camera. Used to make the low
    /// (cutaway) wall from KayKit's full wall. Editor-only: reads the source mesh even if it isn't readable.
    /// </summary>
    public static class MeshCutter
    {
        private struct Vertex
        {
            public Vector3 Position, Normal;
            public Vector2 Uv;

            public static Vertex Lerp(Vertex a, Vertex b, float t) => new()
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Normal = Vector3.Lerp(a.Normal, b.Normal, t).normalized,
                Uv = Vector2.Lerp(a.Uv, b.Uv, t),
            };
        }

        /// <param name="source">Mesh to cut (its local space, y up).</param>
        /// <param name="height">Cut height in the mesh's local units.</param>
        /// <param name="target">Cleared and filled with the result (one submesh).</param>
        public static void CutBelow(Mesh source, float height, Mesh target)
        {
            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            int[] triangles = source.triangles; // all submeshes; the KayKit wall has one material

            var outVertices = new List<Vertex>();
            var outTriangles = new List<int>();
            var polygon = new List<Vertex>(4);
            var corners = new Vertex[3];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int v = triangles[i + k];
                    corners[k] = new Vertex
                    {
                        Position = positions[v],
                        Normal = normals.Length > v ? normals[v] : Vector3.up,
                        Uv = uvs.Length > v ? uvs[v] : Vector2.zero,
                    };
                }

                ClipBelow(corners, height, polygon);
                if (polygon.Count < 3) continue;
                int start = outVertices.Count;
                outVertices.AddRange(polygon);
                for (int k = 1; k < polygon.Count - 1; k++) // fan; the clipped polygon is convex
                {
                    outTriangles.Add(start);
                    outTriangles.Add(start + k);
                    outTriangles.Add(start + k + 1);
                }
            }

            AddCap(outVertices, outTriangles, height, TopFaceUv(positions, normals, uvs));

            target.Clear();
            target.SetVertices(outVertices.ConvertAll(v => v.Position));
            target.SetNormals(outVertices.ConvertAll(v => v.Normal));
            target.SetUVs(0, outVertices.ConvertAll(v => v.Uv));
            target.SetTriangles(outTriangles, 0);
            target.RecalculateBounds();
            target.RecalculateTangents();
        }

        /// <summary>Sutherland–Hodgman against the half-space y ≤ height.</summary>
        private static void ClipBelow(Vertex[] triangle, float height, List<Vertex> result)
        {
            result.Clear();
            for (int k = 0; k < triangle.Length; k++)
            {
                Vertex a = triangle[k], b = triangle[(k + 1) % triangle.Length];
                bool aIn = a.Position.y <= height, bIn = b.Position.y <= height;
                if (aIn) result.Add(a);
                if (aIn != bIn)
                    result.Add(Vertex.Lerp(a, b, (height - a.Position.y) / (b.Position.y - a.Position.y)));
            }
        }

        /// <summary>A flat, up-facing quad over the cut's footprint, coloured like the wall's original top.</summary>
        private static void AddCap(List<Vertex> vertices, List<int> triangles, float height, Vector2 uv)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vertex v in vertices)
            {
                if (v.Position.y < height - 1e-4f) continue; // only the points on the cut
                minX = Mathf.Min(minX, v.Position.x);
                maxX = Mathf.Max(maxX, v.Position.x);
                minZ = Mathf.Min(minZ, v.Position.z);
                maxZ = Mathf.Max(maxZ, v.Position.z);
            }

            if (minX > maxX) return; // nothing crossed the cut

            int start = vertices.Count;
            foreach (Vector3 p in new[]
                     {
                         new Vector3(minX, height, minZ), new Vector3(minX, height, maxZ),
                         new Vector3(maxX, height, maxZ), new Vector3(maxX, height, minZ),
                     })
                vertices.Add(new Vertex { Position = p, Normal = Vector3.up, Uv = uv });

            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        /// <summary>UV of the highest up-facing vertex: the colour of the wall's top in the atlas.</summary>
        private static Vector2 TopFaceUv(Vector3[] positions, Vector3[] normals, Vector2[] uvs)
        {
            int best = -1;
            for (int i = 0; i < positions.Length && i < normals.Length && i < uvs.Length; i++)
                if (normals[i].y > 0.9f && (best < 0 || positions[i].y > positions[best].y)) best = i;
            return best >= 0 ? uvs[best] : Vector2.zero;
        }
    }
}
