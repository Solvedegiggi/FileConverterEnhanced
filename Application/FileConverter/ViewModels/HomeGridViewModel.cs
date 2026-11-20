namespace FileConverter.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows.Input;

    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    using FileConverter.Models;
    using FileConverter.Services;

    public class HomeGridViewModel : ObservableObject
    {
        private readonly IToolCatalogService toolCatalogService;
        private readonly Action<ToolDefinition, string[]> openToolCallback;

        private bool isEditMode;
        private bool isAddDrawerOpen;
        private string addFilterText = string.Empty;
        private string searchFilter = string.Empty;

        public HomeGridViewModel(IToolCatalogService toolCatalogService, Action<ToolDefinition, string[]> openToolCallback)
        {
            this.toolCatalogService = toolCatalogService ?? throw new ArgumentNullException(nameof(toolCatalogService));
            this.openToolCallback = openToolCallback ?? throw new ArgumentNullException(nameof(openToolCallback));

            this.PinnedTools = new ObservableCollection<ToolDefinition>();
            this.AvailableToolsToAdd = new ObservableCollection<ToolDefinition>();

            this.ToggleEditModeCommand = new RelayCommand(() =>
            {
                this.IsEditMode = !this.IsEditMode;
                if (!this.IsEditMode)
                {
                    this.IsAddDrawerOpen = false;
                }
            });

            this.OpenAddDrawerCommand = new RelayCommand(() =>
            {
                this.RefreshAvailableToAdd();
                this.IsAddDrawerOpen = true;
            });

            this.CloseAddDrawerCommand = new RelayCommand(() => this.IsAddDrawerOpen = false);

            this.PinToolCommand = new RelayCommand<ToolDefinition>(tool =>
            {
                if (tool == null)
                {
                    return;
                }

                this.toolCatalogService.PinTool(tool.Id);
                this.Refresh();
            });

            this.UnpinToolCommand = new RelayCommand<ToolDefinition>(tool =>
            {
                if (tool == null)
                {
                    return;
                }

                this.toolCatalogService.UnpinTool(tool.Id);
                this.Refresh();
            });

            this.MoveLeftCommand = new RelayCommand<ToolDefinition>(tool =>
            {
                if (tool == null)
                {
                    return;
                }

                this.toolCatalogService.MovePinnedTool(tool.Id, -1);
                this.Refresh();
            });

            this.MoveRightCommand = new RelayCommand<ToolDefinition>(tool =>
            {
                if (tool == null)
                {
                    return;
                }

                this.toolCatalogService.MovePinnedTool(tool.Id, 1);
                this.Refresh();
            });

            this.OpenToolCommand = new RelayCommand<ToolDefinition>(tool =>
            {
                if (tool != null && !this.IsEditMode)
                {
                    this.openToolCallback(tool, null);
                }
            });

            this.Refresh();
        }

        public ObservableCollection<ToolDefinition> PinnedTools { get; }

        public ObservableCollection<ToolDefinition> AvailableToolsToAdd { get; }

        public bool IsEditMode
        {
            get => this.isEditMode;
            set => this.SetProperty(ref this.isEditMode, value);
        }

        public bool IsAddDrawerOpen
        {
            get => this.isAddDrawerOpen;
            set => this.SetProperty(ref this.isAddDrawerOpen, value);
        }

        public string AddFilterText
        {
            get => this.addFilterText;
            set
            {
                if (this.SetProperty(ref this.addFilterText, value))
                {
                    this.RefreshAvailableToAdd();
                }
            }
        }

        public string SearchFilter
        {
            get => this.searchFilter;
            set
            {
                if (this.SetProperty(ref this.searchFilter, value))
                {
                    this.Refresh();
                }
            }
        }

        public ICommand ToggleEditModeCommand { get; }

        public ICommand OpenAddDrawerCommand { get; }

        public ICommand CloseAddDrawerCommand { get; }

        public ICommand PinToolCommand { get; }

        public ICommand UnpinToolCommand { get; }

        public ICommand MoveLeftCommand { get; }

        public ICommand MoveRightCommand { get; }

        public ICommand OpenToolCommand { get; }

        public void HandleDropOnCard(ToolDefinition tool, string[] droppedFiles)
        {
            if (tool == null || droppedFiles == null || droppedFiles.Length == 0)
            {
                return;
            }

            this.openToolCallback(tool, droppedFiles);
        }

        public void Refresh()
        {
            this.PinnedTools.Clear();
            string query = (this.SearchFilter ?? string.Empty).Trim();

            foreach (ToolDefinition tool in this.toolCatalogService.GetPinnedQuickTools())
            {
                if (string.IsNullOrEmpty(query) ||
                    tool.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (tool.Description != null && tool.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    this.PinnedTools.Add(tool);
                }
            }

            if (this.IsAddDrawerOpen)
            {
                this.RefreshAvailableToAdd();
            }
        }

        private void RefreshAvailableToAdd()
        {
            this.AvailableToolsToAdd.Clear();
            string query = (this.AddFilterText ?? string.Empty).Trim();

            foreach (ToolDefinition item in this.toolCatalogService.GetAllToolsAndPresets())
            {
                if (item.IsPinned)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(query) ||
                    item.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (item.Description != null && item.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    this.AvailableToolsToAdd.Add(item);
                }
            }
        }
    }
}
