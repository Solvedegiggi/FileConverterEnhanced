// <copyright file="PdfCompareService.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    using UglyToad.PdfPig.Content;
    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;

    public enum DiffLineKind
    {
        Unchanged,
        Added,
        Removed,
    }

    public sealed class TextDiffLine
    {
        public DiffLineKind Kind { get; set; }

        public string Prefix => this.Kind == DiffLineKind.Added ? "+ " : (this.Kind == DiffLineKind.Removed ? "- " : "  ");

        public string Text { get; set; }

        public string ColorHex => this.Kind == DiffLineKind.Added ? "#16A34A" : (this.Kind == DiffLineKind.Removed ? "#DC2626" : "#6B7280");
    }

    public sealed class PdfPageCompareResult
    {
        public int PageIndex { get; set; }

        public BitmapSource LeftImage { get; set; }

        public BitmapSource RightImage { get; set; }

        public BitmapSource DiffOverlayImage { get; set; }

        public double ChangedPixelPercent { get; set; }

        public List<TextDiffLine> TextDiffLines { get; set; } = new List<TextDiffLine>();

        public int AddedLinesCount => this.TextDiffLines.Count(l => l.Kind == DiffLineKind.Added);

        public int RemovedLinesCount => this.TextDiffLines.Count(l => l.Kind == DiffLineKind.Removed);
    }

    public static class PdfCompareService
    {
        public static PdfPageCompareResult ComparePages(string pdfA, string pdfB, int zeroBasedPageIndex, int dpi = 144)
        {
            var result = new PdfPageCompareResult
            {
                PageIndex = zeroBasedPageIndex,
            };

            int countA = File.Exists(pdfA) ? PdfRenderService.GetPageCount(pdfA) : 0;
            int countB = File.Exists(pdfB) ? PdfRenderService.GetPageCount(pdfB) : 0;

            using (Bitmap rawA = zeroBasedPageIndex < countA ? PdfRenderService.RenderPageToDrawingBitmap(pdfA, zeroBasedPageIndex, dpi) : null)
            using (Bitmap rawB = zeroBasedPageIndex < countB ? PdfRenderService.RenderPageToDrawingBitmap(pdfB, zeroBasedPageIndex, dpi) : null)
            {
                int maxW = Math.Max(rawA?.Width ?? 1, rawB?.Width ?? 1);
                int maxH = Math.Max(rawA?.Height ?? 1, rawB?.Height ?? 1);

                byte[] bytesA = RenderToNormalizedBgra(rawA, maxW, maxH);
                byte[] bytesB = RenderToNormalizedBgra(rawB, maxW, maxH);

                if (rawA != null)
                {
                    result.LeftImage = CreateFrozenBitmapSource(bytesA, maxW, maxH);
                }

                if (rawB != null)
                {
                    result.RightImage = CreateFrozenBitmapSource(bytesB, maxW, maxH);
                }

                byte[] diffBytes = new byte[maxW * maxH * 4];
                int changedPixels = 0;
                int totalPixels = maxW * maxH;

                for (int i = 0; i < diffBytes.Length; i += 4)
                {
                    int db = Math.Abs(bytesA[i] - bytesB[i]);
                    int dg = Math.Abs(bytesA[i + 1] - bytesB[i + 1]);
                    int dr = Math.Abs(bytesA[i + 2] - bytesB[i + 2]);

                    if (db + dg + dr > 36)
                    {
                        changedPixels++;
                        // Highlight differences in vivid crimson/magenta (#E11D48) blended lightly with B
                        diffBytes[i] = (byte)((bytesB[i] * 0.25) + (72 * 0.75));
                        diffBytes[i + 1] = (byte)((bytesB[i + 1] * 0.25) + (29 * 0.75));
                        diffBytes[i + 2] = (byte)((bytesB[i + 2] * 0.25) + (225 * 0.75));
                        diffBytes[i + 3] = 255;
                    }
                    else
                    {
                        // Softly fade identical pixels so differences stand out cleanly
                        diffBytes[i] = (byte)((bytesA[i] * 0.35) + (255 * 0.65));
                        diffBytes[i + 1] = (byte)((bytesA[i + 1] * 0.35) + (255 * 0.65));
                        diffBytes[i + 2] = (byte)((bytesA[i + 2] * 0.35) + (255 * 0.65));
                        diffBytes[i + 3] = 255;
                    }
                }

                result.ChangedPixelPercent = totalPixels > 0 ? Math.Round((changedPixels * 100.0) / totalPixels, 2) : 0.0;
                result.DiffOverlayImage = CreateFrozenBitmapSource(diffBytes, maxW, maxH);
            }

            List<string> linesA = ExtractPageLines(pdfA, zeroBasedPageIndex);
            List<string> linesB = ExtractPageLines(pdfB, zeroBasedPageIndex);
            result.TextDiffLines = ComputeLineDiff(linesA, linesB);

            return result;
        }

        public static void ExportMarkdownReport(string pdfA, string pdfB, string outputPath)
        {
            int countA = File.Exists(pdfA) ? PdfRenderService.GetPageCount(pdfA) : 0;
            int countB = File.Exists(pdfB) ? PdfRenderService.GetPageCount(pdfB) : 0;
            int maxPages = Math.Max(countA, countB);

            var sb = new StringBuilder();
            sb.AppendLine("# PDF Comparison Report");
            sb.AppendLine();
            sb.AppendLine($"- **Document A:** {pdfA} ({countA} pages)");
            sb.AppendLine($"- **Document B:** {pdfB} ({countB} pages)");
            sb.AppendLine();

            for (int i = 0; i < maxPages; i++)
            {
                PdfPageCompareResult pageRes = ComparePages(pdfA, pdfB, i, 120);
                sb.AppendLine($"## Page {i + 1}");
                sb.AppendLine();
                sb.AppendLine($"- Visual Pixel Difference: **{pageRes.ChangedPixelPercent:F2}%**");
                sb.AppendLine($"- Added Lines: **+{pageRes.AddedLinesCount}**, Removed Lines: **-{pageRes.RemovedLinesCount}**");
                sb.AppendLine();

                if (pageRes.AddedLinesCount > 0 || pageRes.RemovedLinesCount > 0)
                {
                    sb.AppendLine("```diff");
                    foreach (TextDiffLine line in pageRes.TextDiffLines)
                    {
                        sb.AppendLine(line.Prefix + line.Text);
                    }

                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
        }

        private static byte[] RenderToNormalizedBgra(Bitmap source, int canvasW, int canvasH)
        {
            byte[] buffer = new byte[canvasW * canvasH * 4];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = 255;
            }

            if (source == null)
            {
                return buffer;
            }

            using (var normalized = new Bitmap(canvasW, canvasH, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(normalized))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.DrawImage(source, 0, 0, source.Width, source.Height);
                }

                BitmapData data = normalized.LockBits(
                    new Rectangle(0, 0, canvasW, canvasH),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int rowBytes = canvasW * 4;
                    for (int y = 0; y < canvasH; y++)
                    {
                        IntPtr srcRow = IntPtr.Add(data.Scan0, y * data.Stride);
                        Marshal.Copy(srcRow, buffer, y * rowBytes, rowBytes);
                    }
                }
                finally
                {
                    normalized.UnlockBits(data);
                }
            }

            return buffer;
        }

        private static BitmapSource CreateFrozenBitmapSource(byte[] bgra, int width, int height)
        {
            BitmapSource img = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
            img.Freeze();
            return img;
        }

        private static List<string> ExtractPageLines(string pdfPath, int zeroBasedPageIndex)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath) || zeroBasedPageIndex < 0)
            {
                return lines;
            }

            try
            {
                using (PdfPigDoc pdf = PdfPigDoc.Open(pdfPath))
                {
                    if (zeroBasedPageIndex >= pdf.NumberOfPages)
                    {
                        return lines;
                    }

                    Page page = pdf.GetPage(zeroBasedPageIndex + 1);
                    List<Word> words = page.GetWords()
                        .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                        .OrderByDescending(w => w.BoundingBox.Bottom)
                        .ThenBy(w => w.BoundingBox.Left)
                        .ToList();

                    var buckets = new List<List<Word>>();
                    foreach (Word w in words)
                    {
                        List<Word> bucket = buckets.FirstOrDefault(b => Math.Abs(b[0].BoundingBox.Bottom - w.BoundingBox.Bottom) <= 4.0);
                        if (bucket == null)
                        {
                            bucket = new List<Word>();
                            buckets.Add(bucket);
                        }

                        bucket.Add(w);
                    }

                    foreach (List<Word> bucket in buckets)
                    {
                        bucket.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
                        string line = string.Join(" ", bucket.Select(w => w.Text)).Trim();
                        if (!string.IsNullOrEmpty(line))
                        {
                            lines.Add(line);
                        }
                    }
                }
            }
            catch
            {
            }

            return lines;
        }

        private static List<TextDiffLine> ComputeLineDiff(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            int n = a.Count;
            int m = b.Count;
            int[,] dp = new int[n + 1, m + 1];

            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = m - 1; j >= 0; j--)
                {
                    if (string.Equals(a[i], b[j], StringComparison.Ordinal))
                    {
                        dp[i, j] = dp[i + 1, j + 1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                    }
                }
            }

            var diff = new List<TextDiffLine>();
            int x = 0;
            int y = 0;
            while (x < n && y < m)
            {
                if (string.Equals(a[x], b[y], StringComparison.Ordinal))
                {
                    diff.Add(new TextDiffLine { Kind = DiffLineKind.Unchanged, Text = a[x] });
                    x++;
                    y++;
                }
                else if (dp[x + 1, y] >= dp[x, y + 1])
                {
                    diff.Add(new TextDiffLine { Kind = DiffLineKind.Removed, Text = a[x] });
                    x++;
                }
                else
                {
                    diff.Add(new TextDiffLine { Kind = DiffLineKind.Added, Text = b[y] });
                    y++;
                }
            }

            while (x < n)
            {
                diff.Add(new TextDiffLine { Kind = DiffLineKind.Removed, Text = a[x++] });
            }

            while (y < m)
            {
                diff.Add(new TextDiffLine { Kind = DiffLineKind.Added, Text = b[y++] });
            }

            return diff;
        }
    }
}
