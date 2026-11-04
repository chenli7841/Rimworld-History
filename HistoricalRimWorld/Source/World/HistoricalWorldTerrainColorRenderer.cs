using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace HistoricalRimWorld.World
{
    /// <summary>Draws a cached, per-tile color overlay after all native world layers.</summary>
    public static class HistoricalWorldTerrainColorRenderer
    {
        private static RimWorld.Planet.World cachedWorld;
        private static Mesh colorMesh;
        private static bool dirty = true;
        private static RimWorld.Planet.World lastLoggedWorld;
        private static readonly List<Vector3> tileVertices = new List<Vector3>(8);
        private const float SurfaceOffset = 0.05f;

        public static void MarkDirty() => dirty = true;

        public static void DrawAfterWorldLayers()
        {
            RimWorld.Planet.World world = Find.World;
            if (world?.grid?.Surface == null) return;
            if (!ReferenceEquals(world, cachedWorld))
            {
                cachedWorld = world;
                dirty = true;
            }
            if (dirty) Rebuild(world);
            if (colorMesh == null || colorMesh.vertexCount == 0) return;
            Material material = WorldMaterials.VertexColorTransparent;
            if (material != null)
                Graphics.DrawMesh(colorMesh, Matrix4x4.identity, material, WorldCameraManager.WorldLayer);
        }

        private static void Rebuild(RimWorld.Planet.World world)
        {
            dirty = false;
            if (colorMesh != null)
            {
                Object.Destroy(colorMesh);
                colorMesh = null;
            }
            if (world?.grid?.Surface == null) return;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Color32> colors = new List<Color32>();
            List<int> triangles = new List<int>();
            int coloredTileCount = 0;
            for (int tileId = 0; tileId < world.grid.Surface.TilesCount; tileId++)
            {
                if (!HistoricalWorldTerrainColorManager.TryGetEffectiveColor(world, tileId,
                    out Color color, out string source, out string hex)) continue;
                tileVertices.Clear();
                world.grid.GetTileVertices(tileId, tileVertices);
                if (tileVertices.Count < 3) continue;

                Vector3 tileCenter = world.grid.GetTileCenter(tileId);
                Vector3 centerNormal = tileCenter.normalized;
                Vector3 center = centerNormal * (tileCenter.magnitude + SurfaceOffset);
                int first = vertices.Count;
                vertices.Add(center);
                Color32 tint = new Color32((byte)Mathf.RoundToInt(color.r * 255f),
                    (byte)Mathf.RoundToInt(color.g * 255f), (byte)Mathf.RoundToInt(color.b * 255f), 140);
                colors.Add(tint);
                normals.Add(centerNormal);
                foreach (Vector3 vertex in tileVertices)
                {
                    vertices.Add(vertex.normalized * (vertex.magnitude + SurfaceOffset));
                    colors.Add(tint);
                    normals.Add(vertex.normalized);
                }
                for (int index = 0; index < tileVertices.Count; index++)
                {
                    int firstEdge = first + index + 1;
                    int secondEdge = first + ((index + 1) % tileVertices.Count) + 1;
                    triangles.Add(first); triangles.Add(firstEdge); triangles.Add(secondEdge);
                    triangles.Add(first); triangles.Add(secondEdge); triangles.Add(firstEdge);
                }
                coloredTileCount++;
                if (!ReferenceEquals(lastLoggedWorld, world))
                {
                    lastLoggedWorld = world;
                    Log.Message("[Historical RimWorld] Drawing terrain color overlay " + hex
                        + " from " + source + " on tile " + tileId + ".");
                }
            }

            if (vertices.Count == 0) return;
            colorMesh = new Mesh
            {
                name = "Historical RimWorld Tile Terrain Color Overrides",
                indexFormat = IndexFormat.UInt32
            };
            colorMesh.SetVertices(vertices);
            colorMesh.SetNormals(normals);
            colorMesh.SetColors(colors);
            colorMesh.SetTriangles(triangles, 0, true);
            Log.Message("[Historical RimWorld] Rebuilt terrain color overlay for " + coloredTileCount + " tile(s).");
        }
    }
}
