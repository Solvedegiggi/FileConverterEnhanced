namespace FileConverter.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;

    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    using FileConverter.ConversionJobs;
    using FileConverter.Properties;
    using FileConverter.Services;

    using Microsoft.Win32;

    public class UniversalInputFileItem
    {
        public string FilePath { get; set; }

        public string FileName { get; set; }

        public string Extension { get; set; }

        public string SizeText { get; set; }
    }

    public class UniversalOutputOption
    {
        public string DisplayName { get; set; }

        public string GroupName { get; set; }

        public OutputType OutputType { get; set; }

        public string PdfOperationMode { get; set; }

        public ConversionPreset BackingPreset { get; set; }

        public bool SupportsMultiFileMerge { get; set; }
    }

    public class UniversalConverterViewModel : ObservableObject
    {
        private readonly ISettingsService settingsService;
        private readonly IConversionService conversionService;
        private readonly Action navigateToQueueCallback;

        private string webUrl = string.Empty;
        private UniversalOutputOption selectedOutput;
        private ConversionPreset workingPreset;
        private bool useCustomOutputFolder;
        private string customOutputFolder = string.Empty;
        private bool mergeIntoSinglePdf;
        private string statusMessage = string.Empty;

        public UniversalConverterViewModel(
            ISettingsService settingsService,
            IConversionService conversionService,
            Action navigateToQueueCallback)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
            this.navigateToQueueCallback = navigateToQueueCallback;

            this.InputFiles = new ObservableCollection<UniversalInputFileItem>();
            this.CompatibleOutputs = new ObservableCollection<UniversalOutputOption>();

            this.BrowseFilesCommand = new RelayCommand(this.BrowseFiles);
            this.RemoveFileCommand = new RelayCommand<UniversalInputFileItem>(this.RemoveFile);
            this.ClearFilesCommand = new RelayCommand(this.ClearFiles);
            this.BrowseOutputFolderCommand = new RelayCommand(this.BrowseOutputFolder);
            this.ConvertCommand = new RelayCommand(this.Convert, this.CanConvert);
        }

        public ObservableCollection<UniversalInputFileItem> InputFiles { get; }

        public ObservableCollection<UniversalOutputOption> CompatibleOutputs { get; }

        public string WebUrl
        {
            get => this.webUrl;
            set
            {
                if (this.SetProperty(ref this.webUrl, value))
                {
                    this.RefreshCompatibleOutputs();
                    ((RelayCommand)this.ConvertCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public UniversalOutputOption SelectedOutput
        {
            get => this.selectedOutput;
            set
            {
                if (this.SetProperty(ref this.selectedOutput, value))
                {
                    this.BuildWorkingPreset();
                    this.OnPropertyChanged(nameof(this.ShowMergeOption));
                    ((RelayCommand)this.ConvertCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public ConversionPreset WorkingPreset
        {
            get => this.workingPreset;
            private set => this.SetProperty(ref this.workingPreset, value);
        }

        public bool UseCustomOutputFolder
        {
            get => this.useCustomOutputFolder;
            set => this.SetProperty(ref this.useCustomOutputFolder, value);
        }

        public string CustomOutputFolder
        {
            get => this.customOutputFolder;
            set => this.SetProperty(ref this.customOutputFolder, value);
        }

        public bool MergeIntoSinglePdf
        {
            get => this.mergeIntoSinglePdf;
            set
            {
                if (this.SetProperty(ref this.mergeIntoSinglePdf, value) && this.WorkingPreset != null)
                {
                    this.WorkingPreset.SetSettingsValue(
                        ConversionPreset.ConversionSettingKeys.ImageToPdfMerge,
                        value ? "True" : "False");
                }
            }
        }

        public bool ShowMergeOption =>
            this.SelectedOutput != null &&
            this.SelectedOutput.OutputType == OutputType.Pdf &&
            this.InputFiles.Count > 1 &&
            this.SelectedOutput.SupportsMultiFileMerge;

        public string StatusMessage
        {
            get => this.statusMessage;
            private set => this.SetProperty(ref this.statusMessage, value);
        }

        public ICommand BrowseFilesCommand { get; }

        public ICommand RemoveFileCommand { get; }

        public ICommand ClearFilesCommand { get; }

        public ICommand BrowseOutputFolderCommand { get; }

        public ICommand ConvertCommand { get; }

        public void AddFiles(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return;
            }

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    continue;
                }

                if (this.InputFiles.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                FileInfo info = new FileInfo(path);
                string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

                this.InputFiles.Add(new UniversalInputFileItem
                {
                    FilePath = path,
                    FileName = info.Name,
                    Extension = ext,
                    SizeText = FormatBytes(info.Length),
                });
            }

            this.RefreshCompatibleOutputs();
            ((RelayCommand)this.ConvertCommand).NotifyCanExecuteChanged();
        }

        private void BrowseFiles()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All supported files|*.*",
            };

            if (dialog.ShowDialog() == true)
            {
                this.AddFiles(dialog.FileNames);
            }
        }

        private void RemoveFile(UniversalInputFileItem item)
        {
            if (item == null)
            {
                return;
            }

            this.InputFiles.Remove(item);
            this.RefreshCompatibleOutputs();
            ((RelayCommand)this.ConvertCommand).NotifyCanExecuteChanged();
        }

        private void ClearFiles()
        {
            this.InputFiles.Clear();
            this.WebUrl = string.Empty;
            this.RefreshCompatibleOutputs();
            ((RelayCommand)this.ConvertCommand).NotifyCanExecuteChanged();
        }

        private void BrowseOutputFolder()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (!string.IsNullOrWhiteSpace(this.CustomOutputFolder) && Directory.Exists(this.CustomOutputFolder))
                {
                    dialog.SelectedPath = this.CustomOutputFolder;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.CustomOutputFolder = dialog.SelectedPath;
                    this.UseCustomOutputFolder = true;
                }
            }
        }

        private void RefreshCompatibleOutputs()
        {
            this.CompatibleOutputs.Clear();
            this.StatusMessage = string.Empty;

            bool hasUrl = !string.IsNullOrWhiteSpace(this.WebUrl);
            if (this.InputFiles.Count == 0 && !hasUrl)
            {
                this.SelectedOutput = null;
                return;
            }

            if (hasUrl && this.InputFiles.Count == 0)
            {
                this.CompatibleOutputs.Add(new UniversalOutputOption
                {
                    DisplayName = "PDF (Webpage to PDF)",
                    GroupName = Resources.StringDocumentName,
                    OutputType = OutputType.Pdf,
                    PdfOperationMode = "None",
                });
                this.SelectedOutput = this.CompatibleOutputs.FirstOrDefault();
                return;
            }

            List<string> extensions = this.InputFiles.Select(f => f.Extension).Distinct().ToList();

            // Standard output formats compatible with all selected input files
            OutputType[] allOutputs = (OutputType[])Enum.GetValues(typeof(OutputType));
            foreach (OutputType outputType in allOutputs)
            {
                if (outputType == OutputType.None)
                {
                    continue;
                }

                if (extensions.All(ext => Helpers.IsOutputTypeCompatibleWithExtension(outputType, ext)))
                {
                    string cat = Helpers.GetExtensionCategory(Helpers.GetOutputExtension(outputType));
                    bool canMerge = outputType == OutputType.Pdf &&
                                    extensions.All(e => e == "pdf" || Helpers.GetExtensionCategory(e) == Helpers.InputCategoryNames.Image);

                    this.CompatibleOutputs.Add(new UniversalOutputOption
                    {
                        DisplayName = outputType.ToString().ToUpperInvariant(),
                        GroupName = cat,
                        OutputType = outputType,
                        PdfOperationMode = "None",
                        SupportsMultiFileMerge = canMerge,
                    });
                }
            }

            // Specialized PDF operations if all selected files are PDF
            if (extensions.Count == 1 && extensions[0] == "pdf")
            {
                this.CompatibleOutputs.Add(new UniversalOutputOption
                {
                    DisplayName = Resources.ToolCompressPdfTitle + " (PDF)",
                    GroupName = Resources.ToolCategoryOptimize,
                    OutputType = OutputType.Pdf,
                    PdfOperationMode = "Compress",
                });
                this.CompatibleOutputs.Add(new UniversalOutputOption
                {
                    DisplayName = Resources.ToolOcrPdfTitle + " (PDF)",
                    GroupName = Resources.ToolCategoryOptimize,
                    OutputType = OutputType.Pdf,
                    PdfOperationMode = "Ocr",
                });
                this.CompatibleOutputs.Add(new UniversalOutputOption
                {
                    DisplayName = Resources.ToolRotatePdfTitle + " (PDF)",
                    GroupName = Resources.ToolCategoryOrganize,
                    OutputType = OutputType.Pdf,
                    PdfOperationMode = "Rotate",
                });
                this.CompatibleOutputs.Add(new UniversalOutputOption
                {
                    DisplayName = Resources.ToolSplitPdfTitle + " (PDF)",
                    GroupName = Resources.ToolCategoryOrganize,
                    OutputType = OutputType.Pdf,
                    PdfOperationMode = "SplitEachPage",
                });
                this.CompatibleOutputs.Add(new UniversalOutputOption
                {
                    DisplayName = Resources.ToolPageNumbersPdfTitle + " (PDF)",
                    GroupName = Resources.ToolCategoryEditCompare,
                    OutputType = OutputType.Pdf,
                    PdfOperationMode = "PageNumbers",
                });
                if (this.InputFiles.Count > 1)
                {
                    this.CompatibleOutputs.Add(new UniversalOutputOption
                    {
                        DisplayName = Resources.ToolMergePdfTitle + " (PDF)",
                        GroupName = Resources.ToolCategoryOrganize,
                        OutputType = OutputType.Pdf,
                        PdfOperationMode = "Merge",
                        SupportsMultiFileMerge = true,
                    });
                }
            }

            // Matching saved user presets
            if (this.settingsService?.Settings?.ConversionPresets != null)
            {
                foreach (ConversionPreset preset in this.settingsService.Settings.ConversionPresets)
                {
                    if (preset.InputTypes == null)
                    {
                        continue;
                    }

                    if (extensions.All(ext => preset.InputTypes.Contains(ext)))
                    {
                        this.CompatibleOutputs.Add(new UniversalOutputOption
                        {
                            DisplayName = $"{preset.FullName} ({preset.OutputType})",
                            GroupName = Resources.ToolCategoryPresets,
                            OutputType = preset.OutputType,
                            BackingPreset = preset,
                        });
                    }
                }
            }

            if (this.CompatibleOutputs.Count == 0)
            {
                this.StatusMessage = Resources.UniversalNoCompatibleFormats;
            }

            this.SelectedOutput = this.CompatibleOutputs.FirstOrDefault();
            this.OnPropertyChanged(nameof(this.ShowMergeOption));
        }

        private void BuildWorkingPreset()
        {
            if (this.SelectedOutput == null)
            {
                this.WorkingPreset = null;
                return;
            }

            if (this.SelectedOutput.BackingPreset != null)
            {
                this.WorkingPreset = new ConversionPreset("Universal", this.SelectedOutput.BackingPreset);
            }
            else
            {
                string[] exts = this.InputFiles.Select(f => f.Extension).Distinct().ToArray();
                if (exts.Length == 0 && !string.IsNullOrWhiteSpace(this.WebUrl))
                {
                    exts = new[] { "html" };
                }

                var preset = new ConversionPreset("Universal", this.SelectedOutput.OutputType, exts);
                if (!string.IsNullOrEmpty(this.SelectedOutput.PdfOperationMode))
                {
                    preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, this.SelectedOutput.PdfOperationMode);
                }

                this.WorkingPreset = preset;
            }

            this.WorkingPreset.OutputFileNameTemplate = "(p)(f)";
        }

        private bool CanConvert()
        {
            return this.SelectedOutput != null && (this.InputFiles.Count > 0 || !string.IsNullOrWhiteSpace(this.WebUrl));
        }

        private void Convert()
        {
            if (this.WorkingPreset == null)
            {
                return;
            }

            ConversionPreset runPreset = new ConversionPreset(this.WorkingPreset.ShortName, this.WorkingPreset);
            if (this.UseCustomOutputFolder && !string.IsNullOrWhiteSpace(this.CustomOutputFolder))
            {
                string folder = this.CustomOutputFolder.TrimEnd('\\') + "\\";
                runPreset.OutputFileNameTemplate = folder + "(f)";
            }

            if (!string.IsNullOrWhiteSpace(this.WebUrl) && this.InputFiles.Count == 0)
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

                string stubPath = Path.Combine(outFolder, hostStub + ".url");
                ConversionJob htmlJob = new ConversionJob_HtmlToPdf(runPreset, stubPath, rawUrl);
                this.conversionService.RegisterConversionJob(htmlJob);
                this.conversionService.ConvertFilesAsync();
                this.navigateToQueueCallback?.Invoke();
                return;
            }

            List<string> paths = this.InputFiles.Select(f => f.FilePath).ToList();
            if (this.SelectedOutput.PdfOperationMode == "Merge" ||
                (this.MergeIntoSinglePdf && this.ShowMergeOption))
            {
                runPreset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.ImageToPdfMerge, "True");
                if (this.SelectedOutput.PdfOperationMode == "Merge")
                {
                    runPreset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Merge");
                }

                IReadOnlyList<ConversionJob> multiJobs = ConversionJobFactory.Create(runPreset, paths);
                foreach (ConversionJob job in multiJobs)
                {
                    this.conversionService.RegisterConversionJob(job);
                }
            }
            else
            {
                foreach (string path in paths)
                {
                    ConversionJob job = ConversionJobFactory.Create(runPreset, path);
                    this.conversionService.RegisterConversionJob(job);
                }
            }

            this.conversionService.ConvertFilesAsync();
            this.navigateToQueueCallback?.Invoke();
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int idx = 0;
            while (size >= 1024 && idx < units.Length - 1)
            {
                size /= 1024;
                idx++;
            }

            return $"{size:0.#} {units[idx]}";
        }
    }
}
