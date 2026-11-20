namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Windows.Input;

    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    using FileConverter.Models;

    public abstract class WorkspaceViewModelBase : ObservableObject
    {
        protected WorkspaceViewModelBase(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
        {
            this.Tool = tool;
            this.OnBack = onBack;
            this.OnNavigateToQueue = onNavigateToQueue;
            this.BackCommand = new RelayCommand(() => this.OnBack?.Invoke());
        }

        public ToolDefinition Tool { get; }

        protected Action OnBack { get; }

        protected Action OnNavigateToQueue { get; }

        public ICommand BackCommand { get; }

        public ObservableCollection<string> InputFiles { get; } = new ObservableCollection<string>();

        public virtual void LoadFiles(IReadOnlyList<string> files)
        {
            if (files == null)
            {
                return;
            }

            foreach (string file in files)
            {
                if (!string.IsNullOrWhiteSpace(file) && !this.InputFiles.Contains(file))
                {
                    this.InputFiles.Add(file);
                }
            }

            this.OnFilesLoaded();
        }

        protected virtual void OnFilesLoaded()
        {
        }
    }

    public class PendingWorkspaceViewModel : WorkspaceViewModelBase
    {
        public PendingWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }
    }
}
