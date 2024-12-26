using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PretestPosttestLib_CSharp
{
    internal class Iniconfig
    {
        string path;

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32")]
        private static extern bool WritePrivateProfileStructA(string section, string key, string val, int a, string filePath);


        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string defVal, byte[] retVal, int size, string filePath);

        public void INIFile(string INIPath)
        {
            path = INIPath;
        }

        public void IniWriteValue(string Section, string Key, string Value)
        {
            WritePrivateProfileString(Section, Key, Value, path);
        }

        public string IniReadValue(string Section, string Key)
        {
            StringBuilder stringBuilder = new StringBuilder(255);
            GetPrivateProfileString(Section, Key, "", stringBuilder, 255, path);
            return stringBuilder.ToString();
        }

        public byte[] IniReadValues(string section, string key)
        {
            byte[] array = new byte[255];
            GetPrivateProfileString(section, key, "", array, 255, path);
            return array;
        }

        public void IniDeletekey(string Section, string Key)
        {
            Console.WriteLine(Key);
            WritePrivateProfileStructA(Section, Key, null, 255, path);
        }
        public string[] IniReadKeys(string Section)
        {
            byte[] array = new byte[255];
            GetPrivateProfileString(Section, null, "", array, 255, path);
            string key = System.Text.Encoding.Default.GetString(array, 0, array.Length);
            string[] keys = key.Split((char)0);
            return keys;
        }

    }
}
