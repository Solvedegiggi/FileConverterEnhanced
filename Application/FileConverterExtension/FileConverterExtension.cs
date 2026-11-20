// <copyright file="FileConverterExtension.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverterExtension
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Forms;

    using SharpShell.Attributes;
    using SharpShell.SharpContextMenu;

    /// <summary>
    /// File converter context menu extension class.
    /// </summary>
    [ComVisible(true), Guid("AF9B72B5-F4E4-44B0-A3D9-B55B748EFE90")]
    [COMServerAssociation(AssociationType.AllFiles)]
    public class FileConverterExtension : SharpContextMenu
    {
        private const int MaximumProcessArgumentsLength = 8000; // https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/command-line-string-limitation

        private PresetReference[] presetReferences = null;
        private List<MenuEntry> menuEntries = new List<MenuEntry>();

        private HashSet<string> extensionCache = new HashSet<string>();

        private class MenuEntry
        {
            public PresetReference PresetReference;
            public bool Enabled;
            public int ExtensionRefCount;

            public MenuEntry(PresetReference presetReference)
            {
                this.PresetReference = presetReference;
                this.Enabled = false;
                this.ExtensionRefCount = 0;
            }
        }
        
        private bool DisplayPresetIcons
        {
            get
            {
                string displayPresetIcons = PathHelpers.FileConverterRegistryKey.GetValue("DisplayPresetIcons") as string;
                if (displayPresetIcons == null)
                {
                    return false;
                }

                if (!bool.TryParse(displayPresetIcons, out bool value))
                {
                    return false;
                }

                return value;
            }
        }

        private PresetReference[] PresetReferences
        {
            get
            {
                this.LoadExtensionSettingsIfNecessary();

                return this.presetReferences;
            }
        }

        protected override bool CanShowMenu()
        {
            this.RefreshExtensionCacheFromSelectedItems();

            if (this.HasEnhancedToolSupport())
            {
                return true;
            }

            PresetReference[] presets = this.PresetReferences;
            foreach (string extension in this.extensionCache)
            {
                foreach (PresetReference presetReference in presets)
                {
                    if (presetReference.InputTypes.Contains(extension))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected override ContextMenuStrip CreateMenu()
        {
            this.RefreshPresetList();

            bool displayPresetIcons = this.DisplayPresetIcons;

            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem fileConverterItem = new ToolStripMenuItem
            {
                Text = "File Converter",
                Image = new Icon(Properties.Resources.ApplicationIcon, SystemInformation.SmallIconSize).ToBitmap(),
            };

            int menuItemIndex = 0;
            foreach (MenuEntry menuEntry in this.menuEntries)
            {
                menuItemIndex++;
                
                ToolStripMenuItem root = fileConverterItem;
                if (menuEntry.PresetReference.Folders != null)
                {
                    foreach (string folder in menuEntry.PresetReference.Folders)
                    {
                        ToolStripItem[] folderItems = root.DropDownItems.Find(folder, false);
                        if (folderItems.Length == 0)
                        {
                            ToolStripMenuItem folderItem = new ToolStripMenuItem
                            {
                                Name = folder,
                                Text = folder,
                                Image = new Icon(Properties.Resources.FolderIcon, SystemInformation.SmallIconSize).ToBitmap(),
                            };

                            root.DropDownItems.Add(folderItem);
                            root = folderItem;
                        }
                        else
                        {
                            root = folderItems[0] as ToolStripMenuItem;
                        }

                        if (root == null)
                        {
                            break;
                        }
                    }
                }

                if (root == null)
                {
                    // Fallback when something went wrong during folder creation.
                    root = fileConverterItem;
                }

                // Make each menu item text unique using invisible zero-width spaces
                // This prevents Windows Forms menu collision while being completely invisible to users
                string uniqueSuffix = new string('\u200B', menuItemIndex); // Zero-Width Space characters
                string displayText = menuEntry.PresetReference.Name + uniqueSuffix;

                ToolStripMenuItem subItem = new ToolStripMenuItem
                {
                    Text = displayText,
                    Enabled = menuEntry.Enabled
                };

                if (displayPresetIcons)
                {
                    subItem.Image = new Icon(Properties.Resources.PresetIcon, SystemInformation.SmallIconSize).ToBitmap();
                }

                root.DropDownItems.Add(subItem);
                subItem.Click += (sender, args) => this.ConvertFiles(menuEntry.PresetReference.FullName);
            }

            if (this.menuEntries.Count > 0)
            {
                fileConverterItem.DropDownItems.Add(new ToolStripSeparator());
            }

            this.AddPdfAndDocumentToolsSubmenu(fileConverterItem);

            {
                ToolStripMenuItem subItem = new ToolStripMenuItem
                {
                    Text = "Configure presets...",
                    Image = new Icon(Properties.Resources.SettingsIcon, SystemInformation.SmallIconSize).ToBitmap(),
                };

                fileConverterItem.DropDownItems.Add(subItem);
                subItem.Click += (sender, args) => this.OpenSettings();
            }

            menu.Items.Add(fileConverterItem);

            return menu;
        }

        private void RefreshExtensionCacheFromSelectedItems()
        {
            // Retrieve selected files extensions.
            this.extensionCache.Clear();
            foreach (string filePath in this.SelectedItemPaths)
            {
                string extension = Path.GetExtension(filePath);
                if (string.IsNullOrEmpty(extension))
                {
                    continue;
                }

                extension = extension.Substring(1).ToLowerInvariant();

                this.extensionCache.Add(extension);
            }
        }

        private void RefreshPresetList()
        {
            this.RefreshExtensionCacheFromSelectedItems();

            // Activate compatible menu entries.
            PresetReference[] presets = this.presetReferences;
            this.menuEntries.Clear();
            foreach (string extension in this.extensionCache)
            {
                foreach (PresetReference presetReference in presets)
                {
                    if (!presetReference.InputTypes.Contains(extension))
                    {
                        continue;
                    }

                    MenuEntry menuEntry = this.menuEntries.Find(entry => entry.PresetReference.FullName == presetReference.FullName);
                    if (menuEntry == null)
                    {
                        menuEntry = new MenuEntry(presetReference);
                        this.menuEntries.Add(menuEntry);
                    }

                    menuEntry.ExtensionRefCount++;
                }
            }

            // Enable presets compatible with all input files.
            foreach (MenuEntry menuEntry in this.menuEntries)
            {
                menuEntry.Enabled = menuEntry.ExtensionRefCount == this.extensionCache.Count;
            }
        }

        private void LoadExtensionSettingsIfNecessary()
        {
            if (this.presetReferences != null)
            {
                return;
            }

            if (File.Exists(PathHelpers.UserSettingsFilePath))
            {
                try
                {
                    XmlHelpers.LoadFromFile("Settings", PathHelpers.UserSettingsFilePath, out this.presetReferences);
                    return;
                }
                catch
                {
                    // Can't handle this error in the explorer extension.
                }
            }

            try
            {
                XmlHelpers.LoadFromFile("Settings", PathHelpers.DefaultSettingsFilePath, out this.presetReferences);
            }
            catch
            {
                // Can't handle this error in the explorer extension.
            }
        }

        private void OpenSettings()
        {
            if (string.IsNullOrEmpty(PathHelpers.FileConverterPath))
            {
                MessageBox.Show("Can't retrieve the file converter executable path. You should try to reinstall the application.");
                return;
            }

            if (!File.Exists(PathHelpers.FileConverterPath))
            {
                MessageBox.Show($"Can't find the file converter executable ({PathHelpers.FileConverterPath}). You should try to reinstall the application.");
                return;
            }

            ProcessStartInfo processStartInfo = new ProcessStartInfo(PathHelpers.FileConverterPath)
            {
                CreateNoWindow = false, 
                UseShellExecute = false, 
                RedirectStandardOutput = false,
            };

            // Build arguments string.
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("--settings");
            
            processStartInfo.Arguments = stringBuilder.ToString();
            Process exeProcess = Process.Start(processStartInfo);
        }

        private void ConvertFiles(string presetName)
        {
            if (string.IsNullOrEmpty(PathHelpers.FileConverterPath))
            {
                MessageBox.Show("Can't retrieve the file converter executable path. You should try to reinstall the application.");
                return;
            }

            if (!File.Exists(PathHelpers.FileConverterPath))
            {
                MessageBox.Show($"Can't find the file converter executable ({PathHelpers.FileConverterPath}). You should try to reinstall the application.");
                return;
            }

            void BuildConversionPresetArgument(StringBuilder sb)
            {
                sb.Append("--conversion-preset ");
                sb.Append(" \"");
                sb.Append(presetName);
                sb.Append("\"");
            }

            // Build arguments string.
            StringBuilder stringBuilder = new StringBuilder();
            BuildConversionPresetArgument(stringBuilder);
            
            string fileListPath = null;
            foreach (var filePath in this.SelectedItemPaths)
            {
                stringBuilder.Append(" \"");
                stringBuilder.Append(filePath);
                stringBuilder.Append("\"");

                if (stringBuilder.Length >= MaximumProcessArgumentsLength)
                {
                    // Alternative way of passing arguments to not overflow the command line.
                    stringBuilder.Clear();
                    BuildConversionPresetArgument(stringBuilder);

                    // Store list of file to convert in a file in Temp folder.
                    fileListPath = Path.Combine(Path.GetTempPath(), "file-converter-input-list.txt");
                    int index = 1;
                    while (File.Exists(fileListPath))
                    {
                        fileListPath = Path.Combine(Path.GetTempPath(), $"file-converter-input-list-{index}.txt");
                        index++;
                    }

                    using (FileStream file = File.OpenWrite(fileListPath))
                    using (StreamWriter writer = new StreamWriter(file))
                    {
                        foreach (var path in this.SelectedItemPaths)
                        {
                            writer.WriteLine(path);
                        }
                    }

                    stringBuilder.Append(" --input-files ");
                    stringBuilder.Append(" \"");
                    stringBuilder.Append(fileListPath);
                    stringBuilder.Append("\"");
                    break;
                }
            }

            var processStartInfo = new ProcessStartInfo(PathHelpers.FileConverterPath)
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                Arguments = stringBuilder.ToString(),
            };

            Process exeProcess = Process.Start(processStartInfo);
            exeProcess.EnableRaisingEvents = true;
            exeProcess.Exited += (sender, args) =>
            {
                if (fileListPath != null)
                {
                    try
                    {
                        File.Delete(fileListPath);
                    }
                    catch 
                    { 
                    }
                }
            };
        }

        private bool HasEnhancedToolSupport()
        {
            foreach (string ext in this.extensionCache)
            {
                if (ext == "pdf" || ext == "html" || ext == "htm" ||
                    ext == "doc" || ext == "docx" || ext == "odt" ||
                    ext == "ppt" || ext == "pptx" || ext == "odp" ||
                    ext == "xls" || ext == "xlsx" || ext == "ods" ||
                    ext == "jpg" || ext == "jpeg" || ext == "png" || ext == "bmp" ||
                    ext == "webp" || ext == "tif" || ext == "tiff" || ext == "gif" || ext == "avif")
                {
                    return true;
                }
            }

            return false;
        }

        private void AddPdfAndDocumentToolsSubmenu(ToolStripMenuItem parentItem)
        {
            ToolStripMenuItem toolsMenu = new ToolStripMenuItem
            {
                Text = "PDF & Document Tools",
                Image = new Icon(Properties.Resources.ApplicationIcon, SystemInformation.SmallIconSize).ToBitmap(),
            };

            bool hasPdf = this.extensionCache.Contains("pdf");
            bool hasImages = this.extensionCache.Any(e =>
                e == "jpg" || e == "jpeg" || e == "png" || e == "bmp" ||
                e == "webp" || e == "tif" || e == "tiff" || e == "gif" || e == "avif");
            bool hasWord = this.extensionCache.Any(e => e == "doc" || e == "docx" || e == "odt");
            bool hasPpt = this.extensionCache.Any(e => e == "ppt" || e == "pptx" || e == "odp");
            bool hasExcel = this.extensionCache.Any(e => e == "xls" || e == "xlsx" || e == "ods");
            bool hasHtml = this.extensionCache.Any(e => e == "html" || e == "htm");

            int selectedCount = this.SelectedItemPaths.Count();

            if (hasPdf)
            {
                if (selectedCount >= 2)
                {
                    AddToolActionItem(toolsMenu, "Merge PDF", "MergePdf");
                }

                if (selectedCount == 2)
                {
                    AddToolActionItem(toolsMenu, "Compare PDF", "ComparePdf");
                }

                AddToolActionItem(toolsMenu, "Split PDF", "SplitPdf");
                AddToolActionItem(toolsMenu, "Compress PDF", "CompressPdf");
                AddToolActionItem(toolsMenu, "Organize PDF", "OrganizePdf");
                AddToolActionItem(toolsMenu, "Rotate PDF", "RotatePdf");
                AddToolActionItem(toolsMenu, "Crop PDF", "CropPdf");
                AddToolActionItem(toolsMenu, "Edit PDF", "EditPdf");
                AddToolActionItem(toolsMenu, "Page Numbers", "PageNumbersPdf");
                AddToolActionItem(toolsMenu, "OCR PDF", "OcrPdf");
                toolsMenu.DropDownItems.Add(new ToolStripSeparator());
                AddToolActionItem(toolsMenu, "PDF to Word", "PdfToWord");
                AddToolActionItem(toolsMenu, "PDF to PowerPoint", "PdfToPowerPoint");
                AddToolActionItem(toolsMenu, "PDF to Excel", "PdfToExcel");
                AddToolActionItem(toolsMenu, "PDF to JPG", "PdfToJpg");
                AddToolActionItem(toolsMenu, "PDF to Markdown", "PdfToMarkdown");
                toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            }

            if (hasImages)
            {
                AddToolActionItem(toolsMenu, "Images to PDF", "JpgToPdf");
            }

            if (hasWord)
            {
                AddToolActionItem(toolsMenu, "Word to PDF", "WordToPdf");
            }

            if (hasPpt)
            {
                AddToolActionItem(toolsMenu, "PowerPoint to PDF", "PowerPointToPdf");
            }

            if (hasExcel)
            {
                AddToolActionItem(toolsMenu, "Excel to PDF", "ExcelToPdf");
            }

            if (hasHtml)
            {
                AddToolActionItem(toolsMenu, "HTML to PDF", "HtmlToPdf");
            }

            AddToolActionItem(toolsMenu, "Open in File Converter Hub...", "UniversalConverter");
            parentItem.DropDownItems.Add(toolsMenu);
        }

        private void AddToolActionItem(ToolStripMenuItem parent, string text, string toolId)
        {
            ToolStripMenuItem item = new ToolStripMenuItem
            {
                Text = text,
            };
            item.Click += (sender, args) => this.LaunchTool(toolId);
            parent.DropDownItems.Add(item);
        }

        private void LaunchTool(string toolId)
        {
            if (string.IsNullOrEmpty(PathHelpers.FileConverterPath) || !File.Exists(PathHelpers.FileConverterPath))
            {
                MessageBox.Show("Can't retrieve the file converter executable path. You should try to reinstall the application.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("--tool \"").Append(toolId).Append('"');

            foreach (string filePath in this.SelectedItemPaths)
            {
                if (sb.Length + filePath.Length + 4 >= MaximumProcessArgumentsLength)
                {
                    break;
                }

                sb.Append(" \"").Append(filePath).Append('"');
            }

            var processStartInfo = new ProcessStartInfo(PathHelpers.FileConverterPath)
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                Arguments = sb.ToString(),
            };

            Process.Start(processStartInfo);
        }
    }
}
