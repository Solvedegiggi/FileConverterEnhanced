// <copyright file="ConversionJob_PdfOrganize.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using FileConverter.Services;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    public sealed class OrganizePageSpec
    {
        public string SourceFilePath { get; set; }

        public int SourcePageIndex { get; set; }

        public int RotationDelta { get; set; }
    }

    public class ConversionJob_PdfOrganize : ConversionJob
    {
        private readonly IReadOnlyList<OrganizePageSpec> pageSpecs;

        public ConversionJob_PdfOrganize(
            ConversionPreset conversionPreset,
            string primaryFilePath,
            IReadOnlyList<OrganizePageSpec> pageSpecs)
            : base(conversionPreset, primaryFilePath)
        {
            this.pageSpecs = pageSpecs ?? throw new ArgumentNullException(nameof(pageSpecs));
        }

        protected override bool ValidateInputExtension => false;

        protected override void Initialize()
        {
            base.Initialize();

            if (this.pageSpecs == null || this.pageSpecs.Count == 0)
            {
                this.ConversionFailed("No pages selected to save.");
                return;
            }
        }

        protected override void Convert()
        {
            WindowsFontResolver.EnsureInitialized();
            this.UserState = Properties.Resources.ConversionStateConversion;

            var openedDocs = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var outputDoc = new PdfDocument())
                {
                    int total = this.pageSpecs.Count;
                    for (int i = 0; i < total; i++)
                    {
                        if (this.CancelIsRequested)
                        {
                            return;
                        }

                        OrganizePageSpec spec = this.pageSpecs[i];
                        if (string.IsNullOrWhiteSpace(spec.SourceFilePath) || !File.Exists(spec.SourceFilePath))
                        {
                            this.ConversionFailed($"Source PDF not found: {spec.SourceFilePath}");
                            return;
                        }

                        if (!openedDocs.TryGetValue(spec.SourceFilePath, out PdfDocument srcDoc))
                        {
                            srcDoc = PdfReader.Open(spec.SourceFilePath, PdfDocumentOpenMode.Import);
                            openedDocs[spec.SourceFilePath] = srcDoc;
                        }

                        if (spec.SourcePageIndex < 0 || spec.SourcePageIndex >= srcDoc.PageCount)
                        {
                            continue;
                        }

                        PdfPage added = outputDoc.AddPage(srcDoc.Pages[spec.SourcePageIndex]);
                        if (spec.RotationDelta != 0)
                        {
                            added.Rotate = (((added.Rotate + spec.RotationDelta) % 360) + 360) % 360;
                        }

                        this.Progress = (float)(i + 1) / total;
                    }

                    if (outputDoc.PageCount == 0)
                    {
                        this.ConversionFailed("Output PDF would contain zero pages.");
                        return;
                    }

                    outputDoc.Save(this.OutputFilePath);
                }
            }
            finally
            {
                foreach (PdfDocument doc in openedDocs.Values)
                {
                    try
                    {
                        doc.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
