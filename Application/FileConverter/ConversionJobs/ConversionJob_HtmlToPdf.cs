// <copyright file="ConversionJob_HtmlToPdf.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;

    using Microsoft.Web.WebView2.Core;
    using Microsoft.Web.WebView2.Wpf;

    public class ConversionJob_HtmlToPdf : ConversionJob
    {
        public ConversionJob_HtmlToPdf(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        public ConversionJob_HtmlToPdf(ConversionPreset conversionPreset, string inputFilePath, string inputUrl)
            : base(conversionPreset, inputFilePath, inputUrl)
        {
        }

        protected override bool ValidateInputExtension => false;

        protected override bool IsFileBasedInput =>
            string.IsNullOrEmpty(this.InputUrl) && base.IsFileBasedInput;

        protected override void Initialize()
        {
            base.Initialize();

            if (string.IsNullOrWhiteSpace(this.InputUrl) && !File.Exists(this.InputFilePath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
            }
        }

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateConversion;

            string targetUri;
            if (!string.IsNullOrWhiteSpace(this.InputUrl))
            {
                string raw = this.InputUrl.Trim();
                if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !raw.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    raw = "https://" + raw;
                }

                targetUri = raw;
            }
            else
            {
                targetUri = new Uri(Path.GetFullPath(this.InputFilePath)).AbsoluteUri;
            }

            string pageSize = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.HtmlPageSize) ?? "A4";
            string orientation = this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.HtmlPageOrientation) ?? "Portrait";
            bool printBg = !string.Equals(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.HtmlPrintBackground), "False", StringComparison.OrdinalIgnoreCase);
            int delayMs = 500;
            if (int.TryParse(this.ConversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.HtmlDelayMs), out int parsedDelay))
            {
                delayMs = Math.Max(0, Math.Min(15000, parsedDelay));
            }

            Exception capturedException = null;
            bool printSucceeded = false;

            using (var doneEvent = new ManualResetEventSlim(false))
            {
                Thread staThread = new Thread(() =>
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    Window hostWindow = null;
                    WebView2 webView = null;

                    dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            this.Progress = 0.2f;
                            string userDataDir = Path.Combine(Path.GetTempPath(), "FileConverterWebView2");
                            Directory.CreateDirectory(userDataDir);

                            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataDir);

                            webView = new WebView2();
                            hostWindow = new Window
                            {
                                Width = 1280,
                                Height = 900,
                                Left = -32000,
                                Top = -32000,
                                ShowInTaskbar = false,
                                WindowStyle = WindowStyle.None,
                                ShowActivated = false,
                                Opacity = 0.0,
                                Content = webView,
                            };

                            hostWindow.Show();
                            await webView.EnsureCoreWebView2Async(env);

                            var navTcs = new TaskCompletionSource<bool>();
                            webView.CoreWebView2.NavigationCompleted += (s, args) =>
                            {
                                navTcs.TrySetResult(args.IsSuccess);
                            };

                            webView.CoreWebView2.Navigate(targetUri);

                            Task completedTask = await Task.WhenAny(navTcs.Task, Task.Delay(30000));
                            if (completedTask != navTcs.Task)
                            {
                                throw new TimeoutException("Timed out waiting for HTML page to finish loading.");
                            }

                            this.Progress = 0.6f;
                            if (delayMs > 0)
                            {
                                await Task.Delay(delayMs);
                            }

                            CoreWebView2PrintSettings printSettings = env.CreatePrintSettings();
                            printSettings.ShouldPrintBackgrounds = printBg;
                            printSettings.Orientation = string.Equals(orientation, "Landscape", StringComparison.OrdinalIgnoreCase)
                                ? CoreWebView2PrintOrientation.Landscape
                                : CoreWebView2PrintOrientation.Portrait;

                            if (string.Equals(pageSize, "Letter", StringComparison.OrdinalIgnoreCase))
                            {
                                printSettings.PageWidth = 8.5;
                                printSettings.PageHeight = 11.0;
                            }
                            else
                            {
                                printSettings.PageWidth = 8.27;
                                printSettings.PageHeight = 11.69;
                            }

                            this.Progress = 0.85f;
                            printSucceeded = await webView.CoreWebView2.PrintToPdfAsync(this.OutputFilePath, printSettings);
                        }
                        catch (Exception ex)
                        {
                            capturedException = ex;
                        }
                        finally
                        {
                            try
                            {
                                webView?.Dispose();
                                hostWindow?.Close();
                            }
                            catch
                            {
                            }

                            doneEvent.Set();
                            dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                        }
                    }));

                    Dispatcher.Run();
                });

                staThread.SetApartmentState(ApartmentState.STA);
                staThread.IsBackground = true;
                staThread.Start();
                doneEvent.Wait();
            }

            if (capturedException != null)
            {
                string message = capturedException is WebView2RuntimeNotFoundException
                    ? "Microsoft Edge WebView2 Runtime is required to convert HTML/URLs to PDF."
                    : capturedException.Message;
                this.ConversionFailed(message);
                return;
            }

            if (!printSucceeded || !File.Exists(this.OutputFilePath))
            {
                this.ConversionFailed("Failed to print HTML page to PDF.");
                return;
            }

            this.Progress = 1.0f;
        }
    }
}
