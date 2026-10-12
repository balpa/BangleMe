using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BangleMe.Editor
{
    // Generates the surface detail of patterned bands (diamond cuts, weave, embossing) as normal
    // and occlusion maps. U wraps around the wrist; V runs across the band's outer face.
    public static class PatternTextures
    {
        const int Size = 512;

        public static (Texture2D normal, Texture2D occlusion) GetOrCreate(BandPattern pattern, string directory)
        {
            var normalPath = $"{directory}/{pattern} Normal.png";
            var occlusionPath = $"{directory}/{pattern} Occlusion.png";
            if (!File.Exists(normalPath) || !File.Exists(occlusionPath))
            {
                var (height, strength) = HeightField(pattern);
                File.WriteAllBytes(normalPath, EncodeNormals(height, strength));
                File.WriteAllBytes(occlusionPath, EncodeOcclusion(height));
                AssetDatabase.ImportAsset(normalPath);
                AssetDatabase.ImportAsset(occlusionPath);
                Configure(normalPath, TextureImporterType.NormalMap);
                Configure(occlusionPath, TextureImporterType.Default);
            }

            return (AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath), AssetDatabase.LoadAssetAtPath<Texture2D>(occlusionPath));
        }

        static void Configure(string path, TextureImporterType type)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.sRGBTexture = false;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        static (float[,] height, float strength) HeightField(BandPattern pattern)
        {
            Func<float, float, float> function;
            float strength;
            switch (pattern)
            {
                case BandPattern.Facets:
                    function = Facets;
                    strength = 9f;
                    break;
                case BandPattern.Weave:
                    function = Weave;
                    strength = 7f;
                    break;
                default:
                    function = Emboss;
                    strength = 7f;
                    break;
            }

            var height = new float[Size, Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    height[x, y] = function((x + 0.5f) / Size, (y + 0.5f) / Size);
                }
            }
            return (height, strength);
        }

        // Diamond-cut: two crossing families of V grooves leave rows of small pyramids.
        static float Facets(float u, float v)
        {
            var a = Peak(Frac(2f * u + 2f * v));
            var b = Peak(Frac(2f * u - 2f * v));
            return Mathf.Min(a, b) * Window(v, 0.1f);
        }

        // Trabzon-style braid: columns of slanted round strands, alternating direction, between
        // two plain rim wires.
        static float Weave(float u, float v)
        {
            const float rim = 0.08f;
            if (v < rim || v > 1f - rim)
            {
                var t = (Mathf.Min(v, 1f - v) / rim) * 2f - 1f;
                return Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
            }

            const int columns = 4;
            var x = (v - rim) / (1f - 2f * rim) * columns;
            var column = Mathf.FloorToInt(x);
            var across = x - column;
            var slant = column % 2 == 0 ? 1f : -1f;
            var strand = Round(Frac(3f * u + slant * across * 0.9f));
            return strand * Mathf.Pow(Mathf.Sin(Mathf.PI * across), 0.6f);
        }

        // Pressed lattice of soft diamonds on the dome of an ajda bangle.
        static float Emboss(float u, float v)
        {
            var a = Peak(Frac(u + 1.5f * v));
            var b = Peak(Frac(u - 1.5f * v));
            var diamond = Mathf.Pow(Mathf.SmoothStep(0f, 1f, a) * Mathf.SmoothStep(0f, 1f, b), 0.5f);
            // The beaded rims at both edges stay plain.
            return diamond * Window(v, 0.2f);
        }

        static float Frac(float value) => value - Mathf.Floor(value);
        static float Peak(float value) => 1f - Mathf.Abs(2f * value - 1f);
        static float Round(float value) => Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * value - 1f) * (2f * value - 1f)));

        // 1 in the middle of the band, fading to 0 within margin of either edge.
        static float Window(float v, float margin)
        {
            return Mathf.Clamp01(Mathf.Min(v, 1f - v) / margin);
        }

        static byte[] EncodeNormals(float[,] height, float strength)
        {
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var left = height[(x + Size - 1) % Size, y];
                    var right = height[(x + 1) % Size, y];
                    var down = height[x, Mathf.Max(y - 1, 0)];
                    var up = height[x, Mathf.Min(y + 1, Size - 1)];
                    var normal = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                    pixels[y * Size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                }
            }
            return Encode(pixels);
        }

        // Grooves catch less of the surroundings than raised parts do.
        static byte[] EncodeOcclusion(float[,] height)
        {
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var value = Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(height[x, y] / 0.6f)));
                    pixels[y * Size + x] = new Color(value, value, value, 1f);
                }
            }
            return Encode(pixels);
        }

        static byte[] Encode(Color[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            texture.SetPixels(pixels);
            texture.Apply();
            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return bytes;
        }
    }
}
