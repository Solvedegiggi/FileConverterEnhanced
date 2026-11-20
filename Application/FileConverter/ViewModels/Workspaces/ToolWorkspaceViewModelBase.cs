// <copyright file="ToolWorkspaceViewModelBase.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;

    using CommunityToolkit.Mvvm.DependencyInjection;
    using CommunityToolkit.Mvvm.Input;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;

    using Microsoft.Win32;

    public abstract class ToolWorkspaceViewModelBase : WorkspaceViewModelBase
    {
        private bool useCustomOutputFolder;
        private string customOutputFolder = string.Empty;
        private string outputFileName = string.Empty;
        private string statusMessage = string.Empty;

        protected ToolWorkspaceViewModelBase(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
            this.ConversionService = Ioc.Default.GetRequiredService<IConversionService>();
            this.SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();

            this.BrowseFilesCommand = new RelayCommand(this.BrowseFiles);
            this.RemoveFileCommand = new RelayCommand<string>(this.RemoveFile);
            this.MoveFileUpCommand = new RelayCommand<string>(p => this.MoveFile(p, -1));
            this.MoveFileDownCommand = new RelayCommand<string>(p => this.MoveFile(p, 1));
            this.ClearFilesCommand = new RelayCommand(() =>
            {
                this.InputFiles.Clear();
                this.OnFilesChanged();
            });
            this.BrowseOutputFolderCommand = new RelayCommand(this.BrowseOutputFolder);
            this.ExecuteCommand = new RelayCommand(this.ExecuteWrapper, this.CanExecute);
        }

        protected IConversionService ConversionService { get; }

        protected ISettingsService SettingsService { get; }

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

        public string OutputFileName
        {
            get => this.outputFileName;
            set => this.SetProperty(ref this.outputFileName, value);
        }

        public string StatusMessage
        {
            get => this.statusMessage;
            protected set => this.SetProperty(ref this.statusMessage, value);
        }

        public ICommand BrowseFilesCommand { get; }

        public ICommand RemoveFileCommand { get; }

        public ICommand MoveFileUpCommand { get; }

        public ICommand MoveFileDownCommand { get; }

        public ICommand ClearFilesCommand { get; }

        public ICommand BrowseOutputFolderCommand { get; }

        public ICommand ExecuteCommand { get; }

        protected override void OnFilesLoaded()
        {
            base.OnFilesLoaded();
            this.OnFilesChanged();
        }

        protected virtual void OnFilesChanged()
        {
            this.StatusMessage = string.Empty;
            this.NotifyCanExecuteChanged();
        }

        protected void NotifyCanExecuteChanged()
        {
            ((RelayCommand)this.ExecuteCommand).NotifyCanExecuteChanged();
        }

        protected virtual bool CanExecute()
        {
            return this.InputFiles.Count > 0;
        }

        protected abstract void Execute();

        protected void ApplyOutputTemplate(ConversionPreset preset, string explicitFileName = null)
        {
            string folderPrefix = "(p)";
            if (this.UseCustomOutputFolder && !string.IsNullOrWhiteSpace(this.CustomOutputFolder))
            {
                folderPrefix = this.CustomOutputFolder.TrimEnd('\\') + "\\";
            }

            if (!string.IsNullOrWhiteSpace(explicitFileName))
            {
                string cleanName = Path.GetFileNameWithoutExtension(explicitFileName.Trim());
                if (!string.IsNullOrEmpty(cleanName))
                {
                    preset.OutputFileNameTemplate = folderPrefix + cleanName;
                    return;
                }
            }

            preset.OutputFileNameTemplate = folderPrefix + "(f)";
        }

        protected void QueueAndRun(ConversionJob job)
        {
            if (job == null)
            {
                return;
            }

            this.ConversionService.RegisterConversionJob(job);
            this.ConversionService.ConvertFilesAsync();
            this.OnNavigateToQueue?.Invoke();
        }

        protected void QueueAndRun(IEnumerable<ConversionJob> jobs)
        {
            bool any = false;
            foreach (ConversionJob job in jobs)
            {
                if (job != null)
                {
                    this.ConversionService.RegisterConversionJob(job);
                    any = true;
                }
            }

            if (any)
            {
                this.ConversionService.ConvertFilesAsync();
                this.OnNavigateToQueue?.Invoke();
            }
        }

        private void BrowseFiles()
        {
            string filter = "All supported files|*.*";
            if (this.Tool?.AcceptedExtensions != null && this.Tool.AcceptedExtensions.Length > 0)
            {
                string patterns = string.Join(";", this.Tool.AcceptedExtensions.Select(e => "*" + e));
                filter = $"{this.Tool.Title}|{patterns}|All files|*.*";
            }

            var dialog = new OpenFileDialog
            {
                Title = this.Tool?.Title ?? "Select files",
                Multiselect = this.Tool?.AllowsMultipleFiles ?? true,
                Filter = filter,
            };

            if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
            {
                this.LoadFiles(dialog.FileNames);
            }
        }

        private void RemoveFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            this.InputFiles.Remove(path);
            this.OnFilesChanged();
        }

        private void MoveFile(string path, int delta)
        {
            int idx = this.InputFiles.IndexOf(path);
            if (idx < 0)
            {
                return;
            }

            int newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= this.InputFiles.Count)
            {
                return;
            }

            this.InputFiles.Move(idx, newIdx);
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

        private void ExecuteWrapper()
        {
            this.StatusMessage = string.Empty;
            try
            {
                this.Execute();
            }
            catch (Exception ex)
            {
                this.StatusMessage = ex.Message;
            }
        }
    }
}
