namespace FileConverter.Tests
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;

    using FileConverter.Services;

    using PdfSharp.Drawing;
    using PdfSharp.Pdf;

    using A = DocumentFormat.OpenXml.Drawing;
    using Pres = DocumentFormat.OpenXml.Presentation;
    using Spread = DocumentFormat.OpenXml.Spreadsheet;
    using Word = DocumentFormat.OpenXml.Wordprocessing;

    public sealed class TempWorkspaceFixture : IDisposable
    {
        public TempWorkspaceFixture()
        {
            WindowsFontResolver.EnsureInitialized();
            this.RootDir = Path.Combine(Path.GetTempPath(), "FileConverterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.RootDir);
        }

        public string RootDir { get; }

        public string PathFor(string fileName) => Path.Combine(this.RootDir, fileName);

        public string CreateSimplePdf(string fileName, params string[] pageTexts)
        {
            string path = this.PathFor(fileName);
            using (var doc = new PdfDocument())
            {
                var font = new XFont("Arial", 16, XFontStyleEx.Regular);
                foreach (string text in pageTexts)
                {
                    PdfPage page = doc.AddPage();
                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    {
                        gfx.DrawString(text, font, XBrushes.Black, new XPoint(60, 100), XStringFormats.TopLeft);
                    }
                }

                doc.Save(path);
            }

            return path;
        }

        public string CreateRichPdfForMarkdown(string fileName)
        {
            string path = this.PathFor(fileName);
            using (var doc = new PdfDocument())
            {
                PdfPage page = doc.AddPage();
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    var h1 = new XFont("Arial", 22, XFontStyleEx.Bold);
                    var body = new XFont("Arial", 11, XFontStyleEx.Regular);

                    gfx.DrawString("Annual Report", h1, XBrushes.Black, new XPoint(50, 60), XStringFormats.TopLeft);
                    gfx.DrawString("- First bullet item", body, XBrushes.Black, new XPoint(50, 110), XStringFormats.TopLeft);
                    gfx.DrawString("Metric", body, XBrushes.Black, new XPoint(50, 150), XStringFormats.TopLeft);
                    gfx.DrawString("Amount", body, XBrushes.Black, new XPoint(250, 150), XStringFormats.TopLeft);
                    gfx.DrawString("Revenue", body, XBrushes.Black, new XPoint(50, 175), XStringFormats.TopLeft);
                    gfx.DrawString("4200", body, XBrushes.Black, new XPoint(250, 175), XStringFormats.TopLeft);
                }

                doc.Save(path);
            }

            return path;
        }

        public string CreatePdfWithEmbeddedImage(string fileName)
        {
            string path = this.PathFor(fileName);
            string imgPath = this.CreateSolidBitmapPng("embed_src.png", 120, 80, Color.CornflowerBlue, null);

            using (var doc = new PdfDocument())
            {
                PdfPage page = doc.AddPage();
                byte[] imgBytes = File.ReadAllBytes(imgPath);
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                using (var ms = new MemoryStream(imgBytes, 0, imgBytes.Length, writable: false, publiclyVisible: true))
                using (XImage img = XImage.FromStream(ms))
                {
                    gfx.DrawImage(img, 80, 100, 120, 80);
                }

                doc.Save(path);
            }

            return path;
        }

        public string CreateScannedImageOnlyPdf(string fileName, string textToRenderAsPixels)
        {
            string png = this.CreateSolidBitmapPng(
                Path.GetFileNameWithoutExtension(fileName) + "_scan.png",
                1100,
                400,
                Color.White,
                textToRenderAsPixels);

            string pdfPath = this.PathFor(fileName);
            using (var doc = new PdfDocument())
            {
                PdfPage page = doc.AddPage();
                page.Width = XUnit.FromPoint(595);
                page.Height = XUnit.FromPoint(240);
                byte[] pngBytes = File.ReadAllBytes(png);
                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                using (var ms = new MemoryStream(pngBytes, 0, pngBytes.Length, writable: false, publiclyVisible: true))
                using (XImage img = XImage.FromStream(ms))
                {
                    gfx.DrawImage(img, 20, 20, 555, 200);
                }

                doc.Save(pdfPath);
            }

            return pdfPath;
        }

        public string CreateSolidBitmapPng(string fileName, int width, int height, Color bg, string highContrastText)
        {
            string path = this.PathFor(fileName);
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(bg);
                if (!string.IsNullOrEmpty(highContrastText))
                {
                    using (var font = new Font("Arial", 42, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        g.DrawString(highContrastText, font, Brushes.Black, new PointF(40, 120));
                    }
                }

                bmp.Save(path, ImageFormat.Png);
            }

            return path;
        }

        public string CreateSampleDocx(string fileName, string heading, string bodyText)
        {
            string path = this.PathFor(fileName);
            using (WordprocessingDocument doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                MainDocumentPart main = doc.AddMainDocumentPart();
                main.Document = new Word.Document(
                    new Word.Body(
                        new Word.Paragraph(
                            new Word.ParagraphProperties(new Word.ParagraphStyleId { Val = "Heading1" }),
                            new Word.Run(new Word.Text(heading))),
                        new Word.Paragraph(
                            new Word.Run(new Word.Text(bodyText)))));
                main.Document.Save();
            }

            return path;
        }

        public string CreateSampleWideXlsx(string fileName, int cols, int rows)
        {
            string path = this.PathFor(fileName);
            using (SpreadsheetDocument ss = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart wb = ss.AddWorkbookPart();
                wb.Workbook = new Spread.Workbook();
                Spread.Sheets sheets = wb.Workbook.AppendChild(new Spread.Sheets());
                WorksheetPart ws = wb.AddNewPart<WorksheetPart>();

                var sd = new Spread.SheetData();
                for (int r = 1; r <= rows; r++)
                {
                    var row = new Spread.Row { RowIndex = (uint)r };
                    for (int c = 0; c < cols; c++)
                    {
                        string val = r == 1 ? $"HeaderCol{c + 1}" : $"R{r}C{c + 1}";
                        row.AppendChild(new Spread.Cell
                        {
                            DataType = Spread.CellValues.InlineString,
                            InlineString = new Spread.InlineString(new Spread.Text(val)),
                        });
                    }

                    sd.AppendChild(row);
                }

                ws.Worksheet = new Spread.Worksheet(sd);
                ws.Worksheet.Save();
                sheets.AppendChild(new Spread.Sheet { Id = wb.GetIdOfPart(ws), SheetId = 1, Name = "Budget" });
                wb.Workbook.Save();
            }

            return path;
        }

        public string CreateSamplePptx(string fileName, string slideTitle)
        {
            string path = this.PathFor(fileName);
            using (PresentationDocument pres = PresentationDocument.Create(path, PresentationDocumentType.Presentation))
            {
                PresentationPart presPart = pres.AddPresentationPart();
                presPart.Presentation = new Pres.Presentation(
                    new Pres.SlideMasterIdList(),
                    new Pres.SlideIdList(),
                    new Pres.SlideSize { Cx = 12192000, Cy = 6858000 });

                SlideMasterPart masterPart = presPart.AddNewPart<SlideMasterPart>("rIdM1");
                SlideLayoutPart layoutPart = masterPart.AddNewPart<SlideLayoutPart>("rIdL1");
                layoutPart.AddPart(masterPart, "rIdMB");
                layoutPart.SlideLayout = new Pres.SlideLayout(
                    new Pres.CommonSlideData(new Pres.ShapeTree(
                        new Pres.NonVisualGroupShapeProperties(
                            new Pres.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                            new Pres.NonVisualGroupShapeDrawingProperties(),
                            new Pres.ApplicationNonVisualDrawingProperties()),
                        new Pres.GroupShapeProperties(new A.TransformGroup()))));
                masterPart.SlideMaster = new Pres.SlideMaster(
                    new Pres.CommonSlideData(new Pres.ShapeTree(
                        new Pres.NonVisualGroupShapeProperties(
                            new Pres.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                            new Pres.NonVisualGroupShapeDrawingProperties(),
                            new Pres.ApplicationNonVisualDrawingProperties()),
                        new Pres.GroupShapeProperties(new A.TransformGroup()))),
                    new Pres.ColorMap(),
                    new Pres.SlideLayoutIdList(new Pres.SlideLayoutId { Id = 2147483649U, RelationshipId = "rIdL1" }));
                presPart.Presentation.SlideMasterIdList.AppendChild(new Pres.SlideMasterId { Id = 2147483648U, RelationshipId = "rIdM1" });

                SlidePart slidePart = presPart.AddNewPart<SlidePart>("rIdS1");
                slidePart.AddPart(layoutPart);
                slidePart.Slide = new Pres.Slide(
                    new Pres.CommonSlideData(
                        new Pres.ShapeTree(
                            new Pres.NonVisualGroupShapeProperties(
                                new Pres.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                                new Pres.NonVisualGroupShapeDrawingProperties(),
                                new Pres.ApplicationNonVisualDrawingProperties()),
                            new Pres.GroupShapeProperties(new A.TransformGroup()),
                            new Pres.Shape(
                                new Pres.NonVisualShapeProperties(
                                    new Pres.NonVisualDrawingProperties { Id = 2U, Name = "Title" },
                                    new Pres.NonVisualShapeDrawingProperties(),
                                    new Pres.ApplicationNonVisualDrawingProperties()),
                                new Pres.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 457200L, Y = 457200L },
                                        new A.Extents { Cx = 8000000L, Cy = 1000000L })),
                                new Pres.TextBody(
                                    new A.BodyProperties(),
                                    new A.ListStyle(),
                                    new A.Paragraph(new A.Run(new A.Text(slideTitle))))))));
                presPart.Presentation.SlideIdList.AppendChild(new Pres.SlideId { Id = 256U, RelationshipId = "rIdS1" });
                presPart.Presentation.Save();
            }

            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(this.RootDir))
                {
                    Directory.Delete(this.RootDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
