﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    public class BaseTest
    {
        public BaseTest()
        {
            // https://dejanstojanovic.net/aspnet/2015/january/set-entry-assembly-in-unit-testing-methods/
            // https://stackoverflow.com/a/21888521/
            /* Preparing test start */
            Assembly assembly = Assembly.GetCallingAssembly();

            AppDomainManager manager = new AppDomainManager();
            FieldInfo entryAssemblyfield = manager.GetType().GetField("m_entryAssembly", BindingFlags.Instance | BindingFlags.NonPublic);
            entryAssemblyfield.SetValue(manager, assembly);

            AppDomain domain = AppDomain.CurrentDomain;
            FieldInfo domainManagerField = domain.GetType().GetField("_domainManager", BindingFlags.Instance | BindingFlags.NonPublic);
            domainManagerField.SetValue(domain, manager);
            /* Preparing test end */
        }

        public int NumberOfFilesInDirectory(string path)
        {
            return System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(path), "*", System.IO.SearchOption.TopDirectoryOnly).Length;
        }

        public string[] FileNamesAndPath(string path)
        {
            return System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(path), "*").ToArray();
        }

        public void DeleteFiles(string[] fileNames)
        {
            foreach (var fileName in fileNames)
            {
                File.Delete(fileName);
            }
        }

        public static long FileSize(string path)
        {
            return new FileInfo(path).Length;
        }
    }
}
