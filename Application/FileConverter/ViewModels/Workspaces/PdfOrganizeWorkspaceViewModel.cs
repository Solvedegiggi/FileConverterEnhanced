namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;

    using CommunityToolkit.Mvvm.Input;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;

    public class PdfOrganizeWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        public PdfOrganizeWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
            this.Pages = new ObservableCollection<OrganizePageItem>();
            this.MovePageLeftCommand = new RelayCommand<OrganizePageItem>(item => this.MovePage(item, -1));
            this.MovePageRightCommand = new RelayCommand<OrganizePageItem>(item => this.MovePage(item, 1));
            this.RotatePageCommand = new RelayCommand<OrganizePageItem>(this.RotatePage);
            this.DuplicatePageCommand = new RelayCommand<OrganizePageItem>(this.DuplicatePage);
            this.DeletePageCommand = new RelayCommand<OrganizePageItem>(this.DeletePage);
            this.ClearPagesCommand = new RelayCommand(this.ClearAllPages);
        }

        public ObservableCollection<OrganizePageItem> Pages { get; }

        public ICommand MovePageLeftCommand { get; }

        public ICommand MovePageRightCommand { get; }

        public ICommand RotatePageCommand { get; }

        public ICommand DuplicatePageCommand { get; }

        public ICommand DeletePageCommand { get; }

        public ICommand ClearPagesCommand { get; }

        public override void LoadFiles(IReadOnlyList<string> files)
        {
            if (files == null)
            {
                return;
            }

            var newItems = new List<OrganizePageItem>();
            foreach (string path in files)
            {
                if (string.IsNullOrWhiteSpace(path) ||
                    !File.Exists(path) ||
                    !string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!this.InputFiles.Contains(path))
                {
                    this.InputFiles.Add(path);
                }

                int pageCount = PdfRenderService.GetPageCount(path);
                string fileName = Path.GetFileName(path);
                for (int p = 0; p < pageCount; p++)
                {
                    var item = new OrganizePageItem
                    {
                        SourceFilePath = path,
                        SourcePageIndex = p,
                        PageNumberLabel = $"{fileName} • P.{p + 1}",
                    };
                    this.Pages.Add(item);
                    newItems.Add(item);
                }
            }

            this.NotifyCanExecuteChanged();
            _ = this.RenderThumbnailsAsync(newItems);
        }

        protected override bool CanExecute()
        {
            return this.Pages.Count > 0;
        }

        protected override void Execute()
        {
            string primary = this.Pages[0].SourceFilePath;
            List<OrganizePageSpec> specs = this.Pages.Select(p => new OrganizePageSpec
            {
                SourceFilePath = p.SourceFilePath,
                SourcePageIndex = p.SourcePageIndex,
                RotationDelta = p.RotationDelta,
            }).ToList();

            var preset = new ConversionPreset("OrganizePdf", OutputType.Pdf, new[] { "pdf" });
            this.ApplyOutputTemplate(preset, this.OutputFileName);

            this.QueueAndRun(new ConversionJob_PdfOrganize(preset, primary, specs));
        }

        private void MovePage(OrganizePageItem item, int delta)
        {
            if (item == null)
            {
                return;
            }

            int idx = this.Pages.IndexOf(item);
            int target = idx + delta;
            if (idx < 0 || target < 0 || target >= this.Pages.Count)
            {
                return;
            }

            this.Pages.Move(idx, target);
        }

        private void RotatePage(OrganizePageItem item)
        {
            if (item != null)
            {
                item.RotationDelta = (item.RotationDelta + 90) % 360;
            }
        }

        private void DuplicatePage(OrganizePageItem item)
        {
            if (item == null)
            {
                return;
            }

            int idx = this.Pages.IndexOf(item);
            if (idx < 0)
            {
                return;
            }

            var copy = new OrganizePageItem
            {
                SourceFilePath = item.SourceFilePath,
                SourcePageIndex = item.SourcePageIndex,
                RotationDelta = item.RotationDelta,
                Thumbnail = item.Thumbnail,
                PageNumberLabel = item.PageNumberLabel + " (copy)",
            };
            this.Pages.Insert(idx + 1, copy);
            this.NotifyCanExecuteChanged();
        }

        private void DeletePage(OrganizePageItem item)
        {
            if (item != null)
            {
                this.Pages.Remove(item);
                this.NotifyCanExecuteChanged();
            }
        }

        private void ClearAllPages()
        {
            this.Pages.Clear();
            this.InputFiles.Clear();
            this.NotifyCanExecuteChanged();
        }

        private async Task RenderThumbnailsAsync(List<OrganizePageItem> items)
        {
            foreach (OrganizePageItem item in items)
            {
                BitmapSource bmp = await Task.Run(() =>
                    PdfRenderService.RenderPageToBitmapSource(item.SourceFilePath, item.SourcePageIndex, 160, 220)).ConfigureAwait(false);

                if (bmp == null)
                {
                    continue;
                }

                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() => item.Thumbnail = bmp);
                }
                else
                {
                    item.Thumbnail = bmp;
                }
            }
        }
    }
}
