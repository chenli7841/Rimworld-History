$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class AncientForestTextureGenerator
{
    public static void Tree(string path, string species, int seed)
    {
        const int size = 512;
        Random rng = new Random(seed);
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            Color dark, mid, light, trunk;
            Palette(species, out dark, out mid, out light, out trunk);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(58, 20, 23, 17)))
                g.FillEllipse(shadow, 50, 74, 414, 382);

            PointF[] lobes = CanopyCenters(species);
            for (int i = 0; i < lobes.Length; i++)
            {
                float radius = species == "Pine" ? 162f - Math.Abs(lobes[i].X - 256f) * 0.16f : 132f;
                float x = lobes[i].X - radius;
                float y = lobes[i].Y - radius * 0.82f;
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(247, dark)))
                    g.FillEllipse(brush, x + 7, y + 13, radius * 2f, radius * 1.64f);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, mid)))
                    g.FillEllipse(brush, x + 1, y + 3, radius * 2f, radius * 1.58f);
            }

            // Dense leaf clusters create readable silhouettes at RimWorld's small map scale.
            for (int i = 0; i < 190; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2d);
                float radius = (float)Math.Sqrt(rng.NextDouble()) * 163f;
                float cx = 256f + (float)Math.Cos(angle) * radius;
                float cy = 248f + (float)Math.Sin(angle) * radius * 0.80f;
                float w = 12f + (float)rng.NextDouble() * 22f;
                float h = w * (0.62f + (float)rng.NextDouble() * 0.42f);
                Color c = rng.Next(5) == 0 ? light : (rng.Next(3) == 0 ? dark : mid);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(190 + rng.Next(66), c)))
                    g.FillEllipse(brush, cx - w / 2f, cy - h / 2f, w, h);
            }

            if (species == "Pine")
            {
                using (Pen branch = new Pen(Color.FromArgb(210, 89, 69, 43), 19f))
                {
                    branch.StartCap = LineCap.Round; branch.EndCap = LineCap.Round;
                    g.DrawLine(branch, 256, 205, 256, 390);
                }
                for (int y = 215; y < 382; y += 45)
                {
                    int spread = (y - 180) / 2;
                    using (Pen b = new Pen(Color.FromArgb(145, 83, 98, 51), 11f))
                    { b.StartCap = LineCap.Round; b.EndCap = LineCap.Round; g.DrawLine(b, 256, y, 256 - spread, y + 30); g.DrawLine(b, 256, y, 256 + spread, y + 30); }
                }
            }
            else
            {
                using (SolidBrush wood = new SolidBrush(Color.FromArgb(235, trunk)))
                    g.FillEllipse(wood, 215, 217, 82, 94);
                using (Pen roots = new Pen(Color.FromArgb(195, trunk), species == "Banyan" ? 17f : 11f))
                {
                    roots.StartCap = LineCap.Round; roots.EndCap = LineCap.Round;
                    g.DrawLine(roots, 245, 282, 161, 352);
                    g.DrawLine(roots, 267, 280, 351, 350);
                    if (species == "Banyan")
                    { g.DrawLine(roots, 239, 281, 203, 408); g.DrawLine(roots, 274, 278, 307, 414); }
                }
            }

            // Small canopy highlights retain transparent edges and avoid a flat icon-like look.
            for (int i = 0; i < 55; i++)
            {
                float x = 100 + (float)rng.NextDouble() * 312;
                float y = 95 + (float)rng.NextDouble() * 270;
                float r = 3f + (float)rng.NextDouble() * 7f;
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(95 + rng.Next(90), light)))
                    g.FillEllipse(brush, x, y, r, r * 0.72f);
            }

            bitmap.Save(path, ImageFormat.Png);
        }
    }

    public static void Soil(string path, int seed)
    {
        const int size = 256;
        Random rng = new Random(seed);
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                double wave = Math.Sin(2 * Math.PI * x / size * 5) * 2.0
                    + Math.Cos(2 * Math.PI * y / size * 7) * 1.5;
                int n = (int)(wave + rng.Next(-9, 10));
                bitmap.SetPixel(x, y, Color.FromArgb(255, Clamp(104 + n), Clamp(91 + n), Clamp(64 + n)));
            }
            for (int i = 0; i < 270; i++)
            {
                int x = rng.Next(size), y = rng.Next(size), w = rng.Next(2, 9), h = rng.Next(2, 7);
                Color c = i % 3 == 0 ? Color.FromArgb(95, 69, 48) : (i % 3 == 1 ? Color.FromArgb(135, 119, 77) : Color.FromArgb(82, 93, 53));
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(105, c))) g.FillEllipse(brush, x, y, w, h);
            }
            bitmap.Save(path, ImageFormat.Png);
        }
    }

    private static int Clamp(int value) { return Math.Max(0, Math.Min(255, value)); }
    private static PointF[] CanopyCenters(string species)
    {
        if (species == "Pine") return new[] { new PointF(256, 205), new PointF(256, 267), new PointF(256, 322), new PointF(256, 366) };
        if (species == "Banyan") return new[] { new PointF(180, 211), new PointF(258, 178), new PointF(338, 210), new PointF(190, 289), new PointF(322, 292), new PointF(256, 328) };
        if (species == "Camphor") return new[] { new PointF(180, 216), new PointF(252, 187), new PointF(333, 221), new PointF(207, 286), new PointF(303, 292) };
        return new[] { new PointF(185, 221), new PointF(255, 190), new PointF(327, 221), new PointF(207, 292), new PointF(303, 294) };
    }
    private static void Palette(string species, out Color dark, out Color mid, out Color light, out Color trunk)
    {
        if (species == "Pine") { dark = Color.FromArgb(38, 65, 50); mid = Color.FromArgb(55, 91, 65); light = Color.FromArgb(122, 143, 87); trunk = Color.FromArgb(90, 65, 43); return; }
        if (species == "Camphor") { dark = Color.FromArgb(46, 77, 45); mid = Color.FromArgb(70, 111, 57); light = Color.FromArgb(147, 158, 86); trunk = Color.FromArgb(95, 66, 43); return; }
        if (species == "Banyan") { dark = Color.FromArgb(43, 72, 44); mid = Color.FromArgb(62, 100, 52); light = Color.FromArgb(132, 153, 76); trunk = Color.FromArgb(105, 74, 47); return; }
        dark = Color.FromArgb(53, 76, 43); mid = Color.FromArgb(83, 111, 55); light = Color.FromArgb(153, 157, 83); trunk = Color.FromArgb(102, 70, 43);
    }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies @(
    [System.Drawing.Bitmap].Assembly.Location,
    [System.Drawing.PointF].Assembly.Location,
    (Join-Path ([System.IO.Path]::GetDirectoryName([System.Drawing.Bitmap].Assembly.Location)) 'System.Private.Windows.GdiPlus.dll'),
    (Join-Path ([System.IO.Path]::GetDirectoryName([System.Drawing.Bitmap].Assembly.Location)) 'System.Private.Windows.Core.dll')
)
$root = Split-Path -Parent $PSScriptRoot
$treeDir = Join-Path $root 'Textures\Things\Plant\HR'
$soilDir = Join-Path $root 'Textures\Terrain\HR'
New-Item -ItemType Directory -Force -Path $treeDir, $soilDir | Out-Null
[AncientForestTextureGenerator]::Tree((Join-Path $treeDir 'AncientOak.png'), 'Oak', 4201)
[AncientForestTextureGenerator]::Tree((Join-Path $treeDir 'AncientPine.png'), 'Pine', 4202)
[AncientForestTextureGenerator]::Tree((Join-Path $treeDir 'AncientCamphor.png'), 'Camphor', 4203)
[AncientForestTextureGenerator]::Tree((Join-Path $treeDir 'AncientBanyan.png'), 'Banyan', 4204)
[AncientForestTextureGenerator]::Soil((Join-Path $soilDir 'AncientLivingForestSoil.png'), 4205)
Write-Output "Generated four transparent 512x512 tree textures and one 256x256 soil tile."
