// <copyright file="ConversionJob_PdfRotate.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System.Collections.Generic;
    using System.IO;

    using FileConverter.Services;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    public class ConversionJob_PdfRotate : ConversionJob
    {
        public ConversionJob_PdfRotate(ConversionPreset conversionPreset, string inputFilePath)
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

            int angle = 90;
            string rawAngle = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfRotationAngle);
            if (int.TryParse(rawAngle, out int parsed))
            {
                angle = parsed;
            }

            string rangeSpec = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange);

            using (PdfDocument doc = PdfReader.Open(this.InputFilePath, PdfDocumentOpenMode.Modify))
            {
                List<int> targetPages = PageRangeParser.ParseZeroBasedPageIndices(rangeSpec, doc.PageCount);
                for (int i = 0; i < targetPages.Count; i++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    PdfPage page = doc.Pages[targetPages[i]];
                    page.Rotate = (((page.Rotate + angle) % 360) + 360) % 360;
                    this.Progress = (float)(i + 1) / targetPages.Count;
                }

                doc.Save(this.OutputFilePath);
            }
        }
    }
}
