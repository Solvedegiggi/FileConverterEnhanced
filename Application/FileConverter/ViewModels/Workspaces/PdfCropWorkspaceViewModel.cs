namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.IO;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;

    using CommunityToolkit.Mvvm.Input;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;

    public class PdfCropWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private const double MmPerPoint = 25.4 / 72.0;
        private const double PointsPerMm = 72.0 / 25.4;

        private int currentPageIndex;
        private int totalPages;
        private BitmapSource previewImage;
        private double pageWidthPt = 595.0;
        private double pageHeightPt = 842.0;

        private double leftMm;
        private double topMm;
        private double rightMm;
        private double bottomMm;
        private string scope = "AllPages";
        private string pageRange = string.Empty;

        public PdfCropWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
            this.PrevPageCommand = new RelayCommand(() => this.NavigatePage(-1), () => this.CurrentPageIndex > 0);
            this.NextPageCommand = new RelayCommand(() => this.NavigatePage(1), () => this.CurrentPageIndex + 1 < this.TotalPages);
            this.ResetMarginsCommand = new RelayCommand(this.ResetMargins);
        }

        public event EventHandler CropChanged;

        public BitmapSource PreviewImage
        {
            get => this.previewImage;
            private set => this.SetProperty(ref this.previewImage, value);
        }

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
                    this.RefreshPreview();
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

        public string PageIndicatorText => this.TotalPages > 0 ? $"Page {this.CurrentPageIndex + 1} / {this.TotalPages}" : "No PDF loaded";

        public double LeftMm
        {
            get => this.leftMm;
            set
            {
                if (this.SetProperty(ref this.leftMm, Math.Max(0, value)))
                {
                    this.CropChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double TopMm
        {
            get => this.topMm;
            set
            {
                if (this.SetProperty(ref this.topMm, Math.Max(0, value)))
                {
                    this.CropChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double RightMm
        {
            get => this.rightMm;
            set
            {
                if (this.SetProperty(ref this.rightMm, Math.Max(0, value)))
                {
                    this.CropChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double BottomMm
        {
            get => this.bottomMm;
            set
            {
                if (this.SetProperty(ref this.bottomMm, Math.Max(0, value)))
                {
                    this.CropChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double NormLeft => Math.Max(0, Math.Min(0.9, (this.LeftMm * PointsPerMm) / Math.Max(1.0, this.pageWidthPt)));

        public double NormTop => Math.Max(0, Math.Min(0.9, (this.TopMm * PointsPerMm) / Math.Max(1.0, this.pageHeightPt)));

        public double NormRight => Math.Max(0, Math.Min(0.9, (this.RightMm * PointsPerMm) / Math.Max(1.0, this.pageWidthPt)));

        public double NormBottom => Math.Max(0, Math.Min(0.9, (this.BottomMm * PointsPerMm) / Math.Max(1.0, this.pageHeightPt)));

        public string Scope
        {
            get => this.scope;
            set
            {
                if (this.SetProperty(ref this.scope, value))
                {
                    this.OnPropertyChanged(nameof(this.IsAllPages));
                    this.OnPropertyChanged(nameof(this.IsCurrentPage));
                    this.OnPropertyChanged(nameof(this.IsCustomRange));
                }
            }
        }

        public bool IsAllPages { get => this.Scope == "AllPages"; set { if (value) { this.Scope = "AllPages"; } } }

        public bool IsCurrentPage { get => this.Scope == "CurrentPage"; set { if (value) { this.Scope = "CurrentPage"; } } }

        public bool IsCustomRange { get => this.Scope == "CustomRange"; set { if (value) { this.Scope = "CustomRange"; } } }

        public string PageRange
        {
            get => this.pageRange;
            set => this.SetProperty(ref this.pageRange, value);
        }

        public ICommand PrevPageCommand { get; }

        public ICommand NextPageCommand { get; }

        public ICommand ResetMarginsCommand { get; }

        public void SetCropFromNormalizedRect(double normX, double normY, double normW, double normH)
        {
            normX = Math.Max(0, Math.Min(0.95, normX));
            normY = Math.Max(0, Math.Min(0.95, normY));
            double normR = Math.Max(0, 1.0 - (normX + normW));
            double normB = Math.Max(0, 1.0 - (normY + normH));

            this.leftMm = Math.Round(normX * this.pageWidthPt * MmPerPoint, 1);
            this.topMm = Math.Round(normY * this.pageHeightPt * MmPerPoint, 1);
            this.rightMm = Math.Round(normR * this.pageWidthPt * MmPerPoint, 1);
            this.bottomMm = Math.Round(normB * this.pageHeightPt * MmPerPoint, 1);

            this.OnPropertyChanged(nameof(this.LeftMm));
            this.OnPropertyChanged(nameof(this.TopMm));
            this.OnPropertyChanged(nameof(this.RightMm));
            this.OnPropertyChanged(nameof(this.BottomMm));
            this.CropChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnFilesChanged()
        {
            base.OnFilesChanged();
            string path = this.InputFiles.Count > 0 ? this.InputFiles[0] : null;
            this.TotalPages = !string.IsNullOrEmpty(path) && File.Exists(path) ? PdfRenderService.GetPageCount(path) : 0;
            this.currentPageIndex = 0;
            this.OnPropertyChanged(nameof(this.CurrentPageIndex));
            this.OnPropertyChanged(nameof(this.PageIndicatorText));
            ((RelayCommand)this.PrevPageCommand).NotifyCanExecuteChanged();
            ((RelayCommand)this.NextPageCommand).NotifyCanExecuteChanged();
            this.RefreshPreview();
        }

        protected override void Execute()
        {
            var settings = new PdfCropSettings
            {
                LeftMarginPt = this.LeftMm * PointsPerMm,
                TopMarginPt = this.TopMm * PointsPerMm,
                RightMarginPt = this.RightMm * PointsPerMm,
                BottomMarginPt = this.BottomMm * PointsPerMm,
                Scope = this.Scope,
                CurrentPageIndex = this.CurrentPageIndex,
                PageRange = this.PageRange ?? string.Empty,
            };

            var preset = new ConversionPreset("CropPdf", OutputType.Pdf, new[] { "pdf" });
            this.ApplyOutputTemplate(preset);
            this.QueueAndRun(new ConversionJob_PdfCrop(preset, this.InputFiles[0], settings));
        }

        private void NavigatePage(int delta)
        {
            int next = this.CurrentPageIndex + delta;
            if (next >= 0 && next < this.TotalPages)
            {
                this.CurrentPageIndex = next;
            }
        }

        private void ResetMargins()
        {
            this.leftMm = 0;
            this.topMm = 0;
            this.rightMm = 0;
            this.bottomMm = 0;
            this.OnPropertyChanged(nameof(this.LeftMm));
            this.OnPropertyChanged(nameof(this.TopMm));
            this.OnPropertyChanged(nameof(this.RightMm));
            this.OnPropertyChanged(nameof(this.BottomMm));
            this.CropChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshPreview()
        {
            string path = this.InputFiles.Count > 0 ? this.InputFiles[0] : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || this.TotalPages <= 0)
            {
                this.PreviewImage = null;
                return;
            }

            (this.pageWidthPt, this.pageHeightPt) = PdfRenderService.GetPageSizePoints(path, this.CurrentPageIndex);
            this.PreviewImage = PdfRenderService.RenderPageToBitmapSource(path, this.CurrentPageIndex, 600, 820);
            this.CropChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
