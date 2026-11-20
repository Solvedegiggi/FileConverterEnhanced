// <copyright file="ConversionJob_PdfToWord.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Wordprocessing;

    using UglyToad.PdfPig.Content;

    using Drawing2D = DocumentFormat.OpenXml.Drawing;
    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;
    using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
    using WordInterop = NetOffice.WordApi;
    using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

    public class ConversionJob_PdfToWord : ConversionJob
    {
        public ConversionJob_PdfToWord(ConversionPreset conversionPreset, string inputFilePath)
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

            string engine = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfToOfficeEngine) ?? "Native";
            if (string.Equals(engine, "OfficeInterop", StringComparison.OrdinalIgnoreCase) &&
                Helpers.IsMicrosoftOfficeApplicationAvailable(ConversionJob_Office.ApplicationName.Word))
            {
                if (this.TryConvertWithWordInterop())
                {
                    this.Progress = 1.0f;
                    return;
                }
            }

            this.ConvertNative();
        }

        private bool TryConvertWithWordInterop()
        {
            WordInterop.Application app = null;
            WordInterop.Document doc = null;
            try
            {
                app = new WordInterop.Application
                {
                    Visible = false,
                    DisplayAlerts = WordInterop.Enums.WdAlertLevel.wdAlertsNone,
                };
                doc = app.Documents.Open(this.InputFilePath, false, true);
                doc.SaveAs2(this.OutputFilePath, WordInterop.Enums.WdSaveFormat.wdFormatXMLDocument);
                doc.Close(WordInterop.Enums.WdSaveOptions.wdDoNotSaveChanges);
                doc = null;
                return File.Exists(this.OutputFilePath);
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    doc?.Close(WordInterop.Enums.WdSaveOptions.wdDoNotSaveChanges);
                    app?.Quit(WordInterop.Enums.WdSaveOptions.wdDoNotSaveChanges);
                }
                catch
                {
                }
            }
        }

        private void ConvertNative()
        {
            using (PdfPigDoc pdf = PdfPigDoc.Open(this.InputFilePath))
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(this.OutputFilePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                Body body = mainPart.Document.Body;

                uint imageCounter = 1;
                int totalPages = pdf.NumberOfPages;

                for (int pageNum = 1; pageNum <= totalPages; pageNum++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    Page page = pdf.GetPage(pageNum);
                    IReadOnlyList<Word> words = page.GetWords().ToList();
                    double bodySize = ComputeMedianFontSize(words);

                    List<PdfLineInfo> lines = GroupWordsIntoLines(words, bodySize);
                    AppendPageLines(body, lines, bodySize);

                    foreach (IPdfImage pdfImg in page.GetImages())
                    {
                        if (pdfImg.TryGetPng(out byte[] pngBytes) && pngBytes != null && pngBytes.Length > 0)
                        {
                            AppendImageParagraph(mainPart, body, pngBytes, ref imageCounter);
                        }
                    }

                    if (pageNum < totalPages)
                    {
                        body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                    }

                    this.Progress = (float)pageNum / totalPages;
                }

                mainPart.Document.Save();
            }
        }

        private static double ComputeMedianFontSize(IReadOnlyList<Word> words)
        {
            var sizes = words
                .Where(w => w.Letters.Count > 0)
                .Select(w => w.Letters[0].PointSize)
                .OrderBy(s => s)
                .ToList();
            return sizes.Count > 0 ? sizes[sizes.Count / 2] : 11.0;
        }

        private static List<PdfLineInfo> GroupWordsIntoLines(IReadOnlyList<Word> words, double bodySize)
        {
            var sorted = words
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var buckets = new List<List<Word>>();
            double yTol = Math.Max(3.0, bodySize * 0.45);

            foreach (Word w in sorted)
            {
                List<Word> target = buckets.FirstOrDefault(b => Math.Abs(b[0].BoundingBox.Bottom - w.BoundingBox.Bottom) <= yTol);
                if (target == null)
                {
                    target = new List<Word>();
                    buckets.Add(target);
                }

                target.Add(w);
            }

            var lines = new List<PdfLineInfo>(buckets.Count);
            foreach (List<Word> bucket in buckets)
            {
                bucket.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

                double avgFont = bucket
                    .Where(w => w.Letters.Count > 0)
                    .Select(w => w.Letters[0].PointSize)
                    .DefaultIfEmpty(bodySize)
                    .Average();

                var columns = new List<List<Word>>();
                var currentCol = new List<Word>();
                double gapThreshold = Math.Max(20.0, avgFont * 2.1);

                for (int i = 0; i < bucket.Count; i++)
                {
                    if (i > 0 && (bucket[i].BoundingBox.Left - bucket[i - 1].BoundingBox.Right) > gapThreshold)
                    {
                        columns.Add(currentCol);
                        currentCol = new List<Word>();
                    }

                    currentCol.Add(bucket[i]);
                }

                if (currentCol.Count > 0)
                {
                    columns.Add(currentCol);
                }

                lines.Add(new PdfLineInfo
                {
                    Words = bucket,
                    ColumnGroups = columns,
                    AverageFontSize = avgFont,
                });
            }

            return lines;
        }

        private static void AppendPageLines(Body body, List<PdfLineInfo> lines, double bodySize)
        {
            int idx = 0;
            while (idx < lines.Count)
            {
                PdfLineInfo line = lines[idx];

                // Group 2+ consecutive aligned multi-column rows into a clean Word Table
                if (line.ColumnGroups.Count >= 2 &&
                    idx + 1 < lines.Count &&
                    lines[idx + 1].ColumnGroups.Count == line.ColumnGroups.Count)
                {
                    int colCount = line.ColumnGroups.Count;
                    var table = new Table(
                        new TableProperties(
                            new TableBorders(
                                new TopBorder { Val = BorderValues.Single, Size = 4 },
                                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                                new RightBorder { Val = BorderValues.Single, Size = 4 },
                                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

                    while (idx < lines.Count && lines[idx].ColumnGroups.Count == colCount)
                    {
                        var row = new TableRow();
                        for (int c = 0; c < colCount; c++)
                        {
                            var para = new Paragraph();
                            AppendStyledWords(para, lines[idx].ColumnGroups[c], lines[idx].AverageFontSize, forceBold: false);
                            row.AppendChild(new TableCell(para));
                        }

                        table.AppendChild(row);
                        idx++;
                    }

                    body.AppendChild(table);
                    continue;
                }

                bool isHeading = line.AverageFontSize >= bodySize * 1.25;
                var p = new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines
                        {
                            Before = isHeading ? "160" : "0",
                            After = isHeading ? "100" : "80",
                        }));

                AppendStyledWords(p, line.Words, line.AverageFontSize, forceBold: isHeading);
                body.AppendChild(p);
                idx++;
            }
        }

        private static void AppendStyledWords(Paragraph para, IReadOnlyList<Word> words, double fontSizePt, bool forceBold)
        {
            int halfPts = Math.Max(14, Math.Min(72, (int)Math.Round(fontSizePt * 2)));

            for (int i = 0; i < words.Count; i++)
            {
                Word w = words[i];
                string text = (i > 0 ? " " : string.Empty) + w.Text;

                string fontName = w.Letters.Count > 0 ? (w.Letters[0].FontName ?? string.Empty) : string.Empty;
                bool isBold = forceBold || fontName.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isItalic = fontName.IndexOf("Italic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                fontName.IndexOf("Oblique", StringComparison.OrdinalIgnoreCase) >= 0;

                var runProps = new RunProperties
                {
                    FontSize = new FontSize { Val = halfPts.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                };
                if (isBold)
                {
                    runProps.Bold = new Bold();
                }

                if (isItalic)
                {
                    runProps.Italic = new Italic();
                }

                para.AppendChild(new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            }
        }

        private static void AppendImageParagraph(MainDocumentPart mainPart, Body body, byte[] pngBytes, ref uint imgId)
        {
            long cx = 4000000L;
            long cy = 3000000L;
            try
            {
                using (var ms = new MemoryStream(pngBytes))
                using (var img = Image.FromStream(ms))
                {
                    long rawW = img.Width * 9525L;
                    long rawH = img.Height * 9525L;
                    long maxW = 5400000L; // ~15cm
                    if (rawW > maxW && rawW > 0)
                    {
                        double ratio = (double)maxW / rawW;
                        cx = maxW;
                        cy = (long)(rawH * ratio);
                    }
                    else
                    {
                        cx = Math.Max(9525L, rawW);
                        cy = Math.Max(9525L, rawH);
                    }
                }
            }
            catch
            {
            }

            ImagePart imgPart = mainPart.AddImagePart("image/png");
            using (var ms = new MemoryStream(pngBytes))
            {
                imgPart.FeedData(ms);
            }

            string relId = mainPart.GetIdOfPart(imgPart);
            uint id = imgId++;

            var drawing = new Drawing(
                new Wp.Inline(
                    new Wp.Extent { Cx = cx, Cy = cy },
                    new Wp.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new Wp.DocProperties { Id = id, Name = $"PdfImage{id}" },
                    new Wp.NonVisualGraphicFrameDrawingProperties(
                        new Drawing2D.GraphicFrameLocks { NoChangeAspect = true }),
                    new Drawing2D.Graphic(
                        new Drawing2D.GraphicData(
                            new Pic.Picture(
                                new Pic.NonVisualPictureProperties(
                                    new Pic.NonVisualDrawingProperties { Id = id, Name = $"img{id}.png" },
                                    new Pic.NonVisualPictureDrawingProperties()),
                                new Pic.BlipFill(
                                    new Drawing2D.Blip { Embed = relId },
                                    new Drawing2D.Stretch(new Drawing2D.FillRectangle())),
                                new Pic.ShapeProperties(
                                    new Drawing2D.Transform2D(
                                        new Drawing2D.Offset { X = 0L, Y = 0L },
                                        new Drawing2D.Extents { Cx = cx, Cy = cy }),
                                    new Drawing2D.PresetGeometry(new Drawing2D.AdjustValueList())
                                    {
                                        Preset = Drawing2D.ShapeTypeValues.Rectangle,
                                    })))
                        {
                            Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                        })));

            body.AppendChild(new Paragraph(new Run(drawing)));
        }

        private sealed class PdfLineInfo
        {
            public List<Word> Words { get; set; }

            public List<List<Word>> ColumnGroups { get; set; }

            public double AverageFontSize { get; set; }
        }
    }
}
