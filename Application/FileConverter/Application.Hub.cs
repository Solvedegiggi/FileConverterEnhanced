namespace FileConverter
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using CommunityToolkit.Mvvm.DependencyInjection;

    using FileConverter.ViewModels;

    public partial class Application
    {
        private bool TryParseHubArguments(string[] args)
        {
            if (args == null || args.Length <= 1)
            {
                return false;
            }

            bool hasHubFlag = false;
            string toolId = null;
            var files = new List<string>();
            bool verboseFlag = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
                {
                    this.showHelp = true;
                    return true;
                }
                else if (string.Equals(arg, "--hub", StringComparison.OrdinalIgnoreCase))
                {
                    hasHubFlag = true;
                }
                else if (string.Equals(arg, "--tool", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        toolId = args[++i];
                    }

                    hasHubFlag = true;
                }
                else if (string.Equals(arg, "--verbose", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase))
                {
                    verboseFlag = true;
                }
                else if (!arg.StartsWith("--", StringComparison.Ordinal) && File.Exists(arg))
                {
                    files.Add(arg);
                }
            }

            if (!hasHubFlag)
            {
                return false;
            }

            if (verboseFlag)
            {
                this.verbose = true;
            }

            this.showHub = true;
            this.launchToolId = toolId;
            this.launchToolFiles = files;
            return true;
        }
    }
}
