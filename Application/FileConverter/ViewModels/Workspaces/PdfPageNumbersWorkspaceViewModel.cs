namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;

    public class PdfPageNumbersWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private string position = "BottomCenter";
        private string format = "{n} / {total}";
        private string fontFamily = "Arial";
        private double fontSize = 11;
        private string colorHex = "#333333";
        private int startPage = 1;
        private int startNumber = 1;
        private double marginMm = 12;
        private string pageRange = string.Empty;

        public PdfPageNumbersWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }

        public string[] AvailableFonts { get; } = { "Arial", "Calibri", "Segoe UI", "Times New Roman", "Consolas" };

        public string[] PresetFormats { get; } = { "{n} / {total}", "{n}", "Page {n}", "Pag. {n} di {total}" };

        public string Position
        {
            get => this.position;
            set
            {
                if (this.SetProperty(ref this.position, value))
                {
                    this.OnPropertyChanged(nameof(this.IsTopLeft));
                    this.OnPropertyChanged(nameof(this.IsTopCenter));
                    this.OnPropertyChanged(nameof(this.IsTopRight));
                    this.OnPropertyChanged(nameof(this.IsBottomLeft));
                    this.OnPropertyChanged(nameof(this.IsBottomCenter));
                    this.OnPropertyChanged(nameof(this.IsBottomRight));
                }
            }
        }

        public bool IsTopLeft { get => this.Position == "TopLeft"; set { if (value) { this.Position = "TopLeft"; } } }

        public bool IsTopCenter { get => this.Position == "TopCenter"; set { if (value) { this.Position = "TopCenter"; } } }

        public bool IsTopRight { get => this.Position == "TopRight"; set { if (value) { this.Position = "TopRight"; } } }

        public bool IsBottomLeft { get => this.Position == "BottomLeft"; set { if (value) { this.Position = "BottomLeft"; } } }

        public bool IsBottomCenter { get => this.Position == "BottomCenter"; set { if (value) { this.Position = "BottomCenter"; } } }

        public bool IsBottomRight { get => this.Position == "BottomRight"; set { if (value) { this.Position = "BottomRight"; } } }

        public string Format
        {
            get => this.format;
            set => this.SetProperty(ref this.format, value);
        }

        public string FontFamily
        {
            get => this.fontFamily;
            set => this.SetProperty(ref this.fontFamily, value);
        }

        public double FontSize
        {
            get => this.fontSize;
            set => this.SetProperty(ref this.fontSize, Math.Max(6, Math.Min(48, value)));
        }

        public string ColorHex
        {
            get => this.colorHex;
            set => this.SetProperty(ref this.colorHex, value);
        }

        public int StartPage
        {
            get => this.startPage;
            set => this.SetProperty(ref this.startPage, Math.Max(1, value));
        }

        public int StartNumber
        {
            get => this.startNumber;
            set => this.SetProperty(ref this.startNumber, value);
        }

        public double MarginMm
        {
            get => this.marginMm;
            set => this.SetProperty(ref this.marginMm, Math.Max(2, Math.Min(50, value)));
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
                var preset = new ConversionPreset("PdfPageNumbers", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "PageNumbers");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberPosition, this.Position);
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberFormat, string.IsNullOrEmpty(this.Format) ? "{n} / {total}" : this.Format);
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberFont, this.FontFamily ?? "Arial");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberFontSize, this.FontSize.ToString(CultureInfo.InvariantCulture));
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberColor, this.ColorHex ?? "#333333");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberStartPage, this.StartPage.ToString(CultureInfo.InvariantCulture));
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberStartNumber, this.StartNumber.ToString(CultureInfo.InvariantCulture));
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageNumberMargin, this.MarginMm.ToString(CultureInfo.InvariantCulture));
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange, this.PageRange ?? string.Empty);
                this.ApplyOutputTemplate(preset);

                jobs.Add(new ConversionJob_PdfPageNumbers(preset, file));
            }

            this.QueueAndRun(jobs);
        }
    }
}
