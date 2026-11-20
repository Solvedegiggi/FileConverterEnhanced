namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;

    public class OcrLanguageOption : ObservableObject
    {
        private bool isSelected;
        private bool isInstalled;
        private bool isDownloading;
        private double downloadProgress;

        public string Code { get; set; }

        public string DisplayName { get; set; }

        public bool IsSelected
        {
            get => this.isSelected;
            set => this.SetProperty(ref this.isSelected, value);
        }

        public bool IsInstalled
        {
            get => this.isInstalled;
            set => this.SetProperty(ref this.isInstalled, value);
        }

        public bool IsDownloading
        {
            get => this.isDownloading;
            set => this.SetProperty(ref this.isDownloading, value);
        }

        public double DownloadProgress
        {
            get => this.downloadProgress;
            set => this.SetProperty(ref this.downloadProgress, value);
        }
    }

    public class PdfOcrWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private bool skipTextPages = true;
        private int dpi = 300;

        public PdfOcrWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
            string defaultSpec = this.SettingsService?.Settings?.OcrDefaultLanguages ?? "ita+eng";
            var defaultCodes = new HashSet<string>(TessdataService.ParseLanguageCodes(defaultSpec), StringComparer.OrdinalIgnoreCase);

            foreach (var item in TessdataService.GetAvailableCatalogLanguages())
            {
                bool installed = TessdataService.IsLanguageInstalled(item.Code);
                var option = new OcrLanguageOption
                {
                    Code = item.Code,
                    DisplayName = item.DisplayName,
                    IsInstalled = installed,
                    IsSelected = defaultCodes.Contains(item.Code),
                };
                option.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(OcrLanguageOption.IsSelected))
                    {
                        this.NotifyCanExecuteChanged();
                    }
                };
                this.Languages.Add(option);
            }

            this.DownloadLanguageCommand = new RelayCommand<OcrLanguageOption>(opt => _ = this.DownloadLanguageAsync(opt));
        }

        public ObservableCollection<OcrLanguageOption> Languages { get; } = new ObservableCollection<OcrLanguageOption>();

        public int[] AvailableDpi { get; } = { 200, 300, 400 };

        public bool SkipTextPages
        {
            get => this.skipTextPages;
            set => this.SetProperty(ref this.skipTextPages, value);
        }

        public int Dpi
        {
            get => this.dpi;
            set => this.SetProperty(ref this.dpi, value);
        }

        public ICommand DownloadLanguageCommand { get; }

        protected override bool CanExecute()
        {
            return base.CanExecute() && this.Languages.Any(l => l.IsSelected);
        }

        protected override void Execute()
        {
            string langSpec = string.Join("+", this.Languages.Where(l => l.IsSelected).Select(l => l.Code));
            if (string.IsNullOrWhiteSpace(langSpec))
            {
                langSpec = "eng";
            }

            var jobs = new List<ConversionJob>();
            foreach (string file in this.InputFiles)
            {
                var preset = new ConversionPreset("PdfOcr", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Ocr");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.OcrLanguages, langSpec);
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.OcrSkipTextPages, this.SkipTextPages ? "True" : "False");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfRenderDpi, this.Dpi.ToString(CultureInfo.InvariantCulture));
                this.ApplyOutputTemplate(preset);

                jobs.Add(new ConversionJob_PdfOcr(preset, file));
            }

            this.QueueAndRun(jobs);
        }

        private async Task DownloadLanguageAsync(OcrLanguageOption option)
        {
            if (option == null || option.IsDownloading)
            {
                return;
            }

            option.IsDownloading = true;
            option.DownloadProgress = 0;
            this.StatusMessage = string.Empty;

            try
            {
                var progress = new Progress<double>(p => option.DownloadProgress = p * 100.0);
                await TessdataService.EnsureLanguageAsync(option.Code, progress);
                option.IsInstalled = true;
                option.IsSelected = true;
            }
            catch (Exception ex)
            {
                this.StatusMessage = $"Failed to download {option.Code}: {ex.Message}";
            }
            finally
            {
                option.IsDownloading = false;
            }
        }
    }
}
