namespace FileConverter.Views.Workspaces
{
    using System.Windows;
    using System.Windows.Controls;

    using FileConverter.ViewModels.Workspaces;

    public partial class WorkspaceFileListControl : UserControl
    {
        public WorkspaceFileListControl()
        {
            this.InitializeComponent();
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
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
