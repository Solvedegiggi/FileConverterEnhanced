namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;

    public class PdfToMarkdownWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private bool extractImages = true;
        private bool includePageBreaks = true;

        public PdfToMarkdownWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }

        public bool ExtractImages
        {
            get => this.extractImages;
            set => this.SetProperty(ref this.extractImages, value);
        }

        public bool IncludePageBreaks
        {
            get => this.includePageBreaks;
            set => this.SetProperty(ref this.includePageBreaks, value);
        }

        protected override void Execute()
        {
            var jobs = new List<ConversionJob>();
            foreach (string file in this.InputFiles)
            {
                var preset = new ConversionPreset("PdfToMarkdown", OutputType.Md, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfMarkdownExtractImages, this.ExtractImages ? "True" : "False");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfMarkdownIncludePageBreaks, this.IncludePageBreaks ? "True" : "False");
                this.ApplyOutputTemplate(preset);

                jobs.Add(new ConversionJob_PdfToMarkdown(preset, file));
            }

            this.QueueAndRun(jobs);
        }
    }
}
