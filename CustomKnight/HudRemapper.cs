using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace CustomKnight
{
    internal class TextureRemapper
    {
        internal class DiffSpriteRect
        {
            public float x { get; set; }
            public float y { get; set; }
            public float w { get; set; }
            public float h { get; set; }
        }

        internal class DiffSpriteEntry
        {
            public DiffSpriteRect src { get; set; }
            public DiffSpriteRect dst { get; set; }
            public int? rotate { get; set; }
            public bool? flipH { get; set; }
            public bool? flipV { get; set; }
            public bool? rotateFirst { get; set; }
        }

        internal class DiffMapping
        {
            public Dictionary<string, DiffSpriteEntry> sprites { get; set; }
        }

        private static readonly Dictionary<string, DiffMapping> MappingCache = new Dictionary<string, DiffMapping>();
        private readonly string mappingName;

        internal TextureRemapper(string mappingName)
        {
            this.mappingName = mappingName;
        }

        internal bool TryRemap(Texture2D texture, ISelectableSkin skin, out Texture2D remapped)
        {
            remapped = texture;
            if (texture == null)
            {
                return false;
            }

            var mapping = LoadMapping(mappingName)?.sprites;
            if (mapping == null || mapping.Count == 0)
            {
                return false;
            }

            try
            {
                var width = texture.width;
                var height = texture.height;
                var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = texture.GetPixels32();
                var inputTop = FlipV(pixels, width, height);
                var outputTop = new Color32[inputTop.Length];
                Array.Copy(inputTop, outputTop, inputTop.Length);

                foreach (var entry in mapping.Values)
                {
                    if (entry.src == null)
                    {
                        continue;
                    }

                    var oldRect = ToTopLeftRect(entry.src);
                    var newRect = entry.dst == null ? oldRect : ToTopLeftRect(entry.dst);

                    if (!IsValidRect(oldRect, width, height) || !IsValidRect(newRect, width, height))
                    {
                        continue;
                    }

                    var spritePixels = GetPixels32Rect(inputTop, width, oldRect);
                    var rotate = entry.rotate ?? 0;
                    var flipH = entry.flipH ?? false;
                    var flipV = entry.flipV ?? false;
                    var rotateFirst = entry.rotateFirst ?? true;
                    var transformed = ApplyTransform(spritePixels, oldRect.width, oldRect.height, rotate, flipH, flipV, rotateFirst, out var tW, out var tH);

                    if (tW != newRect.width || tH != newRect.height)
                    {
                        transformed = ScaleNearest(transformed, tW, tH, newRect.width, newRect.height);
                        tW = newRect.width;
                        tH = newRect.height;
                    }

                    ClearRect(outputTop, width, newRect);
                    SetPixels32Rect(outputTop, width, newRect, transformed);
                }

                var outputPixels = FlipV(outputTop, width, height);
                output.SetPixels32(outputPixels);
                output.Apply();
                remapped = output;
                return true;
            }
            catch (Exception e)
            {
                CustomKnight.Instance.Log(e.ToString());
                return false;
            }
        }

        internal static DiffMapping LoadMapping(string resourceFileName)
        {
            try
            {
                if (MappingCache.TryGetValue(resourceFileName, out var cached))
                {
                    return cached;
                }

                var asm = Assembly.GetExecutingAssembly();
                var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));
                if (resourceName == null)
                {
                    var empty = new DiffMapping();
                    MappingCache[resourceFileName] = empty;
                    return empty;
                }

                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    var empty = new DiffMapping();
                    MappingCache[resourceFileName] = empty;
                    return empty;
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var mapping = JsonConvert.DeserializeObject<DiffMapping>(json);
                var result = mapping ?? new DiffMapping();
                MappingCache[resourceFileName] = result;
                return result;
            }
            catch (Exception e)
            {
                CustomKnight.Instance.Log(e.ToString());
                var empty = new DiffMapping();
                MappingCache[resourceFileName] = empty;
                return empty;
            }
        }

        private static RectInt ToTopLeftRect(DiffSpriteRect rect)
        {
            var w = Mathf.RoundToInt(rect.w);
            var h = Mathf.RoundToInt(rect.h);
            var x = Mathf.RoundToInt(rect.x);
            var y = Mathf.RoundToInt(rect.y);
            return new RectInt(x, y, w, h);
        }

        private static bool IsValidRect(RectInt rect, int width, int height)
        {
            return rect.x >= 0 && rect.y >= 0 && rect.width > 0 && rect.height > 0 &&
                   rect.x + rect.width <= width && rect.y + rect.height <= height;
        }

        private static void ClearRect(Color32[] pixels, int textureWidth, RectInt rect)
        {
            var transparent = new Color32[rect.width * rect.height];
            SetPixels32Rect(pixels, textureWidth, rect, transparent);
        }

        private static Color32[] GetPixels32Rect(Color32[] pixels, int textureWidth, RectInt rect)
        {
            var result = new Color32[rect.width * rect.height];
            for (var y = 0; y < rect.height; y++)
            {
                var srcIndex = (rect.y + y) * textureWidth + rect.x;
                var dstIndex = y * rect.width;
                Array.Copy(pixels, srcIndex, result, dstIndex, rect.width);
            }
            return result;
        }

        private static void SetPixels32Rect(Color32[] pixels, int textureWidth, RectInt rect, Color32[] src)
        {
            for (var y = 0; y < rect.height; y++)
            {
                var dstIndex = (rect.y + y) * textureWidth + rect.x;
                var srcIndex = y * rect.width;
                Array.Copy(src, srcIndex, pixels, dstIndex, rect.width);
            }
        }

        private static Color32[] ApplyTransform(Color32[] pixels, int width, int height, int rotate, bool flipH, bool flipV, bool rotateFirst, out int outWidth, out int outHeight)
        {
            var result = pixels;
            var w = width;
            var h = height;

            if (rotateFirst)
            {
                if (rotate != 0)
                {
                    result = Rotate(result, w, h, rotate, out w, out h);
                }
                if (flipH)
                {
                    result = FlipH(result, w, h);
                }
                if (flipV)
                {
                    result = FlipV(result, w, h);
                }
            }
            else
            {
                if (flipH)
                {
                    result = FlipH(result, w, h);
                }
                if (flipV)
                {
                    result = FlipV(result, w, h);
                }
                if (rotate != 0)
                {
                    result = Rotate(result, w, h, rotate, out w, out h);
                }
            }

            outWidth = w;
            outHeight = h;
            return result;
        }

        private static Color32[] FlipH(Color32[] pixels, int width, int height)
        {
            var result = new Color32[pixels.Length];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    result[y * width + x] = pixels[y * width + (width - 1 - x)];
                }
            }
            return result;
        }

        private static Color32[] FlipV(Color32[] pixels, int width, int height)
        {
            var result = new Color32[pixels.Length];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    result[y * width + x] = pixels[(height - 1 - y) * width + x];
                }
            }
            return result;
        }

        private static Color32[] Rotate(Color32[] pixels, int width, int height, int degrees, out int outWidth, out int outHeight)
        {
            if (degrees == 90)
            {
                outWidth = height;
                outHeight = width;
                var result = new Color32[pixels.Length];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var newX = height - 1 - y;
                        var newY = x;
                        result[newY * outWidth + newX] = pixels[y * width + x];
                    }
                }
                return result;
            }
            if (degrees == 180)
            {
                outWidth = width;
                outHeight = height;
                var result = new Color32[pixels.Length];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var newX = width - 1 - x;
                        var newY = height - 1 - y;
                        result[newY * outWidth + newX] = pixels[y * width + x];
                    }
                }
                return result;
            }
            if (degrees == 270)
            {
                outWidth = height;
                outHeight = width;
                var result = new Color32[pixels.Length];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var newX = y;
                        var newY = width - 1 - x;
                        result[newY * outWidth + newX] = pixels[y * width + x];
                    }
                }
                return result;
            }

            outWidth = width;
            outHeight = height;
            return pixels;
        }

    private static Color32[] ScaleNearest(Color32[] pixels, int width, int height, int targetWidth, int targetHeight)
    {
            var result = new Color32[targetWidth * targetHeight];
            for (var y = 0; y < targetHeight; y++)
            {
                var srcY = y * height / targetHeight;
                for (var x = 0; x < targetWidth; x++)
                {
                    var srcX = x * width / targetWidth;
                    result[y * targetWidth + x] = pixels[srcY * width + srcX];
                }
            }
        return result;
    }

    }
}
