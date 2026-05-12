using UnityEngine;

namespace MagicExamHall
{
    public static class PixelArtFactory
    {
        private const int Size = 32;
        private const float PixelsPerUnit = 16f;

        public static Sprite CreateSprite(string name, Color primary, Color secondary, PixelSpriteKind kind)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = $"{name} Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Clear(texture);
            switch (kind)
            {
                case PixelSpriteKind.Player:
                    DrawPlayer(texture, primary, secondary);
                    break;
                case PixelSpriteKind.Station:
                    DrawStation(texture, primary, secondary);
                    break;
                case PixelSpriteKind.Target:
                    DrawTarget(texture, primary, secondary);
                    break;
                case PixelSpriteKind.Pulse:
                    DrawPulse(texture, primary);
                    break;
                case PixelSpriteKind.FloorTile:
                    DrawFloorTile(texture, primary, secondary);
                    break;
                case PixelSpriteKind.WallTrim:
                    DrawWallTrim(texture, primary, secondary);
                    break;
                case PixelSpriteKind.Rug:
                    DrawRug(texture, primary, secondary);
                    break;
                case PixelSpriteKind.Bookshelf:
                    DrawBookshelf(texture, primary, secondary);
                    break;
                case PixelSpriteKind.Candle:
                    DrawCandle(texture, primary, secondary);
                    break;
                case PixelSpriteKind.RuneCircle:
                    DrawRuneCircle(texture, primary, secondary);
                    break;
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static void DrawPlayer(Texture2D texture, Color skin, Color robe)
        {
            var outline = new Color(0.035f, 0.032f, 0.045f, 1f);
            var robeDark = Shade(robe, 0.48f);
            var robeMid = Shade(robe, 0.78f);
            var robeLight = Mix(robe, Color.white, 0.28f);
            var hair = new Color(0.22f, 0.13f, 0.08f, 1f);
            var gold = new Color(1f, 0.78f, 0.26f, 1f);

            Ellipse(texture, 16, 5, 9, 3, new Color(0f, 0f, 0f, 0.35f));
            Fill(texture, 11, 6, 10, 3, outline);
            Fill(texture, 12, 7, 8, 2, robeDark);
            Fill(texture, 9, 9, 14, 13, outline);
            Fill(texture, 10, 10, 12, 12, robeMid);
            Fill(texture, 12, 11, 8, 10, robe);
            Fill(texture, 15, 10, 2, 11, robeLight);
            Fill(texture, 8, 12, 3, 8, outline);
            Fill(texture, 21, 12, 3, 8, outline);
            Fill(texture, 9, 13, 2, 6, robeDark);
            Fill(texture, 21, 13, 2, 6, robeDark);
            Set(texture, 14, 16, gold);
            Set(texture, 17, 16, gold);
            Fill(texture, 12, 21, 8, 5, outline);
            Fill(texture, 13, 22, 6, 4, skin);
            Fill(texture, 12, 25, 8, 3, hair);
            Fill(texture, 10, 27, 12, 2, outline);
            Fill(texture, 11, 28, 10, 1, robeDark);
            Fill(texture, 13, 29, 6, 2, robe);
            Set(texture, 13, 23, outline);
            Set(texture, 18, 23, outline);
            Set(texture, 16, 21, Shade(skin, 0.75f));
            Fill(texture, 11, 5, 4, 2, outline);
            Fill(texture, 17, 5, 4, 2, outline);
            Fill(texture, 12, 6, 3, 1, robeDark);
            Fill(texture, 17, 6, 3, 1, robeDark);
        }

        private static void DrawStation(Texture2D texture, Color element, Color accent)
        {
            var outline = new Color(0.035f, 0.032f, 0.045f, 1f);
            var stone = new Color(0.46f, 0.48f, 0.54f, 1f);
            var stoneDark = new Color(0.24f, 0.26f, 0.31f, 1f);
            var stoneLight = new Color(0.66f, 0.68f, 0.74f, 1f);
            var glow = Mix(element, Color.white, 0.22f);

            Ellipse(texture, 16, 5, 12, 4, new Color(0f, 0f, 0f, 0.36f));
            Diamond(texture, 16, 9, 13, 5, outline);
            Diamond(texture, 16, 10, 11, 4, stoneDark);
            Diamond(texture, 16, 12, 10, 4, stone);
            Fill(texture, 8, 12, 16, 8, outline);
            Fill(texture, 9, 13, 14, 7, stoneDark);
            Fill(texture, 10, 16, 12, 3, stone);
            Fill(texture, 11, 18, 10, 2, stoneLight);
            Fill(texture, 11, 19, 10, 2, outline);
            Fill(texture, 12, 20, 8, 2, stone);
            Diamond(texture, 16, 24, 6, 6, outline);
            Diamond(texture, 16, 24, 4, 5, element);
            Diamond(texture, 16, 25, 2, 3, glow);
            Set(texture, 15, 27, Color.white);
            Set(texture, 20, 13, glow);
            Set(texture, 11, 13, glow);
            Line(texture, 10, 14, 22, 14, Shade(element, 0.75f));
            Set(texture, 7, 17, accent);
            Set(texture, 24, 17, accent);
        }

        private static void DrawTarget(Texture2D texture, Color primary, Color secondary)
        {
            var outline = new Color(0.035f, 0.032f, 0.045f, 1f);
            var stone = new Color(0.42f, 0.43f, 0.48f, 1f);
            var stoneLight = new Color(0.64f, 0.66f, 0.72f, 1f);
            var core = Mix(primary, secondary, 0.35f);

            Ellipse(texture, 16, 5, 8, 3, new Color(0f, 0f, 0f, 0.34f));
            Fill(texture, 10, 6, 12, 3, outline);
            Fill(texture, 11, 7, 10, 2, stone);
            Fill(texture, 12, 9, 8, 8, outline);
            Fill(texture, 13, 10, 6, 7, stoneLight);
            Fill(texture, 12, 17, 8, 2, outline);
            Fill(texture, 13, 18, 6, 2, stone);
            Diamond(texture, 16, 23, 7, 7, outline);
            Diamond(texture, 16, 23, 5, 5, core);
            Diamond(texture, 16, 24, 2, 3, Mix(core, Color.white, 0.35f));
            Set(texture, 18, 26, Color.white);
            Set(texture, 11, 14, Shade(core, 0.8f));
            Set(texture, 21, 14, Shade(core, 0.8f));
        }

        private static void DrawPulse(Texture2D texture, Color primary)
        {
            var glow = Mix(primary, Color.white, 0.35f);
            Ring(texture, 16, 16, 11, 10, new Color(primary.r, primary.g, primary.b, 0.75f));
            Ring(texture, 16, 16, 7, 6, new Color(glow.r, glow.g, glow.b, 0.65f));
            Line(texture, 16, 3, 16, 29, new Color(glow.r, glow.g, glow.b, 0.42f));
            Line(texture, 3, 16, 29, 16, new Color(glow.r, glow.g, glow.b, 0.42f));
            Set(texture, 16, 16, Color.white);
        }

        private static void DrawFloorTile(Texture2D texture, Color primary, Color secondary)
        {
            Fill(texture, 0, 0, Size, Size, primary);
            Fill(texture, 0, 0, Size, 1, Shade(primary, 0.55f));
            Fill(texture, 0, 0, 1, Size, Shade(primary, 0.55f));
            Fill(texture, 0, 31, Size, 1, Mix(primary, Color.white, 0.08f));
            Fill(texture, 31, 0, 1, Size, Mix(primary, Color.white, 0.08f));
            Line(texture, 2, 15, 30, 15, secondary);
            Line(texture, 15, 2, 15, 30, secondary);
            Set(texture, 6, 8, Mix(primary, Color.white, 0.1f));
            Set(texture, 22, 5, Shade(primary, 0.68f));
            Set(texture, 25, 22, Mix(primary, Color.white, 0.08f));
            Set(texture, 8, 25, Shade(primary, 0.7f));
            Line(texture, 20, 18, 24, 20, Shade(primary, 0.55f));
            Line(texture, 7, 12, 10, 10, Mix(primary, Color.white, 0.08f));
        }

        private static void DrawWallTrim(Texture2D texture, Color primary, Color secondary)
        {
            Fill(texture, 0, 0, Size, Size, Shade(primary, 0.68f));
            Fill(texture, 0, 20, Size, 12, primary);
            Fill(texture, 0, 17, Size, 3, secondary);
            Fill(texture, 0, 15, Size, 2, Shade(primary, 0.42f));
            for (var x = 0; x < Size; x += 8)
            {
                Line(texture, x, 20, x + 3, 31, Shade(primary, 0.5f));
                Fill(texture, x + 1, 22, 2, 2, Mix(primary, Color.white, 0.12f));
            }
        }

        private static void DrawRug(Texture2D texture, Color primary, Color secondary)
        {
            Fill(texture, 0, 0, Size, Size, Shade(primary, 0.55f));
            Fill(texture, 3, 0, 26, Size, primary);
            Fill(texture, 5, 0, 2, Size, secondary);
            Fill(texture, 25, 0, 2, Size, secondary);
            Fill(texture, 9, 0, 14, Size, Shade(primary, 0.82f));
            for (var y = 3; y < Size; y += 8)
            {
                Diamond(texture, 16, y + 2, 5, 3, secondary);
                Set(texture, 16, y + 2, Color.white);
            }
            Fill(texture, 0, 0, 3, Size, new Color(0f, 0f, 0f, 0.25f));
            Fill(texture, 29, 0, 3, Size, new Color(0f, 0f, 0f, 0.25f));
        }

        private static void DrawBookshelf(Texture2D texture, Color wood, Color accent)
        {
            var outline = new Color(0.04f, 0.025f, 0.018f, 1f);
            Fill(texture, 5, 4, 22, 24, outline);
            Fill(texture, 6, 5, 20, 22, wood);
            Fill(texture, 7, 8, 18, 2, Shade(wood, 0.55f));
            Fill(texture, 7, 16, 18, 2, Shade(wood, 0.55f));
            Fill(texture, 7, 24, 18, 2, Shade(wood, 0.55f));
            var colors = new[]
            {
                accent,
                new Color(0.85f, 0.18f, 0.22f, 1f),
                new Color(0.18f, 0.48f, 0.9f, 1f),
                new Color(0.92f, 0.72f, 0.22f, 1f),
                new Color(0.45f, 0.78f, 0.38f, 1f)
            };
            for (var shelf = 0; shelf < 3; shelf++)
            {
                for (var i = 0; i < 7; i++)
                {
                    var color = colors[(shelf + i) % colors.Length];
                    Fill(texture, 8 + i * 2, 10 + shelf * 8, 1, 5, color);
                    Set(texture, 8 + i * 2, 14 + shelf * 8, Shade(color, 0.6f));
                }
            }
        }

        private static void DrawCandle(Texture2D texture, Color metal, Color flame)
        {
            var outline = new Color(0.04f, 0.035f, 0.03f, 1f);
            Ellipse(texture, 16, 4, 7, 2, new Color(0f, 0f, 0f, 0.3f));
            Fill(texture, 14, 5, 4, 14, outline);
            Fill(texture, 15, 6, 2, 12, metal);
            Fill(texture, 10, 12, 12, 2, outline);
            Fill(texture, 11, 13, 10, 1, Shade(metal, 1.15f));
            Fill(texture, 9, 9, 3, 6, outline);
            Fill(texture, 20, 9, 3, 6, outline);
            Fill(texture, 10, 10, 1, 4, metal);
            Fill(texture, 21, 10, 1, 4, metal);
            Diamond(texture, 16, 22, 4, 6, Shade(flame, 0.85f));
            Diamond(texture, 16, 23, 2, 4, Mix(flame, Color.white, 0.45f));
            Set(texture, 16, 25, Color.white);
            Set(texture, 15, 21, new Color(1f, 0.32f, 0.12f, 1f));
        }

        private static void DrawRuneCircle(Texture2D texture, Color primary, Color secondary)
        {
            Ring(texture, 16, 16, 13, 12, new Color(primary.r, primary.g, primary.b, 0.62f));
            Ring(texture, 16, 16, 9, 8, new Color(primary.r, primary.g, primary.b, 0.35f));
            Line(texture, 16, 4, 16, 28, new Color(secondary.r, secondary.g, secondary.b, 0.35f));
            Line(texture, 4, 16, 28, 16, new Color(secondary.r, secondary.g, secondary.b, 0.35f));
            Line(texture, 8, 8, 24, 24, new Color(primary.r, primary.g, primary.b, 0.25f));
            Line(texture, 8, 24, 24, 8, new Color(primary.r, primary.g, primary.b, 0.25f));
            Set(texture, 16, 29, secondary);
            Set(texture, 16, 3, secondary);
            Set(texture, 29, 16, secondary);
            Set(texture, 3, 16, secondary);
        }

        private static void Clear(Texture2D texture)
        {
            var clear = new Color(0f, 0f, 0f, 0f);
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }
        }

