// <copyright file="ConversionJob_PdfSplit.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using FileConverter.Services;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    public class ConversionJob_PdfSplit : ConversionJob
    {
        public ConversionJob_PdfSplit(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected override bool ValidateInputExtension => false;

        protected override int GetOutputFilesCount()
        {
            try
            {
                List<List<int>> groups = this.BuildPageGroups();
                return Math.Max(1, groups.Count);
            }
            catch
            {
                return 1;
            }
        }

        protected override void Convert()
        {
            WindowsFontResolver.EnsureInitialized();
            this.UserState = Properties.Resources.ConversionStateConversion;

            if (!File.Exists(this.InputFilePath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                return;
            }

            List<List<int>> groups = this.BuildPageGroups();
            if (groups.Count == 0)
            {
                this.ConversionFailed("No pages matched the specified split criteria.");
                return;
            }

            using (PdfDocument inputDoc = PdfReader.Open(this.InputFilePath, PdfDocumentOpenMode.Import))
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    this.CurrentOutputFilePathIndex = Math.Min(i, this.OutputFilePaths.Length - 1);
                    string targetPath = i < this.OutputFilePaths.Length
                        ? this.OutputFilePaths[i]
                        : GenerateIndexedFallbackPath(this.OutputFilePaths[0], i + 1);

                    using (PdfDocument outDoc = new PdfDocument())
                    {
                        foreach (int pageIdx in groups[i])
                        {
                            if (pageIdx >= 0 && pageIdx < inputDoc.PageCount)
                            {
                                outDoc.AddPage(inputDoc.Pages[pageIdx]);
                            }
                        }

                        if (outDoc.PageCount > 0)
                        {
                            outDoc.Save(targetPath);
                        }
                    }

                    this.Progress = (float)(i + 1) / groups.Count;
                }
            }
        }

        private List<List<int>> BuildPageGroups()
        {
            int pageCount;
            using (PdfDocument doc = PdfReader.Open(this.InputFilePath, PdfDocumentOpenMode.Import))
            {
                pageCount = doc.PageCount;
            }

            string mode = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfSplitMode) ?? "SplitAllPages";
            string rangeSpec = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange) ?? string.Empty;

            if (string.Equals(mode, "ExtractRange", StringComparison.OrdinalIgnoreCase))
            {
                List<int> indices = PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, pageCount);
                return indices.Count > 0 ? new List<List<int>> { indices } : new List<List<int>>();
            }

            if (string.Equals(mode, "SplitByRanges", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(rangeSpec))
            {
                return PageRangeParser.ParseRangeGroups(rangeSpec, pageCount);
            }

            if (string.Equals(mode, "SplitEveryN", StringComparison.OrdinalIgnoreCase))
            {
                int n = 1;
                string rawN = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfSplitEveryNPages);
                if (int.TryParse(rawN, out int parsedN) && parsedN > 0)
                {
                    n = parsedN;
                }

                List<int> selected = string.IsNullOrWhiteSpace(rangeSpec)
                    ? Enumerable.Range(0, pageCount).ToList()
                    : PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, pageCount);

                var groups = new List<List<int>>();
                for (int i = 0; i < selected.Count; i += n)
                {
                    groups.Add(selected.Skip(i).Take(n).ToList());
                }

                return groups;
            }

            // Default: SplitAllPages / SplitEachPage
            List<int> pages = string.IsNullOrWhiteSpace(rangeSpec)
                ? Enumerable.Range(0, pageCount).ToList()
                : PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, pageCount);

            return pages.Select(p => new List<int> { p }).ToList();
        }

        private static string GenerateIndexedFallbackPath(string baseOutputPath, int oneBasedIndex)
        {
            string dir = Path.GetDirectoryName(baseOutputPath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(baseOutputPath);
            string ext = Path.GetExtension(baseOutputPath);
            return PathHelpers.GenerateUniquePath(Path.Combine(dir, $"{name}_part_{oneBasedIndex:D3}{ext}"));
        }
    }
}
