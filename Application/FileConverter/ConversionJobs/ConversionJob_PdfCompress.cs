// <copyright file="ConversionJob_PdfCompress.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    public class ConversionJob_PdfCompress : ConversionJob
    {
        private Process gsProcess;

        public ConversionJob_PdfCompress(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected override bool ValidateInputExtension => false;

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateConversion;

            string gsExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gswin64c.exe");
            if (!File.Exists(gsExe))
            {
                this.ConversionFailed("Ghostscript executable (gswin64c.exe) not found.");
                return;
            }

            if (!File.Exists(this.InputFilePath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                return;
            }

            string level = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfCompressionLevel) ?? "Recommended";
            string pdfSettings;
            string extraArgs = string.Empty;

            if (string.Equals(level, "Extreme", StringComparison.OrdinalIgnoreCase))
            {
                pdfSettings = "/screen";
                extraArgs = "-dColorImageResolution=72 -dGrayImageResolution=72 -dMonoImageResolution=150 -dJPEGQ=55";
            }
            else if (string.Equals(level, "Low", StringComparison.OrdinalIgnoreCase))
            {
                pdfSettings = "/printer";
                extraArgs = "-dColorImageResolution=300 -dGrayImageResolution=300";
            }
            else
            {
                pdfSettings = "/ebook";
                extraArgs = "-dColorImageResolution=150 -dGrayImageResolution=150";
            }

            string arguments =
                $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS={pdfSettings} " +
                $"-dNOPAUSE -dQUIET -dBATCH -dAutoRotatePages=/None {extraArgs} " +
                $"-sOutputFile=\"{this.OutputFilePath}\" \"{this.InputFilePath}\"";

            StringBuilder stderr = new StringBuilder();

            var startInfo = new ProcessStartInfo
            {
                FileName = gsExe,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            this.Progress = 0.2f;

            using (var process = new Process { StartInfo = startInfo })
            {
                this.gsProcess = process;
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        stderr.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                this.gsProcess = null;

                if (this.CancelIsRequested)
                {
                    return;
                }

                if (process.ExitCode != 0 || !File.Exists(this.OutputFilePath))
                {
                    string err = stderr.ToString().Trim();
                    this.ConversionFailed(string.IsNullOrEmpty(err)
                        ? $"Ghostscript failed with exit code {process.ExitCode}."
                        : err);
                    return;
                }
            }

            this.Progress = 1.0f;
        }

        public override void Cancel()
        {
            base.Cancel();
            try
            {
                if (this.gsProcess != null && !this.gsProcess.HasExited)
                {
                    this.gsProcess.Kill();
                }
            }
            catch
            {
                // Ignore race when process exits concurrently.
            }
        }
    }
}
