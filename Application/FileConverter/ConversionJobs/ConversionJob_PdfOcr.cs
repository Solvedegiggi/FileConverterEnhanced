// <copyright file="ConversionJob_PdfOcr.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using FileConverter.Services;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.Advanced;
    using PdfSharp.Pdf.IO;

    using Tesseract;

    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;

    public class ConversionJob_PdfOcr : ConversionJob
    {
        public ConversionJob_PdfOcr(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
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

            string languages = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.OcrLanguages);
            if (string.IsNullOrWhiteSpace(languages))
            {
                languages = "ita+eng";
            }

            bool skipTextPages = !string.Equals(
                this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.OcrSkipTextPages),
                "False",
                StringComparison.OrdinalIgnoreCase);

            int dpi = 300;
            if (int.TryParse(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfRenderDpi), out int parsedDpi))
            {
                dpi = Math.Max(150, Math.Min(450, parsedDpi));
            }

            string tessdataDir = TessdataService.PrepareTessdataDirectoryAsync(languages).GetAwaiter().GetResult();
            string effectiveLangSpec = string.Join("+", TessdataService.ParseLanguageCodes(languages));

            using (var engine = new TesseractEngine(tessdataDir, effectiveLangSpec, EngineMode.Default))
            using (PdfPigDoc pigDoc = PdfPigDoc.Open(this.InputFilePath))
            using (PdfDocument doc = PdfReader.Open(this.InputFilePath, PdfDocumentOpenMode.Modify))
            {
                int pageCount = doc.PageCount;
                for (int i = 0; i < pageCount; i++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    if (skipTextPages && i < pigDoc.NumberOfPages)
                    {
                        string existingText = (pigDoc.GetPage(i + 1).Text ?? string.Empty).Trim();
                        if (existingText.Length >= 20)
                        {
                            this.Progress = (float)(i + 1) / pageCount;
                            continue;
                        }
                    }

                    byte[] pngBytes = PdfRenderService.RenderPageToPngBytes(this.InputFilePath, i, dpi);
                    if (pngBytes == null)
                    {
                        this.Progress = (float)(i + 1) / pageCount;
                        continue;
                    }

                    List<OcrWord> words = new List<OcrWord>();
                    int imgWidth;
                    int imgHeight;

                    using (Pix pix = Pix.LoadFromMemory(pngBytes))
                    {
                        imgWidth = pix.Width;
                        imgHeight = pix.Height;

                        using (Page ocrPage = engine.Process(pix))
                        using (ResultIterator iter = ocrPage.GetIterator())
                        {
                            iter.Begin();
                            do
                            {
                                string rawText = iter.GetText(PageIteratorLevel.Word);
                                if (!string.IsNullOrWhiteSpace(rawText) &&
                                    iter.TryGetBoundingBox(PageIteratorLevel.Word, out Rect bounds))
                                {
                                    string clean = rawText.Trim();
                                    if (!string.IsNullOrEmpty(clean))
                                    {
                                        words.Add(new OcrWord
                                        {
                                            Text = clean,
                                            X1 = bounds.X1,
                                            Y1 = bounds.Y1,
                                            X2 = bounds.X2,
                                            Y2 = bounds.Y2,
                                        });
                                    }
                                }
                            }
                            while (iter.Next(PageIteratorLevel.Word));
                        }
                    }

                    if (words.Count > 0)
                    {
                        InjectInvisibleTextLayer(doc, doc.Pages[i], words, imgWidth, imgHeight);
                    }

                    this.Progress = (float)(i + 1) / pageCount;
                }

                doc.Save(this.OutputFilePath);
            }
        }

        private static void InjectInvisibleTextLayer(
            PdfDocument doc,
            PdfPage page,
            List<OcrWord> words,
            int imgWidth,
            int imgHeight)
        {
            if (imgWidth <= 0 || imgHeight <= 0)
            {
                return;
            }

            PdfDictionary resources = page.Elements.GetDictionary("/Resources");
            if (resources == null)
            {
                resources = new PdfDictionary(doc);
                page.Elements["/Resources"] = resources;
            }

            PdfDictionary fonts = resources.Elements.GetDictionary("/Font");
            if (fonts == null)
            {
                fonts = new PdfDictionary(doc);
                resources.Elements["/Font"] = fonts;
            }

            if (!fonts.Elements.ContainsKey("/F_OCR"))
            {
                var ocrFont = new PdfDictionary(doc);
                ocrFont.Elements["/Type"] = new PdfName("/Font");
                ocrFont.Elements["/Subtype"] = new PdfName("/Type1");
                ocrFont.Elements["/BaseFont"] = new PdfName("/Helvetica");
                ocrFont.Elements["/Encoding"] = new PdfName("/WinAnsiEncoding");
                fonts.Elements["/F_OCR"] = ocrFont;
            }

            PdfRectangle mb = page.MediaBox;
            double x0 = mb.X1;
            double y0 = mb.Y1;
            double mbW = mb.Width > 1 ? mb.Width : page.Width.Point;
            double mbH = mb.Height > 1 ? mb.Height : page.Height.Point;
            int rot = ((page.Rotate % 360) + 360) % 360;

            double visW = (rot == 90 || rot == 270) ? mbH : mbW;
            double visH = (rot == 90 || rot == 270) ? mbW : mbH;

            var sb = new StringBuilder();
            sb.AppendLine("q");

            if (rot == 90)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "0 -1 1 0 {0:F3} {1:F3} cm", x0, y0 + mbH));
            }
            else if (rot == 180)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "-1 0 0 -1 {0:F3} {1:F3} cm", x0 + mbW, y0 + mbH));
            }
            else if (rot == 270)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "0 1 -1 0 {0:F3} {1:F3} cm", x0 + mbW, y0));
            }
            else
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "1 0 0 1 {0:F3} {1:F3} cm", x0, y0));
            }

            sb.AppendLine("BT");
            sb.AppendLine("3 Tr"); // Invisible text rendering mode (searchable & selectable without visual overlap)

            foreach (OcrWord word in words)
            {
                double xPt = (word.X1 / (double)imgWidth) * visW;
                double wPt = Math.Max(2.0, ((word.X2 - word.X1) / (double)imgWidth) * visW);
                double hPt = Math.Max(4.0, ((word.Y2 - word.Y1) / (double)imgHeight) * visH);
                double yBottomPt = visH - ((word.Y2 / (double)imgHeight) * visH);

                double fontSize = Math.Max(4.0, Math.Min(72.0, hPt * 0.85));
                double estimatedTextWidth = Math.Max(1.0, word.Text.Length * fontSize * 0.52);
                double hScale = Math.Max(50.0, Math.Min(180.0, (wPt / estimatedTextWidth) * 100.0));

                string escaped = EscapePdfLiteralString(word.Text);
                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "/F_OCR {0:F2} Tf {1:F1} Tz 1 0 0 1 {2:F2} {3:F2} Tm ({4}) Tj\r\n",
                    fontSize,
                    hScale,
                    xPt,
                    yBottomPt,
                    escaped);
            }

            sb.AppendLine("ET");
            sb.AppendLine("Q");

            byte[] streamBytes = Encoding.GetEncoding(1252).GetBytes(sb.ToString());
            var contentDict = new PdfContent(doc);
            contentDict.CreateStream(streamBytes);
            doc.Internals.AddObject(contentDict);
            page.Contents.Elements.Add(contentDict.Reference);
        }

        private static string EscapePdfLiteralString(string text)
        {
            var sb = new StringBuilder(text.Length + 8);
            foreach (char ch in text)
            {
                if (ch == '\\' || ch == '(' || ch == ')')
                {
                    sb.Append('\\').Append(ch);
                }
                else if (ch >= 32)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private struct OcrWord
        {
            public string Text;
            public int X1;
            public int Y1;
            public int X2;
            public int Y2;
        }
    }
}
