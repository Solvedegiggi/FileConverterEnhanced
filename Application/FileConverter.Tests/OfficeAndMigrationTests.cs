namespace FileConverter.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Serialization;

    using DocumentFormat.OpenXml.Packaging;

    using FileConverter.ConversionJobs;
    using FileConverter.Services;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    using Xunit;

    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;
    using Spread = DocumentFormat.OpenXml.Spreadsheet;
    using Word = DocumentFormat.OpenXml.Wordprocessing;

    public class OfficeAndMigrationTests
    {
        private static void RunAndAssertSuccess(ConversionJob job, string outPath)
        {
            job.PrepareConversion(outPath);
            Assert.Equal(ConversionState.Ready, job.State);
            job.StartConversion();
            Assert.True(job.State == ConversionState.Done, $"Job failed: {job.ErrorMessage}");
            Assert.True(File.Exists(outPath), $"Missing output file: {outPath}");
        }

        [Fact]
        public void PdfToWord_CreatesValidReadableDocx()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateRichPdfForMarkdown("doc_src.pdf");
                string outDocx = fx.PathFor("output.docx");

                var preset = new ConversionPreset("ToWord", OutputType.Docx, new[] { "pdf" });
                RunAndAssertSuccess(new ConversionJob_PdfToWord(preset, src), outDocx);

                using (WordprocessingDocument docx = WordprocessingDocument.Open(outDocx, false))
                {
                    string bodyText = docx.MainDocumentPart.Document.Body.InnerText;
                    Assert.Contains("Annual Report", bodyText);
                    Assert.Contains("Revenue", bodyText);
                }
            }
        }

        [Fact]
        public void PdfToPowerPoint_CreatesValid16x9Pptx()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateSimplePdf("deck_src.pdf", "Slide One Heading", "Slide Two Content");
                string outPptx = fx.PathFor("slides.pptx");

                var preset = new ConversionPreset("ToPptx", OutputType.Pptx, new[] { "pdf" });
                RunAndAssertSuccess(new ConversionJob_PdfToPowerPoint(preset, src), outPptx);

                using (PresentationDocument pptx = PresentationDocument.Open(outPptx, false))
                {
                    Assert.Equal(2, pptx.PresentationPart.SlideParts.Count());
                    string slide1Text = pptx.PresentationPart.SlideParts.First().Slide.InnerText;
                    Assert.Contains("Slide", slide1Text);
                }
            }
        }

        [Fact]
        public void PdfToExcel_ClustersColumnsIntoValidXlsx()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateRichPdfForMarkdown("sheet_src.pdf");
                string outXlsx = fx.PathFor("sheet.xlsx");

                var preset = new ConversionPreset("ToXlsx", OutputType.Xlsx, new[] { "pdf" });
                RunAndAssertSuccess(new ConversionJob_PdfToExcel(preset, src), outXlsx);

                using (SpreadsheetDocument xlsx = SpreadsheetDocument.Open(outXlsx, false))
                {
                    WorksheetPart ws = xlsx.WorkbookPart.WorksheetParts.First();
                    string allText = ws.Worksheet.InnerText;
                    Assert.Contains("Metric", allText);
                    Assert.Contains("4200", allText);
                    Assert.Contains(ws.Worksheet.Descendants<Spread.Cell>(), c =>
                        c.DataType != null && c.DataType.Value == Spread.CellValues.Number);
                }
            }
        }

        [Fact]
        public void NativeOfficeToPdf_ConvertsDocxPptxAndWideXlsxWithLandscapeFitColumns()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string docx = fx.CreateSampleDocx("input.docx", "Docx Heading", "Paragraph sample content");
                string docxPdf = fx.PathFor("docx_out.pdf");
                NativeOfficeToPdfConverter.ConvertDocxToPdf(docx, docxPdf);
                using (PdfPigDoc p = PdfPigDoc.Open(docxPdf))
                {
                    Assert.Contains("Docx Heading", p.GetPage(1).Text);
                }

                string pptx = fx.CreateSamplePptx("input.pptx", "Quarter Review Title");
                string pptxPdf = fx.PathFor("pptx_out.pdf");
                NativeOfficeToPdfConverter.ConvertPptxToPdf(pptx, pptxPdf);
                using (PdfPigDoc p = PdfPigDoc.Open(pptxPdf))
                {
                    Assert.Contains("Quarter Review Title", p.GetPage(1).Text);
                }

                string wideXlsx = fx.CreateSampleWideXlsx("wide.xlsx", cols: 12, rows: 5);
                string landscapePdf = fx.PathFor("xlsx_landscape_fit.pdf");
                NativeOfficeToPdfConverter.ConvertXlsxToPdf(wideXlsx, landscapePdf, "Landscape", fitColumnsToPage: true);

                using (PdfDocument pdfDoc = PdfReader.Open(landscapePdf, PdfDocumentOpenMode.Import))
                using (PdfPigDoc pigDoc = PdfPigDoc.Open(landscapePdf))
                {
                    Assert.True(pdfDoc.Pages[0].Width.Point > pdfDoc.Pages[0].Height.Point);
                    string pageText = pigDoc.GetPage(1).Text;
                    Assert.Contains("HeaderCol1", pageText);
                    Assert.Contains("HeaderCol12", pageText);
                }
            }
        }

        [Fact]
        public void SettingsMigration_V4ToV5_PreservesUserPresetsAndAddsNewDefaults()
        {
            string xmlV4 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Settings xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" SerializationVersion=""4"">
  <MaximumNumberOfSimultaneousConversions>4</MaximumNumberOfSimultaneousConversions>
  <ConversionPreset Name=""My Custom Preset"" OutputType=""Mp4"" IsDefaultSettings=""false"">
    <InputTypes>mov</InputTypes>
    <InputPostConversionAction>None</InputPostConversionAction>
    <OutputFileNameTemplate>(p)(f)_custom</OutputFileNameTemplate>
  </ConversionPreset>
  <CheckUpgradeAtStartup>true</CheckUpgradeAtStartup>
</Settings>";

            var serializer = new XmlSerializer(typeof(Settings));
            Settings v4Settings;
            using (var sr = new StringReader(xmlV4))
            {
                v4Settings = (Settings)serializer.Deserialize(sr);
            }

            Assert.Equal(4, v4Settings.SerializationVersion);
            Assert.NotNull(v4Settings.GetPresetFromName("My Custom Preset"));
            Assert.Equal(4, v4Settings.MaximumNumberOfSimultaneousConversions);

            v4Settings.OnDeserializationComplete();
            Assert.Equal("Light", v4Settings.AppTheme);
            Assert.Equal("ita+eng", v4Settings.OcrDefaultLanguages);
            Assert.Contains("MergePdf", v4Settings.PinnedQuickToolIds);
        }

        [Fact]
        public void HelpersAndPageRangeParser_ValidateCompatibilityAndRanges()
        {
            Assert.True(Helpers.IsOutputTypeCompatibleWithExtension(OutputType.Docx, "pdf"));
            Assert.True(Helpers.IsOutputTypeCompatibleWithExtension(OutputType.Pptx, "pdf"));
            Assert.True(Helpers.IsOutputTypeCompatibleWithExtension(OutputType.Xlsx, "pdf"));
            Assert.True(Helpers.IsOutputTypeCompatibleWithExtension(OutputType.Md, "pdf"));
            Assert.True(Helpers.IsOutputTypeCompatibleWithExtension(OutputType.Pdf, "pdf"));
            Assert.True(Helpers.IsOutputTypeCompatibleWithExtension(OutputType.Pdf, "html"));
            Assert.False(Helpers.IsOutputTypeCompatibleWithExtension(OutputType.Docx, "mp3"));

            List<int> parsed = PageRangeParser.ParseZeroBasedPageIndices("1-2, 5, 8-end", 10);
            Assert.Equal(new[] { 0, 1, 4, 7, 8, 9 }, parsed);
        }

        [Fact]
        public void SettingsDefaultXml_DeserializesCleanlyWithVersion5()
        {
            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(baseDir, "Settings.default.xml");
            if (!File.Exists(candidate))
            {
                candidate = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\FileConverter\Settings.default.xml"));
            }

            Assert.True(File.Exists(candidate), $"Settings.default.xml not found at {candidate}");

            var serializer = new XmlSerializer(typeof(Settings));
            Settings defaults;
            using (var sr = new StreamReader(candidate))
            {
                defaults = (Settings)serializer.Deserialize(sr);
            }

            Assert.Equal(5, defaults.SerializationVersion);
            defaults.OnDeserializationComplete();
            Assert.Equal("Light", defaults.AppTheme);
            Assert.Equal("ita+eng", defaults.OcrDefaultLanguages);
            Assert.NotEmpty(defaults.PinnedQuickToolIds);
            Assert.NotNull(defaults.GetPresetFromName("To Word (DOCX)"));
            Assert.NotNull(defaults.GetPresetFromName("Compress PDF"));
            Assert.NotNull(defaults.GetPresetFromName("OCR PDF"));
            Assert.NotNull(defaults.GetPresetFromName("To Markdown"));
        }
    }
}
