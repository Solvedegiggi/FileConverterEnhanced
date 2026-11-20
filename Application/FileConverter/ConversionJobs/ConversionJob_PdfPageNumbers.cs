// <copyright file="ConversionJob_PdfPageNumbers.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using FileConverter.Services;

    using PdfSharp.Drawing;
    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    public class ConversionJob_PdfPageNumbers : ConversionJob
    {
        public ConversionJob_PdfPageNumbers(ConversionPreset conversionPreset, string inputFilePath)
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

            string position = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberPosition) ?? "BottomCenter";
            string format = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberFormat) ?? "{n} / {total}";
            string fontFamily = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberFont) ?? "Arial";
            double fontSize = ParseDouble(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberFontSize), 11.0, 6.0, 72.0);
            XColor color = ParseColor(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberColor));
            int startPage = Math.Max(1, ParseInt(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberStartPage), 1));
            int startNumber = ParseInt(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberStartNumber), 1);
            double marginMm = ParseDouble(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberMargin), 12.0, 2.0, 60.0);
            string rangeSpec = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange);

            double marginPt = marginMm * 72.0 / 25.4;

            using (PdfDocument doc = PdfReader.Open(this.InputFilePath, PdfDocumentOpenMode.Modify))
            {
                int totalPages = doc.PageCount;
                HashSet<int> rangeFilter = string.IsNullOrWhiteSpace(rangeSpec)
                    ? null
                    : new HashSet<int>(PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, totalPages));

                var font = new XFont(fontFamily, fontSize, XFontStyleEx.Regular);
                var brush = new XSolidBrush(color);

                for (int i = 0; i < totalPages; i++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    int oneBasedPage = i + 1;
                    if (oneBasedPage < startPage)
                    {
                        this.Progress = (float)(i + 1) / totalPages;
                        continue;
                    }

                    if (rangeFilter != null && !rangeFilter.Contains(i))
                    {
                        this.Progress = (float)(i + 1) / totalPages;
                        continue;
                    }

                    int displayNum = startNumber + (oneBasedPage - startPage);
                    int displayTotal = startNumber + (totalPages - startPage);
                    string label = format
                        .Replace("{n}", displayNum.ToString(CultureInfo.InvariantCulture))
                        .Replace("{total}", displayTotal.ToString(CultureInfo.InvariantCulture));

                    PdfPage page = doc.Pages[i];
                    using (XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                    {
                        double w = page.Width.Point;
                        double h = page.Height.Point;

                        bool isTop = position.StartsWith("Top", StringComparison.OrdinalIgnoreCase);
                        double y = isTop ? marginPt : Math.Max(marginPt, h - marginPt);

                        double x;
                        XStringFormat stringFormat = new XStringFormat
                        {
                            LineAlignment = isTop ? XLineAlignment.Near : XLineAlignment.Far,
                        };

                        if (position.EndsWith("Left", StringComparison.OrdinalIgnoreCase))
                        {
                            x = marginPt;
                            stringFormat.Alignment = XStringAlignment.Near;
                        }
                        else if (position.EndsWith("Right", StringComparison.OrdinalIgnoreCase))
                        {
                            x = Math.Max(marginPt, w - marginPt);
                            stringFormat.Alignment = XStringAlignment.Far;
                        }
                        else
                        {
                            x = w / 2.0;
                            stringFormat.Alignment = XStringAlignment.Center;
                        }

                        gfx.DrawString(label, font, brush, new XPoint(x, y), stringFormat);
                    }

                    this.Progress = (float)(i + 1) / totalPages;
                }

                doc.Save(this.OutputFilePath);
            }
        }

        private static double ParseDouble(string raw, double fallback, double min, double max)
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                return Math.Max(min, Math.Min(max, val));
            }

            return fallback;
        }

        private static int ParseInt(string raw, int fallback)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int val) ? val : fallback;
        }

        private static XColor ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return XColor.FromArgb(51, 51, 51);
            }

            string clean = hex.Trim().TrimStart('#');
            if (clean.Length == 6 && int.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                byte r = (byte)((rgb >> 16) & 0xFF);
                byte g = (byte)((rgb >> 8) & 0xFF);
                byte b = (byte)(rgb & 0xFF);
                return XColor.FromArgb(r, g, b);
            }

            return XColor.FromArgb(51, 51, 51);
        }
    }
}
