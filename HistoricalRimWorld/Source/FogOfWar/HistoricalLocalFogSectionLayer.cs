using HistoricalRimWorld.Patches.FogOfWar;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.FogOfWar
{
    /// <summary>
    /// Fog presentation owned by RimWorld's section mesh pipeline. Section meshes are
    /// regenerated only after the corresponding fog-state section is marked dirty.
    /// </summary>
    public sealed class HistoricalLocalFogSectionLayer : SectionLayer
    {
        public HistoricalLocalFogSectionLayer(Section section) : base(section)
        {
            relevantChangeTypes = HistoricalMapMeshFlagDefOf.LocalFog;
        }

        public override bool Visible => DebugViewSettings.drawFog
            && HistoricalLocalFogOfWarManager.IsEnabled;

        public override void Regenerate()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;

            HistoricalLocalFogMapComponent fog = Map.GetComponent<HistoricalLocalFogMapComponent>();
            if (fog == null)
                return;

            LayerSubMesh mesh = GetSubMesh(SolidColorMaterials.SimpleSolidColorMaterial(
                Color.black, ShaderDatabase.Transparent));
            mesh.Clear(MeshParts.All);
            mesh.mesh.MarkDynamic();

            CellRect rect = section.CellRect.ClipInsideMap(Map);
            float y = Altitudes.AltitudeFor(AltitudeLayer.FogOfWar) + 0.05f;
            bool hasFog = false;

            foreach (IntVec3 cell in rect)
            {
                int firstVertex = mesh.verts.Count;
                mesh.verts.Add(new Vector3(cell.x, y, cell.z));
                mesh.verts.Add(new Vector3(cell.x + 1f, y, cell.z));
                mesh.verts.Add(new Vector3(cell.x + 1f, y, cell.z + 1f));
                mesh.verts.Add(new Vector3(cell.x, y, cell.z + 1f));

                mesh.tris.Add(firstVertex);
                mesh.tris.Add(firstVertex + 2);
                mesh.tris.Add(firstVertex + 1);
                mesh.tris.Add(firstVertex);
                mesh.tris.Add(firstVertex + 3);
                mesh.tris.Add(firstVertex + 2);

                byte bottomLeftAlpha = SmoothAlphaAtVertex(fog, cell.x, cell.z);
                byte bottomRightAlpha = SmoothAlphaAtVertex(fog, cell.x + 1, cell.z);
                byte topRightAlpha = SmoothAlphaAtVertex(fog, cell.x + 1, cell.z + 1);
                byte topLeftAlpha = SmoothAlphaAtVertex(fog, cell.x, cell.z + 1);
                mesh.colors.Add(new Color32(0, 0, 0, bottomLeftAlpha));
                mesh.colors.Add(new Color32(0, 0, 0, bottomRightAlpha));
                mesh.colors.Add(new Color32(0, 0, 0, topRightAlpha));
                mesh.colors.Add(new Color32(0, 0, 0, topLeftAlpha));
                hasFog |= bottomLeftAlpha != 0 || bottomRightAlpha != 0 || topRightAlpha != 0 || topLeftAlpha != 0;
            }

            mesh.disabled = !hasFog;
            if (hasFog)
                mesh.FinalizeMesh(MeshParts.Verts | MeshParts.Tris | MeshParts.Colors);
        }

        // Each grid corner is shared by up to four cells. Averaging their fog opacity
        // lets the mesh interpolate a short feather across visibility boundaries
        // instead of exposing the binary cell staircase.
        private byte SmoothAlphaAtVertex(HistoricalLocalFogMapComponent fog, int vertexX, int vertexZ)
        {
            int total = 0;
            int count = 0;
            for (int z = vertexZ - 1; z <= vertexZ; z++)
            for (int x = vertexX - 1; x <= vertexX; x++)
            {
                IntVec3 cell = new IntVec3(x, 0, z);
                if (!cell.InBounds(Map)) continue;
                total += fog.FogAlphaAt(Map.cellIndices.CellToIndex(cell));
                count++;
            }

            return count == 0 ? byte.MaxValue : (byte)Mathf.RoundToInt((float)total / count);
        }
    }
}
