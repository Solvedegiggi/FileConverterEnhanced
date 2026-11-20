namespace FileConverter.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows.Input;

    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.DependencyInjection;
    using CommunityToolkit.Mvvm.Input;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;
    using FileConverter.ViewModels.Workspaces;

    using Microsoft.Win32;

    public enum HubSection
    {
        Home,
        Converter,
        AllTools,
        Queue,
        ActiveWorkspace,
    }

    public class HubViewModel : ObservableRecipient
    {
        private readonly ISettingsService settingsService;
        private readonly IConversionService conversionService;
        private readonly IThemeService themeService;
        private readonly IToolCatalogService toolCatalogService;
        private readonly INavigationService navigationService;

        private HubSection activeSection = HubSection.Home;
        private string searchQuery = string.Empty;
        private WorkspaceViewModelBase activeWorkspace;
        private ObservableCollection<ConversionJob> conversionJobs;
        private Func<ToolDefinition, Action, Action, WorkspaceViewModelBase> customWorkspaceFactory;

        public HubViewModel()
        {
            this.settingsService = Ioc.Default.GetRequiredService<ISettingsService>();
            this.conversionService = Ioc.Default.GetRequiredService<IConversionService>();
            this.themeService = Ioc.Default.GetRequiredService<IThemeService>();
            this.toolCatalogService = Ioc.Default.GetRequiredService<IToolCatalogService>();
            this.navigationService = Ioc.Default.GetRequiredService<INavigationService>();

            this.HomeGrid = new HomeGridViewModel(this.toolCatalogService, this.OnToolCardActivated);
            this.UniversalConverter = new UniversalConverterViewModel(
                this.settingsService,
                this.conversionService,
                () => this.ActiveSection = HubSection.Queue);

            this.AllPdfTools = new ObservableCollection<ToolDefinition>();
            this.ConversionJobs = new ObservableCollection<ConversionJob>(this.conversionService.ConversionJobs);
            this.conversionService.ConversionJobsChanged += this.ConversionService_ConversionJobsChanged;
            this.customWorkspaceFactory = WorkspaceFactory.Create;

            this.NavigateSectionCommand = new RelayCommand<string>(sectionName =>
            {
                if (Enum.TryParse(sectionName, out HubSection sec))
                {
                    this.ActiveSection = sec;
                }
            });

            this.ToggleThemeCommand = new RelayCommand(() =>
            {
                this.themeService.ToggleTheme();
                this.OnPropertyChanged(nameof(this.IsDarkTheme));
            });

            this.OpenToolCommand = new RelayCommand<ToolDefinition>(tool =>
            {
                if (tool != null)
                {
                    this.OnToolCardActivated(tool, null);
                }
            });

            this.TogglePinToolCommand = new RelayCommand<ToolDefinition>(tool =>
            {
                if (tool == null)
                {
                    return;
                }

                if (tool.IsPinned)
                {
                    this.toolCatalogService.UnpinTool(tool.Id);
                }
                else
                {
                    this.toolCatalogService.PinTool(tool.Id);
                }

                this.RefreshCatalogs();
            });

            this.OpenSettingsCommand = new RelayCommand(() => this.navigationService.Show(Pages.Settings));
            this.OpenDiagnosticsCommand = new RelayCommand(() => this.navigationService.Show(Pages.Diagnostics));
            this.OpenHelpCommand = new RelayCommand(() => this.navigationService.Show(Pages.Help));
            this.ClearCompletedJobsCommand = new RelayCommand(() => this.conversionService.ClearCompletedJobs());
            this.BackToHomeCommand = new RelayCommand(() => this.ActiveSection = HubSection.Home);
            this.CloseCommand = new RelayCommand<CancelEventArgs>(args => this.navigationService.Close(Pages.Hub, args != null));

            this.RefreshCatalogs();
        }

        public HomeGridViewModel HomeGrid { get; }

        public UniversalConverterViewModel UniversalConverter { get; }

        public ObservableCollection<ToolDefinition> AllPdfTools { get; }

        public ObservableCollection<ConversionJob> ConversionJobs
        {
            get => this.conversionJobs;
            private set
            {
                if (this.conversionJobs != null)
                {
                    foreach (ConversionJob job in this.conversionJobs)
                    {
                        job.PropertyChanged -= this.Job_PropertyChanged;
                    }
                }

                this.SetProperty(ref this.conversionJobs, value);

                if (this.conversionJobs != null)
                {
                    foreach (ConversionJob job in this.conversionJobs)
                    {
                        job.PropertyChanged += this.Job_PropertyChanged;
                    }
                }

                this.OnPropertyChanged(nameof(this.ActiveJobsCount));
                this.OnPropertyChanged(nameof(this.HasActiveJobs));
            }
        }

        public HubSection ActiveSection
        {
            get => this.activeSection;
            set
            {
                if (this.SetProperty(ref this.activeSection, value))
                {
                    this.OnPropertyChanged(nameof(this.IsHomeSelected));
                    this.OnPropertyChanged(nameof(this.IsConverterSelected));
                    this.OnPropertyChanged(nameof(this.IsAllToolsSelected));
                    this.OnPropertyChanged(nameof(this.IsQueueSelected));
                    this.OnPropertyChanged(nameof(this.IsWorkspaceActive));
                }
            }
        }

        public bool IsHomeSelected
        {
            get => this.ActiveSection == HubSection.Home;
            set
            {
                if (value)
                {
                    this.ActiveSection = HubSection.Home;
                }
            }
        }

        public bool IsConverterSelected
        {
            get => this.ActiveSection == HubSection.Converter;
            set
            {
                if (value)
                {
                    this.ActiveSection = HubSection.Converter;
                }
            }
        }

        public bool IsAllToolsSelected
        {
            get => this.ActiveSection == HubSection.AllTools;
            set
            {
                if (value)
                {
                    this.ActiveSection = HubSection.AllTools;
                }
            }
        }

        public bool IsQueueSelected
        {
            get => this.ActiveSection == HubSection.Queue;
            set
            {
                if (value)
                {
                    this.ActiveSection = HubSection.Queue;
                }
            }
        }

        public bool IsWorkspaceActive => this.ActiveSection == HubSection.ActiveWorkspace;

        public WorkspaceViewModelBase ActiveWorkspace
        {
            get => this.activeWorkspace;
            private set => this.SetProperty(ref this.activeWorkspace, value);
        }

        public string SearchQuery
        {
            get => this.searchQuery;
            set
            {
                if (this.SetProperty(ref this.searchQuery, value))
                {
                    this.HomeGrid.SearchFilter = value;
                    this.RefreshAllPdfTools();
                }
            }
        }

        public bool IsDarkTheme => this.themeService.IsDarkTheme;

        public int ActiveJobsCount => this.ConversionJobs?.Count(j => j.State == ConversionState.Ready || j.State == ConversionState.InProgress || j.State == ConversionState.Unknown) ?? 0;

        public bool HasActiveJobs => this.ActiveJobsCount > 0;

        public ICommand NavigateSectionCommand { get; }

        public ICommand ToggleThemeCommand { get; }

        public ICommand OpenToolCommand { get; }

        public ICommand TogglePinToolCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand OpenDiagnosticsCommand { get; }

        public ICommand OpenHelpCommand { get; }

        public ICommand ClearCompletedJobsCommand { get; }

        public ICommand BackToHomeCommand { get; }

        public ICommand CloseCommand { get; }

        public void RegisterWorkspaceFactory(Func<ToolDefinition, Action, Action, WorkspaceViewModelBase> factory)
        {
            this.customWorkspaceFactory = factory;
        }

        public void OpenTool(string toolId, IReadOnlyList<string> filePaths = null)
        {
            if (string.Equals(toolId, ToolIds.UniversalConverter, StringComparison.OrdinalIgnoreCase))
            {
                this.UniversalConverter.AddFiles(filePaths);
                this.ActiveSection = HubSection.Converter;
                return;
            }

            ToolDefinition tool = this.toolCatalogService.FindById(toolId);
            if (tool == null)
            {
                this.ActiveSection = HubSection.Home;
                return;
            }

            this.OnToolCardActivated(tool, filePaths?.ToArray());
        }

        public void RefreshCatalogs()
        {
            this.HomeGrid.Refresh();
            this.RefreshAllPdfTools();
        }

        private void RefreshAllPdfTools()
        {
            this.AllPdfTools.Clear();
            string q = (this.SearchQuery ?? string.Empty).Trim();

            foreach (ToolDefinition tool in this.toolCatalogService.GetBuiltInPdfTools())
            {
                if (string.IsNullOrEmpty(q) ||
                    tool.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (tool.Description != null && tool.Description.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (tool.CategoryName != null && tool.CategoryName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    this.AllPdfTools.Add(tool);
                }
            }
        }

        private void OnToolCardActivated(ToolDefinition tool, string[] droppedFiles)
        {
            if (tool == null)
            {
                return;
            }

            if (tool.IsPresetShortcut)
            {
                this.ExecutePresetShortcut(tool.PresetFullName, droppedFiles);
                return;
            }

            WorkspaceViewModelBase workspace = this.customWorkspaceFactory?.Invoke(
                tool,
                () => this.ActiveSection = HubSection.Home,
                () => this.ActiveSection = HubSection.Queue);

            if (workspace == null)
            {
                workspace = new PendingWorkspaceViewModel(
                    tool,
                    () => this.ActiveSection = HubSection.Home,
                    () => this.ActiveSection = HubSection.Queue);
            }

            if (droppedFiles != null && droppedFiles.Length > 0)
            {
                workspace.LoadFiles(droppedFiles);
            }

            this.ActiveWorkspace = workspace;
            this.ActiveSection = HubSection.ActiveWorkspace;
        }

        private void ExecutePresetShortcut(string presetFullName, string[] files)
        {
            ConversionPreset preset = this.settingsService.Settings.GetPresetFromName(presetFullName);
            if (preset == null)
            {
                return;
            }

            string[] selectedFiles = files;
            if (selectedFiles == null || selectedFiles.Length == 0)
            {
                string filter = "*.*";
                if (preset.InputTypes != null && preset.InputTypes.Count > 0)
                {
                    filter = "Supported files|" + string.Join(";", preset.InputTypes.Select(ext => "*." + ext));
                }

                OpenFileDialog dialog = new OpenFileDialog
                {
                    Title = preset.FullName,
                    Multiselect = true,
                    Filter = filter,
                };

                if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
                {
                    return;
                }

                selectedFiles = dialog.FileNames;
            }

            IReadOnlyList<ConversionJob> jobs = ConversionJobFactory.Create(preset, selectedFiles);
            foreach (ConversionJob job in jobs)
            {
                this.conversionService.RegisterConversionJob(job);
            }

            this.conversionService.ConvertFilesAsync();
            this.ActiveSection = HubSection.Queue;
        }

        private void ConversionService_ConversionJobsChanged(object sender, EventArgs e)
        {
            Action sync = () =>
            {
                this.ConversionJobs = new ObservableCollection<ConversionJob>(this.conversionService.ConversionJobs);
            };

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(sync);
            }
            else
            {
                sync();
            }
        }

        private void Job_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConversionJob.State))
            {
                this.OnPropertyChanged(nameof(this.ActiveJobsCount));
                this.OnPropertyChanged(nameof(this.HasActiveJobs));
            }
        }
    }
}
