using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace HistoricalRimWorld.World
{
    /// <summary>Draws a cached, post-world-layer black mesh over mapped Locked prototype tiles.</summary>
    public static class HistoricalWorldFogRenderer
    {
        private static Mesh fogMesh;
        private static bool dirty = true;
        private static readonly List<Vector3> tileVertices = new List<Vector3>(8);

        public static void MarkDirty()
        {
            dirty = true;
        }

        public static void Draw()
        {
            if (!HistoricalWorldRevealManager.IsActive || !HistoricalWorldTileAccessCache.MappingAvailable) return;
            if (dirty) RebuildMesh();
            if (fogMesh == null || fogMesh.vertexCount == 0) return;

            Material material = WorldMaterials.VertexColor;
            if (material != null) Graphics.DrawMesh(fogMesh, Matrix4x4.identity, material, 0);
        }

        private static void RebuildMesh()
        {
            dirty = false;
            if (fogMesh != null)
            {
                Object.Destroy(fogMesh);
                fogMesh = null;
            }
            if (Find.WorldGrid == null || HistoricalWorldTileAccessCache.LockedTileIds.Count == 0) return;

            List<Vector3> vertices = new List<Vector3>();
            List<Color32> colors = new List<Color32>();
            List<int> triangles = new List<int>();
            foreach (int tileId in HistoricalWorldTileAccessCache.LockedTileIds.OrderBy(id => id))
            {
                tileVertices.Clear();
                Find.WorldGrid.GetTileVertices(tileId, tileVertices);
                if (tileVertices.Count < 3) continue;

                Vector3 center = Find.WorldGrid.GetTileCenter(tileId).normalized *
                    (Find.WorldGrid.GetTileCenter(tileId).magnitude + 0.0005f);
                int first = vertices.Count;
                vertices.Add(center);
                colors.Add(new Color32(0, 0, 0, 255));
                foreach (Vector3 vertex in tileVertices)
                {
                    vertices.Add(vertex.normalized * (vertex.magnitude + 0.0005f));
                    colors.Add(new Color32(0, 0, 0, 255));
                }

                for (int index = 0; index < tileVertices.Count; index++)
                {
                    int firstEdge = first + index + 1;
                    int secondEdge = first + ((index + 1) % tileVertices.Count) + 1;
                    triangles.Add(first);
                    triangles.Add(firstEdge);
                    triangles.Add(secondEdge);
                    triangles.Add(first);
                    triangles.Add(secondEdge);
                    triangles.Add(firstEdge);
                }
            }

            if (vertices.Count == 0) return;
            fogMesh = new Mesh { name = "Historical RimWorld Locked Region Fog", indexFormat = IndexFormat.UInt32 };
            fogMesh.SetVertices(vertices);
            fogMesh.SetColors(colors);
            fogMesh.SetTriangles(triangles, 0, true);
            fogMesh.RecalculateNormals();
        }
    }
}
