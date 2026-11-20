// <copyright file="ConversionJob_PdfToPowerPoint.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Presentation;

    using FileConverter.Services;

    using UglyToad.PdfPig.Content;

    using A = DocumentFormat.OpenXml.Drawing;
    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;

    public class ConversionJob_PdfToPowerPoint : ConversionJob
    {
        private const long SlideWidthEmus = 12192000L; // 16:9 widescreen (13.333 in)
        private const long SlideHeightEmus = 6858000L; // 7.5 in

        public ConversionJob_PdfToPowerPoint(ConversionPreset conversionPreset, string inputFilePath)
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

            using (PdfPigDoc pdf = PdfPigDoc.Open(this.InputFilePath))
            using (PresentationDocument presDoc = PresentationDocument.Create(this.OutputFilePath, PresentationDocumentType.Presentation))
            {
                PresentationPart presPart = presDoc.AddPresentationPart();
                presPart.Presentation = new Presentation(
                    new SlideMasterIdList(),
                    new SlideIdList(),
                    new SlideSize { Cx = (int)SlideWidthEmus, Cy = (int)SlideHeightEmus },
                    new NotesSize { Cx = 6858000, Cy = 9144000 });

                CreateMasterLayoutAndTheme(presPart, out SlideLayoutPart slideLayoutPart);

                uint slideIdCounter = 256U;
                int pageCount = pdf.NumberOfPages;

                for (int pageNum = 1; pageNum <= pageCount; pageNum++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    Page page = pdf.GetPage(pageNum);
                    SlidePart slidePart = presPart.AddNewPart<SlidePart>($"rIdSlide{pageNum}");
                    slidePart.AddPart(slideLayoutPart);

                    var shapeTree = new ShapeTree(
                        new NonVisualGroupShapeProperties(
                            new NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                            new NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()),
                        new GroupShapeProperties(new A.TransformGroup()));

                    uint shapeId = 2U;
                    double pageW = Math.Max(1.0, page.Width);
                    double pageH = Math.Max(1.0, page.Height);

                    List<Word> words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
                    List<IPdfImage> embeddedImages = page.GetImages().ToList();

                    if (words.Count == 0 && embeddedImages.Count == 0)
                    {
                        // Scanned or vector-only page: render full slide image so content is faithfully preserved
                        byte[] renderedPng = PdfRenderService.RenderPageToPngBytes(this.InputFilePath, pageNum - 1, 150);
                        if (renderedPng != null && renderedPng.Length > 0)
                        {
                            AppendPictureShape(slidePart, shapeTree, ref shapeId, renderedPng, 0, 0, SlideWidthEmus, SlideHeightEmus);
                        }
                    }
                    else
                    {
                        foreach (IPdfImage pdfImg in embeddedImages)
                        {
                            if (pdfImg.TryGetPng(out byte[] pngBytes) && pngBytes != null && pngBytes.Length > 0)
                            {
                                var box = pdfImg.Bounds;
                                long px = ClampEmu((box.Left / pageW) * SlideWidthEmus, 0, SlideWidthEmus - 100000);
                                long py = ClampEmu(((pageH - box.Top) / pageH) * SlideHeightEmus, 0, SlideHeightEmus - 100000);
                                long pw = ClampEmu((box.Width / pageW) * SlideWidthEmus, 100000, SlideWidthEmus - px);
                                long ph = ClampEmu((box.Height / pageH) * SlideHeightEmus, 100000, SlideHeightEmus - py);
                                AppendPictureShape(slidePart, shapeTree, ref shapeId, pngBytes, px, py, pw, ph);
                            }
                        }

                        List<SlideTextBlock> blocks = GroupWordsIntoBlocks(words, pageW, pageH);
                        foreach (SlideTextBlock block in blocks)
                        {
                            AppendTextBoxShape(shapeTree, ref shapeId, block);
                        }
                    }

                    slidePart.Slide = new Slide(
                        new CommonSlideData(shapeTree),
                        new ColorMapOverride(new A.MasterColorMapping()));
                    slidePart.Slide.Save();

                    presPart.Presentation.SlideIdList.AppendChild(
                        new SlideId { Id = slideIdCounter++, RelationshipId = $"rIdSlide{pageNum}" });

                    this.Progress = (float)pageNum / pageCount;
                }

                presPart.Presentation.Save();
            }
        }

        private static void CreateMasterLayoutAndTheme(PresentationPart presPart, out SlideLayoutPart slideLayoutPart)
        {
            ThemePart themePart = presPart.AddNewPart<ThemePart>("rIdTheme1");
            themePart.Theme = new A.Theme(
                new A.ThemeElements(
                    new A.ColorScheme(
                        new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                        new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                        new A.Dark2Color(new A.RgbColorModelHex { Val = "1F2937" }),
                        new A.Light2Color(new A.RgbColorModelHex { Val = "F3F4F6" }),
                        new A.Accent1Color(new A.RgbColorModelHex { Val = "0171BB" }),
                        new A.Accent2Color(new A.RgbColorModelHex { Val = "E5533D" }),
                        new A.Accent3Color(new A.RgbColorModelHex { Val = "4F9E3C" }),
                        new A.Accent4Color(new A.RgbColorModelHex { Val = "D6A11E" }),
                        new A.Accent5Color(new A.RgbColorModelHex { Val = "8E44AD" }),
                        new A.Accent6Color(new A.RgbColorModelHex { Val = "2E8B57" }),
                        new A.Hyperlink(new A.RgbColorModelHex { Val = "0563C1" }),
                        new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "954F72" }))
                    { Name = "Office" },
                    new A.FontScheme(
                        new A.MajorFont(
                            new A.LatinFont { Typeface = "Calibri" },
                            new A.EastAsianFont { Typeface = string.Empty },
                            new A.ComplexScriptFont { Typeface = string.Empty }),
                        new A.MinorFont(
                            new A.LatinFont { Typeface = "Calibri" },
                            new A.EastAsianFont { Typeface = string.Empty },
                            new A.ComplexScriptFont { Typeface = string.Empty }))
                    { Name = "Office" },
                    new A.FormatScheme(
                        new A.FillStyleList(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })),
                        new A.LineStyleList(new A.Outline(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })) { Width = 9525 }),
                        new A.EffectStyleList(new A.EffectStyle(new A.EffectList())),
                        new A.BackgroundFillStyleList(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })))
                    { Name = "Office" }))
            { Name = "Office Theme" };
            themePart.Theme.Save();

            SlideMasterPart masterPart = presPart.AddNewPart<SlideMasterPart>("rIdMaster1");
            slideLayoutPart = masterPart.AddNewPart<SlideLayoutPart>("rIdLayout1");
            slideLayoutPart.AddPart(masterPart, "rIdMasterBack");

            slideLayoutPart.SlideLayout = new SlideLayout(
                new CommonSlideData(
                    new ShapeTree(
                        new NonVisualGroupShapeProperties(
                            new NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                            new NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()),
                        new GroupShapeProperties(new A.TransformGroup()))),
                new ColorMapOverride(new A.MasterColorMapping()))
            {
                Type = SlideLayoutValues.Blank,
                Preserve = true,
            };
            slideLayoutPart.SlideLayout.Save();

            masterPart.AddPart(themePart, "rIdThemeMaster");
            masterPart.SlideMaster = new SlideMaster(
                new CommonSlideData(
                    new ShapeTree(
                        new NonVisualGroupShapeProperties(
                            new NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                            new NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()),
                        new GroupShapeProperties(new A.TransformGroup()))),
                new ColorMap
                {
                    Background1 = A.ColorSchemeIndexValues.Light1,
                    Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2,
                    Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1,
                    Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3,
                    Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5,
                    Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink,
                },
                new SlideLayoutIdList(new SlideLayoutId { Id = 2147483649U, RelationshipId = "rIdLayout1" }),
                new TextStyles(
                    new TitleStyle(),
                    new BodyStyle(),
                    new OtherStyle()));
            masterPart.SlideMaster.Save();

            presPart.Presentation.SlideMasterIdList.AppendChild(
                new SlideMasterId { Id = 2147483648U, RelationshipId = "rIdMaster1" });
        }

        private static List<SlideTextBlock> GroupWordsIntoBlocks(List<Word> words, double pageW, double pageH)
        {
            var sorted = words
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var lineBuckets = new List<List<Word>>();
            foreach (Word w in sorted)
            {
                double fontPt = w.Letters.Count > 0 ? w.Letters[0].PointSize : 12.0;
                double tol = Math.Max(3.0, fontPt * 0.45);
                List<Word> target = lineBuckets.FirstOrDefault(b => Math.Abs(b[0].BoundingBox.Bottom - w.BoundingBox.Bottom) <= tol);
                if (target == null)
                {
                    target = new List<Word>();
                    lineBuckets.Add(target);
                }

                target.Add(w);
            }

            var blocks = new List<SlideTextBlock>();
            foreach (List<Word> bucket in lineBuckets)
            {
                bucket.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

                double minLeft = bucket.Min(w => w.BoundingBox.Left);
                double maxRight = bucket.Max(w => w.BoundingBox.Right);
                double minBottom = bucket.Min(w => w.BoundingBox.Bottom);
                double maxTop = bucket.Max(w => w.BoundingBox.Top);
                double avgFontPt = bucket.Where(w => w.Letters.Count > 0).Select(w => w.Letters[0].PointSize).DefaultIfEmpty(12.0).Average();
                bool isBold = bucket.Any(w => w.Letters.Count > 0 && (w.Letters[0].FontName ?? string.Empty).IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0);

                long x = ClampEmu((minLeft / pageW) * SlideWidthEmus, 0, SlideWidthEmus - 400000);
                long y = ClampEmu(((pageH - maxTop) / pageH) * SlideHeightEmus, 0, SlideHeightEmus - 200000);
                long wEmu = ClampEmu(((maxRight - minLeft) / pageW) * SlideWidthEmus + 450000, 600000, SlideWidthEmus - x);
                long hEmu = ClampEmu(((maxTop - minBottom) / pageH) * SlideHeightEmus + 180000, 260000, SlideHeightEmus - y);

                blocks.Add(new SlideTextBlock
                {
                    X = x,
                    Y = y,
                    Width = wEmu,
                    Height = hEmu,
                    Text = string.Join(" ", bucket.Select(w => w.Text)),
                    FontSizeHundredths = Math.Max(800, Math.Min(4800, (int)Math.Round(avgFontPt * 100))),
                    Bold = isBold,
                });
            }

            return blocks;
        }

        private static void AppendTextBoxShape(ShapeTree tree, ref uint shapeId, SlideTextBlock block)
        {
            uint id = shapeId++;
            var shape = new Shape(
                new NonVisualShapeProperties(
                    new NonVisualDrawingProperties { Id = id, Name = $"TextBox {id}" },
                    new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new ApplicationNonVisualDrawingProperties()),
                new ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = block.X, Y = block.Y },
                        new A.Extents { Cx = block.Width, Cy = block.Height }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
                    new A.NoFill()),
                new TextBody(
                    new A.BodyProperties { Wrap = A.TextWrappingValues.Square, LeftInset = 30000, TopInset = 20000, RightInset = 30000, BottomInset = 20000 },
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.Run(
                            new A.RunProperties { Language = "en-US", FontSize = block.FontSizeHundredths, Bold = block.Bold },
                            new A.Text(block.Text)))));

            tree.AppendChild(shape);
        }

        private static void AppendPictureShape(
            SlidePart slidePart,
            ShapeTree tree,
            ref uint shapeId,
            byte[] pngBytes,
            long x,
            long y,
            long width,
            long height)
        {
            uint id = shapeId++;
            ImagePart imgPart = slidePart.AddImagePart("image/png");
            using (var ms = new MemoryStream(pngBytes))
            {
                imgPart.FeedData(ms);
            }

            string relId = slidePart.GetIdOfPart(imgPart);
            var pic = new Picture(
                new NonVisualPictureProperties(
                    new NonVisualDrawingProperties { Id = id, Name = $"Picture {id}" },
                    new NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    new ApplicationNonVisualDrawingProperties()),
                new BlipFill(
                    new A.Blip { Embed = relId },
                    new A.Stretch(new A.FillRectangle())),
                new ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = x, Y = y },
                        new A.Extents { Cx = width, Cy = height }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));

            tree.AppendChild(pic);
        }

        private static long ClampEmu(double value, long min, long max)
        {
            long v = (long)Math.Round(value);
            if (v < min)
            {
                return min;
            }

            if (v > max)
            {
                return max;
            }

            return v;
        }

        private sealed class SlideTextBlock
        {
            public long X { get; set; }

            public long Y { get; set; }

            public long Width { get; set; }

            public long Height { get; set; }

            public string Text { get; set; }

            public int FontSizeHundredths { get; set; }

            public bool Bold { get; set; }
        }
    }
}
