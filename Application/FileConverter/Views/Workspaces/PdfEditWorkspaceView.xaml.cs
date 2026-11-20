namespace FileConverter.Views.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Shapes;

    using FileConverter.Models;
    using FileConverter.ViewModels.Workspaces;

    public partial class PdfEditWorkspaceView : UserControl
    {
        private bool isDrawing;
        private Point dragStart;
        private Shape livePreviewShape;
        private Polyline livePolyline;
        private readonly List<PdfEditPoint> liveFreehandPoints = new List<PdfEditPoint>();

        public PdfEditWorkspaceView()
        {
            this.InitializeComponent();
            this.DataContextChanged += this.OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PdfEditWorkspaceViewModel oldVm)
            {
                oldVm.CanvasStateChanged -= this.Vm_CanvasStateChanged;
            }

            if (e.NewValue is PdfEditWorkspaceViewModel newVm)
            {
                newVm.CanvasStateChanged += this.Vm_CanvasStateChanged;
                this.UpdateStageSizeAndRedraw();
            }
        }

        private void Vm_CanvasStateChanged(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(this.UpdateStageSizeAndRedraw));
        }

        private void UpdateStageSizeAndRedraw()
        {
            if (!(this.DataContext is PdfEditWorkspaceViewModel vm))
            {
                return;
            }

            if (vm.PreviewImage == null)
            {
                this.PageStageGrid.Width = 0;
                this.PageStageGrid.Height = 0;
                this.EditOverlayCanvas.Children.Clear();
                return;
            }

            double scale = 1.15 * vm.Zoom;
            double w = Math.Max(120, vm.PageWidthPt * scale);
            double h = Math.Max(120, vm.PageHeightPt * scale);

            this.PageStageGrid.Width = w;
            this.PageStageGrid.Height = h;
            this.EditOverlayCanvas.Width = w;
            this.EditOverlayCanvas.Height = h;

            this.RedrawOverlay(vm, w, h);
        }

        private void RedrawOverlay(PdfEditWorkspaceViewModel vm, double w, double h)
        {
            this.EditOverlayCanvas.Children.Clear();

            // Draw clickable hints around existing PDF text blocks when Edit Text mode is active
            if (vm.SelectedMode == PdfEditToolMode.SelectAndEditText)
            {
                foreach (PdfTextBlockInfo block in vm.CurrentPageTextBlocks)
                {
                    bool isSelected = block == vm.SelectedTextBlock;
                    var box = new Rectangle
                    {
                        Width = Math.Max(4, block.NormWidth * w),
                        Height = Math.Max(4, block.NormHeight * h),
                        Stroke = new SolidColorBrush(isSelected ? Color.FromRgb(1, 113, 187) : Color.FromArgb(140, 1, 113, 187)),
                        StrokeThickness = isSelected ? 2 : 1,
                        StrokeDashArray = isSelected ? null : new DoubleCollection { 3, 2 },
                        Fill = new SolidColorBrush(isSelected ? Color.FromArgb(40, 1, 113, 187) : Color.FromArgb(12, 1, 113, 187)),
                        IsHitTestVisible = false,
                    };
                    Canvas.SetLeft(box, block.NormX * w);
                    Canvas.SetTop(box, block.NormY * h);
                    this.EditOverlayCanvas.Children.Add(box);
                }
            }

            foreach (PdfEditOperation op in vm.GetOperationsForCurrentPage())
            {
                this.RenderOperationPreview(op, w, h);
            }
        }

        private void RenderOperationPreview(PdfEditOperation op, double w, double h)
        {
            double x = op.NormX * w;
            double y = op.NormY * h;
            double rw = Math.Max(4, op.NormWidth * w);
            double rh = Math.Max(4, op.NormHeight * h);
            Color fg = ParseColor(op.ColorHex, Colors.Crimson);

            switch (op.Kind)
            {
                case PdfEditOperationKind.RedactAndReplaceText:
                {
                    var bg = new Rectangle
                    {
                        Width = rw + 4,
                        Height = rh + 4,
                        Fill = Brushes.White,
                        IsHitTestVisible = false,
                    };
                    Canvas.SetLeft(bg, Math.Max(0, x - 2));
                    Canvas.SetTop(bg, Math.Max(0, y - 2));
                    this.EditOverlayCanvas.Children.Add(bg);

                    if (!string.IsNullOrEmpty(op.Text))
                    {
                        var tb = new TextBlock
                        {
                            Text = op.Text,
                            FontFamily = new FontFamily(op.FontFamily ?? "Arial"),
                            FontSize = Math.Max(8, op.FontSizePt * (w / 595.0)),
                            Foreground = new SolidColorBrush(fg),
                            IsHitTestVisible = false,
                        };
                        Canvas.SetLeft(tb, x);
                        Canvas.SetTop(tb, y);
                        this.EditOverlayCanvas.Children.Add(tb);
                    }

                    break;
                }

                case PdfEditOperationKind.AddText:
                {
                    var tb = new TextBlock
                    {
                        Text = op.Text,
                        FontFamily = new FontFamily(op.FontFamily ?? "Arial"),
                        FontSize = Math.Max(8, op.FontSizePt * (w / 595.0)),
                        Foreground = new SolidColorBrush(fg),
                        IsHitTestVisible = false,
                    };
                    Canvas.SetLeft(tb, x);
                    Canvas.SetTop(tb, y);
                    this.EditOverlayCanvas.Children.Add(tb);
                    break;
                }

                case PdfEditOperationKind.AddImage:
                {
                    if (!string.IsNullOrEmpty(op.ImagePath) && System.IO.File.Exists(op.ImagePath))
                    {
                        var img = new Image
                        {
                            Width = rw,
                            Height = rh,
                            Stretch = Stretch.Fill,
                            Source = new BitmapImage(new Uri(op.ImagePath)),
                            IsHitTestVisible = false,
                        };
                        Canvas.SetLeft(img, x);
                        Canvas.SetTop(img, y);
                        this.EditOverlayCanvas.Children.Add(img);
                    }

                    break;
                }

                case PdfEditOperationKind.Rectangle:
                {
                    var rect = new Rectangle
                    {
                        Width = rw,
                        Height = rh,
                        Stroke = new SolidColorBrush(fg),
                        StrokeThickness = Math.Max(1, op.StrokeWidth),
                        IsHitTestVisible = false,
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    this.EditOverlayCanvas.Children.Add(rect);
                    break;
                }

                case PdfEditOperationKind.Ellipse:
                {
                    var ell = new Ellipse
                    {
                        Width = rw,
                        Height = rh,
                        Stroke = new SolidColorBrush(fg),
                        StrokeThickness = Math.Max(1, op.StrokeWidth),
                        IsHitTestVisible = false,
                    };
                    Canvas.SetLeft(ell, x);
                    Canvas.SetTop(ell, y);
                    this.EditOverlayCanvas.Children.Add(ell);
                    break;
                }

                case PdfEditOperationKind.Highlight:
                {
                    var hl = new Rectangle
                    {
                        Width = rw,
                        Height = rh,
                        Fill = new SolidColorBrush(Color.FromArgb(95, fg.R, fg.G, fg.B)),
                        IsHitTestVisible = false,
                    };
                    Canvas.SetLeft(hl, x);
                    Canvas.SetTop(hl, y);
                    this.EditOverlayCanvas.Children.Add(hl);
                    break;
                }

                case PdfEditOperationKind.Line:
                case PdfEditOperationKind.Arrow:
                {
                    var line = new Line
                    {
                        X1 = x,
                        Y1 = y,
                        X2 = op.NormX2 * w,
                        Y2 = op.NormY2 * h,
                        Stroke = new SolidColorBrush(fg),
                        StrokeThickness = Math.Max(1, op.StrokeWidth),
                        IsHitTestVisible = false,
                    };
                    this.EditOverlayCanvas.Children.Add(line);
                    break;
                }

                case PdfEditOperationKind.Freehand:
                {
                    if (op.Points != null && op.Points.Count > 1)
                    {
                        var poly = new Polyline
                        {
                            Stroke = new SolidColorBrush(fg),
                            StrokeThickness = Math.Max(1, op.StrokeWidth),
                            StrokeLineJoin = PenLineJoin.Round,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round,
                            IsHitTestVisible = false,
                        };
                        foreach (PdfEditPoint pt in op.Points)
                        {
                            poly.Points.Add(new Point(pt.NormX * w, pt.NormY * h));
                        }

                        this.EditOverlayCanvas.Children.Add(poly);
                    }

                    break;
                }
            }
        }

        private void EditOverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(this.DataContext is PdfEditWorkspaceViewModel vm))
            {
                return;
            }

            double w = this.EditOverlayCanvas.ActualWidth;
            double h = this.EditOverlayCanvas.ActualHeight;
            if (w <= 10 || h <= 10)
            {
                return;
            }

            Point pos = e.GetPosition(this.EditOverlayCanvas);
            double nx = Clamp01(pos.X / w);
            double ny = Clamp01(pos.Y / h);

            if (vm.SelectedMode == PdfEditToolMode.SelectAndEditText)
            {
                PdfTextBlockInfo hit = null;
                foreach (PdfTextBlockInfo block in vm.CurrentPageTextBlocks)
                {
                    if (nx >= block.NormX - 0.005 && nx <= block.NormX + block.NormWidth + 0.005 &&
                        ny >= block.NormY - 0.005 && ny <= block.NormY + block.NormHeight + 0.005)
                    {
                        hit = block;
                        break;
                    }
                }

                vm.SelectTextBlock(hit);
                this.UpdateStageSizeAndRedraw();
                return;
            }

            if (vm.SelectedMode == PdfEditToolMode.AddText)
            {
                if (!string.IsNullOrWhiteSpace(vm.NewTextDraft))
                {
                    vm.AddOperation(new PdfEditOperation
                    {
                        Kind = PdfEditOperationKind.AddText,
                        NormX = nx,
                        NormY = ny,
                        Text = vm.NewTextDraft,
                        FontFamily = vm.FontFamily,
                        FontSizePt = vm.FontSizePt,
                        ColorHex = vm.ColorHex,
                    });
                }

                return;
            }

            if (vm.SelectedMode == PdfEditToolMode.AddImage && string.IsNullOrWhiteSpace(vm.SelectedImagePath))
            {
                vm.PickImageCommand.Execute(null);
                if (string.IsNullOrWhiteSpace(vm.SelectedImagePath))
                {
                    return;
                }
            }

            this.isDrawing = true;
            this.dragStart = pos;
            this.EditOverlayCanvas.CaptureMouse();

            Color fg = ParseColor(vm.ColorHex, Colors.Crimson);
            if (vm.SelectedMode == PdfEditToolMode.Freehand)
            {
                this.liveFreehandPoints.Clear();
                this.liveFreehandPoints.Add(new PdfEditPoint { NormX = nx, NormY = ny });
                this.livePolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(fg),
                    StrokeThickness = Math.Max(1, vm.StrokeWidth),
                    IsHitTestVisible = false,
                };
                this.livePolyline.Points.Add(pos);
                this.EditOverlayCanvas.Children.Add(this.livePolyline);
                this.livePreviewShape = null;
            }
            else if (vm.SelectedMode == PdfEditToolMode.Line || vm.SelectedMode == PdfEditToolMode.Arrow)
            {
                this.livePreviewShape = new Line
                {
                    X1 = pos.X,
                    Y1 = pos.Y,
                    X2 = pos.X,
                    Y2 = pos.Y,
                    Stroke = new SolidColorBrush(fg),
                    StrokeThickness = Math.Max(1, vm.StrokeWidth),
                    IsHitTestVisible = false,
                };
                this.EditOverlayCanvas.Children.Add(this.livePreviewShape);
            }
            else
            {
                Shape s = vm.SelectedMode == PdfEditToolMode.Ellipse ? (Shape)new Ellipse() : new Rectangle();
                s.Stroke = vm.SelectedMode == PdfEditToolMode.Highlight ? null : new SolidColorBrush(fg);
                s.StrokeThickness = Math.Max(1, vm.StrokeWidth);
                s.Fill = vm.SelectedMode == PdfEditToolMode.Highlight
                    ? new SolidColorBrush(Color.FromArgb(95, fg.R, fg.G, fg.B))
                    : null;
                s.IsHitTestVisible = false;
                Canvas.SetLeft(s, pos.X);
                Canvas.SetTop(s, pos.Y);
                this.livePreviewShape = s;
                this.EditOverlayCanvas.Children.Add(s);
            }
        }

        private void EditOverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!this.isDrawing)
            {
                return;
            }

            double w = this.EditOverlayCanvas.ActualWidth;
            double h = this.EditOverlayCanvas.ActualHeight;
            Point pos = e.GetPosition(this.EditOverlayCanvas);

            if (this.livePolyline != null)
            {
                this.livePolyline.Points.Add(pos);
                this.liveFreehandPoints.Add(new PdfEditPoint { NormX = Clamp01(pos.X / w), NormY = Clamp01(pos.Y / h) });
                return;
            }

            if (this.livePreviewShape is Line line)
            {
                line.X2 = pos.X;
                line.Y2 = pos.Y;
            }
            else if (this.livePreviewShape != null)
            {
                double x = Math.Min(this.dragStart.X, pos.X);
                double y = Math.Min(this.dragStart.Y, pos.Y);
                Canvas.SetLeft(this.livePreviewShape, x);
                Canvas.SetTop(this.livePreviewShape, y);
                this.livePreviewShape.Width = Math.Abs(pos.X - this.dragStart.X);
                this.livePreviewShape.Height = Math.Abs(pos.Y - this.dragStart.Y);
            }
        }

        private void EditOverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!this.isDrawing)
            {
                return;
            }

            this.isDrawing = false;
            this.EditOverlayCanvas.ReleaseMouseCapture();

            if (!(this.DataContext is PdfEditWorkspaceViewModel vm))
            {
                return;
            }

            double w = Math.Max(1, this.EditOverlayCanvas.ActualWidth);
            double h = Math.Max(1, this.EditOverlayCanvas.ActualHeight);
            Point pos = e.GetPosition(this.EditOverlayCanvas);

            if (vm.SelectedMode == PdfEditToolMode.Freehand)
            {
                if (this.liveFreehandPoints.Count >= 2)
                {
                    vm.AddOperation(new PdfEditOperation
                    {
                        Kind = PdfEditOperationKind.Freehand,
                        ColorHex = vm.ColorHex,
                        StrokeWidth = vm.StrokeWidth,
                        Points = new List<PdfEditPoint>(this.liveFreehandPoints),
                    });
                }

                this.livePolyline = null;
                return;
            }

            if (vm.SelectedMode == PdfEditToolMode.Line || vm.SelectedMode == PdfEditToolMode.Arrow)
            {
                vm.AddOperation(new PdfEditOperation
                {
                    Kind = vm.SelectedMode == PdfEditToolMode.Arrow ? PdfEditOperationKind.Arrow : PdfEditOperationKind.Line,
                    NormX = Clamp01(this.dragStart.X / w),
                    NormY = Clamp01(this.dragStart.Y / h),
                    NormX2 = Clamp01(pos.X / w),
                    NormY2 = Clamp01(pos.Y / h),
                    ColorHex = vm.ColorHex,
                    StrokeWidth = vm.StrokeWidth,
                });
                this.livePreviewShape = null;
                return;
            }

            double x = Math.Min(this.dragStart.X, pos.X);
            double y = Math.Min(this.dragStart.Y, pos.Y);
            double rw = Math.Abs(pos.X - this.dragStart.X);
            double rh = Math.Abs(pos.Y - this.dragStart.Y);

            if (vm.SelectedMode == PdfEditToolMode.AddImage)
            {
                if (rw < 12)
                {
                    rw = w * 0.25;
                }

                if (rh < 12)
                {
                    rh = h * 0.20;
                }

                vm.AddOperation(new PdfEditOperation
                {
                    Kind = PdfEditOperationKind.AddImage,
                    ImagePath = vm.SelectedImagePath,
                    NormX = Clamp01(x / w),
                    NormY = Clamp01(y / h),
                    NormWidth = Math.Max(0.03, rw / w),
                    NormHeight = Math.Max(0.03, rh / h),
                });
                this.livePreviewShape = null;
                return;
            }

            if (rw >= 6 && rh >= 6)
            {
                PdfEditOperationKind kind = PdfEditOperationKind.Rectangle;
                if (vm.SelectedMode == PdfEditToolMode.Ellipse)
                {
                    kind = PdfEditOperationKind.Ellipse;
                }
                else if (vm.SelectedMode == PdfEditToolMode.Highlight)
                {
                    kind = PdfEditOperationKind.Highlight;
                }

                vm.AddOperation(new PdfEditOperation
                {
                    Kind = kind,
                    NormX = Clamp01(x / w),
                    NormY = Clamp01(y / h),
                    NormWidth = rw / w,
                    NormHeight = rh / h,
                    ColorHex = vm.SelectedMode == PdfEditToolMode.Highlight ? "#FACC15" : vm.ColorHex,
                    StrokeWidth = vm.StrokeWidth,
                });
            }

            this.livePreviewShape = null;
        }

        private void Stage_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Stage_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                this.DataContext is WorkspaceViewModelBase vm &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                vm.LoadFiles(files);
            }
        }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        private static Color ParseColor(string hex, Color fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    return (Color)ColorConverter.ConvertFromString(hex);
                }
            }
            catch
            {
            }

            return fallback;
        }
    }
}
