// <copyright file="ConversionJob_PdfMerge.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using FileConverter.Services;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    public class ConversionJob_PdfMerge : ConversionJob
    {
        public ConversionJob_PdfMerge(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        public ConversionJob_PdfMerge(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
            : base(conversionPreset, inputFilePaths)
        {
        }

        protected override bool ValidateInputExtension => false;

        protected override void Initialize()
        {
            base.Initialize();

            if (this.InputFilePaths == null || this.InputFilePaths.Count == 0)
            {
                this.ConversionFailed(Properties.Resources.ErrorInputTypeIncompatibleWithOutputType);
                return;
            }

            foreach (string path in this.InputFilePaths)
            {
                if (!File.Exists(path))
                {
                    this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                    return;
                }
            }
        }

        protected override void Convert()
        {
            WindowsFontResolver.EnsureInitialized();
            this.UserState = Properties.Resources.ConversionStateConversion;

            using (PdfDocument outputDocument = new PdfDocument())
            {
                int totalFiles = this.InputFilePaths.Count;
                for (int fileIndex = 0; fileIndex < totalFiles; fileIndex++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    string inputPath = this.InputFilePaths[fileIndex];
                    using (PdfDocument inputDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import))
                    {
                        int pageCount = inputDocument.PageCount;
                        for (int p = 0; p < pageCount; p++)
                        {
                            if (this.CancelIsRequested)
                            {
                                return;
                            }

                            outputDocument.AddPage(inputDocument.Pages[p]);
                        }
                    }

                    this.Progress = (float)(fileIndex + 1) / totalFiles;
                }

                outputDocument.Save(this.OutputFilePath);
            }
        }
    }
}
