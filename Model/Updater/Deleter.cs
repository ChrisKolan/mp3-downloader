﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031: Do not catch general exception types")]
    class Deleter
    {
        public static void DeleteBinFolderContents()
        {
            var pathToExe = Assembly.GetEntryAssembly().Location;
            var pathToBinFolder = Path.GetDirectoryName(pathToExe) + @"\bin";
            DirectoryInfo directoryInfo = new DirectoryInfo(pathToBinFolder);
            FileInfo[] fileInfoArray = directoryInfo.GetFiles();
            foreach (FileInfo file in fileInfoArray)
            {
                try
                {
                    file.Delete();
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
            }
        }

        public static void DeleteOldFiles()
        {
            var pathToExe = Assembly.GetEntryAssembly().Location;
            var pathToBinFolder = Path.GetDirectoryName(pathToExe);
            DirectoryInfo directoryInfo = new DirectoryInfo(pathToBinFolder);
            FileInfo[] fileInfoArray = directoryInfo.GetFiles("old_*.*");
            foreach (FileInfo file in fileInfoArray)
            {
                file.Delete();
            }
        }
    }
}