        private static void Fill(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (var yy = y; yy < y + height; yy++)
            {
                for (var xx = x; xx < x + width; xx++)
                {
                    Set(texture, xx, yy, color);
                }
            }
        }

        private static void Diamond(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
        {
            for (var y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                for (var x = centerX - radiusX; x <= centerX + radiusX; x++)
                {
                    var dx = Mathf.Abs(x - centerX) / (float)Mathf.Max(radiusX, 1);
                    var dy = Mathf.Abs(y - centerY) / (float)Mathf.Max(radiusY, 1);
                    if (dx + dy <= 1f)
                    {
                        Set(texture, x, y, color);
                    }
                }
            }
        }

        private static void Ellipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
        {
            for (var y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                for (var x = centerX - radiusX; x <= centerX + radiusX; x++)
                {
                    var dx = (x - centerX) / (float)Mathf.Max(radiusX, 1);
                    var dy = (y - centerY) / (float)Mathf.Max(radiusY, 1);
                    if (dx * dx + dy * dy <= 1f)
                    {
                        Set(texture, x, y, color);
                    }
                }
            }
        }

        private static void Ring(Texture2D texture, int centerX, int centerY, int outerRadius, int innerRadius, Color color)
        {
            var outer = outerRadius * outerRadius;
            var inner = innerRadius * innerRadius;
            for (var y = centerY - outerRadius; y <= centerY + outerRadius; y++)
            {
                for (var x = centerX - outerRadius; x <= centerX + outerRadius; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    var d = dx * dx + dy * dy;
                    if (d <= outer && d >= inner)
                    {
                        Set(texture, x, y, color);
                    }
                }
            }
        }

        private static void Line(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            var dx = Mathf.Abs(x1 - x0);
            var dy = -Mathf.Abs(y1 - y0);
            var sx = x0 < x1 ? 1 : -1;
            var sy = y0 < y1 ? 1 : -1;
            var error = dx + dy;
            while (true)
            {
                Set(texture, x0, y0, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                var e2 = 2 * error;
                if (e2 >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void Set(Texture2D texture, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            {
                return;
            }

            texture.SetPixel(x, y, color);
        }

        private static Color Shade(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r * amount),
                Mathf.Clamp01(color.g * amount),
                Mathf.Clamp01(color.b * amount),
                color.a);
        }

        private static Color Mix(Color a, Color b, float t)
        {
            return new Color(
                Mathf.Lerp(a.r, b.r, t),
                Mathf.Lerp(a.g, b.g, t),
                Mathf.Lerp(a.b, b.b, t),
                Mathf.Lerp(a.a, b.a, t));
        }
    }

    public enum PixelSpriteKind
    {
        Player,
        Station,
        Target,
        Pulse,
        FloorTile,
        WallTrim,
        Rug,
        Bookshelf,
        Candle,
        RuneCircle
    }
}
