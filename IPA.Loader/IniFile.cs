using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IPA
{
    /// <summary>
    /// Create a New INI file to store or load data
    /// </summary>
    internal class IniFile
    {
        [DllImport("KERNEL32.DLL", EntryPoint = "GetPrivateProfileStringW",
        SetLastError = true,
        CharSet = CharSet.Unicode, ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall)]
        private static extern int GetPrivateProfileString(
          string lpSection,
          string lpKey,
          string lpDefault,
          StringBuilder lpReturnString,
          int nSize,
          string lpFileName);

        [DllImport("KERNEL32.DLL", EntryPoint = "WritePrivateProfileStringW",
          SetLastError = true,
          CharSet = CharSet.Unicode, ExactSpelling = true,
          CallingConvention = CallingConvention.StdCall)]
        private static extern int WritePrivateProfileString(
          string lpSection,
          string lpKey,
          string lpValue,
          string lpFileName);

        /*private string _path = "";
        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                if (!File.Exists(value))
                    File.WriteAllText(value, "", Encoding.Unicode);
                _path = value;
            }
        }*/

        private FileInfo _iniFileInfo;
        public FileInfo IniFileInfo {
            get => _iniFileInfo;
            set { 
                _iniFileInfo = value;
                if (_iniFileInfo.Exists) return;
                _iniFileInfo.Directory?.Create();
                _iniFileInfo.Create();
            }
        }

        /// <summary>
        /// INIFile Constructor.
        /// </summary>
        /// <PARAM name="iniPath"></PARAM>
        public IniFile(string iniPath)
        {
            IniFileInfo = new FileInfo(iniPath);
            //this.Path = INIPath;
        }

        /// <summary>
        /// Write Data to the INI File
        /// </summary>
        /// <PARAM name="Section"></PARAM>
        /// Section name
        /// <PARAM name="Key"></PARAM>
        /// Key Name
        /// <PARAM name="Value"></PARAM>
        /// Value Name
        public void IniWriteValue(string Section, string Key, string Value)
        {
            WritePrivateProfileString(Section, Key, Value, IniFileInfo.FullName);
        }

        /// <summary>
        /// Read Data Value From the Ini File
        /// </summary>
        /// <PARAM name="Section"></PARAM>
        /// <PARAM name="Key"></PARAM>
        /// <returns></returns>
        public string IniReadValue(string Section, string Key)
        {
            const int MAX_CHARS = 1023;
            StringBuilder result = new StringBuilder(MAX_CHARS);
            GetPrivateProfileString(Section, Key, "", result, MAX_CHARS, IniFileInfo.FullName);
            return result.ToString();
        }
    }
}
