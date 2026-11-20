namespace FileConverter.ViewModels.Workspaces
{
    using System;

    using FileConverter.Models;

    public static class WorkspaceFactory
    {
        public static WorkspaceViewModelBase Create(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
        {
            if (tool == null)
            {
                return null;
            }

            switch (tool.Id)
            {
                case ToolIds.MergePdf:
                    return new PdfMergeWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.SplitPdf:
                    return new PdfSplitWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.CompressPdf:
                    return new PdfCompressWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.RotatePdf:
                    return new PdfRotateWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.PageNumbersPdf:
                    return new PdfPageNumbersWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.PdfToJpg:
                    return new PdfToJpgWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.JpgToPdf:
                    return new ImagesToPdfWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.HtmlToPdf:
                    return new HtmlToPdfWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.OcrPdf:
                    return new PdfOcrWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.PdfToMarkdown:
                    return new PdfToMarkdownWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.WordToPdf:
                case ToolIds.PowerPointToPdf:
                case ToolIds.ExcelToPdf:
                case ToolIds.PdfToWord:
                case ToolIds.PdfToPowerPoint:
                case ToolIds.PdfToExcel:
                    return new OfficeConversionWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.OrganizePdf:
                    return new PdfOrganizeWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.CropPdf:
                    return new PdfCropWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.EditPdf:
                    return new PdfEditWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                case ToolIds.ComparePdf:
                    return new PdfCompareWorkspaceViewModel(tool, onBack, onNavigateToQueue);

                default:
                    return new PendingWorkspaceViewModel(tool, onBack, onNavigateToQueue);
            }
        }
    }
}
