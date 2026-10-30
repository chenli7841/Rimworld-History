# HR-012 Geographic Coverage Implementation

## RimWorld 1.6 API findings

The local `Assembly-CSharp.dll` was inspected directly.

- `WorldGenerator.GenerateWorld(float planetCoverage, string seedString, OverallRainfall, OverallTemperature, OverallPopulation, LandmarkDensity, List<FactionDef>, float pollution)` stores `planetCoverage` in `WorldInfo` before running world generation steps.
- `WorldGrid.GenerateFreshWorld()` calls `CalculateViewCenterAndAngle()` and then creates its required planet layers.
- `WorldGrid.CalculateViewCenterAndAngle()` sets `surfaceViewAngle = WorldInfo.PlanetCoverage * 180` and derives a default `surfaceViewCenter` from `Vector3.back`.
- Core's `Surface` `PlanetLayerSettingsDef` uses `subdivisions=10` and `useSurfaceViewAngle=true`.
- `PlanetLayer.InitializeLayer()` calls `IcosahedronGenerator.GenerateIcosahedron(..., viewCenter, viewAngle)`. `IcosahedronFaceNeeded` uses `MeshUtility.Visible` and adds 18 degrees while deciding which mesh faces to keep. Thus coverage is an angular patch-size input, not a longitude/latitude rectangle or an area-percent formula.
- `PlanetLayer.LongLatOf(int)` derives longitude and latitude from the generated tile-center vector. This is a coordinate system on RimWorld's generated spherical patch; it does not supply real terrain data.
- `Page_CreateWorldParams` exposes the normal coverage choices 0.3, 0.5, and 1.0 (plus a developer-only 0.05 choice). The generation API itself accepts a float. The historical path sets the computed float directly.

## Scenario configuration and calculation

`Defs/HistoricalWorldGenerationDefs/EasternZhouWorldGeneration.xml` configures the requested region as 27–42°N and 102–121°E, with 2° margins. `useRealEarthGeography` is false because no verified terrain dataset is included.

The coverage calculator expands the bounds, samples the spherical rectangle at 1° intervals, averages unit vectors for a center, and finds the maximum great-circle angular distance from that center. It adds a 3° tile-safety margin. Current calculated values are approximately:

- Center: 34.70°N, 111.50°E.
- Angular radius: 15.29°.
- `planetCoverage`: 0.08496 (about 8.5%), giving the vanilla angle calculation an angular view of about 15.29°.

These are calculated inputs, not proof that a world was successfully generated. The world-generation patch centers the surface patch on the calculated unit vector and the game then generates its mesh.

## Verification and failure behavior

After generation, the controller samples the entire margin-expanded bounds at 1° intervals and checks that each sample has a generated tile center within the configured 5° limit. Only after that check does it apply the editable XML terrain. Every explicit map override is also checked against its nearest tile center; duplicate tile assignments or excessive offsets fail instead of silently moving an override elsewhere.

The Harmony targets are:

- Prefix on the actual `WorldGenerator.GenerateWorld` overload above, to replace coverage only for a matched Historical RimWorld scenario and retain its fixed seed/faction filtering.
- Postfix on `WorldGrid.CalculateViewCenterAndAngle`, to replace the generated surface center only for that scenario.
- Prefix on `Page_CreateWorldParams.DoWindowContents`, to keep the displayed value aligned with the automatically calculated coverage.

`HistoricalFactionGenerationContext` now matches a scenario by its configured `vanillaScenarioName`; unrelated scenarios do not activate this behavior. Existing saves retain their serialized WorldGrid and are not regenerated.

## Limits and remaining work

- `useRealEarthGeography` remains disabled. The world has geographic coordinate coverage and prototype terrain, not a reconstructed China map.
- HR-005 region Defs currently lack verified representative coordinates/boundaries. This implementation validates the configured overall bounds; it does not certify exact Luoyi, Zheng, Jin, Qin, Qi, Lu, Song, Wey, or Chu historical sites.
- Faction settlements still use prototype offsets. They should be moved to HR-005 validated coordinates once those are provided.
- The world grid and mesh behavior have been confirmed from the local assembly, but the new center patch and coverage verifier still require an in-game generation run. Compilation alone does not prove runtime coverage.
