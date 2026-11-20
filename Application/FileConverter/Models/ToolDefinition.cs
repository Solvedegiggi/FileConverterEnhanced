namespace FileConverter.Models
{
    using CommunityToolkit.Mvvm.ComponentModel;

    public static class ToolIds
    {
        public const string MergePdf = "MergePdf";
        public const string SplitPdf = "SplitPdf";
        public const string CompressPdf = "CompressPdf";
        public const string PdfToWord = "PdfToWord";
        public const string PdfToPowerPoint = "PdfToPowerPoint";
        public const string PdfToExcel = "PdfToExcel";
        public const string WordToPdf = "WordToPdf";
        public const string PowerPointToPdf = "PowerPointToPdf";
        public const string ExcelToPdf = "ExcelToPdf";
        public const string EditPdf = "EditPdf";
        public const string PdfToJpg = "PdfToJpg";
        public const string JpgToPdf = "JpgToPdf";
        public const string RotatePdf = "RotatePdf";
        public const string HtmlToPdf = "HtmlToPdf";
        public const string OrganizePdf = "OrganizePdf";
        public const string PageNumbersPdf = "PageNumbersPdf";
        public const string OcrPdf = "OcrPdf";
        public const string ComparePdf = "ComparePdf";
        public const string CropPdf = "CropPdf";
        public const string PdfToMarkdown = "PdfToMarkdown";
        public const string UniversalConverter = "UniversalConverter";
    }

    public enum ToolCategory
    {
        Organize,
        Optimize,
        ConvertFromPdf,
        ConvertToPdf,
        EditCompare,
        Presets,
    }

    public class ToolDefinition : ObservableObject
    {
        private bool isPinned;

        public string Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public ToolCategory Category { get; set; }

        public string CategoryName { get; set; }

        public string AccentColorHex { get; set; }

        public string BadgeBgHex { get; set; }

        public string BadgeGlyph { get; set; }

        public bool IsNew { get; set; }

        public bool IsPresetShortcut { get; set; }

        public string PresetFullName { get; set; }

        public string[] AcceptedExtensions { get; set; }

        public bool AllowsMultipleFiles { get; set; }

        public bool IsPinned
        {
            get => this.isPinned;
            set => this.SetProperty(ref this.isPinned, value);
        }
    }
}
