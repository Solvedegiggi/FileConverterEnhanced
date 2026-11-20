namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;

    public class ImagesToPdfWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private bool mergeIntoSinglePdf = true;
        private string pageSize = "A4";
        private string orientation = "Auto";
        private string margin = "Small";

        public ImagesToPdfWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
            this.OutputFileName = "images_merged";
        }

        public string[] PageSizes { get; } = { "A4", "Letter", "Fit" };

        public string[] Orientations { get; } = { "Auto", "Portrait", "Landscape" };

        public string[] Margins { get; } = { "None", "Small", "Big" };

        public bool MergeIntoSinglePdf
        {
            get => this.mergeIntoSinglePdf;
            set => this.SetProperty(ref this.mergeIntoSinglePdf, value);
        }

        public string PageSize
        {
            get => this.pageSize;
            set => this.SetProperty(ref this.pageSize, value);
        }

        public string Orientation
        {
            get => this.orientation;
            set => this.SetProperty(ref this.orientation, value);
        }

        public string Margin
        {
            get => this.margin;
            set => this.SetProperty(ref this.margin, value);
        }

        protected override void Execute()
        {
            var preset = new ConversionPreset("ImagesToPdf", OutputType.Pdf, new[] { "jpg", "jpeg", "png", "bmp", "webp", "tif", "tiff" });
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageSize, this.PageSize ?? "A4");
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageOrientation, this.Orientation ?? "Auto");
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageMargin, this.Margin ?? "Small");
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.ImageToPdfMerge, this.MergeIntoSinglePdf ? "True" : "False");

            if (this.MergeIntoSinglePdf && this.InputFiles.Count > 1)
            {
                this.ApplyOutputTemplate(preset, this.OutputFileName);
                this.QueueAndRun(new ConversionJob_ImageToPdf(preset, this.InputFiles.ToList()));
                return;
            }

            this.ApplyOutputTemplate(preset);
            var jobs = new List<ConversionJob>();
            foreach (string file in this.InputFiles)
            {
                jobs.Add(new ConversionJob_ImageToPdf(preset, file));
            }

            this.QueueAndRun(jobs);
        }
    }
}
