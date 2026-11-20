namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Globalization;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;

    public class PdfSplitWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private string splitMode = "SplitAllPages";
        private string pageRange = string.Empty;
        private int everyNPages = 1;
        private int totalPagesHint;

        public PdfSplitWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }

        public string SplitMode
        {
            get => this.splitMode;
            set
            {
                if (this.SetProperty(ref this.splitMode, value))
                {
                    this.OnPropertyChanged(nameof(this.IsSplitAllPages));
                    this.OnPropertyChanged(nameof(this.IsExtractRange));
                    this.OnPropertyChanged(nameof(this.IsSplitEveryN));
                }
            }
        }

        public bool IsSplitAllPages
        {
            get => this.SplitMode == "SplitAllPages";
            set
            {
                if (value)
                {
                    this.SplitMode = "SplitAllPages";
                }
            }
        }

        public bool IsExtractRange
        {
            get => this.SplitMode == "ExtractRange";
            set
            {
                if (value)
                {
                    this.SplitMode = "ExtractRange";
                }
            }
        }

        public bool IsSplitEveryN
        {
            get => this.SplitMode == "SplitEveryN";
            set
            {
                if (value)
                {
                    this.SplitMode = "SplitEveryN";
                }
            }
        }

        public string PageRange
        {
            get => this.pageRange;
            set => this.SetProperty(ref this.pageRange, value);
        }

        public int EveryNPages
        {
            get => this.everyNPages;
            set => this.SetProperty(ref this.everyNPages, Math.Max(1, value));
        }

        public int TotalPagesHint
        {
            get => this.totalPagesHint;
            private set => this.SetProperty(ref this.totalPagesHint, value);
        }

        protected override void OnFilesChanged()
        {
            base.OnFilesChanged();
            this.TotalPagesHint = this.InputFiles.Count > 0 ? PdfRenderService.GetPageCount(this.InputFiles[0]) : 0;
        }

        protected override void Execute()
        {
            var jobs = new System.Collections.Generic.List<ConversionJob>();
            foreach (string file in this.InputFiles)
            {
                var preset = new ConversionPreset("PdfSplit", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, this.SplitMode);
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfSplitMode, this.SplitMode);
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange, this.PageRange ?? string.Empty);
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfSplitEveryNPages, this.EveryNPages.ToString(CultureInfo.InvariantCulture));
                this.ApplyOutputTemplate(preset);

                jobs.Add(new ConversionJob_PdfSplit(preset, file));
            }

            this.QueueAndRun(jobs);
        }
    }
}
