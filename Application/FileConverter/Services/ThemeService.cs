// <copyright file="ThemeService.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Collections.ObjectModel;
    using System.Windows;

    public interface IThemeService
    {
        event EventHandler<string> ThemeChanged;

        string CurrentTheme { get; }

        bool IsDarkTheme { get; }

        void ApplyTheme(string themeName, bool saveToSettings = false);

        void ToggleTheme();
    }

    public class ThemeService : IThemeService
    {
        private const string LightThemeUri = "Views/Resources/Themes/Colors.Light.xaml";
        private const string DarkThemeUri = "Views/Resources/Themes/Colors.Dark.xaml";

        private readonly ISettingsService settingsService;

        public ThemeService(ISettingsService settingsService)
        {
            this.settingsService = settingsService;
            this.CurrentTheme = settingsService?.Settings?.AppTheme ?? "Light";
        }

        public event EventHandler<string> ThemeChanged;

        public string CurrentTheme { get; private set; }

        public bool IsDarkTheme => string.Equals(this.CurrentTheme, "Dark", StringComparison.OrdinalIgnoreCase);

        public void ApplyTheme(string themeName, bool saveToSettings = false)
        {
            string normalized = string.Equals(themeName, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
            this.CurrentTheme = normalized;

            if (System.Windows.Application.Current != null)
            {
                string targetUriString = normalized == "Dark" ? DarkThemeUri : LightThemeUri;
                Uri newUri = new Uri(targetUriString, UriKind.Relative);
                ResourceDictionary newDict = new ResourceDictionary { Source = newUri };

                Collection<ResourceDictionary> merged = System.Windows.Application.Current.Resources.MergedDictionaries;
                ResourceDictionary existingTheme = null;
                for (int i = 0; i < merged.Count; i++)
                {
                    Uri src = merged[i].Source;
                    if (src != null &&
                        (src.OriginalString.Contains("Colors.Light.xaml") ||
                         src.OriginalString.Contains("Colors.Dark.xaml") ||
                         src.OriginalString.EndsWith("Colors.xaml", StringComparison.OrdinalIgnoreCase)))
                    {
                        existingTheme = merged[i];
                        break;
                    }
                }

                if (existingTheme != null)
                {
                    int index = merged.IndexOf(existingTheme);
                    merged[index] = newDict;
                }
                else
                {
                    merged.Insert(0, newDict);
                }
            }

            if (saveToSettings && this.settingsService?.Settings != null)
            {
                this.settingsService.Settings.AppTheme = normalized;
                this.settingsService.SaveSettings();
            }

            this.ThemeChanged?.Invoke(this, normalized);
        }

        public void ToggleTheme()
        {
            this.ApplyTheme(this.IsDarkTheme ? "Light" : "Dark", saveToSettings: true);
        }
    }
}
