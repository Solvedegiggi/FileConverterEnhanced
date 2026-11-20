namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;

    using CommunityToolkit.Mvvm.Input;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;
    using FileConverter.Services;

    using Microsoft.Win32;

    public class PdfEditWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private readonly List<PdfEditOperation> allOperations = new List<PdfEditOperation>();
        private readonly Stack<PdfEditOperation> redoStack = new Stack<PdfEditOperation>();

        private PdfEditToolMode selectedMode = PdfEditToolMode.SelectAndEditText;
        private int currentPageIndex;
        private int totalPages;
        private BitmapSource previewImage;
        private double pageWidthPt = 595.0;
        private double pageHeightPt = 842.0;

        private string fontFamily = "Arial";
        private double fontSizePt = 14.0;
        private string colorHex = "#DC2626";
        private double strokeWidth = 2.5;
        private double zoom = 1.0;

        private PdfTextBlockInfo selectedTextBlock;
        private string inlineEditText = string.Empty;
        private string newTextDraft = "Sample text";
        private string selectedImagePath = string.Empty;

        public PdfEditWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
            this.PrevPageCommand = new RelayCommand(() => this.NavigatePage(-1), () => this.CurrentPageIndex > 0);
            this.NextPageCommand = new RelayCommand(() => this.NavigatePage(1), () => this.CurrentPageIndex + 1 < this.TotalPages);
            this.UndoCommand = new RelayCommand(this.Undo, () => this.allOperations.Count > 0);
            this.RedoCommand = new RelayCommand(this.Redo, () => this.redoStack.Count > 0);
            this.PickImageCommand = new RelayCommand(this.PickOverlayImage);
            this.ApplyTextEditCommand = new RelayCommand(this.ApplyInlineTextEdit, () => this.SelectedTextBlock != null);
            this.DeleteExistingTextCommand = new RelayCommand(this.DeleteInlineTextBlock, () => this.SelectedTextBlock != null);
            this.ClearPageEditsCommand = new RelayCommand(this.ClearCurrentPageOperations);
        }

        public event EventHandler CanvasStateChanged;

        public ObservableCollection<PdfTextBlockInfo> CurrentPageTextBlocks { get; } = new ObservableCollection<PdfTextBlockInfo>();

        public string[] AvailableFonts { get; } = { "Arial", "Calibri", "Segoe UI", "Times New Roman", "Consolas" };

        public string[] ColorPalette { get; } = { "#111827", "#DC2626", "#2563EB", "#16A34A", "#FACC15", "#9333EA", "#FFFFFF" };

        public PdfEditToolMode SelectedMode
        {
            get => this.selectedMode;
            set
            {
                if (this.SetProperty(ref this.selectedMode, value))
                {
                    this.OnPropertyChanged(nameof(this.IsEditTextMode));
                    this.OnPropertyChanged(nameof(this.IsAddTextMode));
                    this.OnPropertyChanged(nameof(this.IsAddImageMode));
                    this.OnPropertyChanged(nameof(this.IsRectMode));
                    this.OnPropertyChanged(nameof(this.IsEllipseMode));
                    this.OnPropertyChanged(nameof(this.IsLineMode));
                    this.OnPropertyChanged(nameof(this.IsArrowMode));
                    this.OnPropertyChanged(nameof(this.IsHighlightMode));
                    this.OnPropertyChanged(nameof(this.IsFreehandMode));
                    this.CanvasStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsEditTextMode { get => this.SelectedMode == PdfEditToolMode.SelectAndEditText; set { if (value) { this.SelectedMode = PdfEditToolMode.SelectAndEditText; } } }

        public bool IsAddTextMode { get => this.SelectedMode == PdfEditToolMode.AddText; set { if (value) { this.SelectedMode = PdfEditToolMode.AddText; } } }

        public bool IsAddImageMode { get => this.SelectedMode == PdfEditToolMode.AddImage; set { if (value) { this.SelectedMode = PdfEditToolMode.AddImage; } } }

        public bool IsRectMode { get => this.SelectedMode == PdfEditToolMode.Rectangle; set { if (value) { this.SelectedMode = PdfEditToolMode.Rectangle; } } }

        public bool IsEllipseMode { get => this.SelectedMode == PdfEditToolMode.Ellipse; set { if (value) { this.SelectedMode = PdfEditToolMode.Ellipse; } } }

        public bool IsLineMode { get => this.SelectedMode == PdfEditToolMode.Line; set { if (value) { this.SelectedMode = PdfEditToolMode.Line; } } }

        public bool IsArrowMode { get => this.SelectedMode == PdfEditToolMode.Arrow; set { if (value) { this.SelectedMode = PdfEditToolMode.Arrow; } } }

        public bool IsHighlightMode { get => this.SelectedMode == PdfEditToolMode.Highlight; set { if (value) { this.SelectedMode = PdfEditToolMode.Highlight; } } }

        public bool IsFreehandMode { get => this.SelectedMode == PdfEditToolMode.Freehand; set { if (value) { this.SelectedMode = PdfEditToolMode.Freehand; } } }

        public BitmapSource PreviewImage
        {
            get => this.previewImage;
            private set => this.SetProperty(ref this.previewImage, value);
        }

        public double PageWidthPt => this.pageWidthPt;

        public double PageHeightPt => this.pageHeightPt;

        public int CurrentPageIndex
        {
            get => this.currentPageIndex;
            set
            {
                if (this.SetProperty(ref this.currentPageIndex, value))
                {
                    this.SelectedTextBlock = null;
                    this.OnPropertyChanged(nameof(this.PageIndicatorText));
                    ((RelayCommand)this.PrevPageCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)this.NextPageCommand).NotifyCanExecuteChanged();
                    this.RefreshCurrentPage();
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

        public string PageIndicatorText => this.TotalPages > 0 ? $"Page {this.CurrentPageIndex + 1} / {this.TotalPages}" : "No PDF";

        public string FontFamily
        {
            get => this.fontFamily;
            set => this.SetProperty(ref this.fontFamily, value);
        }

        public double FontSizePt
        {
            get => this.fontSizePt;
            set => this.SetProperty(ref this.fontSizePt, Math.Max(6, Math.Min(72, value)));
        }

        public string ColorHex
        {
            get => this.colorHex;
            set => this.SetProperty(ref this.colorHex, value);
        }

        public double StrokeWidth
        {
            get => this.strokeWidth;
            set => this.SetProperty(ref this.strokeWidth, Math.Max(0.5, Math.Min(20, value)));
        }

        public double Zoom
        {
            get => this.zoom;
            set
            {
                if (this.SetProperty(ref this.zoom, Math.Max(0.4, Math.Min(2.5, value))))
                {
                    this.CanvasStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public PdfTextBlockInfo SelectedTextBlock
        {
            get => this.selectedTextBlock;
            set
            {
                if (this.SetProperty(ref this.selectedTextBlock, value))
                {
                    this.InlineEditText = value?.Text ?? string.Empty;
                    this.OnPropertyChanged(nameof(this.HasSelectedTextBlock));
                    ((RelayCommand)this.ApplyTextEditCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)this.DeleteExistingTextCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasSelectedTextBlock => this.SelectedTextBlock != null;

        public string InlineEditText
        {
            get => this.inlineEditText;
            set => this.SetProperty(ref this.inlineEditText, value);
        }

        public string NewTextDraft
        {
            get => this.newTextDraft;
            set => this.SetProperty(ref this.newTextDraft, value);
        }

        public string SelectedImagePath
        {
            get => this.selectedImagePath;
            set => this.SetProperty(ref this.selectedImagePath, value);
        }

        public ICommand PrevPageCommand { get; }

        public ICommand NextPageCommand { get; }

        public ICommand UndoCommand { get; }

        public ICommand RedoCommand { get; }

        public ICommand PickImageCommand { get; }

        public ICommand ApplyTextEditCommand { get; }

        public ICommand DeleteExistingTextCommand { get; }

        public ICommand ClearPageEditsCommand { get; }

        public IReadOnlyList<PdfEditOperation> GetOperationsForCurrentPage()
        {
            return this.allOperations.Where(o => o.PageIndex == this.CurrentPageIndex).ToList();
        }

        public void AddOperation(PdfEditOperation op)
        {
            if (op == null)
            {
                return;
            }

            op.PageIndex = this.CurrentPageIndex;
            this.allOperations.Add(op);
            this.redoStack.Clear();
            this.NotifyHistoryChanged();
            this.CanvasStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectTextBlock(PdfTextBlockInfo block)
        {
            this.SelectedTextBlock = block;
        }

        protected override bool CanExecute()
        {
            return this.InputFiles.Count > 0 && this.allOperations.Count > 0;
        }

        protected override void OnFilesChanged()
        {
            base.OnFilesChanged();
            this.allOperations.Clear();
            this.redoStack.Clear();
            this.SelectedTextBlock = null;
            this.CurrentPageTextBlocks.Clear();

            string path = this.InputFiles.Count > 0 ? this.InputFiles[0] : null;
            this.TotalPages = !string.IsNullOrEmpty(path) && File.Exists(path) ? PdfRenderService.GetPageCount(path) : 0;
            this.currentPageIndex = 0;
            this.OnPropertyChanged(nameof(this.CurrentPageIndex));
            this.OnPropertyChanged(nameof(this.PageIndicatorText));
            this.RefreshCurrentPage();
            this.NotifyHistoryChanged();
        }

        protected override void Execute()
        {
            var preset = new ConversionPreset("EditPdf", OutputType.Pdf, new[] { "pdf" });
            this.ApplyOutputTemplate(preset);
            this.QueueAndRun(new ConversionJob_PdfEdit(preset, this.InputFiles[0], this.allOperations.ToList()));
        }

        private void NavigatePage(int delta)
        {
            int next = this.CurrentPageIndex + delta;
            if (next >= 0 && next < this.TotalPages)
            {
                this.CurrentPageIndex = next;
            }
        }

        private void Undo()
        {
            if (this.allOperations.Count == 0)
            {
                return;
            }

            PdfEditOperation last = this.allOperations[this.allOperations.Count - 1];
            this.allOperations.RemoveAt(this.allOperations.Count - 1);
            this.redoStack.Push(last);
            this.NotifyHistoryChanged();
            this.CanvasStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Redo()
        {
            if (this.redoStack.Count == 0)
            {
                return;
            }

            this.allOperations.Add(this.redoStack.Pop());
            this.NotifyHistoryChanged();
            this.CanvasStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearCurrentPageOperations()
        {
            this.allOperations.RemoveAll(o => o.PageIndex == this.CurrentPageIndex);
            this.redoStack.Clear();
            this.NotifyHistoryChanged();
            this.CanvasStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PickOverlayImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
            };
            if (dialog.ShowDialog() == true)
            {
                this.SelectedImagePath = dialog.FileName;
                this.SelectedMode = PdfEditToolMode.AddImage;
            }
        }

        private void ApplyInlineTextEdit()
        {
            if (this.SelectedTextBlock == null)
            {
                return;
            }

            this.AddOperation(new PdfEditOperation
            {
                Kind = PdfEditOperationKind.RedactAndReplaceText,
                OriginalText = this.SelectedTextBlock.Text,
                Text = this.InlineEditText ?? string.Empty,
                FontFamily = this.FontFamily,
                FontSizePt = this.SelectedTextBlock.FontSizePt > 0 ? this.SelectedTextBlock.FontSizePt : this.FontSizePt,
                ColorHex = this.ColorHex,
                BackgroundColorHex = "#FFFFFF",
                NormX = this.SelectedTextBlock.NormX,
                NormY = this.SelectedTextBlock.NormY,
                NormWidth = this.SelectedTextBlock.NormWidth,
                NormHeight = this.SelectedTextBlock.NormHeight,
                PdfLeft = this.SelectedTextBlock.PdfLeft,
                PdfBottom = this.SelectedTextBlock.PdfBottom,
                PdfRight = this.SelectedTextBlock.PdfRight,
                PdfTop = this.SelectedTextBlock.PdfTop,
            });
            this.SelectedTextBlock = null;
        }

        private void DeleteInlineTextBlock()
        {
            if (this.SelectedTextBlock == null)
            {
                return;
            }

            this.AddOperation(new PdfEditOperation
            {
                Kind = PdfEditOperationKind.RedactAndReplaceText,
                OriginalText = this.SelectedTextBlock.Text,
                Text = string.Empty,
                BackgroundColorHex = "#FFFFFF",
                NormX = this.SelectedTextBlock.NormX,
                NormY = this.SelectedTextBlock.NormY,
                NormWidth = this.SelectedTextBlock.NormWidth,
                NormHeight = this.SelectedTextBlock.NormHeight,
                PdfLeft = this.SelectedTextBlock.PdfLeft,
                PdfBottom = this.SelectedTextBlock.PdfBottom,
                PdfRight = this.SelectedTextBlock.PdfRight,
                PdfTop = this.SelectedTextBlock.PdfTop,
            });
            this.SelectedTextBlock = null;
        }

        private void RefreshCurrentPage()
        {
            this.CurrentPageTextBlocks.Clear();
            string path = this.InputFiles.Count > 0 ? this.InputFiles[0] : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || this.TotalPages <= 0)
            {
                this.PreviewImage = null;
                this.CanvasStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            (this.pageWidthPt, this.pageHeightPt) = PdfRenderService.GetPageSizePoints(path, this.CurrentPageIndex);
            this.PreviewImage = PdfRenderService.RenderPageToBitmapSource(path, this.CurrentPageIndex, 900, 1250);

            foreach (PdfTextBlockInfo block in PdfTextBlockService.ExtractPageTextBlocks(path, this.CurrentPageIndex))
            {
                this.CurrentPageTextBlocks.Add(block);
            }

            this.CanvasStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NotifyHistoryChanged()
        {
            ((RelayCommand)this.UndoCommand).NotifyCanExecuteChanged();
            ((RelayCommand)this.RedoCommand).NotifyCanExecuteChanged();
            this.NotifyCanExecuteChanged();
        }
    }
}
