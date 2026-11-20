namespace FileConverter.Views.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    using FileConverter.Models;
    using FileConverter.ViewModels;

    public partial class HomeGridView : UserControl
    {
        public HomeGridView()
        {
            this.InitializeComponent();
        }

        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep && FindParentButton(dep) != null)
            {
                return;
            }

            if (sender is FrameworkElement fe && fe.DataContext is ToolDefinition tool && this.DataContext is HomeGridViewModel vm)
            {
                if (!vm.IsEditMode)
                {
                    vm.OpenToolCommand.Execute(tool);
                }
            }
        }

        private void Card_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Card_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && sender is FrameworkElement fe && fe.DataContext is ToolDefinition tool && this.DataContext is HomeGridViewModel vm)
            {
                vm.HandleDropOnCard(tool, files);
            }
        }

        private static Button FindParentButton(DependencyObject child)
        {
            DependencyObject current = child;
            while (current != null)
            {
                if (current is Button btn)
                {
                    return btn;
                }

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
