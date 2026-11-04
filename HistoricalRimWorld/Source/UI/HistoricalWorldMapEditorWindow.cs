using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.Geography.Terrain;
using HistoricalRimWorld.World;
using HistoricalRimWorld.WorldFeatures;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    /// <summary>Phase-one world editor: inspect and paint native tile hilliness with a hex brush.</summary>
    public sealed class HistoricalWorldMapEditorWindow : Window
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private readonly Stack<IMapEditCommand> undo = new Stack<IMapEditCommand>();
        private readonly Stack<IMapEditCommand> redo = new Stack<IMapEditCommand>();
        private Vector2 scrollPosition;
        private Hilliness selectedTerrain = Hilliness.SmallHills;
        private BiomeDef selectedBiome;
        private int brushRadius;
        private int descriptionEditTileId = -1;
        private string descriptionBuffer = string.Empty;
        private int colorEditTileId = -1;
        private string tileColorBuffer = string.Empty;
        private int featureColorEditId = -1;
        private string featureColorBuffer = string.Empty;
        private string featureColorPriorityBuffer = "0";
        private RiverDef selectedRiver;
        private string riverCoordinates = string.Empty;
        private string riverPreviewStatus;
        private string status;
        private WorldObjectDef mapMarkerDef;
        private string mapMarkerNameBuffer = string.Empty;
        private string mapMarkerDescriptionBuffer = string.Empty;
        private HistoricalMapLocationWorldObject observedMapMarker;

        public HistoricalWorldMapEditorWindow()
        {
            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            draggable = true;
            windowRect = new Rect(Mathf.Max(10f, Verse.UI.screenWidth - 460f), 70f, 440f, 600f);
        }

        public override Vector2 InitialSize => new Vector2(440f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            if (!HistoricalMapEditManager.CanEdit(Find.World, out string reason))
            {
                Widgets.Label(new Rect(0f, 0f, inRect.width, 70f), reason);
                return;
            }

            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, 2600f);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            HistoricalFactionWorldComponent factionState = Find.World.GetComponent<HistoricalFactionWorldComponent>();
            Find.World.GetComponent<HistoricalMapEditWorldComponent>()?.Initialize(
                factionState.ScenarioDefName, factionState.MapScaleState?.ScaleDefName);
            listing.Label("World Map Editor · " + factionState.ScenarioDefName);
            listing.Label("仅限开发模式。地图修改写入独立的剧本覆盖层，不改动原始 DEM 或 Mod XML。");
            if (!string.IsNullOrWhiteSpace(status)) listing.Label("状态：" + status);
            listing.GapLine();
            DrawSelectedTile(listing);
            listing.GapLine();
            DrawNativeWorldFeatureEditor(listing, factionState);
            DrawRiverEditor(listing);
            DrawHistoricalMapMarkerEditor(listing, factionState);

            Rect historyRect = listing.GetRect(30f);
            if (Widgets.ButtonText(new Rect(historyRect.x, historyRect.y, historyRect.width * 0.48f, historyRect.height),
                "Undo (" + undo.Count + ")") && undo.Count > 0)
            {
                IMapEditCommand command = undo.Pop();
                command.Undo();
                redo.Push(command);
                status = "已撤销：" + command.Label;
            }
            if (Widgets.ButtonText(new Rect(historyRect.x + historyRect.width * 0.52f, historyRect.y,
                historyRect.width * 0.48f, historyRect.height), "Redo (" + redo.Count + ")") && redo.Count > 0)
            {
                IMapEditCommand command = redo.Pop();
                command.Execute();
                undo.Push(command);
                status = "已重做：" + command.Label;
            }

            listing.GapLine();
            listing.Label("当前版本支持：检查 Tile、地形与 Biome 画笔、历史河流、地理区域表单编辑、Undo/Redo 和覆盖层保存。");
            listing.Label("地理区域顶点通过经纬度文本修改；地图鼠标拖拽顶点/标签尚未开放。");
            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawTileTerrainColorEditor(Listing_Standard listing, int tileId)
        {
            HistoricalMapEditWorldComponent edits = Find.World.GetComponent<HistoricalMapEditWorldComponent>();
            if (colorEditTileId != tileId)
            {
                colorEditTileId = tileId;
                tileColorBuffer = edits?.GetTileColorOverride(tileId)?.ColorHex ?? string.Empty;
            }
            listing.Gap();
            listing.Label("Terrain Color · Tile Override: "
                + (string.IsNullOrWhiteSpace(tileColorBuffer) ? "off" : "on"));
            tileColorBuffer = Widgets.TextField(listing.GetRect(28f), tileColorBuffer);
            DrawColorPreview(listing, tileColorBuffer);
            Rect buttons = listing.GetRect(30f);
            if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, buttons.width * 0.55f, buttons.height), "应用 Tile 颜色"))
                ApplyTileTerrainColor(tileId, tileColorBuffer);
            if (Widgets.ButtonText(new Rect(buttons.x + buttons.width * 0.58f, buttons.y,
                buttons.width * 0.42f, buttons.height), "清除 Tile 覆盖"))
                ApplyTileTerrainColor(tileId, null);

            if (HistoricalWorldTerrainColorManager.TryGetEffectiveColor(Find.World, tileId,
                out _, out string source, out string hex))
            {
                listing.Label("Effective Color: " + hex + " (地表材质乘色)  |  Color Source: " + source);
                if (source.StartsWith("World Feature", StringComparison.Ordinal))
                    listing.Label("This tile's color is overridden by a World Feature.");
            }
            else listing.Label("Effective Color: Original Biome material ("
                + GetOriginalBiomeMaterialColor(Find.World.grid.Surface.Tiles[tileId])
                + ")  |  Color Source: Original Biome Color");
            listing.Label("自定义 RGB 会与原版地表纹理相乘；未着色 Tile 保持原版材质。");
            DrawColorPresets(listing, value => tileColorBuffer = value);
        }

        private void DrawFeatureTerrainColorEditor(Listing_Standard listing, WorldFeature feature,
            HistoricalMapScaleTransformer transformer, HistoricalGeoCoordinate representative)
        {
            if (featureColorEditId != feature.uniqueID)
            {
                featureColorEditId = feature.uniqueID;
                HistoricalWorldTerrainColorManager.TryGetFeatureRecord(Find.World, feature,
                    out HistoricalWorldFeatureColorOverride record);
                featureColorBuffer = record?.ColorHex ?? string.Empty;
                featureColorPriorityBuffer = (record?.Priority ?? 0).ToString(Invariant);
            }
            listing.Gap();
            listing.Label("Feature Terrain Color · " + feature.name + "  |  Override: "
                + (string.IsNullOrWhiteSpace(featureColorBuffer) ? "off" : "on"));
            featureColorBuffer = Widgets.TextField(listing.GetRect(28f), featureColorBuffer);
            DrawColorPreview(listing, featureColorBuffer);
            Rect priorityRect = listing.GetRect(28f);
            Widgets.Label(new Rect(priorityRect.x, priorityRect.y, 150f, priorityRect.height), "Color Priority");
            featureColorPriorityBuffer = Widgets.TextField(new Rect(priorityRect.x + 150f, priorityRect.y,
                priorityRect.width - 150f, priorityRect.height), featureColorPriorityBuffer);
            Rect buttons = listing.GetRect(30f);
            if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, buttons.width * 0.55f, buttons.height), "应用 Feature 颜色"))
                ApplyFeatureTerrainColor(feature, transformer, featureColorBuffer, false);
            if (Widgets.ButtonText(new Rect(buttons.x + buttons.width * 0.58f, buttons.y,
                buttons.width * 0.42f, buttons.height), "清除 Feature 覆盖"))
                ApplyFeatureTerrainColor(feature, transformer, null, true);
            DrawColorPresets(listing, value => featureColorBuffer = value);
        }

        private static void DrawColorPreview(Listing_Standard listing, string value)
        {
            if (!HistoricalWorldTerrainColorManager.TryParseHex(value, out Color color, out string normalized))
            { if (!string.IsNullOrWhiteSpace(value)) listing.Label("无效 RGB Hex 颜色。示例：#526B5B"); return; }
            Rect preview = listing.GetRect(24f);
            Widgets.Label(new Rect(preview.x, preview.y, 100f, preview.height), normalized);
            Rect swatch = new Rect(preview.x + 106f, preview.y + 2f, 48f, preview.height - 4f);
            Widgets.DrawBoxSolid(swatch, color);
            Widgets.DrawBox(swatch);
        }

        private static string GetOriginalBiomeMaterialColor(Tile tile)
        {
            try
            {
                Material material = tile?.PrimaryBiome?.DrawMaterial;
                return material == null ? "texture-defined" : "#" + ColorUtility.ToHtmlStringRGB(material.color);
            }
            catch { return "texture-defined"; }
        }

        private static void DrawColorPresets(Listing_Standard listing, Action<string> selected)
        {
            Rect row = listing.GetRect(28f);
            string[] presets = { "#526B5B", "#71866A", "#60775B", "#A8B97B", "#B5B982", "#B6A075" };
            float width = row.width / presets.Length;
            for (int i = 0; i < presets.Length; i++)
            {
                Color color;
                HistoricalWorldTerrainColorManager.TryParseHex(presets[i], out color, out _);
                Rect swatch = new Rect(row.x + i * width + 2f, row.y + 2f, width - 4f, row.height - 4f);
                Widgets.DrawBoxSolid(swatch, color);
                Widgets.DrawBox(swatch);
                if (Widgets.ButtonInvisible(swatch)) selected(presets[i]);
            }
        }

        private void ApplyTileTerrainColor(int tileId, string requestedHex)
        {
            HistoricalMapEditWorldComponent edits = Find.World.GetComponent<HistoricalMapEditWorldComponent>();
            string before = edits?.GetTileColorOverride(tileId)?.ColorHex;
            if (!HistoricalWorldTerrainColorManager.TrySetTileColor(Find.World, tileId,
                requestedHex, out string error))
            { status = error; return; }
            string after = edits?.GetTileColorOverride(tileId)?.ColorHex;
            if (string.Equals(before, after, StringComparison.Ordinal)) return;
            undo.Push(new DelegateMapEditCommand("Tile terrain color " + tileId,
                () => HistoricalWorldTerrainColorManager.TrySetTileColor(Find.World, tileId, after, out _),
                () => HistoricalWorldTerrainColorManager.TrySetTileColor(Find.World, tileId, before, out _)));
            redo.Clear();
            tileColorBuffer = after ?? string.Empty;
            status = string.IsNullOrWhiteSpace(after) ? "已清除 Tile 颜色覆盖。" : "Tile 颜色已更新为 " + after + "。";
        }

        private void ApplyFeatureTerrainColor(WorldFeature feature,
            HistoricalMapScaleTransformer transformer, string requestedHex, bool clear)
        {
            if (transformer == null) { status = "当前历史地图比例尺不可用。"; return; }
            HistoricalWorldTerrainColorManager.TryGetFeatureRecord(Find.World, feature,
                out HistoricalWorldFeatureColorOverride beforeRecord);
            string beforeHex = beforeRecord?.ColorHex;
            int beforePriority = beforeRecord?.Priority ?? 0;
            int afterPriority = beforePriority;
            if (!clear && (!int.TryParse(featureColorPriorityBuffer, NumberStyles.Integer, Invariant, out afterPriority)))
            { status = "Color Priority 必须是整数。"; return; }
            string value = clear ? null : requestedHex;
            if (!HistoricalWorldTerrainColorManager.TrySetFeatureColor(Find.World, feature, value,
                afterPriority, transformer, out string error))
            { status = error; return; }
            HistoricalWorldTerrainColorManager.TryGetFeatureRecord(Find.World, feature,
                out HistoricalWorldFeatureColorOverride afterRecord);
            string afterHex = afterRecord?.ColorHex;
            if (string.Equals(beforeHex, afterHex, StringComparison.Ordinal)
                && beforePriority == (afterRecord?.Priority ?? 0)) return;
            undo.Push(new DelegateMapEditCommand("Feature terrain color " + feature.name,
                () => HistoricalWorldTerrainColorManager.TrySetFeatureColor(Find.World, feature,
                    afterHex, afterPriority, transformer, out _),
                () => HistoricalWorldTerrainColorManager.TrySetFeatureColor(Find.World, feature,
                    beforeHex, beforePriority, transformer, out _)));
            redo.Clear();
            featureColorBuffer = afterHex ?? string.Empty;
            featureColorPriorityBuffer = (afterRecord?.Priority ?? 0).ToString(Invariant);
            status = string.IsNullOrWhiteSpace(afterHex) ? "已清除 Feature 颜色覆盖。"
                : "Feature 颜色已更新为 " + afterHex + "（priority " + afterPriority + "）。";
        }

        private sealed class DelegateMapEditCommand : IMapEditCommand
        {
            private readonly Action execute;
            private readonly Action undoAction;
            public string Label { get; }
            public DelegateMapEditCommand(string label, Action execute, Action undoAction)
            { Label = label; this.execute = execute; this.undoAction = undoAction; }
            public void Execute() => execute();
            public void Undo() => undoAction();
        }

        private void DrawSelectedTile(Listing_Standard listing)
        {
            if (!HistoricalMapEditManager.TryGetSelectedTileId(out int tileId))
            {
                listing.Label("请在地图上选择一个 Tile。编辑窗口外的世界地图仍可点击。");
                return;
            }
            if (tileId < 0 || tileId >= Find.World.grid.TilesCount)
            { listing.Label("当前选中的 Tile 不在活动 WorldGrid 中。"); return; }
            Tile tile = Find.World.grid.Surface.Tiles[tileId];
            Vector2 gameCoordinate = Find.World.grid.Surface.LongLatOf(tileId);
            listing.Label("Tile ID: " + tileId);
            listing.Label("World sphere: " + gameCoordinate.x.ToString("0.000", Invariant)
                + "°, " + gameCoordinate.y.ToString("0.000", Invariant) + "°");
            listing.Label("Terrain: " + tile.hilliness + "  |  Biome: " + (tile.PrimaryBiome?.LabelCap ?? "unknown"));
            listing.Label("Normalized RimWorld elevation: " + tile.elevation.ToString("0.000", Invariant));
            DrawTileTerrainColorEditor(listing, tileId);

            HistoricalMapTileDescriptionWorldComponent descriptions =
                Find.World.GetComponent<HistoricalMapTileDescriptionWorldComponent>();
            if (descriptions == null)
            {
                listing.Label("此世界没有可用的 Tile 描述存档组件。");
            }
            else
            {
                if (descriptionEditTileId != tileId)
                {
                    descriptionEditTileId = tileId;
                    descriptionBuffer = descriptions.GetDescription(tileId) ?? string.Empty;
                }
                listing.Gap();
                listing.Label("Tile description（保存到剧本地图 XML）");
                Rect descriptionRect = listing.GetRect(100f);
                string editedDescription = Widgets.TextArea(descriptionRect, descriptionBuffer);
                if (editedDescription != descriptionBuffer)
                {
                    descriptionBuffer = editedDescription;
                    descriptions.SetDescription(tileId, descriptionBuffer);
                }
            }

            HistoricalMapScaleState scaleState = Find.World.GetComponent<HistoricalFactionWorldComponent>()?.MapScaleState;
            HistoricalMapScaleDef scaleDef = scaleState == null
                ? null : DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(scaleState.ScaleDefName);
            if (scaleDef != null)
            {
                try
                {
                    HistoricalGeoCoordinate historical = new HistoricalMapScaleTransformer(scaleDef)
                        .ToHistoricalCoordinate(new HistoricalGameGeoCoordinate(gameCoordinate.y, gameCoordinate.x));
                    listing.Label("Historical coordinates (longitude, latitude):");
                    string historicalCoordinateText = Math.Abs(historical.Longitude).ToString("0.00000", Invariant)
                        + (historical.Longitude < 0d ? "W" : "E") + ", "
                        + Math.Abs(historical.Latitude).ToString("0.00000", Invariant)
                        + (historical.Latitude < 0d ? "S" : "N");
                    Rect coordinateRect = listing.GetRect(28f);
                    Rect coordinateFieldRect = new Rect(coordinateRect.x, coordinateRect.y,
                        coordinateRect.width - 92f, coordinateRect.height);
                    Rect copyButtonRect = new Rect(coordinateRect.xMax - 86f, coordinateRect.y, 86f, coordinateRect.height);
                    Widgets.TextField(coordinateFieldRect, historicalCoordinateText);
                    if (Widgets.ButtonText(copyButtonRect, "复制"))
                    {
                        GUIUtility.systemCopyBuffer = historicalCoordinateText;
                        status = "已复制历史坐标：" + historicalCoordinateText;
                    }
                }
                catch (Exception exception) { listing.Label("Coordinate conversion error: " + exception.Message); }
            }

            HistoricalFactionWorldComponent factionState = Find.World.GetComponent<HistoricalFactionWorldComponent>();
            listing.GapLine();

            listing.Label("Terrain brush");
            if (listing.ButtonText("Terrain: " + selectedTerrain + "  (click to choose)")) OpenTerrainMenu();
            Rect radiusRect = listing.GetRect(30f);
            Widgets.Label(new Rect(radiusRect.x, radiusRect.y, 130f, radiusRect.height), "Hex radius: " + brushRadius);
            if (Widgets.ButtonText(new Rect(radiusRect.x + 150f, radiusRect.y, 80f, radiusRect.height), "−")
                && brushRadius > 0) brushRadius--;
            if (Widgets.ButtonText(new Rect(radiusRect.x + 240f, radiusRect.y, 80f, radiusRect.height), "+")
                && brushRadius < 4) brushRadius++;
            listing.Label("覆盖格数按六角邻接距离计算：半径 0–4。选择地图上的 Tile，再执行一次画笔操作。");
            if (listing.ButtonText("Apply terrain to brush")) ApplyPaint();
            if (listing.ButtonText("Restore generated terrain in brush")) ApplyRestore();
            DrawBiomeBrush(listing);
            if (listing.ButtonText("保存地形 / Biome / 河流 / Tile 描述修改到剧本 XML"))
                SaveEdits(factionState.ScenarioDefName);
            listing.Label("保存路径：Config\\HistoricalRimWorld\\MapEdits\\"
                + factionState.ScenarioDefName + ".xml");

            HistoricalMapTerrainOverride edit = Find.World.GetComponent<HistoricalMapEditWorldComponent>()?.GetTerrainOverride(tileId);
            HistoricalTerrainTileState imported = Find.World.GetComponent<HistoricalTerrainWorldComponent>()?.GetTile(tileId);
            if (imported != null)
            {
                listing.Label("DEM source: " + (Find.World.GetComponent<HistoricalTerrainWorldComponent>()?.SourceName ?? "Imported"));
                listing.Label("DEM elevation: mean " + imported.MeanElevationMeters.ToString("0", Invariant)
                    + " m, range " + imported.MinElevationMeters.ToString("0", Invariant) + "–"
                    + imported.MaxElevationMeters.ToString("0", Invariant) + " m; mean slope "
                    + imported.MeanSlopeDegrees.ToString("0.0", Invariant) + "°");
            }
            listing.Label(edit == null
                ? (imported == null ? "Terrain source: generated/XML base" : "Terrain source: imported DEM")
                : "Terrain source: ManualOverride (base: " + edit.BaseTerrain + ")");
        }

        private void OpenTerrainMenu()
        {
            List<FloatMenuOption> options = Enum.GetValues(typeof(Hilliness)).Cast<Hilliness>()
                .Select(value => new FloatMenuOption(value.ToString(), () => selectedTerrain = value)).ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawBiomeBrush(Listing_Standard listing)
        {
            listing.GapLine();
            listing.Label("Biome brush");
            if (selectedBiome == null && HistoricalMapEditManager.TryGetSelectedTileId(out int selectedTileId)
                && selectedTileId >= 0 && selectedTileId < Find.World.grid.Surface.TilesCount)
                selectedBiome = Find.World.grid.Surface.Tiles[selectedTileId]?.PrimaryBiome;
            if (listing.ButtonText("Biome: " + (selectedBiome?.LabelCap ?? "请选择") + "（点击选择）"))
            {
                List<FloatMenuOption> options = DefDatabase<BiomeDef>.AllDefsListForReading
                    .Where(biome => biome != null)
                    .OrderBy(biome => biome.LabelCap.ToString(), StringComparer.Ordinal)
                    .Select(biome => new FloatMenuOption(biome.LabelCap + " [" + biome.defName + "]",
                        () => selectedBiome = biome)).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Label("使用上方同一六角画笔半径（0–4）；应用后会立即刷新地图，并支持 Undo/Redo。");
            if (listing.ButtonText("Apply biome to brush")) ApplyBiomePaint();
            if (listing.ButtonText("Restore generated biome in brush")) ApplyBiomeRestore();
        }

        private void DrawRiverEditor(Listing_Standard listing)
        {
            listing.GapLine();
            listing.Label("绘制历史河流");
            if (selectedRiver == null)
                selectedRiver = DefDatabase<RiverDef>.GetNamedSilentFail("River")
                    ?? DefDatabase<RiverDef>.AllDefsListForReading.FirstOrDefault();
            if (selectedRiver == null)
            {
                listing.Label("没有可用的 RimWorld RiverDef。");
                return;
            }

            if (listing.ButtonText("河流类型：" + selectedRiver.LabelCap + "（点击选择）"))
            {
                List<FloatMenuOption> options = DefDatabase<RiverDef>.AllDefsListForReading
                    .OrderBy(item => item.defName)
                    .Select(item => new FloatMenuOption(item.LabelCap.ToString(), () => selectedRiver = item))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Label("逐行输入历史坐标：经度,纬度，顺序按河源到河口。编辑器会按当前剧本比例尺投影并连接相邻 Tile。示例：113.2,34.7");
            Rect coordinatesRect = listing.GetRect(112f);
            riverCoordinates = Widgets.TextArea(coordinatesRect, riverCoordinates);
            Rect riverButtons = listing.GetRect(30f);
            if (Widgets.ButtonText(new Rect(riverButtons.x, riverButtons.y, riverButtons.width * 0.48f, riverButtons.height),
                "预览河道路由")) PreviewRiver();
            if (Widgets.ButtonText(new Rect(riverButtons.x + riverButtons.width * 0.52f, riverButtons.y,
                riverButtons.width * 0.48f, riverButtons.height), "应用河流")) ApplyRiver();
            if (!string.IsNullOrWhiteSpace(riverPreviewStatus)) listing.Label(riverPreviewStatus);
            listing.Label("应用后河流会立即绘制到地图；路径、历史坐标与河流类型会随存档保存，并包含在剧本地图 XML 导出中。");
        }

        private int selectedNativeFeatureId = -1;
        private int lastObservedSelectedTileId = -1;
        private string nativeFeatureNameBuffer = string.Empty;
        private string newFeatureName = string.Empty;
        private FeatureDef newFeatureDef;
        private int featureSelectionRadius = 1;
        private readonly List<int> stagedNewFeatureTiles = new List<int>();

        private void DrawHistoricalMapMarkerEditor(Listing_Standard listing,
            HistoricalFactionWorldComponent factionState)
        {
            listing.GapLine();
            listing.Label("Historical map markers (WorldObject)");
            HistoricalMapLocationWorldObject selectedMarker = Find.WorldSelector?.SingleSelectedObject
                as HistoricalMapLocationWorldObject;
            if (!ReferenceEquals(selectedMarker, observedMapMarker))
            {
                observedMapMarker = selectedMarker;
                if (selectedMarker != null)
                {
                    mapMarkerNameBuffer = selectedMarker.HistoricalLabel ?? selectedMarker.Label;
                    mapMarkerDescriptionBuffer = selectedMarker.HistoricalDescription ?? string.Empty;
                }
            }

            bool hasTile = HistoricalMapEditManager.TryGetSelectedTileId(out int tileId)
                && Find.World?.grid?.Surface != null
                && tileId >= 0 && tileId < Find.World.grid.Surface.TilesCount
                && Find.World.grid.Surface.Tiles[tileId] != null;
            List<WorldObjectDef> markerDefs = DefDatabase<WorldObjectDef>.AllDefsListForReading
                .Where(def => def != null && def.defName.StartsWith("HR_MapMarker_", StringComparison.Ordinal)
                    && def.worldObjectClass == typeof(HistoricalMapLocationWorldObject))
                .OrderBy(def => def.defName, StringComparer.Ordinal).ToList();

            if (selectedMarker == null)
            {
                if (mapMarkerDef == null || !markerDefs.Contains(mapMarkerDef))
                    mapMarkerDef = markerDefs.FirstOrDefault(def => def.defName == "HR_MapMarker_Town")
                        ?? markerDefs.FirstOrDefault();
                if (listing.ButtonText("WorldObjectDef：" + (mapMarkerDef?.LabelCap ?? "未选择") + "（点击选择）"))
                {
                    List<FloatMenuOption> options = markerDefs.Select(def => new FloatMenuOption(
                        MarkerDefDisplayName(def) + "  [" + def.defName + "]", () => mapMarkerDef = def)).ToList();
                    if (options.Count == 0) featureStatus = "没有找到 HR_MapMarker_* WorldObjectDef。";
                    else Find.WindowStack.Add(new FloatMenu(options));
                }
                if (hasTile) listing.Label("创建位置：Tile " + tileId);
                else listing.Label("请先在世界地图点击要放置标记的 Tile。");
            }
            else
            {
                listing.Label("当前选中标记：" + selectedMarker.Label + " ["
                    + selectedMarker.def.defName + "]，Tile " + (int)selectedMarker.Tile);
            }

            listing.Label("名称");
            mapMarkerNameBuffer = Widgets.TextField(listing.GetRect(28f), mapMarkerNameBuffer);
            listing.Label("说明");
            mapMarkerDescriptionBuffer = Widgets.TextArea(listing.GetRect(72f), mapMarkerDescriptionBuffer);

            if (selectedMarker == null)
            {
                if (listing.ButtonText("在所选 Tile 创建地图标记"))
                    CreateHistoricalMapMarker(factionState, tileId, hasTile, mapMarkerDef);
                listing.Label("标记是中立 WorldObject：不生成阵营、殖民地、地图或地形变化；由 RimWorld 存档保存。");
            }
            else
            {
                Rect buttons = listing.GetRect(30f);
                if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, buttons.width * 0.58f, buttons.height),
                    "应用名称 / 说明并加入撤销历史"))
                    ApplyMapMarkerMetadata(selectedMarker);
                if (Widgets.ButtonText(new Rect(buttons.x + buttons.width * 0.62f, buttons.y,
                    buttons.width * 0.38f, buttons.height), "删除标记"))
                    DeleteHistoricalMapMarker(selectedMarker);
            }
            if (!string.IsNullOrWhiteSpace(featureStatus)) listing.Label(featureStatus);
        }

        private static string MarkerDefDisplayName(WorldObjectDef def)
        {
            if (def == null) return "Unknown";
            switch (def.defName)
            {
                case "HR_MapMarker_Town": return "Town / 城镇";
                case "HR_MapMarker_City": return "City / 城市";
                case "HR_MapMarker_MountainPeak": return "Mountain Peak / 山峰";
                case "HR_MapMarker_MountainPass": return "Mountain Pass / 关隘";
                default: return def.LabelCap;
            }
        }

        private void CreateHistoricalMapMarker(HistoricalFactionWorldComponent factionState,
            int tileId, bool hasTile, WorldObjectDef def)
        {
            if (!hasTile) { featureStatus = "创建失败：请先在世界地图选择有效 Tile。"; return; }
            if (def == null || def.worldObjectClass != typeof(HistoricalMapLocationWorldObject))
            { featureStatus = "创建失败：所选 Def 不是 HistoricalMapLocationWorldObject。"; return; }
            if (string.IsNullOrWhiteSpace(mapMarkerNameBuffer))
            { featureStatus = "创建失败：请输入标记名称。"; return; }
            HistoricalMapScaleDef scale = factionState.MapScaleState == null ? null
                : DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(factionState.MapScaleState.ScaleDefName);
            if (scale == null) { featureStatus = "创建失败：当前历史地图比例尺不可用。"; return; }

            try
            {
                Vector2 gameCoordinate = Find.World.grid.Surface.LongLatOf(tileId);
                HistoricalGeoCoordinate historical = new HistoricalMapScaleTransformer(scale)
                    .ToHistoricalCoordinate(new HistoricalGameGeoCoordinate(gameCoordinate.y, gameCoordinate.x));
                WorldObject raw = WorldObjectMaker.MakeWorldObject(def);
                HistoricalMapLocationWorldObject marker = raw as HistoricalMapLocationWorldObject;
                if (marker == null) { featureStatus = "创建失败：WorldObjectMaker 返回了不兼容的对象。"; return; }
                marker.SetHistoricalLabel(mapMarkerNameBuffer.Trim());
                marker.SetHistoricalDescription(mapMarkerDescriptionBuffer ?? string.Empty);
                marker.SetHistoricalGeoCoordinates(historical.Latitude, historical.Longitude,
                    gameCoordinate.y, gameCoordinate.x, scale.scaleFactor);
                marker.Tile = new PlanetTile(tileId, Find.World.grid.Surface);

                IMapEditCommand command = new HistoricalMapMarkerPresenceCommand(Find.World, marker, true);
                command.Execute();
                undo.Push(command);
                redo.Clear();
                featureStatus = MarkerDefDisplayName(def) + "标记“" + marker.Label + "”已创建于 Tile " + tileId + "。";
                mapMarkerNameBuffer = string.Empty;
                mapMarkerDescriptionBuffer = string.Empty;
            }
            catch (Exception exception) { featureStatus = "创建标记失败：" + exception.Message; }
        }

        private void ApplyMapMarkerMetadata(HistoricalMapLocationWorldObject marker)
        {
            if (marker == null || !Find.World.worldObjects.AllWorldObjects.Contains(marker))
            { featureStatus = "编辑失败：标记不在当前世界中。"; return; }
            if (string.IsNullOrWhiteSpace(mapMarkerNameBuffer))
            { featureStatus = "编辑失败：标记名称不能为空。"; return; }
            IMapEditCommand command = new HistoricalMapMarkerMetadataCommand(marker,
                marker.HistoricalLabel ?? marker.Label, marker.HistoricalDescription ?? string.Empty,
                mapMarkerNameBuffer.Trim(), mapMarkerDescriptionBuffer ?? string.Empty);
            command.Execute();
            undo.Push(command);
            redo.Clear();
            featureStatus = "地图标记名称与说明已更新。";
        }

        private void DeleteHistoricalMapMarker(HistoricalMapLocationWorldObject marker)
        {
            if (marker == null || !Find.World.worldObjects.AllWorldObjects.Contains(marker))
            { featureStatus = "删除失败：标记不在当前世界中。"; return; }
            Find.WorldSelector?.Deselect(marker);
            IMapEditCommand command = new HistoricalMapMarkerPresenceCommand(Find.World, marker, false);
            command.Execute();
            undo.Push(command);
            redo.Clear();
            observedMapMarker = null;
            featureStatus = "地图标记已删除；可通过 Undo 恢复。";
        }

        private sealed class HistoricalMapMarkerPresenceCommand : IMapEditCommand
        {
            private readonly RimWorld.Planet.World world;
            private readonly HistoricalMapLocationWorldObject marker;
            private readonly bool addOnExecute;
            public string Label => (addOnExecute ? "Create map marker: " : "Delete map marker: ") + marker.Label;

            public HistoricalMapMarkerPresenceCommand(RimWorld.Planet.World world,
                HistoricalMapLocationWorldObject marker, bool addOnExecute)
            { this.world = world; this.marker = marker; this.addOnExecute = addOnExecute; }

            public void Execute() => SetPresent(addOnExecute);
            public void Undo() => SetPresent(!addOnExecute);

            private void SetPresent(bool present)
            {
                bool exists = world?.worldObjects?.AllWorldObjects?.Contains(marker) == true;
                if (present && !exists) world.worldObjects.Add(marker);
                else if (!present && exists) world.worldObjects.Remove(marker);
            }
        }

        private sealed class HistoricalMapMarkerMetadataCommand : IMapEditCommand
        {
            private readonly HistoricalMapLocationWorldObject marker;
            private readonly string beforeName, beforeDescription, afterName, afterDescription;
            public string Label => "Edit map marker: " + afterName;

            public HistoricalMapMarkerMetadataCommand(HistoricalMapLocationWorldObject marker,
                string beforeName, string beforeDescription, string afterName, string afterDescription)
            { this.marker = marker; this.beforeName = beforeName; this.beforeDescription = beforeDescription;
              this.afterName = afterName; this.afterDescription = afterDescription; }

            public void Execute() => Apply(afterName, afterDescription);
            public void Undo() => Apply(beforeName, beforeDescription);

            private void Apply(string name, string description)
            {
                marker.SetHistoricalLabel(name);
                marker.SetHistoricalDescription(description);
            }
        }

        private void DrawNativeWorldFeatureEditor(Listing_Standard listing, HistoricalFactionWorldComponent factionState)
        {
            listing.GapLine();
            listing.Label("World Features Editor (原版地理特征)");
            listing.Label("将当前原版 Feature 名称、类型及成员 Tile 的历史坐标保存到本剧本 XML：");
            if (listing.ButtonText("保存 Feature / Tile 组成及颜色到 XML")) SaveNativeFeatureLayout(factionState);
            listing.Label("Config\\HistoricalRimWorld\\WorldFeatures\\Layouts\\"
                + factionState.ScenarioDefName + ".xml");
            bool hasTile = HistoricalMapEditManager.TryGetSelectedTileId(out int tileId)
                && tileId >= 0 && tileId < Find.World.grid.Surface.TilesCount;
            WorldFeature tileFeature = hasTile ? Find.World.grid.Surface.Tiles[tileId]?.feature : null;
            List<WorldFeature> nativeFeatures = WorldFeatureAdapter.GetAllFeatures(Find.World)
                .Where(item => ReferenceEquals(item.layer, Find.World.grid.Surface)).ToList();
            if (hasTile && tileId != lastObservedSelectedTileId)
            {
                lastObservedSelectedTileId = tileId;
                if (tileFeature != null) selectedNativeFeatureId = tileFeature.uniqueID;
            }
            if (!nativeFeatures.Any(item => item.uniqueID == selectedNativeFeatureId)) selectedNativeFeatureId = -1;

            HistoricalWorldFeatureOverrideWorldComponent component =
                Find.World.GetComponent<HistoricalWorldFeatureOverrideWorldComponent>();
            component?.Initialize(factionState.ScenarioDefName);
            if (newFeatureDef == null)
                newFeatureDef = DefDatabase<FeatureDef>.GetNamedSilentFail("MountainRange")
                    ?? DefDatabase<FeatureDef>.AllDefsListForReading.FirstOrDefault();

            string selectedFeatureLabel = nativeFeatures.FirstOrDefault(item => item.uniqueID == selectedNativeFeatureId) is WorldFeature selectedFeature
                ? selectedFeature.name + " [" + selectedFeature.def.defName + ", ID " + selectedFeature.uniqueID + "]"
                : "(未选择)";
            if (listing.ButtonText("当前编辑 Feature：" + selectedFeatureLabel))
            {
                List<FloatMenuOption> options = nativeFeatures.OrderBy(item => item.name, StringComparer.Ordinal)
                    .Select(item => new FloatMenuOption(item.name + " [" + item.def.defName + ", ID " + item.uniqueID + "]",
                        () => { selectedNativeFeatureId = item.uniqueID; nativeFeatureNameBuffer = item.name; })).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (hasTile)
            {
                listing.Label("当前 Tile " + tileId + " 的 Feature：" + (tileFeature?.name ?? "(无)"));
                if (selectedNativeFeatureId >= 0 && listing.ButtonText("将所选 Tile 加入当前 Feature"))
                    ApplyFeatureMembership(tileId, true);
                if (selectedNativeFeatureId >= 0 && ReferenceEquals(tileFeature,
                    nativeFeatures.FirstOrDefault(item => item.uniqueID == selectedNativeFeatureId))
                    && listing.ButtonText("从当前 Feature 移除所选 Tile"))
                    ApplyFeatureMembership(tileId, false);

                Rect featureAreaRect = listing.GetRect(30f);
                Widgets.Label(new Rect(featureAreaRect.x, featureAreaRect.y, 148f, featureAreaRect.height),
                    "批量范围（六角半径）: " + featureSelectionRadius);
                if (Widgets.ButtonText(new Rect(featureAreaRect.x + 162f, featureAreaRect.y, 42f, featureAreaRect.height), "−")
                    && featureSelectionRadius > 0) featureSelectionRadius--;
                if (Widgets.ButtonText(new Rect(featureAreaRect.x + 210f, featureAreaRect.y, 42f, featureAreaRect.height), "+")
                    && featureSelectionRadius < 20) featureSelectionRadius++;
                List<int> areaTiles = GetTilesWithinRadius(Find.World.grid, tileId, featureSelectionRadius);
                listing.Label("以当前选中 Tile 为中心，范围内共 " + areaTiles.Count + " 格；会按球面六角邻接扩展。");
                listing.Label("批量加入会将范围内已属于其他 Feature 的 Tile 转移到当前 Feature；可用 Undo 撤销整批操作。");
                if (selectedNativeFeatureId >= 0 && listing.ButtonText("将范围内 " + areaTiles.Count + " 格批量加入当前 Feature"))
                    ApplyFeatureMembershipBatch(areaTiles, true);
                if (selectedNativeFeatureId >= 0 && areaTiles.Any(id =>
                    ReferenceEquals(Find.World.grid.Surface.Tiles[id]?.feature,
                        nativeFeatures.FirstOrDefault(item => item.uniqueID == selectedNativeFeatureId)))
                    && listing.ButtonText("将范围内属于当前 Feature 的 Tile 批量移除"))
                    ApplyFeatureMembershipBatch(areaTiles, false);
                if (listing.ButtonText("将范围内 Tile 全部加入新 Feature 暂存列表"))
                {
                    foreach (int areaTile in areaTiles)
                        if (!stagedNewFeatureTiles.Contains(areaTile)) stagedNewFeatureTiles.Add(areaTile);
                    featureStatus = "已将范围内 " + areaTiles.Count + " 格加入新 Feature 暂存列表。";
                }
                if (stagedNewFeatureTiles.Contains(tileId))
                {
                    if (listing.ButtonText("从新 Feature 暂存列表移除 Tile " + tileId)) stagedNewFeatureTiles.Remove(tileId);
                }
                else if (hasTile && listing.ButtonText("将所选 Tile 加入新 Feature 暂存列表"))
                    stagedNewFeatureTiles.Add(tileId);
            }
            else listing.Label("请在世界地图上选择 Tile；再把它加入现有 Feature 或新 Feature 的 Tile 列表。");

            WorldFeature selected = nativeFeatures.FirstOrDefault(item => item.uniqueID == selectedNativeFeatureId);
            if (selected != null)
            {
                HistoricalMapScaleDef scale = factionState.MapScaleState == null ? null
                    : DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(factionState.MapScaleState.ScaleDefName);
                HistoricalMapScaleTransformer transformer = scale == null ? null : new HistoricalMapScaleTransformer(scale);
                string representative = HistoricalWorldFeatureOverrideManager.TryGetHistoricalRepresentative(
                    selected, transformer, out HistoricalGeoCoordinate featureCoordinate)
                    ? featureCoordinate.Longitude.ToString("0.0000", Invariant) + ", "
                        + featureCoordinate.Latitude.ToString("0.0000", Invariant)
                    : "unavailable";
                List<int> members = WorldFeatureAdapter.GetFeatureTiles(Find.World, selected).ToList();
                listing.Label("FeatureDef：" + selected.def.defName + "  | 代表历史坐标：" + representative);
                listing.Label("覆盖 Tile：" + members.Count + (members.Count == 0 ? string.Empty
                    : "  |  IDs: " + string.Join(", ", members.Take(48)) + (members.Count > 48 ? " …" : string.Empty)));
                DrawFeatureTerrainColorEditor(listing, selected, transformer, featureCoordinate);
                listing.Label("名称");
                nativeFeatureNameBuffer = Widgets.TextField(listing.GetRect(28f), nativeFeatureNameBuffer);
                Rect featureButtons = listing.GetRect(30f);
                if (Widgets.ButtonText(new Rect(featureButtons.x, featureButtons.y,
                    featureButtons.width * 0.48f, featureButtons.height), "重命名并加入撤销历史"))
                    RenameNativeFeature(selected, component, transformer, featureCoordinate);
                if (Widgets.ButtonText(new Rect(featureButtons.x + featureButtons.width * 0.52f,
                    featureButtons.y, featureButtons.width * 0.48f, featureButtons.height), "恢复原始名称"))
                    RestoreNativeFeatureName(selected, component);
            }

            listing.GapLine();
            listing.Label("创建新的原版 WorldFeature");
            listing.Label("名称");
            newFeatureName = Widgets.TextField(listing.GetRect(28f), newFeatureName);
            if (listing.ButtonText("原生 Feature 类型：" + (newFeatureDef?.defName ?? "未选择") + "（点击更改）"))
            {
                List<FloatMenuOption> options = DefDatabase<FeatureDef>.AllDefsListForReading
                    .OrderBy(item => item.defName, StringComparer.Ordinal)
                    .Select(item => new FloatMenuOption(item.defName, () => newFeatureDef = item)).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Label("新 Feature 暂存 Tile 数：" + stagedNewFeatureTiles.Count
                + (stagedNewFeatureTiles.Count == 0 ? string.Empty : " (" + string.Join(", ", stagedNewFeatureTiles.Take(48)) + ")"));
            if (listing.ButtonText("创建原版 Feature 并接管暂存 Tile")) CreateNativeFeature();

            if (listing.ButtonText("保存手动 Feature 名称覆盖（用于新世界）"))
            {
                string error = null;
                if (component != null && component.TrySaveManualOverrides(out string path, out error))
                {
                    featureStatus = "手动名称覆盖已保存：" + path;
                    Log.Message("[HR-016] Saved manual World Feature names to " + path + ".");
                }
                else featureStatus = "保存失败：" + (error ?? "Feature 状态组件不可用。");
            }
            listing.Label("手动覆盖 XML：Config\\HistoricalRimWorld\\WorldFeatures\\"
                + factionState.ScenarioDefName + ".xml");
            listing.Label("当前游戏存档由 RimWorld 原生 WorldFeature.ExposeData 保存名称；此 XML 用于后续新世界重用手动命名。");
            listing.Label("布局 XML 保存 Feature 名称、类型、成员 Tile 历史坐标及 Feature 颜色 / 优先级；重新生成时按布局记录恢复。");
            if (!string.IsNullOrWhiteSpace(featureStatus)) listing.Label(featureStatus);
        }

        private void SaveNativeFeatureLayout(HistoricalFactionWorldComponent factionState)
        {
            HistoricalMapScaleDef layoutScale = factionState?.MapScaleState == null ? null
                : DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(factionState.MapScaleState.ScaleDefName);
            if (HistoricalWorldFeatureLayoutFileStore.TrySave(Find.World, factionState?.ScenarioDefName,
                layoutScale, out string layoutPath, out string layoutError))
            {
                featureStatus = "Feature 区域与成员 Tile 已保存：" + layoutPath;
                Log.Message("[Historical RimWorld] Saved native World Feature layouts to " + layoutPath + ".");
            }
            else featureStatus = "Feature 区域保存失败：" + layoutError;
        }

        private void ApplyFeatureMembership(int tileId, bool add)
        {
            WorldFeature target = WorldFeatureAdapter.GetAllFeatures(Find.World)
                .FirstOrDefault(item => item.uniqueID == selectedNativeFeatureId);
            if (!HistoricalWorldFeatureEditorManager.TryCreateMembershipCommand(Find.World, target,
                tileId, add, out IMapEditCommand command, out string error))
            { featureStatus = error; return; }
            command.Execute();
            undo.Push(command);
            redo.Clear();
            if (!WorldFeatureAdapter.GetAllFeatures(Find.World).Any(item => item.uniqueID == selectedNativeFeatureId))
            { selectedNativeFeatureId = -1; nativeFeatureNameBuffer = string.Empty; }
            featureStatus = (add ? "已加入 Tile " : "已移除 Tile ") + tileId + "；修改已加入撤销历史。";
        }

        private void ApplyFeatureMembershipBatch(IEnumerable<int> tileIds, bool add)
        {
            WorldFeature target = WorldFeatureAdapter.GetAllFeatures(Find.World)
                .FirstOrDefault(item => item.uniqueID == selectedNativeFeatureId);
            if (!HistoricalWorldFeatureEditorManager.TryCreateMembershipBatchCommand(Find.World, target,
                tileIds, add, out IMapEditCommand command, out int changedCount, out string error))
            { featureStatus = error; return; }
            command.Execute();
            undo.Push(command);
            redo.Clear();
            if (!WorldFeatureAdapter.GetAllFeatures(Find.World).Any(item => item.uniqueID == selectedNativeFeatureId))
            { selectedNativeFeatureId = -1; nativeFeatureNameBuffer = string.Empty; }
            featureStatus = "已批量" + (add ? "加入" : "移除") + " " + changedCount
                + " 格；操作已加入撤销历史。";
        }

        private static List<int> GetTilesWithinRadius(WorldGrid grid, int startTile, int radius)
        {
            HashSet<int> visited = new HashSet<int> { startTile };
            Queue<Tuple<int, int>> frontier = new Queue<Tuple<int, int>>();
            frontier.Enqueue(Tuple.Create(startTile, 0));
            List<PlanetTile> neighbors = new List<PlanetTile>(8);
            while (frontier.Count > 0)
            {
                Tuple<int, int> current = frontier.Dequeue();
                if (current.Item2 >= radius) continue;
                neighbors.Clear();
                grid.GetTileNeighbors(new PlanetTile(current.Item1, grid.Surface), neighbors);
                foreach (PlanetTile neighbor in neighbors)
                {
                    int id = (int)neighbor;
                    if (!visited.Add(id)) continue;
                    frontier.Enqueue(Tuple.Create(id, current.Item2 + 1));
                }
            }
            return visited.OrderBy(id => id).ToList();
        }

        private void CreateNativeFeature()
        {
            if (!HistoricalWorldFeatureEditorManager.TryCreateFeatureCommand(Find.World, newFeatureName,
                newFeatureDef, stagedNewFeatureTiles, out IMapEditCommand command, out string error))
            { featureStatus = error; return; }
            command.Execute();
            undo.Push(command);
            redo.Clear();
            if (stagedNewFeatureTiles.Count > 0)
            {
                WorldFeature created = Find.World.grid.Surface.Tiles[stagedNewFeatureTiles[0]]?.feature;
                if (created != null)
                {
                    selectedNativeFeatureId = created.uniqueID;
                    nativeFeatureNameBuffer = created.name;
                }
            }
            stagedNewFeatureTiles.Clear();
            featureStatus = "已创建原版 WorldFeature；可继续添加/移除 Tile，最后保存 Feature layout XML。";
            newFeatureName = string.Empty;
        }

        private string featureStatus;

        private void RenameNativeFeature(WorldFeature feature,
            HistoricalWorldFeatureOverrideWorldComponent component,
            HistoricalMapScaleTransformer transformer, HistoricalGeoCoordinate coordinate)
        {
            if (feature == null || component == null || transformer == null)
            { featureStatus = "无法修改：原版 Feature、场景状态或历史投影不可用。"; return; }
            if (string.IsNullOrWhiteSpace(nativeFeatureNameBuffer))
            { featureStatus = "请输入新名称。"; return; }
            HistoricalWorldFeatureNameRecord before = component.GetRecord(feature.uniqueID)?.Clone();
            string originalName = component.GetOriginalName(feature.uniqueID)
                ?? before?.originalName ?? WorldFeatureAdapter.GetName(feature);
            component.SetOriginalName(feature.uniqueID, originalName);
            HistoricalWorldFeatureNameRecord after = new HistoricalWorldFeatureNameRecord
            {
                featureUniqueId = feature.uniqueID,
                featureType = WorldFeatureAdapter.GetFeatureType(feature),
                historicalName = nativeFeatureNameBuffer.Trim(),
                originalName = originalName,
                targetLongitude = coordinate.Longitude,
                targetLatitude = coordinate.Latitude,
                searchRadiusKm = 100d
            };
            IMapEditCommand command = new NativeFeatureNameEditCommand(
                Find.World, feature, component, WorldFeatureAdapter.GetName(feature), after.historicalName, before, after);
            command.Execute();
            undo.Push(command);
            redo.Clear();
            featureStatus = "已将原生 Feature 重命名为“" + after.historicalName + "”。覆盖 Tile 与地形未改变。";
        }

        private void RestoreNativeFeatureName(WorldFeature feature, HistoricalWorldFeatureOverrideWorldComponent component)
        {
            if (feature == null || component == null) return;
            HistoricalWorldFeatureNameRecord before = component.GetRecord(feature.uniqueID)?.Clone();
            string originalName = component.GetOriginalName(feature.uniqueID) ?? before?.originalName;
            if (string.IsNullOrWhiteSpace(originalName))
            { featureStatus = "没有可恢复的原始名称记录。"; return; }
            IMapEditCommand command = new NativeFeatureNameEditCommand(
                Find.World, feature, component, WorldFeatureAdapter.GetName(feature), originalName, before, null);
            command.Execute();
            undo.Push(command);
            redo.Clear();
            featureStatus = "已恢复此世界生成时的原始名称。";
            nativeFeatureNameBuffer = originalName;
        }

        private sealed class NativeFeatureNameEditCommand : IMapEditCommand
        {
            private readonly RimWorld.Planet.World world;
            private readonly WorldFeature feature;
            private readonly HistoricalWorldFeatureOverrideWorldComponent component;
            private readonly string beforeName, afterName;
            private readonly HistoricalWorldFeatureNameRecord beforeRecord, afterRecord;
            public string Label => "World Feature name: " + afterName;
            public NativeFeatureNameEditCommand(RimWorld.Planet.World world, WorldFeature feature,
                HistoricalWorldFeatureOverrideWorldComponent component, string beforeName, string afterName,
                HistoricalWorldFeatureNameRecord beforeRecord, HistoricalWorldFeatureNameRecord afterRecord)
            { this.world = world; this.feature = feature; this.component = component; this.beforeName = beforeName;
              this.afterName = afterName; this.beforeRecord = beforeRecord?.Clone(); this.afterRecord = afterRecord?.Clone(); }
            public void Execute() => Apply(afterName, afterRecord);
            public void Undo() => Apply(beforeName, beforeRecord);
            private void Apply(string name, HistoricalWorldFeatureNameRecord record)
            {
                if (!WorldFeatureAdapter.TrySetName(feature, name)) return;
                if (record == null) component.RemoveRecord(feature.uniqueID);
                else component.SetRecord(record);
                try { WorldFeatureAdapter.RefreshFeatureLabels(world); }
                catch (Exception exception) { Log.Warning("[HR-016] Could not refresh native feature labels: " + exception.Message); }
            }
        }
        private void PreviewRiver()
        {
            if (!HistoricalRiverPathManager.TryBuildPlanFromText(riverCoordinates, selectedRiver,
                out HistoricalRiverPathPlan plan, out string error))
            {
                riverPreviewStatus = "预览失败：" + error;
                return;
            }
            riverPreviewStatus = "路由预览：" + plan.Waypoints.Count + " 个历史坐标点，映射为 "
                + plan.TileIds.Count + " 个相邻 Tile；最远坐标吸附误差 "
                + plan.MaximumSnapErrorDegrees.ToString("0.##", Invariant) + "°（Tile "
                + plan.TileIds[0] + " → " + plan.TileIds[plan.TileIds.Count - 1] + "）。";
        }

        private void ApplyRiver()
        {
            if (!HistoricalRiverPathManager.TryBuildPlanFromText(riverCoordinates, selectedRiver,
                out HistoricalRiverPathPlan plan, out string error))
            {
                riverPreviewStatus = "应用失败：" + error;
                return;
            }
            if (!HistoricalRiverPathManager.TryCreateEditCommand(plan,
                out IMapEditCommand command, out error))
            {
                riverPreviewStatus = "应用失败：" + error;
                return;
            }
            try
            {
                command.Execute();
                undo.Push(command);
                redo.Clear();
                riverPreviewStatus = "已应用：" + command.Label;
            }
            catch (Exception exception)
            { riverPreviewStatus = "应用失败：" + exception.Message; }
        }

        private void ApplyPaint()
        {
            if (!HistoricalMapEditManager.TryCreateTerrainPaint(selectedTerrain, brushRadius,
                out IMapEditCommand command, out string error))
            { status = error; return; }
            command.Execute();
            undo.Push(command);
            redo.Clear();
            status = "已应用：" + command.Label;
        }

        private void ApplyRestore()
        {
            if (!HistoricalMapEditManager.TryCreateRestore(brushRadius,
                out IMapEditCommand command, out string error))
            { status = error; return; }
            command.Execute();
            undo.Push(command);
            redo.Clear();
            status = "已恢复：" + command.Label;
        }

        private void ApplyBiomePaint()
        {
            if (!HistoricalMapEditManager.TryCreateBiomePaint(selectedBiome, brushRadius,
                out IMapEditCommand command, out string error))
            { status = error; return; }
            command.Execute();
            undo.Push(command);
            redo.Clear();
            status = "已应用：" + command.Label;
        }

        private void ApplyBiomeRestore()
        {
            if (!HistoricalMapEditManager.TryCreateBiomeRestore(brushRadius,
                out IMapEditCommand command, out string error))
            { status = error; return; }
            command.Execute();
            undo.Push(command);
            redo.Clear();
            status = "已恢复：" + command.Label;
        }

        private void SaveEdits(string scenarioId)
        {
            HistoricalMapEditWorldComponent component = Find.World?.GetComponent<HistoricalMapEditWorldComponent>();
            HistoricalMapTileDescriptionWorldComponent descriptions =
                Find.World?.GetComponent<HistoricalMapTileDescriptionWorldComponent>();
            if (component == null)
            { status = "保存失败：地图编辑存档组件不可用。"; Log.Error("[Historical RimWorld] Map edit save failed: component unavailable."); return; }
            if (!string.Equals(component.ScenarioDefName, scenarioId, StringComparison.Ordinal))
            { status = "保存失败：当前地图编辑数据与所选剧本不匹配。"; Log.Error("[Historical RimWorld] Map edit save failed: scenario mismatch."); return; }
            if (HistoricalMapEditFileStore.TrySave(component, descriptions, out string path, out string error))
            {
                status = "剧本地图修改已保存：" + path;
                Log.Message("[Historical RimWorld] Saved scenario map edits to " + path + " ("
                    + component.TerrainOverrides.Count() + " terrain overrides, "
                + component.BiomeOverrides.Count() + " biome overrides, "
                    + component.TileColorOverrides.Count() + " tile color overrides, "
                    + component.RiverPaths.Count() + " river paths, "
                    + (descriptions?.Descriptions.Count() ?? 0) + " tile descriptions).");
            }
            else
            {
                status = "保存失败：" + error;
                Log.Error("[Historical RimWorld] " + status);
            }
        }
    }
}
