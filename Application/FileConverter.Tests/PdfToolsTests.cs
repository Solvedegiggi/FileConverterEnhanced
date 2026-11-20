namespace FileConverter.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    using Xunit;

    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;

    public class PdfToolsTests
    {
        private static void RunAndAssertSuccess(ConversionJob job, params string[] explicitOutputs)
        {
            job.PrepareConversion(explicitOutputs);
            Assert.Equal(ConversionState.Ready, job.State);
            job.StartConversion();
            Assert.True(job.State == ConversionState.Done, $"Job failed: {job.ErrorMessage}");
            Assert.True(File.Exists(job.OutputFilePath), $"Missing output file: {job.OutputFilePath}");
        }

        [Fact]
        public void MergePdf_CombinesPagesAndPreservesText()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string pdfA = fx.CreateSimplePdf("a.pdf", "AlphaPage");
                string pdfB = fx.CreateSimplePdf("b.pdf", "BetaPage1", "BetaPage2");
                string outPdf = fx.PathFor("merged.pdf");

                var preset = new ConversionPreset("Merge", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Merge");

                var job = new ConversionJob_PdfMerge(preset, new[] { pdfA, pdfB });
                RunAndAssertSuccess(job, outPdf);

                using (PdfPigDoc doc = PdfPigDoc.Open(outPdf))
                {
                    Assert.Equal(3, doc.NumberOfPages);
                    Assert.Contains("AlphaPage", doc.GetPage(1).Text);
                    Assert.Contains("BetaPage2", doc.GetPage(3).Text);
                }
            }
        }

        [Fact]
        public void SplitPdf_AllPages_And_ExtractRange_Work()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateSimplePdf("multi.pdf", "FirstPage", "SecondPage", "ThirdPage");

                // SplitAllPages into subfolder
                string splitDir = fx.PathFor("split_all");
                Directory.CreateDirectory(splitDir);
                var splitPreset = new ConversionPreset("SplitAll", OutputType.Pdf, new[] { "pdf" })
                {
                    OutputFileNameTemplate = splitDir + "\\split",
                };
                splitPreset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfSplitMode, "SplitAllPages");

                var splitJob = new ConversionJob_PdfSplit(splitPreset, src);
                splitJob.PrepareConversion();
                splitJob.StartConversion();
                Assert.True(splitJob.State == ConversionState.Done, splitJob.ErrorMessage);
                Assert.Equal(3, Directory.GetFiles(splitDir, "*.pdf").Length);

                // ExtractRange ("2-3")
                string extracted = fx.PathFor("extracted.pdf");
                var extractPreset = new ConversionPreset("Extract", OutputType.Pdf, new[] { "pdf" });
                extractPreset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfSplitMode, "ExtractRange");
                extractPreset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange, "2-3");

                var extractJob = new ConversionJob_PdfSplit(extractPreset, src);
                RunAndAssertSuccess(extractJob, extracted);

                using (PdfPigDoc doc = PdfPigDoc.Open(extracted))
                {
                    Assert.Equal(2, doc.NumberOfPages);
                    Assert.Contains("SecondPage", doc.GetPage(1).Text);
                    Assert.Contains("ThirdPage", doc.GetPage(2).Text);
                }
            }
        }

        [Fact]
        public void CompressPdf_Ghostscript_ProducesValidPdf()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreatePdfWithEmbeddedImage("to_compress.pdf");
                string compressed = fx.PathFor("compressed.pdf");

                var preset = new ConversionPreset("Compress", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Compress");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfCompressionLevel, "Recommended");

                var job = new ConversionJob_PdfCompress(preset, src);
                RunAndAssertSuccess(job, compressed);
                Assert.True(PdfRenderService.GetPageCount(compressed) == 1);
            }
        }

        [Fact]
        public void RotatePdf_AppliesLossless90DegreeRotation()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateSimplePdf("rotate_src.pdf", "RotateMe");
                string outPdf = fx.PathFor("rotated.pdf");

                var preset = new ConversionPreset("Rotate", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Rotate");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfRotationAngle, "90");

                var job = new ConversionJob_PdfRotate(preset, src);
                RunAndAssertSuccess(job, outPdf);

                using (PdfDocument doc = PdfReader.Open(outPdf, PdfDocumentOpenMode.Import))
                {
                    Assert.Equal(90, doc.Pages[0].Rotate);
                }
            }
        }

        [Fact]
        public void PageNumbersPdf_AddsNumbersExtractableByPdfPig()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateSimplePdf("num_src.pdf", "BodyOne", "BodyTwo");
                string outPdf = fx.PathFor("numbered.pdf");

                var preset = new ConversionPreset("PageNumbers", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "PageNumbers");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberPosition, "BottomCenter");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberFormat, "{n} / {total}");

                var job = new ConversionJob_PdfPageNumbers(preset, src);
                RunAndAssertSuccess(job, outPdf);

                using (PdfPigDoc doc = PdfPigDoc.Open(outPdf))
                {
                    Assert.Contains("1 / 2", doc.GetPage(1).Text);
                    Assert.Contains("2 / 2", doc.GetPage(2).Text);
                }
            }
        }

        [Fact]
        public void PdfToImage_PagesAndEmbeddedImages_Work()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreatePdfWithEmbeddedImage("img_src.pdf");

                string renderedJpg = fx.PathFor("page1.jpg");
                var renderPreset = new ConversionPreset("PdfToJpg", OutputType.Jpg, new[] { "pdf" });
                renderPreset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfToImageMode, "PagesToImages");
                RunAndAssertSuccess(new ConversionJob_PdfToImage(renderPreset, src), renderedJpg);
                Assert.True(new FileInfo(renderedJpg).Length > 500);

                string extractedJpg = fx.PathFor("embedded.jpg");
                var extractPreset = new ConversionPreset("ExtractImg", OutputType.Jpg, new[] { "pdf" });
                extractPreset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfToImageMode, "ExtractEmbeddedImages");
                RunAndAssertSuccess(new ConversionJob_PdfToImage(extractPreset, src), extractedJpg);
                Assert.True(new FileInfo(extractedJpg).Length > 200);
            }
        }

        [Fact]
        public void ImageToPdf_MergesMultipleImagesWithOrientationAndSize()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string img1 = fx.CreateSolidBitmapPng("img1.png", 300, 200, Color.Crimson, null);
                string img2 = fx.CreateSolidBitmapPng("img2.png", 300, 200, Color.ForestGreen, null);
                string outPdf = fx.PathFor("images_merged.pdf");

                var preset = new ConversionPreset("ImgToPdf", OutputType.Pdf, new[] { "png" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageSize, "A4");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageOrientation, "Landscape");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageMargin, "Small");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.ImageToPdfMerge, "True");

                RunAndAssertSuccess(new ConversionJob_ImageToPdf(preset, new[] { img1, img2 }), outPdf);

                using (PdfDocument doc = PdfReader.Open(outPdf, PdfDocumentOpenMode.Import))
                {
                    Assert.Equal(2, doc.PageCount);
                    Assert.True(doc.Pages[0].Width.Point > doc.Pages[0].Height.Point);
                }
            }
        }

        [Fact]
        public void OcrPdf_OfflineTesseract_AddsSearchableTextToScannedPdf()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string scannedPdf = fx.CreateScannedImageOnlyPdf("scanned.pdf", "INVOICE 2026");
                using (PdfPigDoc before = PdfPigDoc.Open(scannedPdf))
                {
                    Assert.DoesNotContain("INVOICE", before.GetPage(1).Text);
                }

                string ocrPdf = fx.PathFor("scanned_ocr.pdf");
                var preset = new ConversionPreset("OcrPdf", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Ocr");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.OcrLanguages, "eng+ita");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.OcrSkipTextPages, "False");

                RunAndAssertSuccess(new ConversionJob_PdfOcr(preset, scannedPdf), ocrPdf);

                using (PdfPigDoc after = PdfPigDoc.Open(ocrPdf))
                {
                    string text = after.GetPage(1).Text;
                    Assert.Contains("INVOICE", text, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("2026", text);
                }
            }
        }

        [Fact]
        public void PdfToMarkdown_ProducesHeadingsListsAndTables()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateRichPdfForMarkdown("rich.pdf");
                string outMd = fx.PathFor("rich.md");

                var preset = new ConversionPreset("PdfToMd", OutputType.Md, new[] { "pdf" });
                RunAndAssertSuccess(new ConversionJob_PdfToMarkdown(preset, src), outMd);

                string md = File.ReadAllText(outMd);
                Assert.Contains("# Annual Report", md);
                Assert.Contains("- First bullet item", md);
                Assert.Contains("| Metric | Amount |", md);
                Assert.Contains("| Revenue | 4200 |", md);
            }
        }

        [Fact]
        public void OrganizePdf_ReordersDuplicatesAndRotates()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateSimplePdf("org_src.pdf", "PageAlpha", "PageBeta");
                string outPdf = fx.PathFor("organized.pdf");

                var specs = new List<OrganizePageSpec>
                {
                    new OrganizePageSpec { SourceFilePath = src, SourcePageIndex = 1, RotationDelta = 90 },
                    new OrganizePageSpec { SourceFilePath = src, SourcePageIndex = 0, RotationDelta = 0 },
                    new OrganizePageSpec { SourceFilePath = src, SourcePageIndex = 0, RotationDelta = 180 },
                };

                var preset = new ConversionPreset("Org", OutputType.Pdf, new[] { "pdf" });
                RunAndAssertSuccess(new ConversionJob_PdfOrganize(preset, src, specs), outPdf);

                using (PdfPigDoc pig = PdfPigDoc.Open(outPdf))
                using (PdfDocument sharp = PdfReader.Open(outPdf, PdfDocumentOpenMode.Import))
                {
                    Assert.Equal(3, pig.NumberOfPages);
                    Assert.Contains("PageBeta", pig.GetPage(1).Text);
                    Assert.Contains("PageAlpha", pig.GetPage(2).Text);
                    Assert.Equal(90, sharp.Pages[0].Rotate);
                    Assert.Equal(180, sharp.Pages[2].Rotate);
                }
            }
        }

        [Fact]
        public void CropPdf_ReducesCropBoxLosslessly()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateSimplePdf("crop_src.pdf", "MarginCrop");
                string outPdf = fx.PathFor("cropped.pdf");

                double origW;
                double origH;
                using (PdfDocument doc = PdfReader.Open(src, PdfDocumentOpenMode.Import))
                {
                    origW = doc.Pages[0].Width.Point;
                    origH = doc.Pages[0].Height.Point;
                }

                var settings = new PdfCropSettings
                {
                    LeftMarginPt = 40,
                    TopMarginPt = 50,
                    RightMarginPt = 30,
                    BottomMarginPt = 20,
                    Scope = "AllPages",
                };

                var preset = new ConversionPreset("Crop", OutputType.Pdf, new[] { "pdf" });
                RunAndAssertSuccess(new ConversionJob_PdfCrop(preset, src, settings), outPdf);

                using (PdfDocument outDoc = PdfReader.Open(outPdf, PdfDocumentOpenMode.Import))
                {
                    PdfRectangle crop = outDoc.Pages[0].CropBox;
                    Assert.Equal(origW - 70, crop.Width, 1);
                    Assert.Equal(origH - 70, crop.Height, 1);
                }
            }
        }

        [Fact]
        public void EditPdf_OverlaysAndRedactsExistingTextInPlace()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string src = fx.CreateSimplePdf("edit_src.pdf", "CONFIDENTIAL DRAFT");
                List<PdfTextBlockInfo> blocks = PdfTextBlockService.ExtractPageTextBlocks(src, 0);
                Assert.NotEmpty(blocks);
                PdfTextBlockInfo targetBlock = blocks[0];

                var ops = new List<PdfEditOperation>
                {
                    new PdfEditOperation
                    {
                        PageIndex = 0,
                        Kind = PdfEditOperationKind.RedactAndReplaceText,
                        OriginalText = targetBlock.Text,
                        Text = "APPROVED FINAL",
                        FontFamily = "Arial",
                        FontSizePt = 16,
                        ColorHex = "#111827",
                        BackgroundColorHex = "#FFFFFF",
                        PdfLeft = targetBlock.PdfLeft,
                        PdfBottom = targetBlock.PdfBottom,
                        PdfRight = targetBlock.PdfRight,
                        PdfTop = targetBlock.PdfTop,
                        NormX = targetBlock.NormX,
                        NormY = targetBlock.NormY,
                        NormWidth = targetBlock.NormWidth,
                        NormHeight = targetBlock.NormHeight,
                    },
                    new PdfEditOperation
                    {
                        PageIndex = 0,
                        Kind = PdfEditOperationKind.AddText,
                        Text = "Watermark Note",
                        NormX = 0.1,
                        NormY = 0.5,
                        FontFamily = "Arial",
                        FontSizePt = 14,
                        ColorHex = "#DC2626",
                    },
                    new PdfEditOperation
                    {
                        PageIndex = 0,
                        Kind = PdfEditOperationKind.Highlight,
                        NormX = 0.1,
                        NormY = 0.45,
                        NormWidth = 0.3,
                        NormHeight = 0.05,
                        ColorHex = "#FACC15",
                    },
                };

                string outPdf = fx.PathFor("edited.pdf");
                var preset = new ConversionPreset("Edit", OutputType.Pdf, new[] { "pdf" });
                RunAndAssertSuccess(new ConversionJob_PdfEdit(preset, src, ops), outPdf);

                using (PdfPigDoc edited = PdfPigDoc.Open(outPdf))
                {
                    string text = edited.GetPage(1).Text;
                    Assert.DoesNotContain("CONFIDENTIAL", text);
                    Assert.Contains("APPROVED FINAL", text);
                    Assert.Contains("Watermark Note", text);
                }
            }
        }

        [Fact]
        public void ComparePdf_IdenticalVsModifiedDiff()
        {
            using (var fx = new TempWorkspaceFixture())
            {
                string pdfA = fx.CreateSimplePdf("cmpA.pdf", "Version One Line");
                string pdfSame = fx.CreateSimplePdf("cmpSame.pdf", "Version One Line");
                string pdfB = fx.CreateSimplePdf("cmpB.pdf", "Version Two Modified");

                PdfPageCompareResult sameRes = PdfCompareService.ComparePages(pdfA, pdfSame, 0, 100);
                Assert.Equal(0.0, sameRes.ChangedPixelPercent);
                Assert.Equal(0, sameRes.AddedLinesCount);
                Assert.Equal(0, sameRes.RemovedLinesCount);

                PdfPageCompareResult diffRes = PdfCompareService.ComparePages(pdfA, pdfB, 0, 100);
                Assert.True(diffRes.ChangedPixelPercent > 0.0);
                Assert.True(diffRes.AddedLinesCount >= 1);
                Assert.True(diffRes.RemovedLinesCount >= 1);

                string mdReport = fx.PathFor("diff.md");
                PdfCompareService.ExportMarkdownReport(pdfA, pdfB, mdReport);
                Assert.True(File.Exists(mdReport));
                Assert.Contains("# PDF Comparison Report", File.ReadAllText(mdReport));
            }
        }
    }
}
