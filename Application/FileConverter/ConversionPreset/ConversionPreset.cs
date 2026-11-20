// <copyright file="ConversionPreset.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Xml.Serialization;

    using CommunityToolkit.Mvvm.ComponentModel;

    using FileConverter.Controls;

    [XmlRoot]
    [XmlType]
    public class ConversionPreset : ObservableObject, IXmlSerializable
    {
        private string shortName;

        private OutputType outputType;
        private List<string> inputTypes;
        private InputPostConversionAction inputPostConversionAction;
        private ConversionSettings settings = new ConversionSettings();
        private string outputFileNameTemplate;

        public ConversionPreset()
        {
            this.FullName = Properties.Resources.DefaultPresetName;
        }

        public ConversionPreset(string shortName, OutputType outputType, params string[] inputTypes)
        {
            this.ShortName = shortName;
            this.OutputType = outputType;
            List<string> inputTypeList = new List<string>();
            inputTypeList.AddRange(inputTypes);
            this.InputTypes = inputTypeList;

            this.outputFileNameTemplate = "(p)(f)";
        }

        public ConversionPreset(string shortName, ConversionPreset source, params string[] additionalInputTypes)
        {
            this.ShortName = shortName;
            this.ParentFoldersNames = source.ParentFoldersNames;
            this.OutputType = source.outputType;
            List<string> inputTypeList = new List<string>();
            if (source.inputTypes != null)
            {
                inputTypeList.AddRange(source.inputTypes);
            }

            if (additionalInputTypes != null)
            {
                inputTypeList.AddRange(additionalInputTypes);
            }

            this.InputTypes = inputTypeList;

            this.outputFileNameTemplate = source.OutputFileNameTemplate;

            foreach (KeyValuePair<string, string> conversionSetting in source.settings)
            {
                this.SetSettingsValue(conversionSetting.Key, conversionSetting.Value);
            }
        }

        [XmlAttribute("Name")]
        public string FullName
        {
            get
            {
                string fullName = string.Empty;
                if (this.ParentFoldersNames != null)
                {
                    foreach (string folder in this.ParentFoldersNames)
                    {
                        fullName += $"{folder}/";
                    }
                }

                fullName += this.shortName;

                return fullName;
            }

            set
            {
                string[] folders = value.Split('/');
                if (folders.Length == 0)
                {
                    Diagnostics.Debug.Log("Invalid full name.");
                    this.ShortName = value;
                    return;
                }

                this.ShortName = folders[folders.Length - 1];
                Array.Resize(ref folders, folders.Length - 1);

                this.ParentFoldersNames = folders;
            }
        }

        [XmlIgnore]
        public string ShortName
        {
            get => this.shortName;

            set
            {
                this.shortName = value;
                this.OnPropertyChanged();
            }
        }

        [XmlIgnore]
        public string[] ParentFoldersNames
        {
            get;
            set;
        }

        [XmlAttribute]
        public OutputType OutputType
        {
            get => this.outputType;

            set
            {
                this.outputType = value;
                this.InitializeDefaultSettings(this.outputType, clearExisting: true);
                this.OnPropertyChanged();
                this.CoerceInputTypes();
            }
        }

        [XmlAttribute]
        public bool IsDefaultSettings
        {
            get;
            set;
        }

        [XmlElement]
        public List<string> InputTypes
        {
            get
            {
                return this.inputTypes;
            }

            set
            {
                this.inputTypes = value;
                for (int index = 0; index < this.inputTypes.Count; index++)
                {
                    this.inputTypes[index] = this.inputTypes[index].ToLowerInvariant();
                }

                this.OnPropertyChanged();
            }
        }

        [XmlElement]
        public InputPostConversionAction InputPostConversionAction
        {
            get
            {
                return this.inputPostConversionAction;
            }

            set
            {
                this.inputPostConversionAction = value;
                this.OnPropertyChanged();
            }
        }

        [XmlElement("Settings")]
        public ConversionSetting[] XmlSerializableSettings
        {
            get
            {
                int index = 0;
                ConversionSetting[] settings = new ConversionSetting[this.settings.Count];
                foreach (KeyValuePair<string, string> keyValuePair in this.settings)
                {
                    settings[index] = new ConversionSetting(keyValuePair);
                    index++;
                }

                return settings;
            }

            set
            {
                if (value != null)
                {
                    for (int index = 0; index < value.Length; index++)
                    {
                        // Compatibility issues.
                        if (value[index].Key == "Bitrate")
                        {
                            this.SetSettingsValue(ConversionSettingKeys.AudioBitrate, value[index].Value);
                            continue;
                        }

                        if (value[index].Key == "Encoding")
                        {
                            this.SetSettingsValue(ConversionSettingKeys.AudioEncodingMode, value[index].Value);
                            continue;
                        }

                        // Load settings.
                        this.SetSettingsValue(value[index].Key, value[index].Value);
                    }
                }

                this.OnPropertyChanged(nameof(this.Settings));
            }
        }

        [XmlElement]
        public string OutputFileNameTemplate
        {
            get
            {
                return this.outputFileNameTemplate;
            }

            set
            {
                this.outputFileNameTemplate = value;
                this.OnPropertyChanged();
            }
        }

        [XmlIgnore]
        public string ConversionArchiveFolderName
        {
            get
            {
                return Properties.Resources.ConversionArchives;
            }
        }

        [XmlIgnore]
        public IConversionSettings Settings
        {
            get => this.settings;

            set
            {
                if (value == null)
                {
                    return;
                }

                foreach (KeyValuePair<string, string> conversionSetting in value)
                {
                    if (!this.settings.ContainsKey(conversionSetting.Key))
                    {
                        this.settings.Add(conversionSetting.Key, conversionSetting.Value);
                    }
                    else
                    {
                        this.settings[conversionSetting.Key] = conversionSetting.Value;
                    }
                }

                this.OnPropertyChanged();
            }
        }

        public void Clean()
        {
            // Remove unrelevant settings.
            List<string> settingsToRemove = new List<string>();
            foreach (string settingsKey in this.settings.Keys)
            {
                if (!this.IsRelevantSetting(settingsKey))
                {
                    settingsToRemove.Add(settingsKey);
                }
            }

            for (int index = 0; index < settingsToRemove.Count; index++)
            {
                this.settings.Remove(settingsToRemove[index]);
            }
        }

        public void OnDeserializationComplete()
        {
            if (this.InputTypes != null)
            {
                for (int index = 0; index < this.InputTypes.Count; index++)
                {
                    this.InputTypes[index] = this.InputTypes[index].ToLowerInvariant();
                }
            }

            if (this.OutputType != OutputType.None)
            {
                this.InitializeDefaultSettings(this.OutputType, clearExisting: false);
            }

            this.CoerceInputTypes();
        }

        public void AddInputType(string inputType)
        {
            if (this.inputTypes.Contains(inputType))
            {
                return;
            }

            this.inputTypes.Add(inputType);
            this.OnPropertyChanged(nameof(this.InputTypes));
        }

        public void RemoveInputType(string inputType)
        {
            if (this.inputTypes.Remove(inputType))
            {
                this.OnPropertyChanged(nameof(this.InputTypes));
            }
        }

        public string GenerateOutputFilePath(string inputFilePath, int numberIndex, int numberMax)
        {
            return PathHelpers.GenerateFilePathFromTemplate(inputFilePath, this.OutputType, this.OutputFileNameTemplate, numberIndex, numberMax);
        }

        public void SetSettingsValue(string settingsKey, string value)
        {
            if (string.IsNullOrEmpty(settingsKey))
            {
                throw new ArgumentNullException(nameof(settingsKey));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!this.IsRelevantSetting(settingsKey))
            {
                return;
            }

            if (!this.settings.ContainsKey(settingsKey))
            {
                this.settings.Add(settingsKey, value);
            }

            this.settings[settingsKey] = value;

            this.OnPropertyChanged(nameof(this.Settings));
        }

        public string GetSettingsValue(string settingsKey)
        {
            if (string.IsNullOrEmpty(settingsKey))
            {
                throw new ArgumentNullException(nameof(settingsKey));
            }

            if (this.settings.ContainsKey(settingsKey))
            {
                return this.settings[settingsKey];
            }

            return null;
        }

        public T GetSettingsValue<T>(string settingsKey)
        {
            string settingsValue = this.GetSettingsValue(settingsKey);

            if (settingsValue == null)
            {
                return default(T);
            }

            Type type = typeof(T);
            if (type.IsEnum)
            {
                return (T)Enum.Parse(type, settingsValue);
            }

            return (T)Convert.ChangeType(settingsValue, type, NumberFormatInfo.InvariantInfo);
        }

        public bool IsRelevantSetting(string settingsKey)
        {
            return this.Settings.ContainsKey(settingsKey);
        }

        private void CoerceInputTypes()
        {
            if (this.inputTypes == null)
            {
                return;
            }

            for (int index = 0; index < this.inputTypes.Count; index++)
            {
                string inputType = this.inputTypes[index];
                if (!Helpers.IsOutputTypeCompatibleWithExtension(this.OutputType, inputType))
                {
                    this.RemoveInputType(inputType);
                    index--;
                }
            }
        }

        private void InitializeDefaultSettings(OutputType outputType, bool clearExisting)
        {
            if (clearExisting)
            {
                this.settings.Clear();
            }

            switch (outputType)
            {
                // Audio
                case OutputType.Aac:
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioBitrate, "128");
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioChannelCount, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                case OutputType.Flac:
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioChannelCount, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                case OutputType.Ogg:
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioBitrate, "160");
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioChannelCount, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                case OutputType.Mp3:
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioEncodingMode, EncodingMode.Mp3VBR.ToString(), clearExisting);
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioBitrate, "190");
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioChannelCount, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                case OutputType.Wav:
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioEncodingMode, EncodingMode.Wav16.ToString(), clearExisting);
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioChannelCount, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                // Video
                case OutputType.Avi:
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableAudio, "True");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoQuality, "20");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioBitrate, "190");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                case OutputType.Mkv:
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableAudio, "True");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoQuality, "28");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoEncodingSpeed, "Medium");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioBitrate, "128");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                case OutputType.Mp4:
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableAudio, "True");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoQuality, "28");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoEncodingSpeed, "Medium");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioBitrate, "128");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                case OutputType.Ogv:
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableAudio, "True");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoQuality, "7");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioBitrate, "160");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                case OutputType.Webm:
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableAudio, "True");
                    this.InitializeSettingsValue(ConversionSettingKeys.AudioBitrate, "160");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoQuality, "40");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.EnableFFMPEGCustomCommand, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.FFMPEGCustomCommand, string.Empty);
                    break;

                // Images
                case OutputType.Avif:
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageQuality, "50");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageClampSizePowerOf2, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageMaximumSize, "0");
                    break;

                case OutputType.Gif:
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.VideoFramesPerSecond, "15");
                    break;

                case OutputType.Png:
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageClampSizePowerOf2, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageMaximumSize, "0");
                    break;

                case OutputType.Jpg:
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageQuality, "90");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageClampSizePowerOf2, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageMaximumSize, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfToImageMode, "PagesToImages");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfRenderDpi, "200");
                    break;

                case OutputType.Webp:
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageQuality, "40");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageScale, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageRotation, "0");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageClampSizePowerOf2, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageMaximumSize, "0");
                    break;

                case OutputType.Ico:
                    break;

                // Documents
                case OutputType.Pdf:
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfOperationMode, "None");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfCompressionLevel, "Recommended");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfRotationAngle, "90");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageRange, string.Empty);
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfSplitMode, "SplitAllPages");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfSplitEveryNPages, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageSize, "A4");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageOrientation, "Auto");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageMargin, "Small");
                    this.InitializeSettingsValue(ConversionSettingKeys.ImageToPdfMerge, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageNumberPosition, "BottomCenter");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageNumberFormat, "{n} / {total}");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageNumberFont, "Arial");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageNumberFontSize, "11");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageNumberColor, "#333333");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageNumberStartPage, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageNumberStartNumber, "1");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfPageNumberMargin, "12");
                    this.InitializeSettingsValue(ConversionSettingKeys.ExcelPageOrientation, "Default");
                    this.InitializeSettingsValue(ConversionSettingKeys.ExcelFitColumnsToPage, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.OcrLanguages, "ita+eng");
                    this.InitializeSettingsValue(ConversionSettingKeys.OcrSkipTextPages, "True");
                    this.InitializeSettingsValue(ConversionSettingKeys.HtmlPageOrientation, "Portrait");
                    this.InitializeSettingsValue(ConversionSettingKeys.HtmlPageSize, "A4");
                    this.InitializeSettingsValue(ConversionSettingKeys.HtmlPrintBackground, "True");
                    this.InitializeSettingsValue(ConversionSettingKeys.HtmlDelayMs, "500");
                    break;

                case OutputType.Docx:
                case OutputType.Pptx:
                case OutputType.Xlsx:
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfToOfficeEngine, "Native");
                    break;

                case OutputType.Md:
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfMarkdownExtractImages, "False");
                    this.InitializeSettingsValue(ConversionSettingKeys.PdfMarkdownIncludePageBreaks, "False");
                    break;

                default:
                    throw new System.Exception("Missing default settings for type " + outputType);
            }

            this.OnPropertyChanged(nameof(this.Settings));
        }

        private void InitializeSettingsValue(string settingsKey, string value, bool force = false)
        {
            if (string.IsNullOrEmpty(settingsKey))
            {
                throw new ArgumentNullException("key");
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!this.settings.ContainsKey(settingsKey))
            {
                this.settings.Add(settingsKey, value);
            }
            else if (force)
            {
                this.settings[settingsKey] = value;
            }
        }

        public struct ConversionSetting
        {
            public ConversionSetting(KeyValuePair<string, string> keyValuePair)
            {
                this.Key = keyValuePair.Key;
                this.Value = keyValuePair.Value;
            }

            [XmlAttribute]
            public string Key
            {
                get;
                set;
            }

            [XmlAttribute]
            public string Value
            {
                get;
                set;
            }
        }

        public struct ConversionSettingKeys
        {
            public const string AudioEncodingMode = "AudioEncodingMode";
            public const string AudioBitrate = "AudioBitrate";
            public const string AudioChannelCount = "AudioChannelCount";
            public const string ImageQuality = "ImageQuality";
            public const string ImageScale = "ImageScale";
            public const string ImageRotation = "ImageRotation";
            public const string ImageClampSizePowerOf2 = "ImageClampSizePowerOf2";
            public const string ImageMaximumSize = "ImageMaximumSize";
            public const string VideoQuality = "VideoQuality";
            public const string VideoEncodingSpeed = "VideoEncodingSpeed";
            public const string VideoScale = "VideoScale";
            public const string VideoRotation = "VideoRotation";
            public const string VideoFramesPerSecond = "VideoFramesPerSecond";
            public const string FFMPEGCustomCommand = "FFMPEGCustomCommand";

            public const string EnableAudio = "EnableAudio";
            public const string EnableVideo = "EnableVideo";
            public const string EnableFFMPEGCustomCommand = "EnableFFMPEGCustomCommand";

            public const string PdfOperationMode = "PdfOperationMode";
            public const string PdfCompressionLevel = "PdfCompressionLevel";
            public const string PdfRotationAngle = "PdfRotationAngle";
            public const string PdfPageRange = "PdfPageRange";
            public const string PdfSplitMode = "PdfSplitMode";
            public const string PdfSplitEveryNPages = "PdfSplitEveryNPages";
            public const string PdfToImageMode = "PdfToImageMode";
            public const string PdfRenderDpi = "PdfRenderDpi";
            public const string PdfPageSize = "PdfPageSize";
            public const string PdfPageOrientation = "PdfPageOrientation";
            public const string PdfPageMargin = "PdfPageMargin";
            public const string ImageToPdfMerge = "ImageToPdfMerge";
            public const string PdfPageNumberPosition = "PdfPageNumberPosition";
            public const string PdfPageNumberFormat = "PdfPageNumberFormat";
            public const string PdfPageNumberFont = "PdfPageNumberFont";
            public const string PdfPageNumberFontSize = "PdfPageNumberFontSize";
            public const string PdfPageNumberColor = "PdfPageNumberColor";
            public const string PdfPageNumberStartPage = "PdfPageNumberStartPage";
            public const string PdfPageNumberStartNumber = "PdfPageNumberStartNumber";
            public const string PdfPageNumberMargin = "PdfPageNumberMargin";
            public const string ExcelPageOrientation = "ExcelPageOrientation";
            public const string ExcelFitColumnsToPage = "ExcelFitColumnsToPage";
            public const string OcrLanguages = "OcrLanguages";
            public const string OcrSkipTextPages = "OcrSkipTextPages";
            public const string PdfMarkdownExtractImages = "PdfMarkdownExtractImages";
            public const string PdfMarkdownIncludePageBreaks = "PdfMarkdownIncludePageBreaks";
            public const string PdfToOfficeEngine = "PdfToOfficeEngine";
            public const string HtmlPageOrientation = "HtmlPageOrientation";
            public const string HtmlPageSize = "HtmlPageSize";
            public const string HtmlPrintBackground = "HtmlPrintBackground";
            public const string HtmlDelayMs = "HtmlDelayMs";
        }
    }
}
