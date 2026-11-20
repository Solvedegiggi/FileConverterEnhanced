namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;

    public class PdfCompressWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private string compressionLevel = "Recommended";

        public PdfCompressWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }

        public string CompressionLevel
        {
            get => this.compressionLevel;
            set
            {
                if (this.SetProperty(ref this.compressionLevel, value))
                {
                    this.OnPropertyChanged(nameof(this.IsExtreme));
                    this.OnPropertyChanged(nameof(this.IsRecommended));
                    this.OnPropertyChanged(nameof(this.IsLow));
                }
            }
        }

        public bool IsExtreme
        {
            get => this.CompressionLevel == "Extreme";
            set
            {
                if (value)
                {
                    this.CompressionLevel = "Extreme";
                }
            }
        }

        public bool IsRecommended
        {
            get => this.CompressionLevel == "Recommended";
            set
            {
                if (value)
                {
                    this.CompressionLevel = "Recommended";
                }
            }
        }

        public bool IsLow
        {
            get => this.CompressionLevel == "Low";
            set
            {
                if (value)
                {
                    this.CompressionLevel = "Low";
                }
            }
        }

        protected override void Execute()
        {
            var jobs = new List<ConversionJob>();
            foreach (string file in this.InputFiles)
            {
                var preset = new ConversionPreset("PdfCompress", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Compress");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfCompressionLevel, this.CompressionLevel);
                this.ApplyOutputTemplate(preset);

                jobs.Add(new ConversionJob_PdfCompress(preset, file));
            }

            this.QueueAndRun(jobs);
        }
    }
}
