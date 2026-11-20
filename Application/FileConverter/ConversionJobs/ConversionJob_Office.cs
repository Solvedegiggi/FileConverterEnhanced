// <copyright file="ConversionJob_Office.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;

    using CommunityToolkit.Mvvm.DependencyInjection;

    using FileConverter.Services;

    public abstract class ConversionJob_Office : ConversionJob
    {
        protected ConversionJob_Office() : base()
        {
        }

        protected ConversionJob_Office(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
        }

        public enum ApplicationName
        {
            None,

            Word,
            Excel,
            PowerPoint
        }

        protected abstract ApplicationName Application
        {
            get;
        }

        protected bool UseLibreOfficeFallback
        {
            get;
            private set;
        }

        protected bool UseNativeFallback
        {
            get;
            private set;
        }

        protected override bool IsCancelable() => false;

        protected bool ShouldUseMicrosoftOfficeInterop()
        {
            bool preferInterop = true;
            try
            {
                ISettingsService settingsService = Ioc.Default.GetService<ISettingsService>();
                if (settingsService?.Settings != null)
                {
                    preferInterop = settingsService.Settings.PreferOfficeInteropWhenAvailable;
                }
            }
            catch
            {
            }

            return preferInterop && Helpers.IsMicrosoftOfficeApplicationAvailable(this.Application);
        }

        protected override void Initialize()
        {
            base.Initialize();

            this.UseLibreOfficeFallback = false;
            this.UseNativeFallback = false;

            if (this.ShouldUseMicrosoftOfficeInterop())
            {
                return;
            }

            if (LibreOfficeHelper.IsAvailable())
            {
                this.UseLibreOfficeFallback = true;
                return;
            }

            string ext = Path.GetExtension(this.InputFilePath ?? string.Empty).ToLowerInvariant();
            if ((this.Application == ApplicationName.Word && ext == ".docx") ||
                (this.Application == ApplicationName.Excel && ext == ".xlsx") ||
                (this.Application == ApplicationName.PowerPoint && ext == ".pptx"))
            {
                this.UseNativeFallback = true;
                return;
            }

            switch (this.Application)
            {
                case ApplicationName.Word:
                    this.ConversionFailed(Properties.Resources.ErrorMicrosoftWordIsNotAvailable);
                    return;

                case ApplicationName.PowerPoint:
                    this.ConversionFailed(Properties.Resources.ErrorMicrosoftPowerPointIsNotAvailable);
                    return;

                case ApplicationName.Excel:
                    this.ConversionFailed(Properties.Resources.ErrorMicrosoftExcelIsNotAvailable);
                    return;

                default:
                    this.ConversionFailed(Properties.Resources.ErrorMicrosoftOfficeIsNotAvailable);
                    return;
            }
        }

        protected override void OnConversionFailed()
        {
            base.OnConversionFailed();

            this.ReleaseOfficeApplicationInstanceIfNeeded();
        }

        protected abstract void InitializeOfficeApplicationInstanceIfNecessary();

        protected abstract void ReleaseOfficeApplicationInstanceIfNeeded();
    }
}