namespace FileConverter.Views.Workspaces
{
    using System.Windows;
    using System.Windows.Controls;

    using FileConverter.ViewModels.Workspaces;

    public partial class PdfCompareWorkspaceView : UserControl
    {
        private bool isSyncingScroll;

        public PdfCompareWorkspaceView()
        {
            this.InitializeComponent();
        }

        private void SyncScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (this.isSyncingScroll)
            {
                return;
            }

            this.isSyncingScroll = true;
            try
            {
                if (sender == this.LeftScrollViewer)
                {
                    this.RightScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
                    this.RightScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
                }
                else if (sender == this.RightScrollViewer)
                {
                    this.LeftScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
                    this.LeftScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
                }
            }
            finally
            {
                this.isSyncingScroll = false;
            }
        }

        private void CompareArea_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void CompareArea_Drop(object sender, DragEventArgs e)
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
