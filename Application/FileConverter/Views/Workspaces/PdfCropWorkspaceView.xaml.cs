namespace FileConverter.Views.Workspaces
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    using FileConverter.ViewModels.Workspaces;

    public partial class PdfCropWorkspaceView : UserControl
    {
        private bool isDragging;
        private Point dragStart;

        public PdfCropWorkspaceView()
        {
            this.InitializeComponent();
            this.DataContextChanged += this.OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PdfCropWorkspaceViewModel oldVm)
            {
                oldVm.CropChanged -= this.Vm_CropChanged;
            }

            if (e.NewValue is PdfCropWorkspaceViewModel newVm)
            {
                newVm.CropChanged += this.Vm_CropChanged;
                this.UpdateCropVisual();
            }
        }

        private void Vm_CropChanged(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(this.UpdateCropVisual));
        }

        private void CropOverlayCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateCropVisual();
        }

        private void CropOverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.CropOverlayCanvas.ActualWidth < 10 || this.CropOverlayCanvas.ActualHeight < 10)
            {
                return;
            }

            this.isDragging = true;
            this.dragStart = e.GetPosition(this.CropOverlayCanvas);
            this.CropOverlayCanvas.CaptureMouse();
        }

        private void CropOverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!this.isDragging)
            {
                return;
            }

            Point pos = e.GetPosition(this.CropOverlayCanvas);
            double w = this.CropOverlayCanvas.ActualWidth;
            double h = this.CropOverlayCanvas.ActualHeight;

            double x1 = Math.Max(0, Math.Min(w, Math.Min(this.dragStart.X, pos.X)));
            double y1 = Math.Max(0, Math.Min(h, Math.Min(this.dragStart.Y, pos.Y)));
            double x2 = Math.Max(0, Math.Min(w, Math.Max(this.dragStart.X, pos.X)));
            double y2 = Math.Max(0, Math.Min(h, Math.Max(this.dragStart.Y, pos.Y)));

            if (this.DataContext is PdfCropWorkspaceViewModel vm && (x2 - x1) > 8 && (y2 - y1) > 8)
            {
                vm.SetCropFromNormalizedRect(x1 / w, y1 / h, (x2 - x1) / w, (y2 - y1) / h);
            }
        }

        private void CropOverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (this.isDragging)
            {
                this.isDragging = false;
                this.CropOverlayCanvas.ReleaseMouseCapture();
            }
        }

        private void UpdateCropVisual()
        {
            if (!(this.DataContext is PdfCropWorkspaceViewModel vm))
            {
                return;
            }

            double w = this.CropOverlayCanvas.ActualWidth;
            double h = this.CropOverlayCanvas.ActualHeight;
            if (w <= 1 || h <= 1)
            {
                return;
            }

            double x = vm.NormLeft * w;
            double y = vm.NormTop * h;
            double rw = Math.Max(8, w * Math.Max(0.02, 1.0 - vm.NormLeft - vm.NormRight));
            double rh = Math.Max(8, h * Math.Max(0.02, 1.0 - vm.NormTop - vm.NormBottom));

            Canvas.SetLeft(this.CropRectVisual, x);
            Canvas.SetTop(this.CropRectVisual, y);
            this.CropRectVisual.Width = rw;
            this.CropRectVisual.Height = rh;
        }

        private void PreviewArea_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void PreviewArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                this.DataContext is WorkspaceViewModelBase vm &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                vm.LoadFiles(files);
            }
        }
    }
}
