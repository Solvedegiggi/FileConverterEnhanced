// <copyright file="ConversionJob_PdfToImage.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;

    using FileConverter.Services;

    using ImageMagick;

    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;
    using UglyToad.PdfPig.Content;

    public class ConversionJob_PdfToImage : ConversionJob
    {
        public ConversionJob_PdfToImage(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected override bool ValidateInputExtension => false;

        protected override int GetOutputFilesCount()
        {
            try
            {
                string mode = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfToImageMode) ?? "PagesToImages";
                string rangeSpec = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange);

                if (string.Equals(mode, "ExtractEmbeddedImages", StringComparison.OrdinalIgnoreCase))
                {
                    int totalImages = 0;
                    using (PdfPigDoc pdf = PdfPigDoc.Open(this.InputFilePath))
                    {
                        List<int> pageIndices = PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, pdf.NumberOfPages);
                        foreach (int zeroIdx in pageIndices)
                        {
                            Page page = pdf.GetPage(zeroIdx + 1);
                            totalImages += page.GetImages().Count();
                        }
                    }

                    return Math.Max(1, totalImages);
                }

                int pageCount = PdfRenderService.GetPageCount(this.InputFilePath);
                List<int> pages = PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, pageCount);
                return Math.Max(1, pages.Count);
            }
            catch
            {
                return 1;
            }
        }

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateConversion;

            if (!File.Exists(this.InputFilePath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                return;
            }

            string mode = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfToImageMode) ?? "PagesToImages";
            int dpi = ParseInt(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfRenderDpi), 200, 72, 600);
            int jpegQuality = ParseInt(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.ImageQuality), 90, 10, 100);
            string rangeSpec = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange);
            bool saveAsPng = this.ConversionPreset.OutputType == OutputType.Png;

            if (string.Equals(mode, "ExtractEmbeddedImages", StringComparison.OrdinalIgnoreCase))
            {
                this.ExtractEmbeddedImages(rangeSpec, saveAsPng, jpegQuality);
                return;
            }

            this.RenderPagesToImages(rangeSpec, dpi, saveAsPng, jpegQuality);
        }

        private void RenderPagesToImages(string rangeSpec, int dpi, bool saveAsPng, int jpegQuality)
        {
            int pageCount = PdfRenderService.GetPageCount(this.InputFilePath);
            List<int> pageIndices = PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, pageCount);
            if (pageIndices.Count == 0)
            {
                this.ConversionFailed("No pages matched the specified page range.");
                return;
            }

            for (int i = 0; i < pageIndices.Count; i++)
            {
                if (this.CancelIsRequested)
                {
                    return;
                }

                this.CurrentOutputFilePathIndex = Math.Min(i, this.OutputFilePaths.Length - 1);
                string targetPath = i < this.OutputFilePaths.Length
                    ? this.OutputFilePaths[i]
                    : GenerateFallbackPath(this.OutputFilePaths[0], i + 1);

                using (Bitmap bmp = PdfRenderService.RenderPageToDrawingBitmap(this.InputFilePath, pageIndices[i], dpi))
                {
                    if (bmp == null)
                    {
                        this.ConversionFailed($"Failed to render page {pageIndices[i] + 1}.");
                        return;
                    }

                    SaveBitmap(bmp, targetPath, saveAsPng, jpegQuality);
                }

                this.Progress = (float)(i + 1) / pageIndices.Count;
            }
        }

        private void ExtractEmbeddedImages(string rangeSpec, bool saveAsPng, int jpegQuality)
        {
            int written = 0;
            using (PdfPigDoc pdf = PdfPigDoc.Open(this.InputFilePath))
            {
                List<int> pageIndices = PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, pdf.NumberOfPages);
                var imagesToExtract = new List<IPdfImage>();
                foreach (int zeroIdx in pageIndices)
                {
                    Page page = pdf.GetPage(zeroIdx + 1);
                    imagesToExtract.AddRange(page.GetImages());
                }

                if (imagesToExtract.Count == 0)
                {
                    this.ConversionFailed("No embedded images were found inside this PDF.");
                    return;
                }

                for (int i = 0; i < imagesToExtract.Count; i++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    this.CurrentOutputFilePathIndex = Math.Min(written, this.OutputFilePaths.Length - 1);
                    string targetPath = written < this.OutputFilePaths.Length
                        ? this.OutputFilePaths[written]
                        : GenerateFallbackPath(this.OutputFilePaths[0], written + 1);

                    if (TrySavePdfImage(imagesToExtract[i], targetPath, saveAsPng, jpegQuality))
                    {
                        written++;
                    }

                    this.Progress = (float)(i + 1) / imagesToExtract.Count;
                }
            }

            if (written == 0)
            {
                this.ConversionFailed("Unable to decode any embedded images from the PDF.");
            }
        }

        private static bool TrySavePdfImage(IPdfImage pdfImg, string targetPath, bool saveAsPng, int jpegQuality)
        {
            if (pdfImg.TryGetPng(out byte[] pngBytes) && pngBytes != null && pngBytes.Length > 0)
            {
                using (var ms = new MemoryStream(pngBytes))
                using (var img = Image.FromStream(ms))
                using (var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        g.DrawImage(img, 0, 0, img.Width, img.Height);
                    }

                    SaveBitmap(bmp, targetPath, saveAsPng, jpegQuality);
                    return true;
                }
            }

            if (pdfImg.RawBytes.Length > 0)
            {
                byte[] raw = pdfImg.RawBytes.ToArray();
                try
                {
                    using (var magick = new MagickImage(raw))
                    {
                        magick.Format = saveAsPng ? MagickFormat.Png : MagickFormat.Jpg;
                        if (!saveAsPng)
                        {
                            magick.Quality = (uint)jpegQuality;
                            magick.Alpha(AlphaOption.Remove);
                            magick.BackgroundColor = MagickColors.White;
                        }

                        magick.Write(targetPath);
                        return true;
                    }
                }
                catch
                {
                    // Skip un-decodable raw stream formats such as custom masks.
                }
            }

            return false;
        }

        private static void SaveBitmap(Bitmap bmp, string targetPath, bool saveAsPng, int jpegQuality)
        {
            if (saveAsPng)
            {
                bmp.Save(targetPath, ImageFormat.Png);
                return;
            }

            ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageDecoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
            if (jpegCodec != null)
            {
                using (var encParams = new EncoderParameters(1))
                {
                    encParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)jpegQuality);
                    bmp.Save(targetPath, jpegCodec, encParams);
                    return;
                }
            }

            bmp.Save(targetPath, ImageFormat.Jpeg);
        }

        private static string GenerateFallbackPath(string baseOutputPath, int oneBasedIndex)
        {
            string dir = Path.GetDirectoryName(baseOutputPath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(baseOutputPath);
            string ext = Path.GetExtension(baseOutputPath);
            return PathHelpers.GenerateUniquePath(Path.Combine(dir, $"{name}_{oneBasedIndex:D3}{ext}"));
        }

        private static int ParseInt(string raw, int fallback, int min, int max)
        {
            if (int.TryParse(raw, out int parsed))
            {
                return Math.Max(min, Math.Min(max, parsed));
            }

            return fallback;
        }
    }
}
