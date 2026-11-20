namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows.Input;

    using CommunityToolkit.Mvvm.Input;

    using FileConverter.Models;
    using FileConverter.Services;

    using Microsoft.Win32;

    public class PdfCompareWorkspaceViewModel : WorkspaceViewModelBase
    {
        private string pdfPathA = string.Empty;
        private string pdfPathB = string.Empty;
        private int currentPageIndex;
        private int totalPages;
        private double zoom = 1.0;
        private string viewMode = "SideBySide";
        private PdfPageCompareResult compareResult;
        private string statusMessage = string.Empty;

        public PdfCompareWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
            this.BrowsePdfACommand = new RelayCommand(() => this.BrowsePdf(isLeft: true));
            this.BrowsePdfBCommand = new RelayCommand(() => this.BrowsePdf(isLeft: false));
            this.PrevPageCommand = new RelayCommand(() => this.NavigatePage(-1), () => this.CurrentPageIndex > 0);
            this.NextPageCommand = new RelayCommand(() => this.NavigatePage(1), () => this.CurrentPageIndex + 1 < this.TotalPages);
            this.ExportReportCommand = new RelayCommand(this.ExportReport, this.CanCompare);
        }

        public string PdfPathA
        {
            get => this.pdfPathA;
            set
            {
                if (this.SetProperty(ref this.pdfPathA, value))
                {
                    this.OnPropertyChanged(nameof(this.PdfNameA));
                    this.RecomputeComparison(resetPage: true);
                }
            }
        }

        public string PdfPathB
        {
            get => this.pdfPathB;
            set
            {
                if (this.SetProperty(ref this.pdfPathB, value))
                {
                    this.OnPropertyChanged(nameof(this.PdfNameB));
                    this.RecomputeComparison(resetPage: true);
                }
            }
        }

        public string PdfNameA => string.IsNullOrWhiteSpace(this.PdfPathA) ? "Select Document A..." : Path.GetFileName(this.PdfPathA);

        public string PdfNameB => string.IsNullOrWhiteSpace(this.PdfPathB) ? "Select Document B..." : Path.GetFileName(this.PdfPathB);

        public int CurrentPageIndex
        {
            get => this.currentPageIndex;
            set
            {
                if (this.SetProperty(ref this.currentPageIndex, value))
                {
                    this.OnPropertyChanged(nameof(this.PageIndicatorText));
                    ((RelayCommand)this.PrevPageCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)this.NextPageCommand).NotifyCanExecuteChanged();
                    this.RecomputeComparison(resetPage: false);
                }
            }
        }

        public int TotalPages
        {
            get => this.totalPages;
            private set
            {
                if (this.SetProperty(ref this.totalPages, value))
                {
                    this.OnPropertyChanged(nameof(this.PageIndicatorText));
                    ((RelayCommand)this.PrevPageCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)this.NextPageCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public string PageIndicatorText => this.TotalPages > 0 ? $"Page {this.CurrentPageIndex + 1} / {this.TotalPages}" : "Select two PDF files";

        public double Zoom
        {
            get => this.zoom;
            set => this.SetProperty(ref this.zoom, Math.Max(0.4, Math.Min(2.5, value)));
        }

        public string ViewMode
        {
            get => this.viewMode;
            set
            {
                if (this.SetProperty(ref this.viewMode, value))
                {
                    this.OnPropertyChanged(nameof(this.IsSideBySideMode));
                    this.OnPropertyChanged(nameof(this.IsDiffOverlayMode));
                    this.OnPropertyChanged(nameof(this.IsTextDiffMode));
                }
            }
        }

        public bool IsSideBySideMode { get => this.ViewMode == "SideBySide"; set { if (value) { this.ViewMode = "SideBySide"; } } }

        public bool IsDiffOverlayMode { get => this.ViewMode == "DiffOverlay"; set { if (value) { this.ViewMode = "DiffOverlay"; } } }

        public bool IsTextDiffMode { get => this.ViewMode == "TextDiff"; set { if (value) { this.ViewMode = "TextDiff"; } } }

        public PdfPageCompareResult CompareResult
        {
            get => this.compareResult;
            private set
            {
                if (this.SetProperty(ref this.compareResult, value))
                {
                    this.OnPropertyChanged(nameof(this.DiffSummaryText));
                }
            }
        }

        public string DiffSummaryText => this.CompareResult != null
            ? $"Visual diff: {this.CompareResult.ChangedPixelPercent:F2}%  |  +{this.CompareResult.AddedLinesCount} / -{this.CompareResult.RemovedLinesCount} lines"
            : string.Empty;

        public string StatusMessage
        {
            get => this.statusMessage;
            private set => this.SetProperty(ref this.statusMessage, value);
        }

        public ICommand BrowsePdfACommand { get; }

        public ICommand BrowsePdfBCommand { get; }

        public ICommand PrevPageCommand { get; }

        public ICommand NextPageCommand { get; }

        public ICommand ExportReportCommand { get; }

        public override void LoadFiles(IReadOnlyList<string> files)
        {
            base.LoadFiles(files);
            if (files == null || files.Count == 0)
            {
                return;
            }

            foreach (string path in files)
            {
                if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(this.PdfPathA))
                {
                    this.PdfPathA = path;
                }
                else if (string.IsNullOrEmpty(this.PdfPathB))
                {
                    this.PdfPathB = path;
                }
                else
                {
                    this.PdfPathB = path;
                }
            }
        }

        private void BrowsePdf(bool isLeft)
        {
            var dialog = new OpenFileDialog { Filter = "PDF Files|*.pdf" };
            if (dialog.ShowDialog() == true)
            {
                if (isLeft)
                {
                    this.PdfPathA = dialog.FileName;
                }
                else
                {
                    this.PdfPathB = dialog.FileName;
                }
            }
        }

        private bool CanCompare()
        {
            return File.Exists(this.PdfPathA) && File.Exists(this.PdfPathB);
        }

        private void NavigatePage(int delta)
        {
            int next = this.CurrentPageIndex + delta;
            if (next >= 0 && next < this.TotalPages)
            {
                this.CurrentPageIndex = next;
            }
        }

        private void RecomputeComparison(bool resetPage)
        {
            ((RelayCommand)this.ExportReportCommand).NotifyCanExecuteChanged();
            this.StatusMessage = string.Empty;

            if (!this.CanCompare())
            {
                this.TotalPages = 0;
                this.CompareResult = null;
                return;
            }

            int countA = PdfRenderService.GetPageCount(this.PdfPathA);
            int countB = PdfRenderService.GetPageCount(this.PdfPathB);
            this.TotalPages = Math.Max(countA, countB);

            if (resetPage)
            {
                this.currentPageIndex = 0;
                this.OnPropertyChanged(nameof(this.CurrentPageIndex));
                this.OnPropertyChanged(nameof(this.PageIndicatorText));
                ((RelayCommand)this.PrevPageCommand).NotifyCanExecuteChanged();
                ((RelayCommand)this.NextPageCommand).NotifyCanExecuteChanged();
            }

            if (this.TotalPages > 0)
            {
                this.CompareResult = PdfCompareService.ComparePages(this.PdfPathA, this.PdfPathB, this.CurrentPageIndex, 144);
            }
        }

        private void ExportReport()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Markdown Report|*.md|Text File|*.txt",
                FileName = "pdf_compare_report.md",
            };
            if (dialog.ShowDialog() == true)
            {
                PdfCompareService.ExportMarkdownReport(this.PdfPathA, this.PdfPathB, dialog.FileName);
                this.StatusMessage = $"Report saved: {dialog.FileName}";
            }
        }
    }
}
