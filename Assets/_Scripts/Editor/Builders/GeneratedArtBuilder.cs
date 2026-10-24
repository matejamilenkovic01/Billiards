using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Everything Kenney doesn't have: ball textures and icons, table cloth and wood, cue art, a few UI shapes.
// Deterministic (fixed seed, no wall-clock input): running it again produces identical files.
public static class GeneratedArtBuilder
{
    public const string ArtFolder = "Assets/_Art/Generated";
    public const string MaterialsFolder = "Assets/_Materials";

    private const int Seed = 4242;
    private const string LitShader = "Universal Render Pipeline/Lit";

    public static string BallTexturePath(int number) => $"{ArtFolder}/ball_{number:00}.png";
    public static string BallSpritePath(int number) => $"{ArtFolder}/ballui_{number:00}.png";
    public const string UnknownBallSpritePath = ArtFolder + "/ballui_unknown.png";
    public static string BallMaterialPath(int number) => $"{MaterialsFolder}/Ball_{number}.mat";
    public static string CueSpritePath(string id) => $"{ArtFolder}/cue_{id}.png";
    public static string CueAlbedoPath(string id) => $"{ArtFolder}/cue_{id}_albedo.png";
    public static string CueMaterialPath(int oneBasedIndex) => $"{MaterialsFolder}/Cue_{oneBasedIndex}.mat";
    public static string MaterialPath(string name) => $"{MaterialsFolder}/{name}.mat";

    // Standard pool colours; stripes 9-15 reuse 1-7.
    public static readonly Color[] BallColors =
    {
        Hex("F4EFE2"), // 0 cue ball (ivory)
        Hex("F2B705"), // 1 yellow
        Hex("1C48C4"), // 2 blue
        Hex("D0201D"), // 3 red
        Hex("54217F"), // 4 purple
        Hex("EE6A12"), // 5 orange
        Hex("12804A"), // 6 green
        Hex("7A1B24"), // 7 maroon
        Hex("121212")  // 8 black
    };

    public static Color BallColor(int number) => number <= 8 ? BallColors[number] : BallColors[number - 8];

    private static readonly Color Ivory = Hex("F4EFE2");
    private static readonly Color Ink = Hex("141414");

    [MenuItem("Tools/Billiards/Builders/Generated Art")]
    public static void Build()
    {
        Directory.CreateDirectory(ArtFolder);

        for (int number = 0; number < 16; number++)
        {
            WritePng(BallTexturePath(number), BallTexture(number));
            WritePng(BallSpritePath(number), BallSprite(number));
        }
        WritePng(UnknownBallSpritePath, BallSprite(-1));

        WritePng(ArtFolder + "/felt.png", Felt());
        WritePng(ArtFolder + "/wood.png", Wood());
        WritePng(ArtFolder + "/carpet.png", Carpet());
        WritePng(ArtFolder + "/wallpaper.png", Wallpaper());

        WritePng(ArtFolder + "/icon_coin.png", Coin());
        WritePng(ArtFolder + "/icon_bolt.png", Bolt());
        WritePng(ArtFolder + "/icon_hand.png", Hand());
        WritePng(ArtFolder + "/icon_eye.png", Eye());
        WritePng(ArtFolder + "/icon_pinch.png", Pinch());

        foreach (DataAssetBuilder.CueSpec cue in DataAssetBuilder.CueSpecs)
        {
            WritePng(CueSpritePath(cue.Id), CueSprite(cue));
            WritePng(CueAlbedoPath(cue.Id), CueAlbedo(cue));
        }

        WritePng(ArtFolder + "/ui_white.png", Solid(4, 4, Color.white));
        WritePng(ArtFolder + "/ui_circle.png", Circle(128, false));
        WritePng(ArtFolder + "/ui_ring.png", Circle(128, true));
        WritePng(ArtFolder + "/ui_round_24.png", RoundedRect(64, 64, 24));
        WritePng(ArtFolder + "/ui_glow.png", Glow(128));
        WritePng(ArtFolder + "/tooltip_arrow.png", Triangle(48, 24));
        WritePng(ArtFolder + "/guide_dots.png", GuideDots());
        WritePng(ArtFolder + "/guide_band.png", GuideBandProfile());
        WritePng(ArtFolder + "/guide_chevron.png", GuideChevron());
        WritePng(ArtFolder + "/guide_dash.png", GuideDash());
        WritePng(ArtFolder + "/guide_line_profile.png", GuideLineProfile());
        WritePng(ArtFolder + "/guide_tip.png", GuideTip());
        WritePng(ArtFolder + "/guide_blob_shadow.png", BlobShadow(64));
        WritePng(ArtFolder + "/inlay_oval.png", InlayOval(64, 32));
        WritePng(ArtFolder + "/ui_gloss.png", Gloss(64, 16));
        WritePng(ArtFolder + "/ui_gloss_h.png", Gloss(16, 64, true));
        WritePng(ArtFolder + "/ui_power_fill.png", PowerFill(8, 256));
        WritePng(ArtFolder + "/ui_grip.png", GripRing(208, 88));
        WritePng(ArtFolder + "/ui_wheel_shade.png", WheelShade(256, 8));

        AssetDatabase.Refresh();
        Debug.Log("GeneratedArtBuilder: done");
    }

    #region Balls
    // Front number circle faces +Z, back one -Z. Seen from outside at +Z, the viewer's right is -X.
    private static readonly float NumberCircleRadius = 28f * Mathf.Deg2Rad;
    private static readonly float StripeHalfAngle = 31f * Mathf.Deg2Rad;
    private static readonly float CueDotRadius = 4.5f * Mathf.Deg2Rad;
    private const int BallTextureWidth = 512;
    private const int BallTextureHeight = 256;
    private const int BallSpriteSize = 96;

