namespace FileConverter.Views
{
    using System.ComponentModel;
    using System.Windows;

    using FileConverter.ViewModels;

    public partial class HubWindow : Window
    {
        public HubWindow()
        {
            this.InitializeComponent();
        }

        private void HubWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.DataContext is HubViewModel vm)
            {
                vm.CloseCommand.Execute(e);
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is HubViewModel vm)
            {
                vm.SearchQuery = string.Empty;
            }
        }
    }
}
