// <copyright file="OrganizePageItem.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Models
{
    using System.Windows.Media.Imaging;

    using CommunityToolkit.Mvvm.ComponentModel;

    public class OrganizePageItem : ObservableObject
    {
        private int rotationDelta;
        private BitmapSource thumbnail;
        private string pageNumberLabel;
        private bool isSelected;

        public string SourceFilePath { get; set; }

        public int SourcePageIndex { get; set; }

        public int RotationDelta
        {
            get => this.rotationDelta;
            set => this.SetProperty(ref this.rotationDelta, ((value % 360) + 360) % 360);
        }

        public BitmapSource Thumbnail
        {
            get => this.thumbnail;
            set => this.SetProperty(ref this.thumbnail, value);
        }

        public string PageNumberLabel
        {
            get => this.pageNumberLabel;
            set => this.SetProperty(ref this.pageNumberLabel, value);
        }

        public bool IsSelected
        {
            get => this.isSelected;
            set => this.SetProperty(ref this.isSelected, value);
        }
    }
}
