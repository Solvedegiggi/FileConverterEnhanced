// <copyright file="PdfTextBlockService.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using FileConverter.Models;

    using UglyToad.PdfPig.Content;
    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;

    public static class PdfTextBlockService
    {
        public static List<PdfTextBlockInfo> ExtractPageTextBlocks(string pdfPath, int zeroBasedPageIndex)
        {
            var blocks = new List<PdfTextBlockInfo>();
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath) || zeroBasedPageIndex < 0)
            {
                return blocks;
            }

            try
            {
                using (PdfPigDoc pdf = PdfPigDoc.Open(pdfPath))
                {
                    if (zeroBasedPageIndex >= pdf.NumberOfPages)
                    {
                        return blocks;
                    }

                    Page page = pdf.GetPage(zeroBasedPageIndex + 1);
                    double pageW = Math.Max(1.0, page.Width);
                    double pageH = Math.Max(1.0, page.Height);

                    List<Word> words = page.GetWords()
                        .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                        .OrderByDescending(w => w.BoundingBox.Bottom)
                        .ThenBy(w => w.BoundingBox.Left)
                        .ToList();

                    // Group horizontally adjacent words on the same baseline into editable phrases
                    var lineBuckets = new List<List<Word>>();
                    foreach (Word word in words)
                    {
                        double fontSize = word.Letters.Count > 0 ? word.Letters[0].PointSize : 11.0;
                        double yTol = Math.Max(3.0, fontSize * 0.45);
                        List<Word> bucket = lineBuckets.FirstOrDefault(b => Math.Abs(b[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= yTol);
                        if (bucket == null)
                        {
                            bucket = new List<Word>();
                            lineBuckets.Add(bucket);
                        }

                        bucket.Add(word);
                    }

                    foreach (List<Word> bucket in lineBuckets)
                    {
                        bucket.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

                        var spanGroups = new List<List<Word>>();
                        var currentSpan = new List<Word> { bucket[0] };

                        for (int i = 1; i < bucket.Count; i++)
                        {
                            double fontSize = bucket[i].Letters.Count > 0 ? bucket[i].Letters[0].PointSize : 11.0;
                            double gap = bucket[i].BoundingBox.Left - bucket[i - 1].BoundingBox.Right;
                            if (gap > Math.Max(18.0, fontSize * 1.8))
                            {
                                spanGroups.Add(currentSpan);
                                currentSpan = new List<Word>();
                            }

                            currentSpan.Add(bucket[i]);
                        }

                        spanGroups.Add(currentSpan);

                        foreach (List<Word> group in spanGroups)
                        {
                            double left = group.Min(w => w.BoundingBox.Left);
                            double right = group.Max(w => w.BoundingBox.Right);
                            double bottom = group.Min(w => w.BoundingBox.Bottom);
                            double top = group.Max(w => w.BoundingBox.Top);
                            double fontSize = group.Where(w => w.Letters.Count > 0)
                                .Select(w => w.Letters[0].PointSize)
                                .DefaultIfEmpty(11.0)
                                .Average();
                            string fontName = group.FirstOrDefault(w => w.Letters.Count > 0)?.Letters[0].FontName ?? "Arial";
                            string text = string.Join(" ", group.Select(w => w.Text));

                            double normX = Clamp01(left / pageW);
                            double normY = Clamp01((pageH - top) / pageH);
                            double normW = Math.Max(0.01, Clamp01((right - left) / pageW));
                            double normH = Math.Max(0.01, Clamp01((top - bottom) / pageH));

                            blocks.Add(new PdfTextBlockInfo
                            {
                                PageIndex = zeroBasedPageIndex,
                                Text = text,
                                FontName = fontName,
                                FontSizePt = fontSize,
                                NormX = normX,
                                NormY = normY,
                                NormWidth = normW,
                                NormHeight = normH,
                                PdfLeft = left,
                                PdfBottom = bottom,
                                PdfRight = right,
                                PdfTop = top,
                            });
                        }
                    }
                }
            }
            catch
            {
            }

            return blocks;
        }

        private static double Clamp01(double val)
        {
            if (val < 0.0)
            {
                return 0.0;
            }

            if (val > 1.0)
            {
                return 1.0;
            }

            return val;
        }
    }
}
