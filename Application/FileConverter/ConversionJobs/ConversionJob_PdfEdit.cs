// <copyright file="ConversionJob_PdfEdit.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using FileConverter.Models;
    using FileConverter.Services;

    using ImageMagick;

    using PdfSharp.Drawing;
    using PdfSharp.Pdf;
    using PdfSharp.Pdf.Advanced;
    using PdfSharp.Pdf.Content;
    using PdfSharp.Pdf.Content.Objects;
    using PdfSharp.Pdf.IO;

    public class ConversionJob_PdfEdit : ConversionJob
    {
        private readonly IReadOnlyList<PdfEditOperation> operations;

        public ConversionJob_PdfEdit(
            ConversionPreset conversionPreset,
            string inputFilePath,
            IReadOnlyList<PdfEditOperation> operations)
            : base(conversionPreset, inputFilePath)
        {
            this.operations = operations ?? Array.Empty<PdfEditOperation>();
        }

        protected override bool ValidateInputExtension => false;

        protected override void Convert()
        {
            WindowsFontResolver.EnsureInitialized();
            this.UserState = Properties.Resources.ConversionStateConversion;

            if (!File.Exists(this.InputFilePath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                return;
            }

            var opsByPage = this.operations
                .GroupBy(op => op.PageIndex)
                .ToDictionary(g => g.Key, g => g.ToList());

            using (PdfDocument doc = PdfReader.Open(this.InputFilePath, PdfDocumentOpenMode.Modify))
            {
                int pageCount = doc.PageCount;
                for (int pageIdx = 0; pageIdx < pageCount; pageIdx++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    if (!opsByPage.TryGetValue(pageIdx, out List<PdfEditOperation> pageOps) || pageOps.Count == 0)
                    {
                        this.Progress = (float)(pageIdx + 1) / Math.Max(1, pageCount);
                        continue;
                    }

                    PdfPage page = doc.Pages[pageIdx];

                    // Pass 1: In-place existing text redaction / content-stream removal + background patch + replacement text
                    List<PdfEditOperation> redactions = pageOps
                        .Where(o => o.Kind == PdfEditOperationKind.RedactAndReplaceText)
                        .ToList();

                    if (redactions.Count > 0)
                    {
                        TryStripMatchingTextOperators(doc, page, redactions);
                    }

                    using (XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                    {
                        double pageW = page.Width.Point;
                        double pageH = page.Height.Point;

                        foreach (PdfEditOperation redact in redactions)
                        {
                            double rx = redact.PdfRight > redact.PdfLeft ? redact.PdfLeft : redact.NormX * pageW;
                            double rw = redact.PdfRight > redact.PdfLeft
                                ? Math.Max(4.0, redact.PdfRight - redact.PdfLeft)
                                : Math.Max(4.0, redact.NormWidth * pageW);
                            double ry = redact.PdfTop > redact.PdfBottom
                                ? Math.Max(0.0, pageH - redact.PdfTop)
                                : redact.NormY * pageH;
                            double rh = redact.PdfTop > redact.PdfBottom
                                ? Math.Max(4.0, redact.PdfTop - redact.PdfBottom)
                                : Math.Max(4.0, redact.NormHeight * pageH);

                            XColor bgColor = ParseHexColor(redact.BackgroundColorHex, 255, XColor.FromArgb(255, 255, 255));
                            gfx.DrawRectangle(new XSolidBrush(bgColor), rx - 1.5, ry - 1.5, rw + 3.0, rh + 3.0);

                            if (!string.IsNullOrEmpty(redact.Text))
                            {
                                XColor fg = ParseHexColor(redact.ColorHex, 255, XColor.FromArgb(17, 24, 39));
                                double fs = Math.Max(6.0, Math.Min(72.0, redact.FontSizePt > 0 ? redact.FontSizePt : rh * 0.78));
                                var font = new XFont(string.IsNullOrWhiteSpace(redact.FontFamily) ? "Arial" : redact.FontFamily, fs, XFontStyleEx.Regular);
                                gfx.DrawString(redact.Text, font, new XSolidBrush(fg), new XPoint(rx, ry), XStringFormats.TopLeft);
                            }
                        }

                        // Pass 2: Additive overlays (text, images, shapes, highlights, freehand strokes)
                        foreach (PdfEditOperation op in pageOps)
                        {
                            if (op.Kind == PdfEditOperationKind.RedactAndReplaceText)
                            {
                                continue;
                            }

                            DrawOverlayOperation(gfx, op, pageW, pageH);
                        }
                    }

                    this.Progress = (float)(pageIdx + 1) / Math.Max(1, pageCount);
                }

                doc.Save(this.OutputFilePath);
            }
        }

        private static void TryStripMatchingTextOperators(
            PdfDocument doc,
            PdfPage page,
            List<PdfEditOperation> redactions)
        {
            var tokensToRemove = new List<string>();
            foreach (PdfEditOperation red in redactions)
            {
                if (!string.IsNullOrWhiteSpace(red.OriginalText))
                {
                    tokensToRemove.Add(red.OriginalText.Trim());
                    foreach (string word in red.OriginalText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (word.Length >= 3)
                        {
                            tokensToRemove.Add(word);
                        }
                    }
                }
            }

            if (tokensToRemove.Count == 0)
            {
                return;
            }

            try
            {
                CSequence sequence = ContentReader.ReadContent(page);
                bool modified = StripOperatorsFromSequence(sequence, tokensToRemove);
                if (!modified)
                {
                    return;
                }

                byte[] rewrittenBytes = sequence.ToContent();
                var rewritten = new PdfContent(doc);
                rewritten.CreateStream(rewrittenBytes);
                doc.Internals.AddObject(rewritten);
                page.Contents.Elements.Clear();
                page.Contents.Elements.Add(rewritten.Reference);
            }
            catch
            {
                // Content-stream parsing is best-effort; Pass 1 still paints opaque background patch cleanly.
            }
        }

        private static bool StripOperatorsFromSequence(CSequence sequence, List<string> tokensToRemove)
        {
            bool changed = false;
            for (int i = sequence.Count - 1; i >= 0; i--)
            {
                CObject item = sequence[i];
                if (item is CSequence nested)
                {
                    if (StripOperatorsFromSequence(nested, tokensToRemove))
                    {
                        changed = true;
                    }

                    continue;
                }

                if (item is COperator op &&
                    (op.OpCode.OpCodeName == OpCodeName.Tj || op.OpCode.OpCodeName == OpCodeName.TJ))
                {
                    string drawnText = ExtractOperatorText(op).Trim();
                    if (!string.IsNullOrEmpty(drawnText) &&
                        tokensToRemove.Any(t =>
                            string.Equals(drawnText, t, StringComparison.OrdinalIgnoreCase) ||
                            t.IndexOf(drawnText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            drawnText.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        sequence.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static string ExtractOperatorText(COperator op)
        {
            var sb = new StringBuilder();
            foreach (CObject operand in op.Operands)
            {
                if (operand is CString cs)
                {
                    sb.Append(cs.Value);
                }
                else if (operand is CArray arr)
                {
                    foreach (CObject elem in arr)
                    {
                        if (elem is CString inner)
                        {
                            sb.Append(inner.Value);
                        }
                    }
                }
            }

            return sb.ToString();
        }

        private static void DrawOverlayOperation(XGraphics gfx, PdfEditOperation op, double pageW, double pageH)
        {
            double x = op.NormX * pageW;
            double y = op.NormY * pageH;
            double w = Math.Max(4.0, op.NormWidth * pageW);
            double h = Math.Max(4.0, op.NormHeight * pageH);

            switch (op.Kind)
            {
                case PdfEditOperationKind.AddText:
                {
                    if (string.IsNullOrEmpty(op.Text))
                    {
                        return;
                    }

                    XColor color = ParseHexColor(op.ColorHex, 255, XColor.FromArgb(17, 24, 39));
                    double fs = Math.Max(6.0, Math.Min(96.0, op.FontSizePt > 0 ? op.FontSizePt : 14.0));
                    var font = new XFont(string.IsNullOrWhiteSpace(op.FontFamily) ? "Arial" : op.FontFamily, fs, XFontStyleEx.Regular);
                    gfx.DrawString(op.Text, font, new XSolidBrush(color), new XPoint(x, y), XStringFormats.TopLeft);
                    break;
                }

                case PdfEditOperationKind.AddImage:
                {
                    if (string.IsNullOrWhiteSpace(op.ImagePath) || !File.Exists(op.ImagePath))
                    {
                        return;
                    }

                    using (MemoryStream ms = LoadImageStream(op.ImagePath))
                    using (XImage img = XImage.FromStream(ms))
                    {
                        gfx.DrawImage(img, x, y, w, h);
                    }

                    break;
                }

                case PdfEditOperationKind.Rectangle:
                {
                    XColor color = ParseHexColor(op.ColorHex, 255, XColor.FromArgb(220, 38, 38));
                    var pen = new XPen(color, Math.Max(0.5, op.StrokeWidth));
                    gfx.DrawRectangle(pen, x, y, w, h);
                    break;
                }

                case PdfEditOperationKind.Ellipse:
                {
                    XColor color = ParseHexColor(op.ColorHex, 255, XColor.FromArgb(220, 38, 38));
                    var pen = new XPen(color, Math.Max(0.5, op.StrokeWidth));
                    gfx.DrawEllipse(pen, x, y, w, h);
                    break;
                }

                case PdfEditOperationKind.Line:
                case PdfEditOperationKind.Arrow:
                {
                    double x2 = op.NormX2 * pageW;
                    double y2 = op.NormY2 * pageH;
                    XColor color = ParseHexColor(op.ColorHex, 255, XColor.FromArgb(220, 38, 38));
                    double sw = Math.Max(0.5, op.StrokeWidth);
                    var pen = new XPen(color, sw);
                    gfx.DrawLine(pen, x, y, x2, y2);

                    if (op.Kind == PdfEditOperationKind.Arrow)
                    {
                        double angle = Math.Atan2(y2 - y, x2 - x);
                        double headLen = Math.Max(8.0, sw * 4.5);
                        double spread = 0.48;
                        var p1 = new XPoint(x2 - (headLen * Math.Cos(angle - spread)), y2 - (headLen * Math.Sin(angle - spread)));
                        var p2 = new XPoint(x2 - (headLen * Math.Cos(angle + spread)), y2 - (headLen * Math.Sin(angle + spread)));
                        gfx.DrawPolygon(new XSolidBrush(color), new[] { new XPoint(x2, y2), p1, p2 }, XFillMode.Winding);
                    }

                    break;
                }

                case PdfEditOperationKind.Highlight:
                {
                    XColor color = ParseHexColor(op.ColorHex, 95, XColor.FromArgb(95, 250, 204, 21));
                    gfx.DrawRectangle(new XSolidBrush(color), x, y, w, h);
                    break;
                }

                case PdfEditOperationKind.Freehand:
                {
                    if (op.Points == null || op.Points.Count < 2)
                    {
                        return;
                    }

                    XColor color = ParseHexColor(op.ColorHex, 255, XColor.FromArgb(37, 99, 235));
                    var pen = new XPen(color, Math.Max(0.5, op.StrokeWidth))
                    {
                        LineCap = XLineCap.Round,
                        LineJoin = XLineJoin.Round,
                    };
                    XPoint[] pts = op.Points.Select(p => new XPoint(p.NormX * pageW, p.NormY * pageH)).ToArray();
                    gfx.DrawLines(pen, pts);
                    break;
                }
            }
        }

        private static MemoryStream LoadImageStream(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
            {
                byte[] bytes = File.ReadAllBytes(path);
                return new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
            }

            using (var magick = new MagickImage(path))
            {
                magick.Format = MagickFormat.Png;
                var ms = new MemoryStream();
                magick.Write(ms);
                ms.Position = 0;
                return ms;
            }
        }

        private static XColor ParseHexColor(string hex, byte alpha, XColor fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            string clean = hex.Trim().TrimStart('#');
            if (clean.Length == 6 && int.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                return XColor.FromArgb(alpha, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
            }

            return fallback;
        }
    }
}
