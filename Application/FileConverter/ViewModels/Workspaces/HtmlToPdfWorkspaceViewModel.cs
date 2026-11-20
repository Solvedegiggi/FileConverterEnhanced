namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;

    public class HtmlToPdfWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private string webUrl = string.Empty;
        private string pageSize = "A4";
        private string orientation = "Portrait";
        private bool printBackground = true;
        private int delayMs = 500;

        public HtmlToPdfWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }

        public string[] PageSizes { get; } = { "A4", "Letter" };

        public string[] Orientations { get; } = { "Portrait", "Landscape" };

        public string WebUrl
        {
            get => this.webUrl;
            set
            {
                if (this.SetProperty(ref this.webUrl, value))
                {
                    this.NotifyCanExecuteChanged();
                }
            }
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

        public bool PrintBackground
        {
            get => this.printBackground;
            set => this.SetProperty(ref this.printBackground, value);
        }

        public int DelayMs
        {
            get => this.delayMs;
            set => this.SetProperty(ref this.delayMs, Math.Max(0, Math.Min(10000, value)));
        }

        protected override bool CanExecute()
        {
            return this.InputFiles.Count > 0 || !string.IsNullOrWhiteSpace(this.WebUrl);
        }

        protected override void Execute()
        {
            var jobs = new List<ConversionJob>();

            foreach (string htmlFile in this.InputFiles)
            {
                var filePreset = this.BuildPreset();
                this.ApplyOutputTemplate(filePreset);
                jobs.Add(new ConversionJob_HtmlToPdf(filePreset, htmlFile));
            }

            if (!string.IsNullOrWhiteSpace(this.WebUrl))
            {
                string rawUrl = this.WebUrl.Trim();
                string outFolder = this.UseCustomOutputFolder && !string.IsNullOrWhiteSpace(this.CustomOutputFolder)
                    ? this.CustomOutputFolder.TrimEnd('\\')
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                string hostStub = "webpage";
                string uriCandidate = rawUrl.Contains("://") ? rawUrl : "https://" + rawUrl;
                if (Uri.TryCreate(uriCandidate, UriKind.Absolute, out Uri parsedUri) &&
                    !string.IsNullOrWhiteSpace(parsedUri.Host))
                {
                    hostStub = string.Concat(parsedUri.Host.Split(Path.GetInvalidFileNameChars()));
                }

                var urlPreset = this.BuildPreset();
                urlPreset.OutputFileNameTemplate = outFolder + "\\" + (string.IsNullOrWhiteSpace(this.OutputFileName) ? "(f)" : Path.GetFileNameWithoutExtension(this.OutputFileName));
                string stubPath = Path.Combine(outFolder, hostStub + ".url");
                jobs.Add(new ConversionJob_HtmlToPdf(urlPreset, stubPath, rawUrl));
            }

            this.QueueAndRun(jobs);
        }

        private ConversionPreset BuildPreset()
        {
            var preset = new ConversionPreset("HtmlToPdf", OutputType.Pdf, new[] { "html", "htm" });
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.HtmlPageSize, this.PageSize ?? "A4");
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.HtmlPageOrientation, this.Orientation ?? "Portrait");
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.HtmlPrintBackground, this.PrintBackground ? "True" : "False");
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.HtmlDelayMs, this.DelayMs.ToString(CultureInfo.InvariantCulture));
            return preset;
        }
    }
}
