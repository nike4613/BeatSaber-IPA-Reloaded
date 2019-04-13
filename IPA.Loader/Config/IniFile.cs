using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IPA.Config
{
    /// <summary>
    /// Create a New INI file to store or load data
    /// </summary>
    [Obsolete("Jesus, this uses old 16-bit system calls!")]
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
        /// <PARAM name="section"></PARAM>
        /// Section name
        /// <PARAM name="key"></PARAM>
        /// Key Name
        /// <PARAM name="value"></PARAM>
        /// Value Name
        public void IniWriteValue(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, IniFileInfo.FullName);
        }

        /// <summary>
        /// Read Data Value From the Ini File
        /// </summary>
        /// <PARAM name="section"></PARAM>
        /// <PARAM name="key"></PARAM>
        /// <returns></returns>
        public string IniReadValue(string section, string key)
        {
            const int maxChars = 1023;
            StringBuilder result = new StringBuilder(maxChars);
            GetPrivateProfileString(section, key, "", result, maxChars, IniFileInfo.FullName);
            return result.ToString();
        }
    }
}