    // Equirectangular: u = longitude, v = latitude (bottom to top), matching Unity's sphere UVs.
    private static Texture2D BallTexture(int number)
    {
        const int samples = 3;
        var texture = NewTexture(BallTextureWidth, BallTextureHeight);
        var pixels = new Color[BallTextureWidth * BallTextureHeight];
        for (int y = 0; y < BallTextureHeight; y++)
        {
            for (int x = 0; x < BallTextureWidth; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float u = (x + (sx + 0.5f) / samples) / BallTextureWidth;
                        float v = (y + (sy + 0.5f) / samples) / BallTextureHeight;
                        sum += BallSurface(number, DirectionFromUv(u, v));
                    }
                }
                Color color = sum / (samples * samples);
                color.a = 1f;
                pixels[y * BallTextureWidth + x] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    // Longitude convention chosen to match the built-in sphere mesh UVs (front circle at +Z).
    public static Vector3 DirectionFromUv(float u, float v)
    {
        float longitude = u * 2f * Mathf.PI;
        float latitude = (v - 0.5f) * Mathf.PI;
        float cosLat = Mathf.Cos(latitude);
        return new Vector3(-cosLat * Mathf.Cos(longitude), Mathf.Sin(latitude), -cosLat * Mathf.Sin(longitude));
    }

    // number -1 = the grey "?" ball used while the table is open.
    private static Color BallSurface(int number, Vector3 direction)
    {
        if (number == 0)
            return Vector3.Angle(direction, Vector3.forward) * Mathf.Deg2Rad <= CueDotRadius ? Hex("C62828") : Ivory;

        bool unknown = number < 0;
        Color baseColor = unknown ? Hex("8E8E8E") : BallColor(number);
        bool isStripe = number >= 9;
        float latitude = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f));
        Color color = isStripe ? (Mathf.Abs(latitude) <= StripeHalfAngle ? baseColor : Ivory) : baseColor;

