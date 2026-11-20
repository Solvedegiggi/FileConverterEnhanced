// <copyright file="ConversionJob_ImageToPdf.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using FileConverter.Services;

    using ImageMagick;

    using PdfSharp.Drawing;
    using PdfSharp.Pdf;

    public class ConversionJob_ImageToPdf : ConversionJob
    {
        public ConversionJob_ImageToPdf(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        public ConversionJob_ImageToPdf(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
            : base(conversionPreset, inputFilePaths)
        {
        }

        protected override bool ValidateInputExtension => false;

        protected override void Convert()
        {
            WindowsFontResolver.EnsureInitialized();
            this.UserState = Properties.Resources.ConversionStateConversion;

            if (this.InputFilePaths == null || this.InputFilePaths.Count == 0)
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                return;
            }

            string pageSize = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageSize) ?? "A4";
            string orientation = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageOrientation) ?? "Auto";
            string marginSetting = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageMargin) ?? "Small";

            double marginPt = 28.35; // Small = 10mm
            if (string.Equals(marginSetting, "None", StringComparison.OrdinalIgnoreCase))
            {
                marginPt = 0;
            }
            else if (string.Equals(marginSetting, "Big", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(marginSetting, "Large", StringComparison.OrdinalIgnoreCase))
            {
                marginPt = 70.87; // 25mm
            }

            using (PdfDocument document = new PdfDocument())
            {
                int total = this.InputFilePaths.Count;
                for (int i = 0; i < total; i++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    string path = this.InputFilePaths[i];
                    if (!File.Exists(path))
                    {
                        this.ConversionFailed($"File not found: {path}");
                        return;
                    }

                    using (MemoryStream imgStream = LoadAsPdfSharpCompatibleStream(path))
                    using (XImage xImg = XImage.FromStream(imgStream))
                    {
                        double imgW = xImg.PointWidth > 0 ? xImg.PointWidth : xImg.PixelWidth * 72.0 / 96.0;
                        double imgH = xImg.PointHeight > 0 ? xImg.PointHeight : xImg.PixelHeight * 72.0 / 96.0;

                        var (pageW, pageH) = ComputePageDimensionsPt(pageSize, orientation, imgW, imgH, marginPt);

                        PdfPage page = document.AddPage();
                        page.Width = XUnit.FromPoint(pageW);
                        page.Height = XUnit.FromPoint(pageH);

                        double availW = Math.Max(10, pageW - (2 * marginPt));
                        double availH = Math.Max(10, pageH - (2 * marginPt));
                        double scale = Math.Min(availW / imgW, availH / imgH);

                        double drawW = imgW * scale;
                        double drawH = imgH * scale;
                        double drawX = marginPt + ((availW - drawW) / 2.0);
                        double drawY = marginPt + ((availH - drawH) / 2.0);

                        using (XGraphics gfx = XGraphics.FromPdfPage(page))
                        {
                            gfx.DrawImage(xImg, drawX, drawY, drawW, drawH);
                        }
                    }

                    this.Progress = (float)(i + 1) / total;
                }

                document.Save(this.OutputFilePath);
            }
        }

        private static (double WidthPt, double HeightPt) ComputePageDimensionsPt(
            string pageSize,
            string orientation,
            double imgW,
            double imgH,
            double marginPt)
        {
            if (string.Equals(pageSize, "Fit", StringComparison.OrdinalIgnoreCase))
            {
                return (imgW + (2 * marginPt), imgH + (2 * marginPt));
            }

            double w = 595.28; // A4
            double h = 841.89;
            if (string.Equals(pageSize, "Letter", StringComparison.OrdinalIgnoreCase))
            {
                w = 612.0;
                h = 792.0;
            }

            bool useLandscape = false;
            if (string.Equals(orientation, "Landscape", StringComparison.OrdinalIgnoreCase))
            {
                useLandscape = true;
            }
            else if (string.Equals(orientation, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                useLandscape = imgW > imgH;
            }

            return useLandscape ? (Math.Max(w, h), Math.Min(w, h)) : (Math.Min(w, h), Math.Max(w, h));
        }

        private static MemoryStream LoadAsPdfSharpCompatibleStream(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif")
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
    }
}
