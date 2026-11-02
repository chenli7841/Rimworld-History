#!/usr/bin/env python3
"""Create a compact, offline HRDEM raster cache from one or more GeoTIFF/COGs.

Requires Python 3, rasterio and numpy. The cache contains averaged elevation
samples in metres; it does not contain game terrain or historical vegetation.
"""

import argparse
import glob
import json
import math
import os
import struct
import sys
import zlib
from datetime import datetime, timezone

import numpy as np
import rasterio
from rasterio.enums import Resampling
from rasterio.transform import from_origin
from rasterio.vrt import WarpedVRT


MAGIC = b"HRDEM001"
VERSION = 1


def parse_args():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("inputs", nargs="+", help="GeoTIFF/COG file paths or glob patterns")
    p.add_argument("--output", required=True, help="Output .hrdem cache path")
    p.add_argument("--west", type=float, default=109.0)
    p.add_argument("--south", type=float, default=31.0)
    p.add_argument("--east", type=float, default=117.0)
    p.add_argument("--north", type=float, default=37.0)
    p.add_argument("--resolution", type=float, default=0.01,
                   help="Output cell size in degrees (default 0.01, about 1 km)")
    p.add_argument("--source", default="Copernicus DEM GLO-30 Public")
    p.add_argument("--source-version", default="2021 release; record tile release in metadata")
    p.add_argument("--license", default="Copernicus DEM Licence; verify the terms bundled with the downloaded release")
    p.add_argument("--attribution", default="Copernicus DEM, accessed by the mod author")
    return p.parse_args()


def expand_inputs(patterns):
    paths = []
    for pattern in patterns:
        matches = glob.glob(pattern)
        paths.extend(matches or ([pattern] if os.path.isfile(pattern) else []))
    result = sorted(set(os.path.abspath(path) for path in paths))
    if not result:
        raise ValueError("No input GeoTIFF/COG files matched the supplied paths.")
    return result


def main():
    args = parse_args()
    if not (-180 <= args.west < args.east <= 180 and -90 <= args.south < args.north <= 90):
        raise ValueError("Bounds must be ordered and within longitude/latitude limits.")
    if not (0 < args.resolution <= 1):
        raise ValueError("Resolution must be greater than 0 and no greater than 1 degree.")

    inputs = expand_inputs(args.inputs)
    width = int(math.ceil((args.east - args.west) / args.resolution))
    height = int(math.ceil((args.north - args.south) / args.resolution))
    transform = from_origin(args.west, args.north, args.resolution, args.resolution)
    total = np.zeros((height, width), dtype=np.float64)
    counts = np.zeros((height, width), dtype=np.uint16)
    nodata_values = []
    used = []

    for path in inputs:
        with rasterio.open(path) as src:
            if src.crs is None:
                raise ValueError("Input has no CRS: " + path)
            if src.count < 1:
                raise ValueError("Input has no raster band: " + path)
            vrt_options = {
                "crs": "EPSG:4326", "transform": transform,
                "width": width, "height": height,
                "resampling": Resampling.average,
                "nodata": src.nodata if src.nodata is not None else -9999.0,
                "dtype": "float32",
            }
            with WarpedVRT(src, **vrt_options) as vrt:
                data = vrt.read(1, masked=True)
                valid = ~np.ma.getmaskarray(data) & np.isfinite(data.data)
                if not valid.any():
                    continue
                total[valid] += data.data[valid]
                counts[valid] += 1
                nodata_values.append(src.nodata)
                used.append(os.path.basename(path))

    valid = counts > 0
    if not valid.any():
        raise ValueError("The source rasters do not overlap the requested geographic bounds.")
    values = np.full((height, width), -32768.0, dtype=np.float32)
    values[valid] = (total[valid] / counts[valid]).astype(np.float32)

    output = os.path.abspath(args.output)
    os.makedirs(os.path.dirname(output), exist_ok=True)
    header = struct.pack("<8siii5d", MAGIC, VERSION, width, height,
                         args.west, args.south, args.resolution, args.resolution, -32768.0)
    raw = header + values.astype("<f4", copy=False).tobytes(order="C")
    compressor = zlib.compressobj(level=9, wbits=-15)
    encoded = compressor.compress(raw) + compressor.flush()
    with open(output, "wb") as stream:
        stream.write(encoded)

    metadata = {
        "format": "HRDEM001", "formatVersion": VERSION,
        "source": args.source, "sourceVersion": args.source_version,
        "license": args.license, "attribution": args.attribution,
        "accessedUtc": datetime.now(timezone.utc).isoformat(),
        "inputFiles": used, "bounds": {"west": args.west, "south": args.south,
            "east": args.east, "north": args.north},
        "cellSizeDegrees": args.resolution, "width": width, "height": height,
        "validCells": int(valid.sum()), "totalCells": int(width * height),
        "nodataMeters": -32768.0,
        "processing": "GDAL/rasterio average resampling to EPSG:4326; row order north-to-south; values are source DEM metres.",
        "limitations": "Output cells are raster averages, not exact RimWorld tile-footprint intersections. Copernicus GLO-30 is a DSM, not bare-earth terrain. Historical vegetation, rivers and drainage are not inferred by this tool."
    }
    with open(os.path.splitext(output)[0] + ".json", "w", encoding="utf-8") as stream:
        json.dump(metadata, stream, ensure_ascii=False, indent=2)

    print("Wrote", output)
    print("Grid:", width, "x", height, "valid cells:", int(valid.sum()), "/", width * height)
    print("Source files:", len(used), "metadata:", os.path.splitext(output)[0] + ".json")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("DEM import failed:", exc, file=sys.stderr)
        sys.exit(2)
