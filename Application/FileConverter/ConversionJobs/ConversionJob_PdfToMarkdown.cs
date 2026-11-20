// <copyright file="ConversionJob_PdfToMarkdown.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Core;
    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;

    public class ConversionJob_PdfToMarkdown : ConversionJob
    {
        private static readonly Regex NumberedListRegex = new Regex(@"^\d+[\.\)]\s+", RegexOptions.Compiled);

        public ConversionJob_PdfToMarkdown(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected override bool ValidateInputExtension => false;

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateConversion;

            if (!File.Exists(this.InputFilePath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                return;
            }

            bool extractImages = string.Equals(
                this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfMarkdownExtractImages),
                "True",
                StringComparison.OrdinalIgnoreCase);

            bool includePageBreaks = string.Equals(
                this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfMarkdownIncludePageBreaks),
                "True",
                StringComparison.OrdinalIgnoreCase);

            string outputDir = Path.GetDirectoryName(this.OutputFilePath) ?? string.Empty;
            string assetsFolderName = Path.GetFileNameWithoutExtension(this.OutputFilePath) + "_assets";
            string assetsDir = Path.Combine(outputDir, assetsFolderName);

            var sb = new StringBuilder();

            using (PdfPigDoc pdf = PdfPigDoc.Open(this.InputFilePath))
            {
                int pageCount = pdf.NumberOfPages;
                for (int pageNum = 1; pageNum <= pageCount; pageNum++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    Page page = pdf.GetPage(pageNum);
                    IReadOnlyList<Word> words = page.GetWords().ToList();
                    double bodyFontSize = ComputeMedianFontSize(words);

                    IReadOnlyList<Hyperlink> links = Array.Empty<Hyperlink>();
                    try
                    {
                        links = page.GetHyperlinks().ToList();
                    }
                    catch
                    {
                        links = Array.Empty<Hyperlink>();
                    }

                    List<MarkdownLine> lines = GroupWordsIntoLines(words, links, bodyFontSize);
                    AppendLinesAsMarkdown(sb, lines, bodyFontSize);

                    if (extractImages)
                    {
                        int imgIndex = 0;
                        foreach (IPdfImage img in page.GetImages())
                        {
                            if (img.TryGetPng(out byte[] pngBytes) && pngBytes != null && pngBytes.Length > 0)
                            {
                                imgIndex++;
                                Directory.CreateDirectory(assetsDir);
                                string imgFileName = $"page{pageNum}_img{imgIndex}.png";
                                File.WriteAllBytes(Path.Combine(assetsDir, imgFileName), pngBytes);
                                sb.AppendLine();
                                sb.AppendLine($"![Page {pageNum} Image {imgIndex}]({assetsFolderName}/{imgFileName})");
                                sb.AppendLine();
                            }
                        }
                    }

                    if (includePageBreaks && pageNum < pageCount)
                    {
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine();
                    }

                    this.Progress = (float)pageNum / pageCount;
                }
            }

            File.WriteAllText(this.OutputFilePath, sb.ToString().TrimEnd() + Environment.NewLine, new UTF8Encoding(false));
        }

        private static double ComputeMedianFontSize(IReadOnlyList<Word> words)
        {
            var sizes = new List<double>();
            foreach (Word word in words)
            {
                if (word.Letters.Count > 0)
                {
                    sizes.Add(word.Letters[0].PointSize);
                }
            }

            if (sizes.Count == 0)
            {
                return 11.0;
            }

            sizes.Sort();
            return sizes[sizes.Count / 2];
        }

        private static List<MarkdownLine> GroupWordsIntoLines(
            IReadOnlyList<Word> words,
            IReadOnlyList<Hyperlink> links,
            double bodyFontSize)
        {
            var sortedWords = words
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var lineBuckets = new List<List<Word>>();
            double yTolerance = Math.Max(3.0, bodyFontSize * 0.45);

            foreach (Word word in sortedWords)
            {
                if (string.IsNullOrWhiteSpace(word.Text))
                {
                    continue;
                }

                List<Word> matchingLine = null;
                foreach (List<Word> bucket in lineBuckets)
                {
                    if (Math.Abs(bucket[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= yTolerance)
                    {
                        matchingLine = bucket;
                        break;
                    }
                }

                if (matchingLine == null)
                {
                    matchingLine = new List<Word>();
                    lineBuckets.Add(matchingLine);
                }

                matchingLine.Add(word);
            }

            var lines = new List<MarkdownLine>();
            foreach (List<Word> bucket in lineBuckets)
            {
                bucket.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

                double avgFont = bucket
                    .Where(w => w.Letters.Count > 0)
                    .Select(w => w.Letters[0].PointSize)
                    .DefaultIfEmpty(bodyFontSize)
                    .Average();

                // Detect multi-column cells separated by wide horizontal gaps
                var cellTexts = new List<string>();
                var currentCellWords = new List<Word>();
                double wideGapThreshold = Math.Max(18.0, avgFont * 1.9);

                for (int i = 0; i < bucket.Count; i++)
                {
                    if (i > 0)
                    {
                        double gap = bucket[i].BoundingBox.Left - bucket[i - 1].BoundingBox.Right;
                        if (gap > wideGapThreshold)
                        {
                            cellTexts.Add(FormatWordRun(currentCellWords, links, formatInlineStyles: false));
                            currentCellWords.Clear();
                        }
                    }

                    currentCellWords.Add(bucket[i]);
                }

                if (currentCellWords.Count > 0)
                {
                    cellTexts.Add(FormatWordRun(currentCellWords, links, formatInlineStyles: false));
                }

                lines.Add(new MarkdownLine
                {
                    PlainText = FormatWordRun(bucket, links, formatInlineStyles: false),
                    FormattedText = FormatWordRun(bucket, links, formatInlineStyles: true),
                    AverageFontSize = avgFont,
                    Columns = cellTexts,
                });
            }

            return lines;
        }

        private static string FormatWordRun(IReadOnlyList<Word> words, IReadOnlyList<Hyperlink> links, bool formatInlineStyles)
        {
            if (words.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < words.Count; i++)
            {
                Word word = words[i];
                if (i > 0)
                {
                    sb.Append(' ');
                }

                string token = word.Text;
                string linkUri = FindLinkUri(word.BoundingBox, links);

                if (formatInlineStyles && word.Letters.Count > 0)
                {
                    string fontName = word.Letters[0].FontName ?? string.Empty;
                    bool isBold = fontName.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isItalic = fontName.IndexOf("Italic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    fontName.IndexOf("Oblique", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isBold && isItalic)
                    {
                        token = $"***{token}***";
                    }
                    else if (isBold)
                    {
                        token = $"**{token}**";
                    }
                    else if (isItalic)
                    {
                        token = $"*{token}*";
                    }
                }

                if (!string.IsNullOrEmpty(linkUri))
                {
                    token = $"[{token}]({linkUri})";
                }

                sb.Append(token);
            }

            return Regex.Replace(sb.ToString(), @"\*\*\s+\*\*", " ")
                .Replace("* *", " ")
                .Trim();
        }

        private static string FindLinkUri(PdfRectangle wordBox, IReadOnlyList<Hyperlink> links)
        {
            if (links == null || links.Count == 0)
            {
                return null;
            }

            double cx = (wordBox.Left + wordBox.Right) / 2.0;
            double cy = (wordBox.Bottom + wordBox.Top) / 2.0;

            foreach (Hyperlink link in links)
            {
                PdfRectangle rect = link.Bounds;
                if (cx >= rect.Left && cx <= rect.Right && cy >= rect.Bottom && cy <= rect.Top)
                {
                    return link.Uri;
                }
            }

            return null;
        }

        private static void AppendLinesAsMarkdown(StringBuilder sb, List<MarkdownLine> lines, double bodyFontSize)
        {
            int idx = 0;
            while (idx < lines.Count)
            {
                MarkdownLine line = lines[idx];

                // GFM Table block if 2+ consecutive multi-column lines with matching column counts
                if (line.Columns.Count >= 2 &&
                    idx + 1 < lines.Count &&
                    lines[idx + 1].Columns.Count == line.Columns.Count)
                {
                    int colCount = line.Columns.Count;
                    sb.AppendLine();
                    sb.AppendLine("| " + string.Join(" | ", line.Columns.Select(EscapeMarkdownTableCell)) + " |");
                    sb.AppendLine("| " + string.Join(" | ", Enumerable.Repeat("---", colCount)) + " |");
                    idx++;

                    while (idx < lines.Count && lines[idx].Columns.Count == colCount)
                    {
                        sb.AppendLine("| " + string.Join(" | ", lines[idx].Columns.Select(EscapeMarkdownTableCell)) + " |");
                        idx++;
                    }

                    sb.AppendLine();
                    continue;
                }

                if (line.AverageFontSize >= bodyFontSize * 1.55)
                {
                    sb.AppendLine();
                    sb.AppendLine("# " + line.PlainText);
                    sb.AppendLine();
                }
                else if (line.AverageFontSize >= bodyFontSize * 1.28)
                {
                    sb.AppendLine();
                    sb.AppendLine("## " + line.PlainText);
                    sb.AppendLine();
                }
                else if (line.AverageFontSize >= bodyFontSize * 1.13)
                {
                    sb.AppendLine();
                    sb.AppendLine("### " + line.PlainText);
                    sb.AppendLine();
                }
                else if (line.PlainText.StartsWith("• ") || line.PlainText.StartsWith("- ") || line.PlainText.StartsWith("* "))
                {
                    sb.AppendLine("- " + line.FormattedText.Substring(2).Trim());
                }
                else if (NumberedListRegex.IsMatch(line.PlainText))
                {
                    sb.AppendLine(line.FormattedText);
                }
                else
                {
                    sb.AppendLine(line.FormattedText);
                    if (idx + 1 < lines.Count && lines[idx + 1].AverageFontSize >= bodyFontSize * 1.13)
                    {
                        sb.AppendLine();
                    }
                }

                idx++;
            }
        }

        private static string EscapeMarkdownTableCell(string cell)
        {
            return (cell ?? string.Empty).Replace("|", "\\|").Trim();
        }

        private sealed class MarkdownLine
        {
            public string PlainText { get; set; }

            public string FormattedText { get; set; }

            public double AverageFontSize { get; set; }

            public List<string> Columns { get; set; }
        }
    }
}
