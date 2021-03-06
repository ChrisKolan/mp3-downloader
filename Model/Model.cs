﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using Webhook;
using Logger;
using Serilog;

namespace Model
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822: Mark members as static")]
    public class ModelClass : INotifyPropertyChanged, INotifyDataErrorInfo, IDisposable
    {
        #region Fields
        private string _standardOutput;
        private string _timersOutput;
        private string _buttonContent;
        private static bool _measureProcessingTime;
        private static bool _measureDownloadTime;
        private string _finishedMessage;
        private string _downloadedFileSize;
        private int _progressBarPercent;
        private double _taskBarProgressValue;
        private TaskbarItemProgressState _taskbarItemProgressStateModel;
        private bool _isIndeterminate;
        private bool _isButtonEnabled;
        private bool _isInputEnabled;
        private string _selectedQuality;
        private string _helpButtonToolTip;
        private string _folderButtonToolTip;
        private string _downloadLink;
        private bool _isComboBoxEnabled;
        private bool _isAutomaticDownloadSelected;
        private bool _isUsingLastQualitySelected;
        private bool _downloadLinkEnabled;
        private double _downloadFileSize;
        private int _audioVideoDownloadCounter;
        private int _processingTime = 1;
        private int _downloadTime = 1;
        private readonly int _timerResolution = 100;
        private readonly SynchronizationContext _synchronizationContext;
        private Thread _currentThreadPoolWorker;
        private bool _isDownloadRunning;
        private bool _isOnline = true;
        private SolidColorBrush _glowBrushColor;
        private bool _isClipboardCaptureSelected;
        private int _pingerCounter;
        private int _lastUsedQualityIndex;
        private string _informationAndExceptionOutput;
        private string _textBoxHelper;
        private CircularQueue<string> _informationAndException;
        private bool _isWebsitesUnlockerSelected;
        #endregion

        #region Constructor
        public ModelClass()
        {
            Initalize();
            EnableInteractions();
            PeriodicTimerProcessing = new Timer(_ => ProcessingTimeMeasurement(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_timerResolution));
            PeriodicTimerDownload = new Timer(_ => DownloadTimeMeasurement(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_timerResolution));
            PeriodicTimerPinger = new Timer(_ => TimerPinger(), null, TimeSpan.FromMilliseconds(15000), TimeSpan.FromMilliseconds(1000));
            PeriodicTimerClipper = new Timer(_ => TimerClipper(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_timerResolution));
            QualityDefault = HelpersModel.QualityDefault();
            Quality = HelpersModel.QualityObservableCollection();
            SelectedQuality = Quality[0];
            _ = ApplicationUpdater.UpdateAsync(this);
            GlowBrushColor = new SolidColorBrush(Colors.LightBlue);
            _synchronizationContext = SynchronizationContext.Current;
        }
        #endregion

        #region Properties

        public List<string> QualityDefault { get; }
        public ILogger Log { get; set; }

        public ObservableCollection<string> Quality { get; }

        public string LocalVersions { get; set; }

        [Required]
        [CustomValidation(typeof(ModelClass), "ValidateDownloadLink")]
        public string DownloadLink
        {
            get { return _downloadLink; }
            set
            {
                _downloadLink = value;
                if (IsWebsitesUnlockerSelected && IsAutomaticDownloadSelected)
                {
                    DownloadButtonClick();
                }
                OnPropertyChanged(nameof(DownloadLink));
                ValidateAsync();
            }
        }

        public string DownloadedFileSize
        {
            get { return _downloadedFileSize; }
            set 
            { 
                if(value != null && _downloadedFileSize != value)
                {
                    if (_audioVideoDownloadCounter < 2)
                    {
                        if (double.TryParse(value.Remove(value.Length - 3), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var downloadedSize))
                        {
                            _downloadFileSize += downloadedSize;
                        }
                        _audioVideoDownloadCounter++;
                    }
                    _downloadedFileSize = _downloadFileSize.ToString(CultureInfo.InvariantCulture) + "MiB";
                }
            }
        }

        public bool DownloadLinkEnabled
        {
            get { return _downloadLinkEnabled; }
            set
            {
                _downloadLinkEnabled = value;
                OnPropertyChanged(nameof(DownloadLinkEnabled));
            }
        }

        public bool IsComboBoxEnabled
        {
            get { return _isComboBoxEnabled; }
            set
            {
                _isComboBoxEnabled = value;
                OnPropertyChanged(nameof(IsComboBoxEnabled));
            }
        }

        public bool IsAutomaticDownloadSelected
        {
            get { return _isAutomaticDownloadSelected; }
            set
            {
                _isAutomaticDownloadSelected = value;
                OnPropertyChanged(nameof(IsAutomaticDownloadSelected));
            }
        }

        public bool IsUsingLastQualitySelected
        {
            get { return _isUsingLastQualitySelected; }
            set
            {
                _isUsingLastQualitySelected = value;
                OnPropertyChanged(nameof(IsUsingLastQualitySelected));
            }
        }

        public bool IsWebsitesUnlockerSelected
        {
            get { return _isWebsitesUnlockerSelected; }
            set
            {
                _isWebsitesUnlockerSelected = value;
                Quality.Clear();
                if (_isWebsitesUnlockerSelected)
                {
                    TextBoxHelper = Localization.Properties.Resources.TextBoxInitial;
                    Quality.Add(Localization.Properties.Resources.TextBoxContentInitial);
                }
                else
                {
                    TextBoxHelper = Localization.Properties.Resources.TextBoxYouTubeInitial;
                    Quality.Add(HelpersModel.QualityObservableCollection()[0]);
                }
                SelectedQuality = Quality[0];
                OnPropertyChanged(nameof(IsWebsitesUnlockerSelected));
            }
        }

        public string SelectedQuality
        {
            get { return _selectedQuality; }
            set
            {
                _selectedQuality = value;
                if (value != null)
                {
                    if (IsAutomaticDownloadSelected)
                    {
                        DownloadButtonClick();
                    }
                    _lastUsedQualityIndex = Quality.IndexOf(SelectedQuality);
                    OnPropertyChanged(nameof(SelectedQuality));
                }
                else
                {
                    _lastUsedQualityIndex = 2;
                }
            }
        }

        public string CurrentStandardOutputLine { get; set; }

        public string TextBoxHelper
        {
            get { return _textBoxHelper; }
            set
            {
                _textBoxHelper = value;
                OnPropertyChanged(nameof(TextBoxHelper));
            }
        }
        public string StandardOutput
        {
            get { return _standardOutput; }
            set
            {
                _standardOutput = value;
                OnPropertyChanged(nameof(StandardOutput));
            }
        }
        public string TimersOutput
        {
            get { return _timersOutput; }
            set
            {
                _timersOutput = value;
                OnPropertyChanged(nameof(TimersOutput));
            }
        }
        public string ButtonContent
        {
            get { return _buttonContent; }
            set
            {
                _buttonContent = value;
                OnPropertyChanged(nameof(ButtonContent));
            }
        }
        public int ProgressBarPercent
        {
            get { return _progressBarPercent; }
            set
            {
                _progressBarPercent = value;
                OnPropertyChanged(nameof(ProgressBarPercent));
            }
        }
        public double TaskBarProgressValue
        {
            get { return _taskBarProgressValue; }
            set
            {
                _taskBarProgressValue = value;
                OnPropertyChanged(nameof(TaskBarProgressValue));
            }
        }
        public TaskbarItemProgressState TaskbarItemProgressStateModel
        {
            get { return _taskbarItemProgressStateModel; }
            set
            {
                _taskbarItemProgressStateModel = value;
                OnPropertyChanged(nameof(TaskbarItemProgressStateModel));
            }
        }

        public bool IsIndeterminate
        {
            get { return _isIndeterminate; }
            set
            {
                _isIndeterminate = value;
                OnPropertyChanged(nameof(IsIndeterminate));
            }
        }

        public bool IsButtonEnabled
        {
            get { return _isButtonEnabled; }
            set
            {
                _isButtonEnabled = value;
                OnPropertyChanged(nameof(IsButtonEnabled));
            }
        }

        public bool IsInputEnabled
        {
            get { return _isInputEnabled; }
            set
            {
                _isInputEnabled = value;
                OnPropertyChanged(nameof(IsInputEnabled));
            }
        }

        public string FolderButtonToolTip
        {
            get { return _folderButtonToolTip; }
            set
            {
                _folderButtonToolTip = value;
                OnPropertyChanged(nameof(FolderButtonToolTip));
            }
        }

        public string HelpButtonToolTip
        {
            get { return _helpButtonToolTip; }
            set
            {
                _helpButtonToolTip = value;
                OnPropertyChanged(nameof(HelpButtonToolTip));
            }
        }

        public string InformationAndExceptionOutput
        {
            get { return _informationAndExceptionOutput; }
            set 
            {
                Contract.Requires(value != null);
                _informationAndException.Enqueue(DateTime.Now.ToString(CultureInfo.InvariantCulture) + ": " + value);
                _informationAndExceptionOutput = string.Join(Environment.NewLine, _informationAndException);
                OnPropertyChanged(nameof(InformationAndExceptionOutput));
            }
        }

        public SolidColorBrush GlowBrushColor
        {
            get { return _glowBrushColor; }
            set 
            { 
                _glowBrushColor = value;
                OnPropertyChanged(nameof(GlowBrushColor));
            }
        }

        public bool IsClipboardCaptureSelected
        {
            get { return _isClipboardCaptureSelected; }
            set 
            { 
                _isClipboardCaptureSelected = value;
                OnPropertyChanged(nameof(IsClipboardCaptureSelected));
            }
        }

        public Timer PeriodicTimerProcessing { get; }
        public Timer PeriodicTimerDownload { get; }
        public Timer PeriodicTimerPinger { get; }
        public Timer PeriodicTimerClipper { get; }
        #endregion

        #region Methods
        public void DownloadButtonClick()
        {
            if (string.IsNullOrWhiteSpace(DownloadLink))
            {
                DownloadLinkDisabler(this);
                StandardOutput = Localization.Properties.Resources.StandardOutputEmptyLink;
                return;
            }
            if (!_isDownloadRunning)
            {
                ButtonContent = Localization.Properties.Resources.ButtonContentCancel;
                Task.Run(() => ThreadPoolWorker());
            }
            else
            {
                _measureProcessingTime = false;
                _measureDownloadTime = false;
                _downloadedFileSize = null;
                _processingTime = 1;
                _downloadTime = 1;
                _isDownloadRunning = false;
                StandardOutput = Localization.Properties.Resources.StandardOutputReady;
                TimersOutput = string.Empty;
                ButtonContent = Localization.Properties.Resources.ButtonContentDownload;
                EnableInteractions();
                _currentThreadPoolWorker.Abort();
            }
        }
        public void HelpButtonClick()
        {
            Process.Start("https://chriskolan.github.io/AudioDownloader/details.html");
        }
        public void DownloadLinkButtonClick()
        {
            Process.Start("https://github.com/ChrisKolan/AudioDownloader/releases/latest/download/AudioDownloader.zip");
        }
        public void FolderButtonClick()
        {
            var pathToAudioFolder = ApplicationPaths.GetAudioPath();
            Directory.CreateDirectory(pathToAudioFolder);
            Process.Start(pathToAudioFolder);
        }
        public void ChangeFolderButtonClick()
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if ((bool)dialog.ShowDialog())
            {
                string path = dialog.SelectedPath;
                ApplicationPaths.SetAudioPath(path);
                FolderButtonToolTip = HelpersModel.ToolTipFolder;
            }
        }
        private void ThreadPoolWorker()
        {
            _isDownloadRunning = true;
            string selectedQuality = HelpersModel.GetQuality(SelectedQuality);
            Log.Information(selectedQuality);
            DisableInteractions();
            Thread.CurrentThread.IsBackground = true;
            _currentThreadPoolWorker = Thread.CurrentThread;

            StandardOutput = Localization.Properties.Resources.StartingDownload;
            var statusWithLink = Localization.Properties.Resources.StatusLogStartingDownloadOfTheLink + DownloadLink;
            InformationAndExceptionOutput = statusWithLink; 
            Log.Information(statusWithLink);
            string command;
            (command, _finishedMessage) = HelpersModel.CreateCommandAndMessage(selectedQuality, DownloadLink);

            var startinfo = new ProcessStartInfo("CMD.exe", command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var readLineExtractor = new ReadLineExtractorModel();
            var process = new Process { StartInfo = startinfo };
            process.Start();

            var reader = process.StandardOutput;
            while (!reader.EndOfStream)
            {
                StandardOutput = reader.ReadLine();
                StandardOutput = HelpersModel.StandardOutputLocalizator(StandardOutput);
                (_measureDownloadTime, _downloadedFileSize, IsIndeterminate, TaskbarItemProgressStateModel, ProgressBarPercent, TaskBarProgressValue, _measureProcessingTime, StandardOutput) = readLineExtractor.Extract(StandardOutput, _finishedMessage);
                Thread.Sleep(_timerResolution);
            }

            process.WaitForExit();
            _measureProcessingTime = false;

            if (_downloadedFileSize == null || string.IsNullOrWhiteSpace(_downloadedFileSize))
            {
                TimersOutput = string.Empty;
                TaskBarProgressValue = HelpersModel.GetTaskBarProgressValue(100, 100);
                TaskbarItemProgressStateModel = TaskbarItemProgressState.Error;
                Thread.Sleep(1000);
                if (_isOnline)
                {
                    if (IsWebsitesUnlockerSelected)
                    {
                        if (!DownloadLink.Contains("https://www."))
                        {
                            StandardOutput = Localization.Properties.Resources.NotValidLink;
                            Log.Error(StandardOutput);
                        }
                        else
                        {
                            StandardOutput = Localization.Properties.Resources.NoFileDownloadedUnsupportedWebsite;
                            Log.Error(StandardOutput);
                        }
                    }
                    else
                    {
                        StandardOutput = Localization.Properties.Resources.ErrorNoFileDownloadedUpdatesAreNeeded;
                        Log.Error(StandardOutput);
                    }
                }
                else
                {
                    StandardOutput = Localization.Properties.Resources.ErrorNoInternetConnectionNoFileDownloaded;
                    Log.Error(StandardOutput);
                }
                ButtonContent = Localization.Properties.Resources.ButtonContentDownload;
                EnableInteractions();
                _isDownloadRunning = false;
                process.Dispose();
                return;
            }
            if (_downloadedFileSize == Localization.Properties.Resources.FileHasAlreadyBeenDownloaded)
            {
                TimersOutput = string.Empty;
                TaskBarProgressValue = HelpersModel.GetTaskBarProgressValue(100, 100);
                TaskbarItemProgressStateModel = TaskbarItemProgressState.Paused;
                Thread.Sleep(1000);
                var output = Localization.Properties.Resources.StandardOutputReady + ". " + _downloadedFileSize;
                StandardOutput = output;
                Log.Information(output);
                ButtonContent = Localization.Properties.Resources.ButtonContentDownload;
                EnableInteractions();
                _isDownloadRunning = false;
                process.Dispose();
                return;
            }
            else
            {
                var processingTimeTimer = (_processingTime * _timerResolution) / 1000.0;
                var downloadTimeTimer = (_downloadTime * _timerResolution) / 1000.0;
                (string fileName, double fileSize) = GetFileNameAndSize(selectedQuality);
                if (_downloadedFileSize.Contains("~"))
                {
                    _downloadedFileSize = _downloadedFileSize.Substring(1);
                }
                TimersOutput = string.Empty;
                StandardOutput = Localization.Properties.Resources.ProcessingTime + processingTimeTimer.ToString("N0", CultureInfo.InvariantCulture) + "s. " +
                                 Localization.Properties.Resources.DownloadTime + downloadTimeTimer.ToString("N0", CultureInfo.InvariantCulture) + "s. " +
                                 Localization.Properties.Resources.DownloadedFileSize + _downloadedFileSize + ". " +
                                 Localization.Properties.Resources.TranscodedFileSize + fileSize.ToString("F", CultureInfo.InvariantCulture) + "MiB. ";

                if (double.TryParse(_downloadedFileSize.Remove(_downloadedFileSize.Length - 3), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var downloadedFileSize))
                {
                    var ratio = downloadedFileSize / fileSize;
                    StandardOutput += Localization.Properties.Resources.Ratio + ratio.ToString("F", CultureInfo.InvariantCulture) + ".";
                }

                SendData(fileName, StandardOutput);
                _downloadedFileSize = null;
                _processingTime = 1;
                _downloadTime = 1;
                ButtonContent = Localization.Properties.Resources.ButtonContentDownload;
                EnableInteractions();
                _isDownloadRunning = false;
                process.Dispose();
                InformationAndExceptionOutput = Localization.Properties.Resources.StatusLogFinishedDownload;
                Log.Information(StandardOutput);
            }
        }

        private void SendData(string fileName, string standardOutput)
        {
            try
            {
                UpdateWebhook(DownloadLink, fileName, standardOutput);
            }
            catch (HttpRequestException webhookException)
            {
                TaskBarProgressValue = HelpersModel.GetTaskBarProgressValue(100, 100);
                TaskbarItemProgressStateModel = TaskbarItemProgressState.Error;
                Thread.Sleep(1000);
                StandardOutput = Localization.Properties.Resources.ExceptionUpdatingWebhookFailed;
                Log.Error(webhookException, Localization.Properties.Resources.ExceptionUpdatingWebhookFailed);
                _informationAndExceptionOutput = webhookException.Message;
                _downloadedFileSize = null;
                EnableInteractions();
                return;
            }
        }

        private void GetYouTubeAvailableFormatsWorker(Object state)
        {
            IsIndeterminate = true;
            IsComboBoxEnabled = false;
            TaskbarItemProgressStateModel = TaskbarItemProgressState.Indeterminate;
            var command = "/C bin\\youtube-dl.exe -F " + DownloadLink;
            var availableFormats = new List<string>();
            var availableAudioFormats = new List<string>();
            availableFormats.Add(Environment.NewLine);
            availableFormats.Add("==========================================================================");
            availableFormats.Add(Localization.Properties.Resources.AdvancedInformationAvailableYouTubeFileFormats);

            var startinfo = new ProcessStartInfo("CMD.exe", command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = new Process { StartInfo = startinfo };
            process.Start();

            var reader = process.StandardOutput;
            while (!reader.EndOfStream)
            {
                var currentLine = reader.ReadLine();
                if (currentLine.Contains("["))
                {
                    availableFormats.Add(currentLine);
                    continue;
                }

                availableFormats.Add(ArragementFileFormatsOutput(currentLine));
                
                if (currentLine.Contains("audio only"))
                {
                    availableAudioFormats.Add(currentLine);
                }
            }
            if(!(state is SynchronizationContext uiContext))
            {
                process.Dispose();
                return;
            }
            uiContext.Send(UpdateUiFromTheWorkerThread, availableAudioFormats);
            HelpButtonToolTip = LocalVersions + String.Join(Environment.NewLine, availableFormats.ToArray());
            IsIndeterminate = false;
            TaskbarItemProgressStateModel = TaskbarItemProgressState.Normal;
            process.Dispose();
        }

        private void UpdateUiFromTheWorkerThread(object state)
        {
            Quality.Clear();
            var availableAudioFormats = state as List<string>;
            var qualityDynamic = new List<string>();
            var qualityDynamicFormat = new List<string>();
            availableAudioFormats.Reverse();
            bool addOpus = true, addVorbis = true, addM4a = true;

            foreach (var item in availableAudioFormats)
            {
                if (HelpersModel.FindFormat(item).Contains("opus"))
                {
                    if (addOpus)
                    {
                        qualityDynamic.Add(Localization.Properties.Resources.AudioQualityRawWebm);
                        qualityDynamic.Add(Localization.Properties.Resources.AudioQualityRawOpus);
                        addOpus = false;
                    }
                }
                if (HelpersModel.FindFormat(item).Contains("vorbis"))
                {
                    if (addVorbis)
                    {
                        qualityDynamic.Add(Localization.Properties.Resources.AudioQualityRawVorbis);
                        addVorbis = false;
                    }
                }
                if (HelpersModel.FindFormat(item).Contains("m4a"))
                {
                    if (addM4a)
                    {
                        qualityDynamic.Add(Localization.Properties.Resources.AudioQualityRawAac);
                        addM4a = false;
                    }
                }

                qualityDynamicFormat.Add(ArragementDynamicFormatsOutput(item));
            }

            qualityDynamic.ForEach(Quality.Add);
            QualityDefault.ForEach(Quality.Add);
            qualityDynamicFormat.ForEach(Quality.Add);
            Quality.Add(Localization.Properties.Resources.AudioAndVideoBestYouTubeQuality);

            var optimalQualityIndex = Quality.ToList().FindIndex(x => x.Contains("m4a"));
            var defaultQualityCount = 19;
            if (optimalQualityIndex != -1)
            {
                if (IsUsingLastQualitySelected && Quality.Count == defaultQualityCount)
                {
                    SelectedQuality = Quality[_lastUsedQualityIndex];
                }
                else
                {
                    SelectedQuality = Quality[optimalQualityIndex];
                }
            }
            else
            {
                Quality.Clear();
                Quality.Add(Localization.Properties.Resources.AudioQualityCouldNotBeRetrieved);
                SelectedQuality = Quality[0];
                if (_isOnline)
                {
                    StandardOutput = Localization.Properties.Resources.StandardOutputYouTubeLinkIsInvalid;
                    Log.Error(StandardOutput);
                }
                else
                {
                    StandardOutput = Localization.Properties.Resources.StandardOutputNoInternetConnection;
                    Log.Error(StandardOutput);
                }
                IsButtonEnabled = false;
                return;
            }
            StandardOutput = Localization.Properties.Resources.StandardOutputReady;
            IsComboBoxEnabled = true;
        }
        private static string ArragementDynamicFormatsOutput(string currentLine)
        {
            string result;
            var stringCleanedFromSpaces = System.Text.RegularExpressions.Regex.Replace(currentLine, @"\s+", " ");
            var index = stringCleanedFromSpaces.IndexOf(' ', 0);
            var replaceFirstSpace = stringCleanedFromSpaces.Insert(index, "\t").Remove(index + 1, 1);

            var index2 = replaceFirstSpace.IndexOf(' ', index);
            result = replaceFirstSpace.Insert(index2, "\t\t\t").Remove(index2 + 2, 1);
            return result;
        }

        private static string ArragementFileFormatsOutput(string currentLine)
        {
            string result;

            if (currentLine.Contains("format code"))
            {
                result = "format code\textension\tresolution note";
                return result;
            }
            
            var stringCleanedFromSpaces = System.Text.RegularExpressions.Regex.Replace(currentLine, @"\s+", " ");
            var index = stringCleanedFromSpaces.IndexOf(' ', 0);
            var replaceFirstSpace = stringCleanedFromSpaces.Insert(index, "\t\t").Remove(index + 2, 1);

            var index2 = replaceFirstSpace.IndexOf(' ', index);
            result = replaceFirstSpace.Insert(index2, "\t\t").Remove(index2 + 2, 1);
            return result;
        }

        private void Initalize()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var audioDownloaderPath = @"\AudioDownloader\AudioDownloader.txt";
            Log = LoggerSerilog.Create(appDataPath + audioDownloaderPath);
            LogSystemInformation.Log(Log);
            StandardOutput = Localization.Properties.Resources.StandardOutputReady;
            ButtonContent = Localization.Properties.Resources.ButtonContentDownload;
            FolderButtonToolTip = HelpersModel.ToolTipFolder;
            TextBoxHelper = Localization.Properties.Resources.TextBoxYouTubeInitial;
            _informationAndException = new CircularQueue<string>(20);
            InformationAndExceptionOutput = Localization.Properties.Resources.StatusLogApplicationInitialized;
            Log.Information(Localization.Properties.Resources.StatusLogApplicationInitialized);
        }

        public void EnableInteractions()
        {
            IsIndeterminate = false;
            IsInputEnabled = true;
            IsButtonEnabled = true;
            IsComboBoxEnabled = true;
            ProgressBarPercent = 0;
            TaskBarProgressValue = HelpersModel.GetTaskBarProgressValue(100, ProgressBarPercent);
            TaskbarItemProgressStateModel = TaskbarItemProgressState.Normal;
        }

        public void DisableInteractions()
        {
            IsIndeterminate = true;
            IsInputEnabled = false;
            IsComboBoxEnabled = false;
            ProgressBarPercent = 0;
            _downloadFileSize = 0.0;
            _audioVideoDownloadCounter = 0;
            TaskBarProgressValue = HelpersModel.GetTaskBarProgressValue(100, ProgressBarPercent);
            TaskbarItemProgressStateModel = TaskbarItemProgressState.Indeterminate;
        }

        private void ProcessingTimeMeasurement()
        {
            if (!_measureProcessingTime)
            {
                return;
            }
            _processingTime++;
            IsIndeterminate = true;
            TimersOutput = Localization.Properties.Resources.ProcessingTime + ((_processingTime * _timerResolution) / 1000.0).ToString("N1", CultureInfo.InvariantCulture) + "s";
            TaskbarItemProgressStateModel = TaskbarItemProgressState.Indeterminate;
        }

        private void DownloadTimeMeasurement()
        {
            if (!_measureDownloadTime)
            {
                return;
            }
            if (_isOnline)
            {
                _downloadTime++;
                TimersOutput = Localization.Properties.Resources.DownloadTime + ((_downloadTime * _timerResolution) / 1000.0).ToString("N1", CultureInfo.InvariantCulture) + "s";
            }
        }

        private void TimerClipper()
        {
            if (IsClipboardCaptureSelected)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (DownloadLink == null)
                    {
                        DownloadLink = Clipboard.GetText();
                    }
                    if (!DownloadLink.Contains(Clipboard.GetText()))
                    {
                        DownloadLink = Clipboard.GetText();
                    }
                }, System.Windows.Threading.DispatcherPriority.Send);
            }
        }

        private void TimerPinger()
        {
            var secondsToIndicateOfflineStatus = 7;
            if (PingUtility.Pinger(out string pingException))
            {
                _isOnline = true;
                if (TimersOutput != null && TimersOutput.Contains("Offline"))
                {
                    TimersOutput = string.Empty;
                }
                if (StandardOutput.Contains(Localization.Properties.Resources.StandardOutputNoInternetConnection))
                {
                    StandardOutput = Localization.Properties.Resources.InternetConnectionReestablished;
                }
                if (Application.Current == null)
                {
                    return;
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    GlowBrushColor = new SolidColorBrush(Colors.LightBlue);
                });
                _pingerCounter = 0;
                HelpButtonToolTip = PingUtility.AddPingToHelpButtonToolTip(HelpButtonToolTip);
            }
            else
            {
                if (_pingerCounter >= secondsToIndicateOfflineStatus)
                {
                    InformationAndExceptionOutput = pingException;
                    _isOnline = false;
                    TimersOutput = "Offline";
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        GlowBrushColor = new SolidColorBrush(Colors.Red);
                    });
                    HelpButtonToolTip = PingUtility.AddPingToHelpButtonToolTip(HelpButtonToolTip);
                }
                _pingerCounter++;
            }
        }

        private void UpdateWebhook(string youtubeLink, string fileName, string standardOutput)
        {
            string eventName = ConfigurationManager.AppSettings["event"];
            string secretKey = ConfigurationManager.AppSettings["writeKey"];
            if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(secretKey))
            {
                return;
            }
            var pcName = Environment.MachineName;
            var values = new Dictionary<string, string>
            {
                { "value1", pcName + " " + standardOutput.Substring(6) },
                { "value2", fileName },
                { "value3", youtubeLink }
            };
            WebhookTrigger.SendRequestAsync(values, eventName, secretKey);
        }

        private (string fileName, double fileSize) GetFileNameAndSize(string selectedQuality)
        {
            string path = selectedQuality.Contains("video") ? ApplicationPaths.AudioVideoPath : ApplicationPaths.GetAudioPath();
            var directory = new DirectoryInfo(path);
            var myFile = directory.GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            var length = new FileInfo(path + myFile.ToString()).Length;

            return (fileName: myFile.ToString(), fileSize: ConvertBytesToMegabytes(length));
        }

        private static double ConvertBytesToMegabytes(long bytes)
        {
            return (bytes / 1024f) / 1024f;
        }

        private void DownloadLinkDisabler(ModelClass model)
        {
            model.DownloadLinkEnabled = false;
        }

        #endregion

        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region INotifyDataErrorInfo implementation

        private readonly ConcurrentDictionary<string, List<string>> _errors = new ConcurrentDictionary<string, List<string>>();

        public static ValidationResult ValidateDownloadLink(object value, ValidationContext context)
        {
            Contract.Requires(value != null);
            Contract.Requires(context != null);
            var model = (ModelClass)context.ObjectInstance;
            if (model.IsWebsitesUnlockerSelected)
            {
                return RetreiveQuality(model);
            }
            if (!value.ToString().Contains("https://www.youtube.com/"))
            {
                model.DownloadLinkDisabler(model);
                model.IsButtonEnabled = false;
                model.IsComboBoxEnabled = false;
                return new ValidationResult(Localization.Properties.Resources.ValidationYouTubeLinkNotValid, new List<string> { "DownloadLink" });
            }

            return RetreiveQuality(model);
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        public IEnumerable GetErrors(string propertyName)
        {
            _errors.TryGetValue(propertyName, out List<string> errorsForName);
            return errorsForName;
        }

        public bool HasErrors
        {
            get { return _errors.Any(kv => kv.Value != null && kv.Value.Count > 0); }
        }

        public Task ValidateAsync()
        {
            return Task.Run(() => Validate());
        }

        private readonly object _lock = new object();
        public void Validate()
        {
            lock (_lock)
            {
                var validationContext = new ValidationContext(this, null, null);
                var validationResults = new List<ValidationResult>();
                Validator.TryValidateObject(this, validationContext, validationResults, true);

                foreach (var kv in _errors.ToList())
                {
                    if (validationResults.All(r => r.MemberNames.All(m => m != kv.Key)))
                    {
                        _errors.TryRemove(kv.Key, out List<string> outLi);
                        OnErrorsChanged(kv.Key);
                    }
                }

                var q = from r in validationResults
                        from m in r.MemberNames
                        group r by m into g
                        select g;

                foreach (var prop in q)
                {
                    var messages = prop.Select(r => r.ErrorMessage).ToList();

                    if (_errors.ContainsKey(prop.Key))
                    {
                        _errors.TryRemove(prop.Key, out List<string> outLi);
                    }
                    _errors.TryAdd(prop.Key, messages);
                    OnErrorsChanged(prop.Key);
                }
            }
        }

        private static ValidationResult RetreiveQuality(ModelClass model)
        {
            model.DownloadLinkDisabler(model);
            model.IsButtonEnabled = true;
            model.IsComboBoxEnabled = true;
            if (model.IsWebsitesUnlockerSelected)
            {
                return ValidationResult.Success;
            }
            model.StandardOutput = Localization.Properties.Resources.StandardOutputRetrievingQuality;
            model.Log.Information(Localization.Properties.Resources.StandardOutputRetrievingQuality);
            Task.Run(() => model.GetYouTubeAvailableFormatsWorker(model._synchronizationContext));
            return ValidationResult.Success;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    PeriodicTimerClipper.Dispose();
                    PeriodicTimerDownload.Dispose();
                    PeriodicTimerPinger.Dispose();
                    PeriodicTimerProcessing.Dispose();
                }
                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Model()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion

        #endregion
    }
}