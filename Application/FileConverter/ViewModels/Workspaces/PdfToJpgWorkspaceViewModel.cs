namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;

    public class PdfToJpgWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private string mode = "PagesToImages";
        private int dpi = 200;
        private int jpegQuality = 90;
        private string pageRange = string.Empty;

        public PdfToJpgWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }

        public int[] AvailableDpi { get; } = { 100, 150, 200, 300, 400 };

        public string Mode
        {
            get => this.mode;
            set
            {
                if (this.SetProperty(ref this.mode, value))
                {
                    this.OnPropertyChanged(nameof(this.IsPagesToImages));
                    this.OnPropertyChanged(nameof(this.IsExtractEmbeddedImages));
                }
            }
        }

        public bool IsPagesToImages
        {
            get => this.Mode == "PagesToImages";
            set
            {
                if (value)
                {
                    this.Mode = "PagesToImages";
                }
            }
        }

        public bool IsExtractEmbeddedImages
        {
            get => this.Mode == "ExtractEmbeddedImages";
            set
            {
                if (value)
                {
                    this.Mode = "ExtractEmbeddedImages";
                }
            }
        }

        public int Dpi
        {
            get => this.dpi;
            set => this.SetProperty(ref this.dpi, value);
        }

        public int JpegQuality
        {
            get => this.jpegQuality;
            set => this.SetProperty(ref this.jpegQuality, Math.Max(10, Math.Min(100, value)));
        }

        public string PageRange
        {
            get => this.pageRange;
            set => this.SetProperty(ref this.pageRange, value);
        }

        protected override void Execute()
        {
            var jobs = new List<ConversionJob>();
            foreach (string file in this.InputFiles)
            {
                var preset = new ConversionPreset("PdfToJpg", OutputType.Jpg, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfToImageMode, this.Mode);
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfRenderDpi, this.Dpi.ToString(CultureInfo.InvariantCulture));
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.ImageQuality, this.JpegQuality.ToString(CultureInfo.InvariantCulture));
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange, this.PageRange ?? string.Empty);
                this.ApplyOutputTemplate(preset);

                jobs.Add(new ConversionJob_PdfToImage(preset, file));
            }

            this.QueueAndRun(jobs);
        }
    }
}
