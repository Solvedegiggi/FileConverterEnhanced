namespace FileConverter.ViewModels.Workspaces
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using FileConverter.ConversionJobs;
    using FileConverter.Models;

    public class PdfRotateWorkspaceViewModel : ToolWorkspaceViewModelBase
    {
        private int rotationAngle = 90;
        private string pageRange = string.Empty;

        public PdfRotateWorkspaceViewModel(ToolDefinition tool, Action onBack, Action onNavigateToQueue)
            : base(tool, onBack, onNavigateToQueue)
        {
        }

        public int RotationAngle
        {
            get => this.rotationAngle;
            set
            {
                if (this.SetProperty(ref this.rotationAngle, value))
                {
                    this.OnPropertyChanged(nameof(this.IsRotate90Cw));
                    this.OnPropertyChanged(nameof(this.IsRotate180));
                    this.OnPropertyChanged(nameof(this.IsRotate90Ccw));
                }
            }
        }

        public bool IsRotate90Cw
        {
            get => this.RotationAngle == 90;
            set
            {
                if (value)
                {
                    this.RotationAngle = 90;
                }
            }
        }

        public bool IsRotate180
        {
            get => this.RotationAngle == 180;
            set
            {
                if (value)
                {
                    this.RotationAngle = 180;
                }
            }
        }

        public bool IsRotate90Ccw
        {
            get => this.RotationAngle == -90 || this.RotationAngle == 270;
            set
            {
                if (value)
                {
                    this.RotationAngle = -90;
                }
            }
        }

        public string PageRange
        {
            get => this.pageRange;
            set => this.SetProperty(ref this.pageRange, value);
        }

        protected override void Execute()
        {
            var jobs = new List<ConversionJob>();
            foreach (string file in this.InputFiles)
            {
                var preset = new ConversionPreset("PdfRotate", OutputType.Pdf, new[] { "pdf" });
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode, "Rotate");
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfRotationAngle, this.RotationAngle.ToString(CultureInfo.InvariantCulture));
                preset.SetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfPageRange, this.PageRange ?? string.Empty);
                this.ApplyOutputTemplate(preset);

                jobs.Add(new ConversionJob_PdfRotate(preset, file));
            }

            this.QueueAndRun(jobs);
        }
    }
}