        string label = unknown ? "?" : number.ToString();
        color = NumberCircle(direction, Vector3.forward, Vector3.left, label, color);
        if (!unknown)
            color = NumberCircle(direction, Vector3.back, Vector3.right, label, color);
        return color;
    }

    private static Color NumberCircle(Vector3 direction, Vector3 center, Vector3 right, string label, Color under)
    {
        float cosAngle = Vector3.Dot(direction, center);
        if (cosAngle < Mathf.Cos(NumberCircleRadius))
            return under;

        // Gnomonic projection onto the plane touching the ball at the circle centre.
        Vector3 projected = direction / cosAngle;
        float radius = Mathf.Tan(NumberCircleRadius);
        float x = Vector3.Dot(projected, right) / radius;
        float y = projected.y / radius;
        return GlyphInk(label, x, y) ? Ink : Ivory;
    }

    // UI ball: shaded sphere seen from the front, tilted a little so it reads as 3D.
    private static Texture2D BallSprite(int number)
    {
        const int samples = 4;
        Quaternion tilt = Quaternion.Euler(14f, -12f, 0f);
        Vector3 light = new Vector3(-0.45f, 0.65f, 0.62f).normalized;
        var texture = NewTexture(BallSpriteSize, BallSpriteSize);
        float half = BallSpriteSize * 0.5f;
        float radius = half - 3f;

        for (int py = 0; py < BallSpriteSize; py++)
        {
            for (int px = 0; px < BallSpriteSize; px++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float x = (px + (sx + 0.5f) / samples - half) / radius;
                        float y = (py + (sy + 0.5f) / samples - half) / radius;
                        float r2 = x * x + y * y;
                        if (r2 > 1f)
                        {
                            // Soft drop shadow under the ball.
                            float sx2 = x - 0.06f;
                            float sy2 = y + 0.1f;
                            float shadow = Mathf.Clamp01(1f - (Mathf.Sqrt(sx2 * sx2 + sy2 * sy2) - 0.95f) / 0.08f) * 0.35f;
                            sum += new Color(0f, 0f, 0f, shadow);
                            continue;
                        }

                        var normal = new Vector3(x, y, Mathf.Sqrt(1f - r2));
                        // View space has the viewer's right at +X; the ball's front circle uses -X as its right.
                        Vector3 local = Quaternion.Inverse(tilt) * new Vector3(-normal.x, normal.y, normal.z);
                        Color albedo = BallSurface(number, local.normalized);
                        float diffuse = Mathf.Clamp01(Vector3.Dot(normal, light));
                        Vector3 half3 = (light + Vector3.forward).normalized;
                        float specular = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(normal, half3)), 60f) * 0.7f;
                        float rim = Mathf.Pow(1f - normal.z, 3f) * 0.35f;
                        Color lit = albedo * (0.38f + 0.72f * diffuse) * (1f - rim) + Color.white * specular;
                        lit.a = 1f;
                        sum += lit;
                    }
                }
                Color color = sum / (samples * samples);
                // Premultiplied sum -> straight alpha.
                if (color.a > 0f)
                {
                    float a = color.a;
                    color = new Color(color.r / a, color.g / a, color.b / a, a);
                }
                texture.SetPixel(px, py, color);
            }
        }
        texture.Apply();
        return texture;
    }
    #endregion

    #region Digit font
    // 5x7 bitmap digits, row 0 at the top. Smoothed by bilinear sampling + threshold, so strokes get round.
    private static readonly string[][] Glyphs =
    {
        new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " }, // 0
        new[] { "  #  ", " ##  ", "# #  ", "  #  ", "  #  ", "  #  ", "#####" }, // 1
        new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" }, // 2
        new[] { " ### ", "#   #", "    #", "  ## ", "    #", "#   #", " ### " }, // 3
        new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " }, // 4
        new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " }, // 5
        new[] { "  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### " }, // 6
        new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " }, // 7
        new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " }, // 8
        new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " }, // 9
        new[] { " ### ", "#   #", "    #", "   # ", "  #  ", "     ", "  #  " }  // ?
    };

    private const int GlyphColumns = 5;
    private const int GlyphRows = 7;
    private const float GlyphThreshold = 0.3f;

    // x, y in circle units (circle radius = 1), y up.
    private static bool GlyphInk(string label, float x, float y)
    {
        float height = label.Length == 1 ? 1.1f : 0.9f;
        float cell = height / GlyphRows;
        int widthCells = label.Length * GlyphColumns + (label.Length - 1);
        float gx = x / cell + widthCells * 0.5f;
        float gy = GlyphRows * 0.5f - y / cell;     // from the top

        for (int i = 0; i < label.Length; i++)
        {
            float localX = gx - i * (GlyphColumns + 1);
            if (localX < -1f || localX > GlyphColumns + 1f)
                continue;

            int glyph = label[i] == '?' ? 10 : label[i] - '0';
            if (SampleGlyph(Glyphs[glyph], localX, gy) > GlyphThreshold)
                return true;
        }
        return false;
    }

    private static float SampleGlyph(string[] rows, float x, float y)
    {
        float fx = x - 0.5f;
        float fy = y - 0.5f;
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0;
        float ty = fy - y0;
        float a = Cell(rows, x0, y0);
        float b = Cell(rows, x0 + 1, y0);
        float c = Cell(rows, x0, y0 + 1);
        float d = Cell(rows, x0 + 1, y0 + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Cell(string[] rows, int column, int row)
    {
        if (row < 0 || row >= GlyphRows || column < 0 || column >= GlyphColumns)
            return 0f;
        return rows[row][column] == '#' ? 1f : 0f;
    }
    #endregion

    #region Table surfaces
    private static Texture2D Felt()
    {
        const int size = 256;
        var texture = NewTexture(size, size);
        Color baseColor = Hex("1C6B41");
        var random = new System.Random(Seed);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float broad = TileNoise(x, y, size, 8, Seed) * 0.5f + TileNoise(x, y, size, 16, Seed + 1) * 0.3f;
                float grain = (float)(random.NextDouble() - 0.5) * 0.05f;
                float shade = 1f + (broad - 0.4f) * 0.08f + grain;
                Color color = baseColor * shade;
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        return texture;
    }

    private static Texture2D Wood()
    {
        const int size = 256;
        var texture = NewTexture(size, size);
        Color dark = Hex("2E150B");
        Color light = Hex("6A3519");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Grain runs along x; the distortion warps the rings so they don't look ruled.
                float warp = TileNoise(x, y, size, 4, Seed + 2) * 2.2f + TileNoise(x, y, size, 8, Seed + 3) * 0.6f;
                float rings = 0.5f + 0.5f * Mathf.Sin((y / (float)size * 7f + warp) * 2f * Mathf.PI);
                rings = Mathf.Pow(rings, 2.5f);
                float fibre = TileNoise(x, y * 8, size, 32, Seed + 4) * 0.18f;
                Color color = Color.Lerp(dark, light, 0.25f + rings * 0.45f + fibre);
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        return texture;
    }

    // Club carpet: deep burgundy with a faint gold diamond lattice. Tileable.
    private static Texture2D Carpet()
    {
        const int size = 256;
        var texture = NewTexture(size, size);
        Color baseColor = Hex("4E1719");
        Color lattice = Hex("8C6A2E");
        var random = new System.Random(Seed + 10);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Diamond lattice: lines along both diagonals, 2 cells per tile.
                float u = x / (float)size * 2f;
                float v = y / (float)size * 2f;
                float d1 = Mathf.Abs(Mathf.Repeat(u + v, 1f) - 0.5f);
                float d2 = Mathf.Abs(Mathf.Repeat(u - v, 1f) - 0.5f);
                float line = Mathf.Clamp01(1f - Mathf.Min(d1, d2) * 40f);
                float dot = Mathf.Clamp01(1f - (Mathf.Abs(Mathf.Repeat(u, 1f) - 0.5f) + Mathf.Abs(Mathf.Repeat(v, 1f) - 0.5f)) * 9f);
                float noise = TileNoise(x, y, size, 32, Seed + 11) * 0.12f + (float)(random.NextDouble() - 0.5) * 0.06f;
                Color color = Color.Lerp(baseColor, lattice, line * 0.35f + dot * 0.5f) * (0.94f + noise);
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        return texture;
    }

    // Dark green wallpaper with soft vertical stripes. Tileable.
    private static Texture2D Wallpaper()
    {
        const int size = 256;
        var texture = NewTexture(size, size);
        Color dark = Hex("244A3F");
        Color light = Hex("33604F");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float stripe = Mathf.Sin(x / (float)size * Mathf.PI * 2f * 8f) * 0.5f + 0.5f;
                stripe = Mathf.Pow(stripe, 6f);
                float noise = TileNoise(x, y, size, 16, Seed + 12) * 0.08f;
                Color color = Color.Lerp(dark, light, stripe * 0.8f) * (0.96f + noise);
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        return texture;
    }

    // Value noise that wraps every `size` pixels, with `cells` lattice cells across.
    private static float TileNoise(int x, int y, int size, int cells, int seed)
    {
        float fx = (float)(x % size) / size * cells;
        float fy = (float)(((y % size) + size) % size) / size * cells;
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        float tx = Smooth(fx - x0);
        float ty = Smooth(fy - y0);
        float a = Lattice(x0 % cells, y0 % cells, seed);
        float b = Lattice((x0 + 1) % cells, y0 % cells, seed);
        float c = Lattice(x0 % cells, (y0 + 1) % cells, seed);
        float d = Lattice((x0 + 1) % cells, (y0 + 1) % cells, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Lattice(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }
    #endregion

    #region Icons
    private const int IconSize = 128;

    private static Texture2D Coin()
    {
        var center = new Vector2(64f, 66f);
        const float radius = 54f;
        Color rim = Hex("C8860F");
        Color faceTop = Hex("FFD54A");
        Color faceBottom = Hex("F0A91C");
        Color ring = Hex("D9951A");
        Color star = Hex("FFE58A");

        return Draw(IconSize, p =>
        {
            float shadowDistance = Vector2.Distance(p, center + new Vector2(0f, -5f)) - radius;
            Color color = new Color(0f, 0f, 0f, Coverage(shadowDistance) * 0.35f);

            float distance = Vector2.Distance(p, center) - radius;
            if (distance > 1f)
                return color;

            float t = Mathf.InverseLerp(center.y - radius, center.y + radius, p.y);
            Color face = Color.Lerp(faceBottom, faceTop, t);
            Color body = distance > -7f ? rim : face;
            if (Mathf.Abs(distance + 15f) < 2f)
                body = ring;
            if (StarDistance(p - center, 22f, 10f) < 0f)
                body = star;
            return Over(color, body, Coverage(distance));
        });
    }

    private static Texture2D Bolt()
    {
        Vector2[] polygon =
        {
            new(70, 124), new(26, 58), new(58, 58), new(42, 4), new(102, 74), new(68, 74), new(90, 124)
        };
        Color fill = Hex("FFD23F");
        Color outline = Hex("B97700");
        return Draw(IconSize, p =>
        {
            float distance = PolygonDistance(p, polygon);
            Color body = distance > -5f ? outline : fill;
            return new Color(body.r, body.g, body.b, Coverage(distance));
        });
    }

    private static Texture2D Hand()
    {
        return Draw(IconSize, p =>
        {
            float distance = RoundedBoxDistance(p - new Vector2(66f, 44f), new Vector2(26f, 26f), 14f);
            distance = Mathf.Min(distance, CapsuleDistance(p, new Vector2(46f, 60f), new Vector2(46f, 100f), 8f));
            distance = Mathf.Min(distance, CapsuleDistance(p, new Vector2(61f, 64f), new Vector2(61f, 112f), 8f));
            distance = Mathf.Min(distance, CapsuleDistance(p, new Vector2(76f, 64f), new Vector2(76f, 108f), 8f));
            distance = Mathf.Min(distance, CapsuleDistance(p, new Vector2(90f, 58f), new Vector2(90f, 96f), 7.5f));
            distance = Mathf.Min(distance, CapsuleDistance(p, new Vector2(44f, 40f), new Vector2(20f, 68f), 9f));
            return new Color(1f, 1f, 1f, Coverage(distance));
        });
    }

    // Pinch gesture (v3 V2): two finger dots with arrows pointing apart along the diagonal.
    private static Texture2D Pinch()
    {
        const int size = 128;
        var a = new Vector2(0.36f, 0.36f) * size;
        var b = new Vector2(0.64f, 0.64f) * size;
        float dot = 0.1f * size;
        float stroke = 0.035f * size;
        return Draw(size, p =>
        {
            float d = Mathf.Min((p - a).magnitude - dot, (p - b).magnitude - dot);
            // Arrows from each dot outwards: a shaft and a head.
            var outA = new Vector2(0.14f, 0.14f) * size;
            var outB = new Vector2(0.86f, 0.86f) * size;
            d = Mathf.Min(d, CapsuleDistance(p, a, outA, stroke));
            d = Mathf.Min(d, CapsuleDistance(p, b, outB, stroke));
            d = Mathf.Min(d, CapsuleDistance(p, outA, outA + new Vector2(0.16f, 0f) * size, stroke));
            d = Mathf.Min(d, CapsuleDistance(p, outA, outA + new Vector2(0f, 0.16f) * size, stroke));
            d = Mathf.Min(d, CapsuleDistance(p, outB, outB - new Vector2(0.16f, 0f) * size, stroke));
            d = Mathf.Min(d, CapsuleDistance(p, outB, outB - new Vector2(0f, 0.16f) * size, stroke));
            return new Color(1f, 1f, 1f, Coverage(d));
        });
    }

    private static Texture2D Eye()
    {
        const float circleRadius = 70f;
        const float offset = 46f;
        var center = new Vector2(64f, 64f);
        return Draw(IconSize, p =>
        {
            // Almond = intersection of two big circles; draw its outline, then the iris.
            float upper = Vector2.Distance(p, center + new Vector2(0f, -offset)) - circleRadius;
            float lower = Vector2.Distance(p, center + new Vector2(0f, offset)) - circleRadius;
            float almond = Mathf.Max(upper, lower);
            float outline = Mathf.Abs(almond) - 4.5f;
            float iris = Vector2.Distance(p, center) - 19f;
            float pupil = Vector2.Distance(p, center) - 7f;
            float shape = Mathf.Min(outline, Mathf.Max(iris, -pupil));
            return new Color(1f, 1f, 1f, Coverage(shape));
        });
    }
    #endregion

    #region Cues
    private const int CueSpriteWidth = 1024;
    private const int CueSpriteHeight = 64;
    private const float CueMarginPx = 8f;
    private const float CueButtHalfPx = 24f;

    // Where the ferrule and the leather tip start along the cue (t: 0 = butt, 1 = tip), from their lengths (v3 V3).
    public static float CueTipStart01(MatchConfig config) => 1f - config.CueTipLength / config.CueLength;
    public static float CueFerruleStart01(MatchConfig config) => 1f - (config.CueTipLength + config.CueFerruleLength) / config.CueLength;

    private static MatchConfig _cueConfig;
    private static MatchConfig CueConfig => _cueConfig != null ? _cueConfig : _cueConfig = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);

    // t: 0 = butt, 1 = tip. Same layout is used for the UI sprite and the 3D albedo.
    public static Color CueColorAt(DataAssetBuilder.CueSpec cue, float t)
    {
        if (t >= CueTipStart01(CueConfig)) return Hex("4A6FA5");                      // chalked leather tip
        if (t >= CueFerruleStart01(CueConfig)) return Hex("F3EEDF");                  // ivory ferrule
        if (t < 0.012f) return Hex("101010");                              // bumper
        if (t < 0.30f) return cue.Butt;                                    // sleeve
        if (t < 0.305f) return cue.Band;
        if (t < 0.42f) return Color.Lerp(cue.Butt, Color.black, 0.45f);   // wrap
        if (t < 0.425f) return cue.Band;
        if (t < 0.47f) return cue.Butt;                                    // forearm
        if (t < 0.485f) return cue.Band;
        if (t < 0.50f) return Hex("E9E2CF");                               // joint
        return Color.Lerp(cue.Shaft, Color.Lerp(cue.Shaft, Color.white, 0.25f), (t - 0.5f) / 0.475f);
    }

    // Smoothness along the cue, stored in the albedo's alpha: lacquered wood, polished ferrule, matte chalked tip.
    private static float CueSmoothnessAt(float t) =>
        t >= CueTipStart01(CueConfig) ? 0.08f : t >= CueFerruleStart01(CueConfig) ? 0.6f : t < 0.012f ? 0.4f : 0.72f;

    public static float CueRadius01(float t) => CueClearance.Radius01(t);

    private static Texture2D CueSprite(DataAssetBuilder.CueSpec cue)
    {
        var texture = NewTexture(CueSpriteWidth, CueSpriteHeight);
        float length = CueSpriteWidth - 2f * CueMarginPx;
        float centerY = CueSpriteHeight * 0.5f;
        Color glow = Color.Lerp(cue.Band, Color.white, 0.3f);

        for (int y = 0; y < CueSpriteHeight; y++)
        {
            for (int x = 0; x < CueSpriteWidth; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float t = Mathf.Clamp01((px - CueMarginPx) / length);
                float half = CueRadius01(t) * CueButtHalfPx;
                // Signed distance to the cue outline: sides, plus rounded ends.
                float side = Mathf.Abs(py - centerY) - half;
                float ends = Mathf.Max(CueMarginPx - px, px - (CueSpriteWidth - CueMarginPx));
                float distance = Mathf.Max(side, ends);

                Color color = Color.clear;
                if (cue.Glow > 0f && distance > 0f)
                    color = new Color(glow.r, glow.g, glow.b, Mathf.Clamp01(1f - distance / 7f) * cue.Glow);

                if (distance < 1f)
                {
                    float yn = Mathf.Clamp((py - centerY) / Mathf.Max(half, 0.01f), -1f, 1f);
                    float nz = Mathf.Sqrt(1f - yn * yn);
                    float shade = 0.5f + 0.42f * nz + 0.15f * yn;
                    float highlight = Mathf.Exp(-Mathf.Pow((yn - 0.42f) / 0.14f, 2f)) * 0.35f;
                    Color body = CueColorAt(cue, t) * shade + Color.white * highlight;
                    body.a = 1f;
                    color = Over(color, body, Coverage(distance));
                }
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        return texture;
    }

    // 2048 px over the cue's length: under a millimetre per pixel, so the 12 mm tip and 25 mm ferrule are crisp.
    private static Texture2D CueAlbedo(DataAssetBuilder.CueSpec cue)
    {
        const int width = 2048;
        const int height = 4;
        var texture = NewTexture(width, height);
        for (int x = 0; x < width; x++)
        {
            float t = (x + 0.5f) / width;
            Color color = CueColorAt(cue, t);
            color.a = CueSmoothnessAt(t);
            for (int y = 0; y < height; y++)
                texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return texture;
    }
    #endregion

    // Power strip grip (v3 V5): a rounded brass ring across the cue, shaded like a cylinder, with a sheen on top.
    private static Texture2D GripRing(int width, int height)
    {
        var texture = NewTexture(width, height);
        float radius = height * 0.5f - 2f;
        var a = new Vector2(radius + 2f, height * 0.5f);
        var b = new Vector2(width - radius - 2f, height * 0.5f);
        Color dark = Hex("6B4A1C"), mid = Hex("C8973E"), light = Hex("FFE7A8");
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float distance = CapsuleDistance(p, a, b, radius);
                float coverage = Coverage(distance);
                if (coverage <= 0f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }
                float across = Mathf.Clamp((p.y - height * 0.5f) / radius, -1f, 1f);   // -1 bottom .. 1 top
                Color body = across < 0f ? Color.Lerp(mid, dark, -across) : Color.Lerp(mid, light, across * 0.8f);
                float sheen = Mathf.Exp(-Mathf.Pow((across - 0.45f) / 0.16f, 2f)) * 0.55f;
                body = Color.Lerp(body, Color.white, sheen);
                // Dark rim so it reads on the light cue and on the dark strip.
                body = Color.Lerp(Hex("2A1C0C"), body, Mathf.Clamp01(-distance - 1.5f));
                body.a = coverage;
                texture.SetPixel(x, y, body);
            }
        }
        texture.Apply();
        return texture;
    }

    #region UI shapes
    private static Texture2D Solid(int width, int height, Color color)
    {
        var texture = NewTexture(width, height);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                texture.SetPixel(x, y, color);
        texture.Apply();
        return texture;
    }

    private static Texture2D Circle(int size, bool ring)
    {
        float radius = size * 0.5f - 2f;
        var center = new Vector2(size * 0.5f, size * 0.5f);
        return Draw(size, p =>
        {
            float distance = Vector2.Distance(p, center) - radius;
            if (ring)
                distance = Mathf.Abs(distance + 7f) - 7f;
            return new Color(1f, 1f, 1f, Coverage(distance));
        });
    }

    private static Texture2D RoundedRect(int width, int height, float radius)
    {
        var texture = NewTexture(width, height);
        var half = new Vector2(width * 0.5f, height * 0.5f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f) - half;
                float distance = RoundedBoxDistance(p, half - new Vector2(radius, radius), radius);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Coverage(distance)));
            }
        }
        texture.Apply();
        return texture;
    }

    private static Texture2D Glow(int size)
    {
        var center = new Vector2(size * 0.5f, size * 0.5f);
        return Draw(size, p =>
        {
            float t = Mathf.Clamp01(Vector2.Distance(p, center) / (size * 0.5f));
            float alpha = Mathf.Pow(1f - t, 2f);
            return new Color(1f, 1f, 1f, alpha);
        });
    }

    // Horizontal highlight across a vertical bar: a bright band left of centre, fading out (cylinder look).
    // Glossy highlight band across a bar: along x for an upright bar, along y (highlight near the top) for a
    // lying one.
    private static Texture2D Gloss(int width, int height, bool acrossHeight = false)
    {
        var texture = NewTexture(width, height);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float t = acrossHeight ? 1f - (y + 0.5f) / height : (x + 0.5f) / width;
                float band = Mathf.Exp(-Mathf.Pow((t - 0.3f) / 0.13f, 2f)) * 0.55f;
                float edge = Mathf.Pow(Mathf.Abs(t - 0.5f) * 2f, 3f) * 0.35f;
                texture.SetPixel(x, y, band > edge ? new Color(1f, 1f, 1f, band) : new Color(0f, 0f, 0f, edge));
            }
        }
        texture.Apply();
        return texture;
    }

    // One tile of the aim line: a soft line with a brighter dot in the middle (repeated along the line).
    private static Texture2D GuideDots()
    {
        const int width = 64;
        const int height = 16;
        var texture = NewTexture(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f - width * 0.5f) / (height * 0.5f);
                float dy = (y + 0.5f - height * 0.5f) / (height * 0.5f);
                float dot = Mathf.Clamp01(1.4f - Mathf.Sqrt(dx * dx + dy * dy) * 1.4f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, dot)));
            }
        }
        texture.Apply();
        return texture;
    }

    // Across-band profile for the guide bands (y = across, 0..1 over 2R): r = 1 in the fill, 0 in the dark
    // contour along both edges (1 mm of the 57 mm band, v3 V1), anti-aliased so the edge stays soft.
    private static Texture2D GuideBandProfile()
    {
        const int width = 4;
        const int height = 256;
        const float contour = 0.001f / 0.05715f;
        var texture = NewTexture(width, height);
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            float edge = Mathf.Min(v, 1f - v);
            float fill = Mathf.Clamp01((edge - contour) * height + 0.5f);
            float alpha = Mathf.Clamp01(edge * height + 0.5f);
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, new Color(fill, fill, fill, alpha));
        }
        texture.Apply();
        return texture;
    }

    // Across the thin cue ball line (v3 V1): 7 mm = a 4 mm white core with a 1.5 mm dark shadow each side that
    // fades out, so the line reads over the white cue ball and over light cloth.
    private static Texture2D GuideLineProfile()
    {
        const int width = 4;
        const int height = 64;
        const float core = 4f / 7f;
        var texture = NewTexture(width, height);
        for (int y = 0; y < height; y++)
        {
            float across = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2f;   // 0 centre, 1 edge
            float fill = Mathf.Clamp01((core - across) * height * 0.5f + 0.5f);
            float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(core, 1f, across));
            alpha = Mathf.Max(fill, alpha * alpha);
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, new Color(fill, fill, fill, alpha));
        }
        texture.Apply();
        return texture;
    }

    // Soft contact shadow under a ball (v3 V6): black, alpha falling off from the centre.
    private static Texture2D BlobShadow(int size)
    {
        var texture = NewTexture(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, r)), 1.4f);
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        texture.Apply();
        return texture;
    }

    // Oval mother-of-pearl inlay for the rail sights (v3 V6): pearly gradient, soft edge in alpha.
    private static Texture2D InlayOval(int width, int height)
    {
        var texture = NewTexture(width, height);
        Color a = Hex("F2EEE6"), b = Hex("CFDCE2");
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2((x + 0.5f) / width * 2f - 1f, (y + 0.5f) / height * 2f - 1f);
                float r = p.magnitude;
                float edge = Mathf.Clamp01((1f - r) * width * 0.5f);
                Color c = Color.Lerp(a, b, Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(p.x * 5f + p.y * 3f)));
                c.a = edge;
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply();
        return texture;
    }

    // Rounded arrow tip (v3 V1) for the end of a band: base along the bottom edge (open, it joins the band), point
    // at the top. White fill, a thin dark contour on the slanted sides; tinted by the guide colour.
    private static Texture2D GuideTip()
    {
        const int size = 64;
        float round = 0.16f * size;
        var apex = new Vector2(0.5f * size, size - round - 1f);
        var left = new Vector2(1f, 0f);
        var right = new Vector2(size - 1f, 0f);
        float contourPx = 2.2f;
        return Draw(size, p =>
        {
            // Inside test against the two slanted sides (offset by the rounding), plus the rounded apex.
            float SideDistance(Vector2 a, Vector2 b)
            {
                Vector2 edge = (b - a).normalized;
                var normal = new Vector2(edge.y, -edge.x);
                return Vector2.Dot(p - a, normal);
            }
            float d = Mathf.Max(SideDistance(left, apex), SideDistance(apex, right)) - round;
            if (p.y > apex.y)
                d = Mathf.Max(d, (p - apex).magnitude - round);
            d = Mathf.Max(d, -p.y);   // open base
            float coverage = Coverage(d);
            if (coverage <= 0f)
                return Color.clear;
            float fill = p.y < contourPx ? 1f : Mathf.Clamp01(-d - contourPx + 0.5f);
            Color color = Color.Lerp(new Color(0f, 0f, 0f, 1f), Color.white, fill);
            color.a = coverage;
            return color;
        });
    }

    // One chevron tile (x along the band, pointing +x; y across): soft white, no shadow (v3 V1: calm, low contrast).
    private static Texture2D GuideChevron()
    {
        const int size = 64;
        var tip = new Vector2(0.62f, 0.5f) * size;
        var upper = new Vector2(0.44f, 0.72f) * size;
        var lower = new Vector2(0.44f, 0.28f) * size;
        float halfStroke = 0.045f * size;
        const float softness = 3.5f;
        return Draw(size, p =>
        {
            float distance = Mathf.Min(CapsuleDistance(p, upper, tip, halfStroke), CapsuleDistance(p, lower, tip, halfStroke));
            float alpha = Mathf.Clamp01(0.5f - distance / softness);
            return new Color(1f, 1f, 1f, alpha * alpha * (3f - 2f * alpha));
        });
    }

    // Dashes for thin dashed guide lines: one tile = dash + gap.
    private static Texture2D GuideDash()
    {
        const int width = 32;
        const int height = 8;
        var texture = NewTexture(width, height);
        for (int x = 0; x < width; x++)
        {
            float alpha = Coverage(Mathf.Abs(x + 0.5f - width * 0.3f) - width * 0.3f);
            for (int y = 0; y < height; y++)
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        return texture;
    }

    // Power fill along the cue (G7): one colour family, white at the top to warm orange at the bottom, so it
    // never reads like the release bar's green / yellow / red.
    private static Texture2D PowerFill(int width, int height)
    {
        var texture = NewTexture(width, height);
        Color top = new(1f, 0.97f, 0.9f, 0.55f);
        Color bottom = new(1f, 0.55f, 0.15f, 0.8f);
        for (int y = 0; y < height; y++)
        {
            Color color = Color.Lerp(bottom, top, (y + 0.5f) / height);
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return texture;
    }

    // Fine aim roller shading: dark at both ends, clear in the middle.
    private static Texture2D WheelShade(int width, int height)
    {
        var texture = NewTexture(width, height);
        for (int x = 0; x < width; x++)
        {
            float edge = Mathf.Abs((x + 0.5f) / width - 0.5f) * 2f;
            var color = new Color(0f, 0f, 0f, Mathf.Pow(edge, 2.2f) * 0.85f);
            for (int y = 0; y < height; y++)
                texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return texture;
    }

    // Downward-pointing triangle; the flat edge is at the top.
    private static Texture2D Triangle(int width, int height)
    {
        var texture = NewTexture(width, height);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float halfWidthAtRow = (y + 0.5f) / height * (width * 0.5f);
                float distance = halfWidthAtRow - Mathf.Abs(x + 0.5f - width * 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(distance + 0.5f)));
            }
        }
        texture.Apply();
        return texture;
    }
    #endregion

    #region Materials
    [MenuItem("Tools/Billiards/Builders/Materials")]
    public static void BuildMaterials()
    {
        Directory.CreateDirectory(MaterialsFolder);

        for (int number = 0; number < 16; number++)
        {
            Material ball = LitMaterial(BallMaterialPath(number));
            ball.SetTexture("_BaseMap", Load<Texture2D>(BallTexturePath(number)));
            ball.SetColor("_BaseColor", Color.white);
            ball.SetFloat("_Smoothness", 0.9f);
            ball.SetFloat("_Metallic", 0f);
        }

        // v3 V6: Poly Haven PBR where downloaded (PolyHavenBuilder), else the generated textures. The cloth is grey
        // and tinted by the venue's felt colour (VenueView); its default colour is the Riverside green.
        Color feltGreen = Hex("1F6B45");
        Material felt = LitMaterial(MaterialPath("Felt"));
        PbrOrGenerated(felt, PolyHavenBuilder.Cloth, ArtFolder + "/felt.png", 0.12f, 0.35f);
        felt.SetColor("_BaseColor", feltGreen);

        Material cushion = LitMaterial(MaterialPath("Cushion"));
        PbrOrGenerated(cushion, PolyHavenBuilder.Cloth, ArtFolder + "/felt.png", 0.12f, 0.35f);
        cushion.SetColor("_BaseColor", feltGreen * 0.92f);

        Material rail = LitMaterial(MaterialPath("Rail"));
        rail.SetTexture("_BaseMap", Load<Texture2D>(ArtFolder + "/felt.png"));
        rail.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.86f));
        rail.SetFloat("_Smoothness", 0.08f);

        Material wood = LitMaterial(MaterialPath("Wood"));
        PbrOrGenerated(wood, PolyHavenBuilder.Wood, ArtFolder + "/wood.png", 0.45f, 0.6f);
        wood.SetColor("_BaseColor", Color.white);

        Material liner = LitMaterial(MaterialPath("PocketLiner"));
        Texture2D linerAlbedo = Load<Texture2D>(PolyHavenBuilder.LinerAlbedoPath);
        PbrOrGenerated(liner, PolyHavenBuilder.Leather, ArtFolder + "/wood.png", 0.3f, 0.6f);
        liner.SetTexture("_BaseMap", linerAlbedo);
        liner.SetColor("_BaseColor", linerAlbedo != null ? Color.white : Hex("2A1A12"));

        // The canopy over the table: an emissive panel (VenueView sets its colour) and a dark metal frame.
        Material panel = LitMaterial(MaterialPath("LampPanel"));
        panel.SetTexture("_BaseMap", null);
        panel.SetColor("_BaseColor", Color.black);
        panel.SetFloat("_Smoothness", 0.2f);
        panel.EnableKeyword("_EMISSION");
        panel.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        panel.SetColor("_EmissionColor", new Color(1.25f, 1.18f, 1.05f));
        SimpleLit("LampFrame", Hex("1A1A1C"), 0.55f, 0.7f);

        // Chalk puff specks (v3 V7): URP particles, unlit, soft round sprite.
        Material chalk = ShaderMaterial(MaterialPath("ChalkParticle"), "Universal Render Pipeline/Particles/Unlit");
        chalk.SetTexture("_BaseMap", Load<Texture2D>(ArtFolder + "/ui_glow.png"));
        chalk.SetColor("_BaseColor", Color.white);
        chalk.SetFloat("_Surface", 1f);
        chalk.SetFloat("_Blend", 0f);
        chalk.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        chalk.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        chalk.SetFloat("_ZWrite", 0f);
        chalk.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        chalk.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // Contact shadows: the guide shader (texture x colour x vertex alpha), never seen through balls.
        Material blob = GuideMaterial(MaterialPath("BallShadow"));
        blob.SetTexture("_MainTex", Load<Texture2D>(ArtFolder + "/guide_blob_shadow.png"));
        blob.SetColor("_Color", Color.white);
        blob.SetFloat("_OccludedAlpha", 0f);

        Material pocket = LitMaterial(MaterialPath("PocketBlack"));
        pocket.SetColor("_BaseColor", Hex("070707"));
        pocket.SetFloat("_Smoothness", 0.05f);

        // Oval mother-of-pearl sights: cut out by the texture's alpha, a faint glow.
        Material diamond = LitMaterial(MaterialPath("Diamond"));
        diamond.SetTexture("_BaseMap", Load<Texture2D>(ArtFolder + "/inlay_oval.png"));
        diamond.SetColor("_BaseColor", Color.white);
        diamond.SetFloat("_Smoothness", 0.85f);
        diamond.SetFloat("_AlphaClip", 1f);
        diamond.SetFloat("_Cutoff", 0.5f);
        diamond.EnableKeyword("_ALPHATEST_ON");
        diamond.EnableKeyword("_EMISSION");
        diamond.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        diamond.SetColor("_EmissionColor", Hex("EDE6D6") * 0.25f);

        for (int i = 0; i < DataAssetBuilder.CueSpecs.Length; i++)
        {
            DataAssetBuilder.CueSpec spec = DataAssetBuilder.CueSpecs[i];
            Material cue = LitMaterial(CueMaterialPath(i + 1));
            cue.SetTexture("_BaseMap", Load<Texture2D>(CueAlbedoPath(spec.Id)));
            cue.SetColor("_BaseColor", Color.white);
            // Smoothness from the albedo's alpha (wood / ferrule / tip, v3 V3).
            cue.SetFloat("_Smoothness", 1f);
            cue.SetFloat("_SmoothnessTextureChannel", 1f);
            cue.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (spec.Glow > 0f)
            {
                cue.EnableKeyword("_EMISSION");
                cue.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                cue.SetColor("_EmissionColor", spec.Band * spec.Glow * 0.6f);
            }
            else
            {
                cue.DisableKeyword("_EMISSION");
                cue.SetColor("_EmissionColor", Color.black);
            }
        }

        Material carpet = LitMaterial(MaterialPath("Carpet"));
        carpet.SetTexture("_BaseMap", Load<Texture2D>(ArtFolder + "/carpet.png"));
        carpet.SetColor("_BaseColor", Color.white);
        carpet.SetFloat("_Smoothness", 0.05f);

        Material wallpaper = LitMaterial(MaterialPath("Wallpaper"));
        wallpaper.SetTexture("_BaseMap", Load<Texture2D>(ArtFolder + "/wallpaper.png"));
        wallpaper.SetColor("_BaseColor", Color.white);
        wallpaper.SetFloat("_Smoothness", 0.15f);

        Material wainscot = LitMaterial(MaterialPath("Wainscot"));
        wainscot.SetTexture("_BaseMap", Load<Texture2D>(ArtFolder + "/wood.png"));
        wainscot.SetColor("_BaseColor", new Color(0.75f, 0.68f, 0.62f));
        wainscot.SetFloat("_Smoothness", 0.5f);

        SimpleLit("Ceiling", Hex("15110E"), 0.05f);

        // v3 V6: the room is lit by a soft glow of its own (emission = its texture) on top of a low ambient, so it
        // reads clearly but stays darker than the table, and the balls keep their contrast.
        RoomGlow(carpet, 0.8f);
        RoomGlow(wallpaper, 1.0f);
        RoomGlow(wainscot, 0.8f);
        SimpleLit("Brass", Hex("B8893A"), 0.75f, 0.8f);
        SimpleLit("LampShade", Hex("1D4A32"), 0.55f);
        SimpleLit("BarTop", Hex("3A2216"), 0.7f);
        SimpleLit("Leather", Hex("4A1E14"), 0.35f);
        SimpleLit("PictureA", Hex("6E5B3A"), 0.3f);
        SimpleLit("PictureB", Hex("2F4A5E"), 0.3f);
        SimpleLit("BottleGreen", Hex("2E5E2A"), 0.85f);
        SimpleLit("BottleAmber", Hex("8A4F14"), 0.85f);
        Material glow = SimpleLit("LampGlow", Hex("2A1E10"), 0.2f);
        glow.EnableKeyword("_EMISSION");
        glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        glow.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.55f) * 1.2f);

        Material guideLine = GuideMaterial(MaterialPath("GuideLine"));
        guideLine.SetTexture("_MainTex", Load<Texture2D>(ArtFolder + "/guide_dots.png"));
        GuideMaterial(MaterialPath("GuideSolid"));
        Material guideDash = GuideMaterial(MaterialPath("GuideDash"));
        guideDash.SetTexture("_MainTex", Load<Texture2D>(ArtFolder + "/guide_dash.png"));
        // v3 V1: soft bands (1 mm contour at 35%, chevrons at 45%), seen through balls at 25%.
        Material band = ShaderMaterial(MaterialPath("GuideBand"), "Billiards/GuideBand");
        band.SetTexture("_MainTex", Load<Texture2D>(ArtFolder + "/guide_chevron.png"));
        band.SetTexture("_BandTex", Load<Texture2D>(ArtFolder + "/guide_band.png"));
        band.SetColor("_ContourColor", new Color(0f, 0f, 0f, 0.35f));
        band.SetFloat("_ChevronAlpha", 0.45f);
        band.SetFloat("_OccludedAlpha", 0.25f);
        // The thin cue ball line: same shader, its own profile, no chevrons, seen through balls at 35%.
        Material thinLine = ShaderMaterial(MaterialPath("GuideThinLine"), "Billiards/GuideBand");
        thinLine.SetTexture("_MainTex", null);
        thinLine.SetTexture("_BandTex", Load<Texture2D>(ArtFolder + "/guide_line_profile.png"));
        thinLine.SetColor("_ContourColor", new Color(0.05f, 0.04f, 0.03f, 0.6f));
        thinLine.SetFloat("_ChevronAlpha", 0f);
        thinLine.SetFloat("_OccludedAlpha", 0.35f);
        Material tip = GuideMaterial(MaterialPath("GuideTip"));
        tip.SetTexture("_MainTex", Load<Texture2D>(ArtFolder + "/guide_tip.png"));
        tip.SetFloat("_OccludedAlpha", 0.25f);
        ShaderMaterial(MaterialPath("GhostBall"), "Billiards/GhostBall");

        AssetDatabase.SaveAssets();
        Debug.Log("GeneratedArtBuilder: materials done");
    }

    private static void RoomGlow(Material material, float strength)
    {
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
        material.SetColor("_EmissionColor", material.GetColor("_BaseColor") * strength);
    }

    // Albedo + normal + smoothness map from Poly Haven when imported, else the generated albedo and a flat smoothness.
    private static void PbrOrGenerated(Material material, string polyHavenId, string generatedAlbedo, float smoothness, float normalScale)
    {
        Texture2D albedo = PolyHavenBuilder.LoadAlbedo(polyHavenId);
        Texture2D normal = PolyHavenBuilder.LoadNormal(polyHavenId);
        Texture2D mask = PolyHavenBuilder.LoadMask(polyHavenId);
        material.SetTexture("_BaseMap", albedo != null ? albedo : Load<Texture2D>(generatedAlbedo));
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", normalScale);
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
        }
        if (mask != null)
        {
            // Smoothness = mask alpha x _Smoothness (here used as the top value).
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", Mathf.Min(1f, smoothness * 1.4f));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else
        {
            material.SetTexture("_MetallicGlossMap", null);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
        }
    }

    // Reuses the existing asset so references (and GUIDs) survive a rebuild.
    private static Material LitMaterial(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(LitShader));
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader.name != LitShader)
        {
            material.shader = Shader.Find(LitShader);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material SimpleLit(string name, Color color, float smoothness, float metallic = 0f)
    {
        Material material = LitMaterial(MaterialPath(name));
        material.SetTexture("_BaseMap", null);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        return material;
    }

    private static Material ShaderMaterial(string path, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GuideMaterial(string path)
    {
        Shader shader = Shader.Find("Billiards/Guide");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            Debug.LogError($"GeneratedArtBuilder: missing {path}");
        return asset;
    }
    #endregion

    #region Drawing helpers
    private static Texture2D Draw(int size, Func<Vector2, Color> shader)
    {
        var texture = NewTexture(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, shader(new Vector2(x + 0.5f, y + 0.5f)));
        texture.Apply();
        return texture;
    }

    // 1px anti-aliased edge from a signed distance (negative inside).
    private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

    private static Color Over(Color under, Color over, float coverage)
    {
        float alpha = coverage * over.a + under.a * (1f - coverage * over.a);
        if (alpha <= 0f)
            return Color.clear;
        Color color = (over * coverage * over.a + under * under.a * (1f - coverage * over.a)) / alpha;
        color.a = alpha;
        return color;
    }

    private static float RoundedBoxDistance(Vector2 p, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize;
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    private static float CapsuleDistance(Vector2 p, Vector2 a, Vector2 b, float radius)
    {
        Vector2 pa = p - a;
        Vector2 ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - radius;
    }

    private static float PolygonDistance(Vector2 p, Vector2[] polygon)
    {
        float distance = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            Vector2 e = b - a;
            Vector2 w = p - a;
            Vector2 closest = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            distance = Mathf.Min(distance, closest.magnitude);
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside ? -distance : distance;
    }

    private static float StarDistance(Vector2 p, float outer, float inner)
    {
        var points = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float r = i % 2 == 0 ? outer : inner;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
        }
        return PolygonDistance(p, points);
    }

    private static Texture2D NewTexture(int width, int height) =>
        new(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };

    private static void WritePng(string path, Texture2D texture)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
    #endregion
}
