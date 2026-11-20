// <copyright file="ConversionJob_PdfCrop.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using FileConverter.Services;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    public sealed class PdfCropSettings
    {
        public double LeftMarginPt { get; set; }

        public double TopMarginPt { get; set; }

        public double RightMarginPt { get; set; }

        public double BottomMarginPt { get; set; }

        public string Scope { get; set; } = "AllPages"; // "AllPages", "CurrentPage", "CustomRange"

        public int CurrentPageIndex { get; set; }

        public string PageRange { get; set; } = string.Empty;
    }

    public class ConversionJob_PdfCrop : ConversionJob
    {
        private readonly PdfCropSettings cropSettings;

        public ConversionJob_PdfCrop(ConversionPreset conversionPreset, string inputFilePath, PdfCropSettings cropSettings)
            : base(conversionPreset, inputFilePath)
        {
            this.cropSettings = cropSettings ?? throw new ArgumentNullException(nameof(cropSettings));
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

            using (PdfDocument doc = PdfReader.Open(this.InputFilePath, PdfDocumentOpenMode.Modify))
            {
                int pageCount = doc.PageCount;
                List<int> targetPages;

                if (string.Equals(this.cropSettings.Scope, "CurrentPage", StringComparison.OrdinalIgnoreCase))
                {
                    int clamped = Math.Max(0, Math.Min(pageCount - 1, this.cropSettings.CurrentPageIndex));
                    targetPages = new List<int> { clamped };
                }
                else if (string.Equals(this.cropSettings.Scope, "CustomRange", StringComparison.OrdinalIgnoreCase))
                {
                    targetPages = PageRangeParser.ParseZeroBasedPageIndices(this.cropSettings.PageRange, pageCount);
                }
                else
                {
                    targetPages = PageRangeParser.ParseZeroBasedPageIndices(string.Empty, pageCount);
                }

                for (int i = 0; i < targetPages.Count; i++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    PdfPage page = doc.Pages[targetPages[i]];
                    PdfRectangle baseBox = page.CropBox.Width > 1 && page.CropBox.Height > 1
                        ? page.CropBox
                        : page.MediaBox;

                    int rot = ((page.Rotate % 360) + 360) % 360;
                    double l = Math.Max(0, this.cropSettings.LeftMarginPt);
                    double t = Math.Max(0, this.cropSettings.TopMarginPt);
                    double r = Math.Max(0, this.cropSettings.RightMarginPt);
                    double b = Math.Max(0, this.cropSettings.BottomMarginPt);

                    double lPdf;
                    double bPdf;
                    double rPdf;
                    double tPdf;

                    if (rot == 90)
                    {
                        lPdf = t;
                        tPdf = r;
                        rPdf = b;
                        bPdf = l;
                    }
                    else if (rot == 180)
                    {
                        lPdf = r;
                        bPdf = t;
                        rPdf = l;
                        tPdf = b;
                    }
                    else if (rot == 270)
                    {
                        lPdf = b;
                        bPdf = r;
                        rPdf = t;
                        tPdf = l;
                    }
                    else
                    {
                        lPdf = l;
                        bPdf = b;
                        rPdf = r;
                        tPdf = t;
                    }

                    double x1 = baseBox.X1 + lPdf;
                    double y1 = baseBox.Y1 + bPdf;
                    double x2 = baseBox.X2 - rPdf;
                    double y2 = baseBox.Y2 - tPdf;

                    if (x2 - x1 < 20.0)
                    {
                        double mid = (baseBox.X1 + baseBox.X2) / 2.0;
                        x1 = mid - 10.0;
                        x2 = mid + 10.0;
                    }

                    if (y2 - y1 < 20.0)
                    {
                        double mid = (baseBox.Y1 + baseBox.Y2) / 2.0;
                        y1 = mid - 10.0;
                        y2 = mid + 10.0;
                    }

                    var cropped = new PdfRectangle(
                        new PdfSharp.Drawing.XPoint(x1, y1),
                        new PdfSharp.Drawing.XPoint(x2, y2));

                    page.CropBox = cropped;
                    page.TrimBox = cropped;

                    this.Progress = (float)(i + 1) / Math.Max(1, targetPages.Count);
                }

                doc.Save(this.OutputFilePath);
            }
        }
    }
}
