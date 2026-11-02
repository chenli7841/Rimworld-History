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
        private int brushRadius;
        private int descriptionEditTileId = -1;
        private string descriptionBuffer = string.Empty;
        private RiverDef selectedRiver;
        private string riverCoordinates = string.Empty;
        private string riverPreviewStatus;
        private string status;

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

            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, 1800f);
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
            if (listing.ButtonText("保存地形 / 河流 / Tile 描述修改到剧本 XML"))
                SaveEdits(factionState.ScenarioDefName);
            listing.Label("保存路径：Config\\HistoricalRimWorld\\MapEdits\\"
                + factionState.ScenarioDefName + ".xml");

            DrawRiverEditor(listing);
            DrawNativeWorldFeatureEditor(listing, factionState);

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
            listing.Label("当前版本支持：检查 Tile、地形画笔、历史河流、地理区域表单编辑、Undo/Redo 和覆盖层保存。");
            listing.Label("地理区域顶点通过经纬度文本修改；地图鼠标拖拽顶点/标签尚未开放。");
            listing.End();
            Widgets.EndScrollView();
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
                    string historicalCoordinateText = historical.Longitude.ToString("0.00000", Invariant)
                        + "," + historical.Latitude.ToString("0.00000", Invariant);
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
        private string nativeFeatureNameBuffer = string.Empty;

        private void DrawNativeWorldFeatureEditor(Listing_Standard listing, HistoricalFactionWorldComponent factionState)
        {
            listing.GapLine();
            listing.Label("World Features Editor (原版地理特征)");
            if (!HistoricalMapEditManager.TryGetSelectedTileId(out int tileId)
                || !HistoricalWorldFeatureOverrideManager.TryGetFeatureForTile(Find.World, tileId, out WorldFeature feature))
            {
                listing.Label("先选择一个带有原版地理特征的 Tile。山脉、湖泊、森林等按原版 Feature 类型筛选；河流不是 WorldFeature。");
                return;
            }

            HistoricalWorldFeatureOverrideWorldComponent component =
                Find.World.GetComponent<HistoricalWorldFeatureOverrideWorldComponent>();
            component?.Initialize(factionState.ScenarioDefName);
            if (selectedNativeFeatureId != feature.uniqueID)
            {
                selectedNativeFeatureId = feature.uniqueID;
                nativeFeatureNameBuffer = WorldFeatureAdapter.GetName(feature);
            }
            HistoricalMapScaleDef scale = factionState.MapScaleState == null ? null
                : DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(factionState.MapScaleState.ScaleDefName);
            HistoricalMapScaleTransformer transformer = scale == null ? null : new HistoricalMapScaleTransformer(scale);
            string representative = HistoricalWorldFeatureOverrideManager.TryGetHistoricalRepresentative(
                feature, transformer, out HistoricalGeoCoordinate featureCoordinate)
                ? featureCoordinate.Longitude.ToString("0.0000", Invariant) + ", "
                    + featureCoordinate.Latitude.ToString("0.0000", Invariant)
                : "unavailable";
            listing.Label("原生名称：" + WorldFeatureAdapter.GetName(feature));
            listing.Label("FeatureDef：" + WorldFeatureAdapter.GetFeatureType(feature)
                + "  |  Unique ID（仅当前世界有效）：" + feature.uniqueID);
            listing.Label("代表历史坐标：" + representative + "  |  覆盖 Tile："
                + WorldFeatureAdapter.GetFeatureTiles(Find.World, feature).Count);
            listing.Label("新名称");
            nativeFeatureNameBuffer = Widgets.TextField(listing.GetRect(28f), nativeFeatureNameBuffer);

            Rect featureButtons = listing.GetRect(30f);
            if (Widgets.ButtonText(new Rect(featureButtons.x, featureButtons.y,
                featureButtons.width * 0.48f, featureButtons.height), "重命名并加入撤销历史"))
                RenameNativeFeature(feature, component, transformer, featureCoordinate);
            if (Widgets.ButtonText(new Rect(featureButtons.x + featureButtons.width * 0.52f,
                featureButtons.y, featureButtons.width * 0.48f, featureButtons.height), "恢复原始随机名称"))
                RestoreNativeFeatureName(feature, component);

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
            if (!string.IsNullOrWhiteSpace(featureStatus)) listing.Label(featureStatus);
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
