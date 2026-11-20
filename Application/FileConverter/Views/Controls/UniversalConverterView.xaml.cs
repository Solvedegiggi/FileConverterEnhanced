namespace FileConverter.Views.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    using FileConverter.ViewModels;

    public partial class UniversalConverterView : UserControl
    {
        public UniversalConverterView()
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && this.DataContext is UniversalConverterViewModel vm)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                vm.AddFiles(files);
            }
        }
    }
}
