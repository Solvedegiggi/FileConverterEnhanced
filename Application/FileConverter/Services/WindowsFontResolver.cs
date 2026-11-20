// <copyright file="WindowsFontResolver.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using PdfSharp.Fonts;

    /// <summary>
    /// Resolves standard Windows TrueType/OpenType fonts for PDFsharp 6.x on .NET Framework 4.8.
    /// </summary>
    public class WindowsFontResolver : IFontResolver
    {
        private static readonly object InitLock = new object();
        private static bool initialized;

        private readonly Dictionary<string, string> fontFileCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public WindowsFontResolver()
        {
            this.BuildFontCache();
        }

        public static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            lock (InitLock)
            {
                if (initialized)
                {
                    return;
                }

                try
                {
                    if (GlobalFontSettings.FontResolver == null)
                    {
                        GlobalFontSettings.FontResolver = new WindowsFontResolver();
                    }
                }
                catch (InvalidOperationException)
                {
                    // FontResolver was already registered by another component or test.
                }

                initialized = true;
            }
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            string normalized = (familyName ?? "Arial").Trim();
            string suffix = (isBold ? "B" : string.Empty) + (isItalic ? "I" : string.Empty);
            string key = normalized + "#" + suffix;

            if (this.TryResolveFontFile(normalized, isBold, isItalic, out _))
            {
                return new FontResolverInfo(key);
            }

            // Fallback to Arial or Segoe UI
            if (this.TryResolveFontFile("Arial", isBold, isItalic, out _))
            {
                return new FontResolverInfo("Arial#" + suffix);
            }

            return new FontResolverInfo("Segoe UI#" + suffix);
        }

        public byte[] GetFont(string faceName)
        {
            string family = faceName;
            bool isBold = false;
            bool isItalic = false;

            int sepIndex = faceName.IndexOf('#');
            if (sepIndex >= 0)
            {
                family = faceName.Substring(0, sepIndex);
                string style = faceName.Substring(sepIndex + 1);
                isBold = style.Contains("B");
                isItalic = style.Contains("I");
            }

            if (this.TryResolveFontFile(family, isBold, isItalic, out string filePath) && File.Exists(filePath))
            {
                return File.ReadAllBytes(filePath);
            }

            string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string[] fallbacks = new[] { "arial.ttf", "segoeui.ttf", "calibri.ttf", "tahoma.ttf" };
            foreach (string fallback in fallbacks)
            {
                string full = Path.Combine(fontsDir, fallback);
                if (File.Exists(full))
                {
                    return File.ReadAllBytes(full);
                }
            }

            throw new FileNotFoundException($"Unable to resolve any font file for '{faceName}'.");
        }

        private void BuildFontCache()
        {
            string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (!Directory.Exists(fontsDir))
            {
                return;
            }

            AddKnownMapping("arial", "arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf", fontsDir);
            AddKnownMapping("segoe ui", "segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf", fontsDir);
            AddKnownMapping("calibri", "calibri.ttf", "calibrib.ttf", "calibrii.ttf", "calibriz.ttf", fontsDir);
            AddKnownMapping("times new roman", "times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf", fontsDir);
            AddKnownMapping("courier new", "cour.ttf", "courbd.ttf", "couri.ttf", "courbi.ttf", fontsDir);
            AddKnownMapping("consolas", "consola.ttf", "consolab.ttf", "consolai.ttf", "consolaz.ttf", fontsDir);
            AddKnownMapping("tahoma", "tahoma.ttf", "tahomabd.ttf", "tahoma.ttf", "tahomabd.ttf", fontsDir);
            AddKnownMapping("verdana", "verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf", fontsDir);
            AddKnownMapping("georgia", "georgia.ttf", "georgiab.ttf", "georgiai.ttf", "georgiaz.ttf", fontsDir);
            AddKnownMapping("trebuchet ms", "trebuc.ttf", "trebucbd.ttf", "trebucit.ttf", "trebucbi.ttf", fontsDir);
            AddKnownMapping("helvetica", "arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf", fontsDir);
            AddKnownMapping("sans-serif", "segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf", fontsDir);
            AddKnownMapping("serif", "times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf", fontsDir);
            AddKnownMapping("monospace", "consola.ttf", "consolab.ttf", "consolai.ttf", "consolaz.ttf", fontsDir);
        }

        private void AddKnownMapping(string familyLower, string regular, string bold, string italic, string boldItalic, string fontsDir)
        {
            RegisterIfExists(familyLower + "#", Path.Combine(fontsDir, regular));
            RegisterIfExists(familyLower + "#B", Path.Combine(fontsDir, bold));
            RegisterIfExists(familyLower + "#I", Path.Combine(fontsDir, italic));
            RegisterIfExists(familyLower + "#BI", Path.Combine(fontsDir, boldItalic));
        }

        private void RegisterIfExists(string key, string path)
        {
            if (File.Exists(path))
            {
                this.fontFileCache[key] = path;
            }
        }

        private bool TryResolveFontFile(string familyName, bool isBold, bool isItalic, out string filePath)
        {
            string familyLower = (familyName ?? "arial").Trim().ToLowerInvariant();
            string suffix = (isBold ? "B" : string.Empty) + (isItalic ? "I" : string.Empty);

            if (this.fontFileCache.TryGetValue(familyLower + "#" + suffix, out filePath))
            {
                return true;
            }

            if (this.fontFileCache.TryGetValue(familyLower + "#", out filePath))
            {
                return true;
            }

            // Try direct file match in Windows Fonts directory
            string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (Directory.Exists(fontsDir))
            {
                string compact = familyLower.Replace(" ", string.Empty);
                string[] candidates = new[]
                {
                    Path.Combine(fontsDir, compact + ".ttf"),
                    Path.Combine(fontsDir, compact + ".otf"),
                    Path.Combine(fontsDir, familyLower + ".ttf")
                };

                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        this.fontFileCache[familyLower + "#" + suffix] = candidate;
                        filePath = candidate;
                        return true;
                    }
                }
            }

            filePath = null;
            return false;
        }
    }
}
