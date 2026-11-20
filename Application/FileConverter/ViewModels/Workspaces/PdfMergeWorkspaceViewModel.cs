namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Linq;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;

    public class PdfMergeWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        public PdfMergeWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
            this.OutputFileName = "merged";
        }

        protected override bool CanExecute()
        {
            return this.InputFiles.Count >= 2;
        }

        protected override void Execute()
        {
            var preset = new ConversionPreset("PdfMerge", OutputType.Pdf, new[] { "pdf" });
            preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Merge");
            this.ApplyOutputTemplate(preset, this.OutputFileName);

            this.QueueAndRun(new ConversionJob_PdfMerge(preset, this.InputFiles.ToList()));
        }
    }
}
