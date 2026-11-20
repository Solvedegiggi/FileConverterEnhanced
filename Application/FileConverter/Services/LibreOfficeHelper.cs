// <copyright file="LibreOfficeHelper.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;

    public static class LibreOfficeHelper
    {
        public static bool IsAvailable()
        {
            return !string.IsNullOrEmpty(Helpers.FindLibreOfficeExecutable());
        }

        public static bool ConvertToPdf(string inputPath, string outputPdfPath, out string errorMessage)
        {
            errorMessage = string.Empty;
            string sofficePath = Helpers.FindLibreOfficeExecutable();
            if (string.IsNullOrEmpty(sofficePath) || !File.Exists(sofficePath))
            {
                errorMessage = "LibreOffice (soffice.exe) not found.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
            {
                errorMessage = "Input file not found.";
                return false;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "FileConverterLO_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string arguments = $"--headless --nologo --nodefault --nofirststartwizard --convert-to pdf --outdir \"{tempDir}\" \"{inputPath}\"";
                var stderr = new StringBuilder();

                var startInfo = new ProcessStartInfo
                {
                    FileName = sofficePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var process = new Process { StartInfo = startInfo })
                {
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

                    if (!process.WaitForExit(120000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        errorMessage = "LibreOffice conversion timed out.";
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        errorMessage = stderr.Length > 0
                            ? stderr.ToString().Trim()
                            : $"LibreOffice exited with code {process.ExitCode}.";
                        return false;
                    }
                }

                string expectedPdf = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(inputPath) + ".pdf");
                if (!File.Exists(expectedPdf))
                {
                    expectedPdf = Directory.GetFiles(tempDir, "*.pdf").FirstOrDefault();
                }

                if (string.IsNullOrEmpty(expectedPdf) || !File.Exists(expectedPdf))
                {
                    errorMessage = "LibreOffice did not produce an output PDF.";
                    return false;
                }

                string outDir = Path.GetDirectoryName(outputPdfPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                if (File.Exists(outputPdfPath))
                {
                    File.Delete(outputPdfPath);
                }

                File.Copy(expectedPdf, outputPdfPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
