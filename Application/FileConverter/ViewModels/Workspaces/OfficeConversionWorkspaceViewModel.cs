namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;

    public class OfficeConversionWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private string excelOrientation = "Default";
        private bool excelFitColumnsToPage = true;
        private bool useOfficeInteropForPdfToWord;

        public OfficeConversionWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }

        public bool ShowExcelOptions => this.Tool?.Id == ToolIds.ExcelToPdf;

        public bool ShowPdfToWordOptions => this.Tool?.Id == ToolIds.PdfToWord;

        public string BackendStatusSummary
        {
            get
            {
                if (this.Tool?.Id == ToolIds.WordToPdf ||
                    this.Tool?.Id == ToolIds.PowerPointToPdf ||
                    this.Tool?.Id == ToolIds.ExcelToPdf)
                {
                    if (LibreOfficeHelper.IsAvailable())
                    {
                        return "MS Office / LibreOffice / Built-in OpenXML engine ready.";
                    }

                    return "Uses Microsoft Office if installed, with automatic built-in OpenXML offline engine fallback (.docx / .pptx / .xlsx).";
                }

                return "Runs 100% offline using the built-in native OpenXML generator (no Office installation required).";
            }
        }

        public string ExcelOrientation
        {
            get => this.excelOrientation;
            set
            {
                if (this.SetProperty(ref this.excelOrientation, value))
                {
                    this.OnPropertyChanged(nameof(this.IsOrientationDefault));
                    this.OnPropertyChanged(nameof(this.IsOrientationPortrait));
                    this.OnPropertyChanged(nameof(this.IsOrientationLandscape));
                }
            }
        }

        public bool IsOrientationDefault { get => this.ExcelOrientation == "Default"; set { if (value) { this.ExcelOrientation = "Default"; } } }

        public bool IsOrientationPortrait { get => this.ExcelOrientation == "Portrait"; set { if (value) { this.ExcelOrientation = "Portrait"; } } }

        public bool IsOrientationLandscape { get => this.ExcelOrientation == "Landscape"; set { if (value) { this.ExcelOrientation = "Landscape"; } } }

        public bool ExcelFitColumnsToPage
        {
            get => this.excelFitColumnsToPage;
            set => this.SetProperty(ref this.excelFitColumnsToPage, value);
        }

        public bool UseOfficeInteropForPdfToWord
        {
            get => this.useOfficeInteropForPdfToWord;
            set => this.SetProperty(ref this.useOfficeInteropForPdfToWord, value);
        }

        protected override void Execute()
        {
            OutputType targetType = this.ResolveOutputType();
            var jobs = new List<ConversionJob>();

            foreach (string file in this.InputFiles)
            {
                var preset = new ConversionPreset("OfficeTool", targetType, new[] { "pdf", "doc", "docx", "odt", "ppt", "pptx", "odp", "xls", "xlsx", "ods" });

                if (this.ShowExcelOptions)
                {
                    preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.ExcelPageOrientation, this.ExcelOrientation);
                    preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.ExcelFitColumnsToPage, this.ExcelFitColumnsToPage ? "True" : "False");
                }

                if (this.ShowPdfToWordOptions)
                {
                    preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfToOfficeEngine, this.UseOfficeInteropForPdfToWord ? "OfficeInterop" : "Native");
                }

                this.ApplyOutputTemplate(preset);
                jobs.Add(ConversionJobFactory.Create(preset, file));
            }

            this.QueueAndRun(jobs);
        }

        private OutputType ResolveOutputType()
        {
            switch (this.Tool?.Id)
            {
                case ToolIds.PdfToWord:
                    return OutputType.Docx;

                case ToolIds.PdfToPowerPoint:
                    return OutputType.Pptx;

                case ToolIds.PdfToExcel:
                    return OutputType.Xlsx;

                default:
                    return OutputType.Pdf;
            }
        }
    }
}
