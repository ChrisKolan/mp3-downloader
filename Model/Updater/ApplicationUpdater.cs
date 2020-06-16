﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Model
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031: Do not catch general exception types")]
    class ApplicationUpdater
    {
        public static async Task UpdateAsync(ModelClass model)
        {
            string localVersions;
            if (UpdatesNeeded(out string configurationErrorsException))
            {
                model.StandardOutput = Localization.Properties.Resources.StandardOutputCheckingForUpdates;
                model.DisableInteractions();
                model.InformationAndExceptionOutput = configurationErrorsException;

                try
                {
                    await UpdatesDownloader.DownloadUpdatesAsync(model).ConfigureAwait(false);
                }
                catch (TaskCanceledException exception)
                {
                    model.DownloadLinkEnabled = true;
                    model.StandardOutput = Localization.Properties.Resources.UpdateFailedIncreaseTimeoutClickHereToDownloadManually;
                    localVersions = GetLocalVersions();
                    model.LocalVersions = localVersions;
                    model.HelpButtonToolTip = localVersions;
                    model.EnableInteractions();
                    model.InformationAndExceptionOutput = exception.Message;
                    return;
                }
                catch (Exception exception)
                {
                    model.DownloadLinkEnabled = true;
                    model.StandardOutput = Localization.Properties.Resources.UpdateFailedClickHereToDownloadManually;
                    localVersions = GetLocalVersions();
                    model.LocalVersions = localVersions;
                    model.HelpButtonToolTip = localVersions;
                    model.EnableInteractions();
                    model.InformationAndExceptionOutput = exception.Message;
                    return;
                }
            }

            model.StandardOutput = Localization.Properties.Resources.StandardOutputReady;
            localVersions = GetLocalVersions();
            model.LocalVersions = localVersions;
            model.HelpButtonToolTip = localVersions;
            model.EnableInteractions();
        }
        private static bool UpdatesNeeded(out string configurationErrorsException)
        {
            double updateIfElapsedTimeExceeded = 10; 
            DateTime lastStoredUpdateDateTimeFromBinary;
            string lastStoredUpdateDateTime = ApplicationSettingsProvider.GetValue("UpdateDateTime");
            if (lastStoredUpdateDateTime != null)
            {
                var lastStoredUpdateDateTimeLong = long.Parse(lastStoredUpdateDateTime, CultureInfo.InvariantCulture);
                lastStoredUpdateDateTimeFromBinary = DateTime.FromBinary(lastStoredUpdateDateTimeLong);
                var currentUpdateDateTime = UpdateSettings(out string configurationErrorsExceptionInternal);
                var elapsedTimeBetweenUpdates = currentUpdateDateTime.Subtract(lastStoredUpdateDateTimeFromBinary).TotalMinutes;
                configurationErrorsException = configurationErrorsExceptionInternal;
                return elapsedTimeBetweenUpdates > updateIfElapsedTimeExceeded;
            }
            else
            {
                UpdateSettings(out string configurationErrorsExceptionInternal);
                configurationErrorsException = configurationErrorsExceptionInternal;
                return true;
            }
        }
        private static DateTime UpdateSettings(out string configurationErrorsException)
        {
            var currentUpdateDateTime = DateTime.Now;
            var currentUpdateDateTimeString = currentUpdateDateTime.ToBinary().ToString(CultureInfo.InvariantCulture);
            ApplicationSettingsProvider.TryAddOrUpdateApplicationSettings("UpdateDateTime", currentUpdateDateTimeString, out string configurationErrorsExceptionInternal);
            configurationErrorsException = configurationErrorsExceptionInternal;

            return currentUpdateDateTime;
        }
        private static string GetLocalVersions()
        {
            var localVersionsNamesAndNumber = new List<string>
            {
                Localization.Properties.Resources.PressButtonToGetOnlineHelp,
                "===========================",
                Localization.Properties.Resources.SoftwareVersion,
                "----------------------|-----------------------"
            };
            var localVersions = LocalVersionProvider.Versions();
            var localVersionsSoftwareNames = new List<string>
            {
                "Audio Downloader  |\t",
                "Youtube-dl\t   |\t"
            };

            for (int i = 0; i < localVersions.Count; i++)
            {
                localVersionsNamesAndNumber.Add(localVersionsSoftwareNames[i] + localVersions[i]);
            }

            localVersionsNamesAndNumber.Add("FFmpeg\t\t   |\t4.2.1");

            return String.Join(Environment.NewLine, localVersionsNamesAndNumber.ToArray());
        }
    }
}
