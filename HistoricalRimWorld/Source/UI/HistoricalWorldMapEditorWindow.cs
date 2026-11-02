using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.Geography.Terrain;
using HistoricalRimWorld.World;
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
        private string geographicId = string.Empty;
        private string geographicName = string.Empty;
        private string geographicEnglishName = string.Empty;
        private string geographicCoordinates = string.Empty;
        private string geographicLabelPosition = string.Empty;
        private string geographicDescription = string.Empty;
        private string geographicSourceNotes = "Editor-authored approximate geometry; replace with source-verified coordinates.";
        private string geographicStatus;
        private HistoricalGeographicRegionType geographicType = HistoricalGeographicRegionType.GeographicRegion;
        private HistoricalGeographicGeometryType geographicGeometry = HistoricalGeographicGeometryType.Polygon;
        private float geographicRotation;
        private bool geographicAutoRotation;
        private string selectedGeographicId;
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

            DrawRiverEditor(listing);
            DrawGeographicRegionEditor(listing, factionState.ScenarioDefName);

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

            if (listing.ButtonText("Save scenario map edits")) SaveEdits(factionState.ScenarioDefName);
            if (!string.IsNullOrWhiteSpace(status)) listing.Label(status);
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

        private void DrawGeographicRegionEditor(Listing_Standard listing, string scenarioId)
        {
            HistoricalGeographicRegionWorldComponent component = Find.World
                .GetComponent<HistoricalGeographicRegionWorldComponent>();
            component?.InitializeForScenario(scenarioId);
            listing.GapLine();
            listing.Label("Geographic Regions");
            listing.Label("地理几何使用历史经纬度，不依赖 Tile ID；同一 Tile 可以同时落在多个地理实体内。");

            List<HistoricalGeographicRegionData> regions = (component?.Regions
                ?? Enumerable.Empty<HistoricalGeographicRegionData>()).Where(item => item != null)
                .OrderBy(item => item.defName, StringComparer.Ordinal).ToList();
            if (listing.ButtonText("选择地理实体：" + (selectedGeographicId ?? "(new)")))
            {
                List<FloatMenuOption> options = regions.Select(item => new FloatMenuOption(
                    item.defName + " · " + HistoricalGeographicRegionManager.ResolveName(item),
                    () => LoadGeographicRegion(item))).ToList();
                options.Insert(0, new FloatMenuOption("新建地理实体", ResetGeographicRegion));
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Label("DefName / ID");
            geographicId = Widgets.TextField(listing.GetRect(28f), geographicId);
            listing.Label("中文/默认名称");
            geographicName = Widgets.TextField(listing.GetRect(28f), geographicName);
            listing.Label("English name (optional)");
            geographicEnglishName = Widgets.TextField(listing.GetRect(28f), geographicEnglishName);
            if (listing.ButtonText("类型：" + geographicType + "（点击选择）"))
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(HistoricalGeographicRegionType))
                    .Cast<HistoricalGeographicRegionType>().Select(item => new FloatMenuOption(item.ToString(),
                        () => geographicType = item)).ToList()));
            if (listing.ButtonText("几何：" + geographicGeometry + "（点击选择）"))
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(HistoricalGeographicGeometryType))
                    .Cast<HistoricalGeographicGeometryType>().Select(item => new FloatMenuOption(item.ToString(),
                        () => geographicGeometry = item)).ToList()));
            listing.Label("顶点：每行一个 经度,纬度；LineString 至少2点，Polygon至少3点。编辑文本可移动/新增/删除任意顶点。");
            Rect coordinatesRect = listing.GetRect(130f);
            geographicCoordinates = Widgets.TextArea(coordinatesRect, geographicCoordinates);
            Rect vertexButtons = listing.GetRect(28f);
            if (Widgets.ButtonText(new Rect(vertexButtons.x, vertexButtons.y, vertexButtons.width * 0.48f, vertexButtons.height),
                "追加所选 Tile 的历史坐标") && TryGetSelectedHistoricalCoordinate(out string coordinate))
                geographicCoordinates = string.IsNullOrWhiteSpace(geographicCoordinates)
                    ? coordinate : geographicCoordinates.TrimEnd() + "\n" + coordinate;
            if (Widgets.ButtonText(new Rect(vertexButtons.x + vertexButtons.width * 0.52f, vertexButtons.y,
                vertexButtons.width * 0.48f, vertexButtons.height), "移除末尾顶点"))
                geographicCoordinates = RemoveLastCoordinateLine(geographicCoordinates);

            listing.Label("标签锚点 经度,纬度（留空则自动估算）");
            geographicLabelPosition = Widgets.TextField(listing.GetRect(28f), geographicLabelPosition);
            listing.Label("标签角度：" + geographicRotation.ToString("0.#", Invariant) + "°");
            geographicRotation = Widgets.HorizontalSlider(listing.GetRect(22f), geographicRotation, -90f, 90f);
            listing.CheckboxLabeled("沿河流/线几何自动调整标签方向", ref geographicAutoRotation);
            listing.Label("历史描述");
            geographicDescription = Widgets.TextArea(listing.GetRect(64f), geographicDescription);
            listing.Label("来源与精度说明（必填；无可靠边界时标注 approximate）");
            geographicSourceNotes = Widgets.TextArea(listing.GetRect(56f), geographicSourceNotes);

            Rect actionRect = listing.GetRect(30f);
            if (Widgets.ButtonText(new Rect(actionRect.x, actionRect.y, actionRect.width * 0.48f, actionRect.height),
                selectedGeographicId == null ? "创建/保存地理实体" : "应用区域修改")) SaveGeographicRegion(component, scenarioId);
            if (Widgets.ButtonText(new Rect(actionRect.x + actionRect.width * 0.52f, actionRect.y,
                actionRect.width * 0.48f, actionRect.height), "删除地理实体") && selectedGeographicId != null)
                DeleteGeographicRegion(component);

            if (listing.ButtonText("保存地理区域覆盖层 XML"))
            {
                string error = null;
                if (component != null && component.TrySaveOverrides(out string path, out error))
                    geographicStatus = "地理区域已保存：" + path;
                else geographicStatus = error ?? "地理区域组件不可用。";
            }
            if (!string.IsNullOrWhiteSpace(geographicStatus)) listing.Label(geographicStatus);
            listing.Label("覆盖 Tile 数：" + (selectedGeographicId == null ? 0
                : HistoricalGeographicRegionManager.GetTilesForRegion(selectedGeographicId).Count));
        }

        private void LoadGeographicRegion(HistoricalGeographicRegionData data)
        {
            if (data == null) return;
            selectedGeographicId = data.defName;
            geographicId = data.defName ?? string.Empty;
            geographicName = data.scenarioNameOverride ?? data.historicalName ?? data.label ?? string.Empty;
            geographicEnglishName = data.englishName ?? string.Empty;
            geographicType = data.regionType;
            geographicGeometry = data.geometryType;
            geographicRotation = data.labelRotation;
            geographicAutoRotation = data.autoLabelRotation;
            geographicDescription = data.historicalDescription ?? string.Empty;
            geographicSourceNotes = data.sourceNotes ?? string.Empty;
            List<HistoricalGeoVertex> vertices = data.geometryType == HistoricalGeographicGeometryType.Point
                || data.geometryType == HistoricalGeographicGeometryType.LineString
                ? data.coordinates ?? new List<HistoricalGeoVertex>()
                : (data.polygons?.FirstOrDefault()?.vertices ?? data.coordinates ?? new List<HistoricalGeoVertex>());
            geographicCoordinates = string.Join("\n", vertices.Where(item => item != null)
                .Select(item => item.longitude.ToString("0.#####", Invariant) + "," + item.latitude.ToString("0.#####", Invariant)));
            geographicLabelPosition = data.hasLabelPosition && data.labelPosition != null
                ? data.labelPosition.longitude.ToString("0.#####", Invariant) + ","
                    + data.labelPosition.latitude.ToString("0.#####", Invariant) : string.Empty;
            geographicStatus = "已载入 " + data.defName;
        }

        private void ResetGeographicRegion()
        {
            selectedGeographicId = null;
            geographicId = geographicName = geographicEnglishName = geographicCoordinates = string.Empty;
            geographicLabelPosition = geographicDescription = string.Empty;
            geographicSourceNotes = "Editor-authored approximate geometry; replace with source-verified coordinates.";
            geographicType = HistoricalGeographicRegionType.GeographicRegion;
            geographicGeometry = HistoricalGeographicGeometryType.Polygon;
            geographicRotation = 0f;
            geographicAutoRotation = false;
            geographicStatus = "新建地理实体";
        }

        private void SaveGeographicRegion(HistoricalGeographicRegionWorldComponent component, string scenarioId)
        {
            if (component == null) { geographicStatus = "地理区域存档组件不可用。"; return; }
            if (string.IsNullOrWhiteSpace(geographicId) || string.IsNullOrWhiteSpace(geographicName))
            { geographicStatus = "DefName 和中文/默认名称不能为空。"; return; }
            if (!TryParseCoordinates(geographicCoordinates, out List<HistoricalGeoVertex> vertices, out string error))
            { geographicStatus = error; return; }
            if (!TryParseOptionalCoordinate(geographicLabelPosition, out HistoricalGeoVertex labelPosition, out error))
            { geographicStatus = error; return; }

            HistoricalGeographicRegionData before = component.GetRegion(geographicId)?.Clone();
            HistoricalGeographicRegionData after = new HistoricalGeographicRegionData
            {
                defName = geographicId.Trim(), label = geographicName.Trim(), scenarioId = scenarioId,
                regionType = geographicType, historicalName = geographicName.Trim(),
                scenarioNameOverride = geographicName.Trim(), englishName = geographicEnglishName.Trim(),
                geometryType = geographicGeometry, coordinates = vertices,
                polygons = geographicGeometry == HistoricalGeographicGeometryType.Polygon
                    || geographicGeometry == HistoricalGeographicGeometryType.MultiPolygon
                    ? new List<HistoricalGeoPolygon> { new HistoricalGeoPolygon { vertices = vertices.Select(CloneGeoVertex).ToList() } }
                    : new List<HistoricalGeoPolygon>(),
                hasLabelPosition = labelPosition != null, labelPosition = labelPosition,
                labelRotation = geographicRotation, autoLabelRotation = geographicAutoRotation,
                minZoom = 0f, maxZoom = 1f, labelPriority = 50,
                confidence = HistoricalBoundaryConfidence.Approximate,
                source = "World Map Editor", sourceNotes = geographicSourceNotes,
                historicalDescription = geographicDescription
            };
            string[] validation = after.Validate().ToArray();
            if (validation.Length > 0) { geographicStatus = "校验失败：" + string.Join(" ", validation); return; }
            IMapEditCommand command = new GeographicRegionEditCommand(component, before, after);
            command.Execute();
            undo.Push(command);
            redo.Clear();
            selectedGeographicId = after.defName;
            geographicStatus = "已应用修改。点击“保存地理区域覆盖层 XML”写入可编辑的独立 XML 文件。";
        }

        private void DeleteGeographicRegion(HistoricalGeographicRegionWorldComponent component)
        {
            HistoricalGeographicRegionData before = component?.GetRegion(selectedGeographicId)?.Clone();
            if (before == null) { geographicStatus = "找不到要删除的地理实体。"; return; }
            IMapEditCommand command = new GeographicRegionEditCommand(component, before, null);
            command.Execute();
            undo.Push(command);
            redo.Clear();
            geographicStatus = "已删除；保存地理区域覆盖层 XML 后生效于下一次加载。";
            selectedGeographicId = null;
        }

        private bool TryGetSelectedHistoricalCoordinate(out string text)
        {
            text = null;
            if (!HistoricalMapEditManager.TryGetSelectedTileId(out int tileId)
                || tileId < 0 || tileId >= Find.World.grid.TilesCount) return false;
            HistoricalFactionWorldComponent faction = Find.World.GetComponent<HistoricalFactionWorldComponent>();
            HistoricalMapScaleDef scale = faction?.MapScaleState == null ? null
                : DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(faction.MapScaleState.ScaleDefName);
            if (scale == null) return false;
            Vector2 coordinate = Find.World.grid.Surface.LongLatOf(tileId);
            HistoricalGeoCoordinate historical = new HistoricalMapScaleTransformer(scale)
                .ToHistoricalCoordinate(new HistoricalGameGeoCoordinate(coordinate.y, coordinate.x));
            text = historical.Longitude.ToString("0.#####", Invariant) + ","
                + historical.Latitude.ToString("0.#####", Invariant);
            return true;
        }

        private static bool TryParseCoordinates(string text, out List<HistoricalGeoVertex> result, out string error)
        {
            result = new List<HistoricalGeoVertex>();
            error = null;
            string[] lines = (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < lines.Length; index++)
            {
                if (!TryParseOptionalCoordinate(lines[index], out HistoricalGeoVertex vertex, out error) || vertex == null)
                { error = "第 " + (index + 1) + " 行坐标无效，请使用 经度,纬度。"; return false; }
                result.Add(vertex);
            }
            return true;
        }

        private static bool TryParseOptionalCoordinate(string text, out HistoricalGeoVertex vertex, out string error)
        {
            vertex = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            string[] parts = text.Split(',');
            if (parts.Length != 2 || !double.TryParse(parts[0].Trim(), NumberStyles.Float, Invariant, out double longitude)
                || !double.TryParse(parts[1].Trim(), NumberStyles.Float, Invariant, out double latitude))
            { error = "坐标格式无效，请使用 经度,纬度。"; return false; }
            vertex = new HistoricalGeoVertex { longitude = longitude, latitude = latitude };
            return true;
        }

        private static string RemoveLastCoordinateLine(string value)
        {
            List<string> lines = (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (lines.Count > 0) lines.RemoveAt(lines.Count - 1);
            return string.Join("\n", lines);
        }

        private static HistoricalGeoVertex CloneGeoVertex(HistoricalGeoVertex vertex) => new HistoricalGeoVertex
        { longitude = vertex.longitude, latitude = vertex.latitude };

        private sealed class GeographicRegionEditCommand : IMapEditCommand
        {
            private readonly HistoricalGeographicRegionWorldComponent component;
            private readonly HistoricalGeographicRegionData before;
            private readonly HistoricalGeographicRegionData after;
            public string Label => (after ?? before)?.defName ?? "geographic region";
            public GeographicRegionEditCommand(HistoricalGeographicRegionWorldComponent component,
                HistoricalGeographicRegionData before, HistoricalGeographicRegionData after)
            { this.component = component; this.before = before?.Clone(); this.after = after?.Clone(); }
            public void Execute() => Apply(after);
            public void Undo() => Apply(before);
            private void Apply(HistoricalGeographicRegionData value)
            {
                string id = (value ?? before ?? after)?.defName;
                if (string.IsNullOrWhiteSpace(id) || component == null) return;
                if (value == null) component.RemoveRegion(id);
                else component.SetRegion(value);
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
            if (HistoricalMapEditFileStore.TrySave(component, descriptions, out string path, out string error))
                status = "剧本覆盖层已保存：" + path;
            else status = error;
        }
    }
}
