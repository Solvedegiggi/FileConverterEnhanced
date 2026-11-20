// <copyright file="TessdataService.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Manages bundled and on-demand Tesseract traineddata language files.
    /// </summary>
    public static class TessdataService
    {
        private const string DownloadBaseUrl = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/";

        private static readonly (string Code, string DisplayName)[] Catalog =
        {
            ("eng", "English (eng)"),
            ("ita", "Italiano (ita)"),
            ("fra", "Français (fra)"),
            ("deu", "Deutsch (deu)"),
            ("spa", "Español (spa)"),
            ("por", "Português (por)"),
            ("nld", "Nederlands (nld)"),
            ("pol", "Polski (pol)"),
            ("rus", "Русский (rus)"),
            ("jpn", "日本語 (jpn)"),
            ("chi_sim", "中文 Simplified (chi_sim)"),
            ("ara", "العربية (ara)"),
        };

        public static string BundledTessdataDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

        public static string UserTessdataDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileConverter",
                "tessdata");

        public static IReadOnlyList<(string Code, string DisplayName)> GetAvailableCatalogLanguages()
        {
            return Catalog;
        }

        public static IReadOnlyList<string> GetInstalledLanguages()
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string dir in new[] { BundledTessdataDirectory, UserTessdataDirectory })
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (string file in Directory.GetFiles(dir, "*.traineddata"))
                {
                    if (new FileInfo(file).Length > 0)
                    {
                        found.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
            }

            return found.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static bool IsLanguageInstalled(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode))
            {
                return false;
            }

            string fileName = langCode.Trim() + ".traineddata";
            string bundled = Path.Combine(BundledTessdataDirectory, fileName);
            string user = Path.Combine(UserTessdataDirectory, fileName);

            return (File.Exists(bundled) && new FileInfo(bundled).Length > 0) ||
                   (File.Exists(user) && new FileInfo(user).Length > 0);
        }

        /// <summary>
        /// Ensures every language in a '+'-delimited specification exists inside a single unified tessdata directory.
        /// Copies bundled files into the user cache directory when user-downloaded languages are mixed in.
        /// </summary>
        public static async Task<string> PrepareTessdataDirectoryAsync(
            string languageSpec,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            string[] langs = ParseLanguageCodes(languageSpec);
            if (langs.Length == 0)
            {
                langs = new[] { "eng" };
            }

            bool allInBundled = Directory.Exists(BundledTessdataDirectory) &&
                                langs.All(l =>
                                {
                                    string p = Path.Combine(BundledTessdataDirectory, l + ".traineddata");
                                    return File.Exists(p) && new FileInfo(p).Length > 0;
                                });

            if (allInBundled)
            {
                return BundledTessdataDirectory;
            }

            Directory.CreateDirectory(UserTessdataDirectory);

            for (int i = 0; i < langs.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string code = langs[i];
                string userFile = Path.Combine(UserTessdataDirectory, code + ".traineddata");
                if (File.Exists(userFile) && new FileInfo(userFile).Length > 0)
                {
                    continue;
                }

                string bundledFile = Path.Combine(BundledTessdataDirectory, code + ".traineddata");
                if (File.Exists(bundledFile) && new FileInfo(bundledFile).Length > 0)
                {
                    File.Copy(bundledFile, userFile, overwrite: true);
                    continue;
                }

                await EnsureLanguageAsync(code, progress, cancellationToken).ConfigureAwait(false);
            }

            return UserTessdataDirectory;
        }

        public static async Task EnsureLanguageAsync(
            string langCode,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(langCode))
            {
                throw new ArgumentException("Language code cannot be empty.", nameof(langCode));
            }

            string code = langCode.Trim();
            string targetFile = Path.Combine(UserTessdataDirectory, code + ".traineddata");
            if (File.Exists(targetFile) && new FileInfo(targetFile).Length > 0)
            {
                progress?.Report(1.0);
                return;
            }

            string bundledFile = Path.Combine(BundledTessdataDirectory, code + ".traineddata");
            if (File.Exists(bundledFile) && new FileInfo(bundledFile).Length > 0)
            {
                Directory.CreateDirectory(UserTessdataDirectory);
                File.Copy(bundledFile, targetFile, overwrite: true);
                progress?.Report(1.0);
                return;
            }

            Directory.CreateDirectory(UserTessdataDirectory);
            string url = DownloadBaseUrl + Uri.EscapeDataString(code) + ".traineddata";
            string tempFile = targetFile + ".tmp";

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        byte[] buffer = new byte[81920];
                        long readTotal = 0;
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                            readTotal += read;
                            if (totalBytes > 0)
                            {
                                progress?.Report((double)readTotal / totalBytes);
                            }
                        }
                    }
                }
            }

            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }

            File.Move(tempFile, targetFile);
            progress?.Report(1.0);
        }

        public static string[] ParseLanguageCodes(string languageSpec)
        {
            if (string.IsNullOrWhiteSpace(languageSpec))
            {
                return Array.Empty<string>();
            }

            return languageSpec
                .Split(new[] { '+', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
