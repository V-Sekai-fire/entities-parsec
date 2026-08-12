using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WallpaperParsec
{
    /// <summary>
    /// Hand-rolled key=value file rather than a serializer, so the app has no dependency
    /// beyond what ships in the .NET Framework on every Windows 11 machine.
    /// </summary>
    internal sealed class Settings
    {
        public const string AllMonitors = "*";

        public string ProcessName = "parsecd";
        public string LaunchPath = "";
        public string Monitor = AllMonitors;
        public bool AutoAttach = false;
        public bool KeepAttached = true;

        public static string Directory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WallpaperParsec");
            }
        }

        private static string FilePath
        {
            get { return Path.Combine(Directory, "settings.ini"); }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath))
                    return s;

                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#')
                        continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    string key = trimmed.Substring(0, eq).Trim();
                    string value = trimmed.Substring(eq + 1).Trim();

                    switch (key)
                    {
                        case "ProcessName": s.ProcessName = value; break;
                        case "LaunchPath": s.LaunchPath = value; break;
                        case "Monitor": s.Monitor = value; break;
                        case "AutoAttach": s.AutoAttach = ParseBool(value); break;
                        case "KeepAttached": s.KeepAttached = ParseBool(value); break;
                    }
                }
            }
            catch (IOException)
            {
                // A corrupt or locked settings file is not worth failing startup over.
            }
            return s;
        }

        public void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                List<string> lines = new List<string>();
                lines.Add("# WallpaperParsec settings");
                lines.Add("ProcessName=" + ProcessName);
                lines.Add("LaunchPath=" + LaunchPath);
                lines.Add("Monitor=" + Monitor);
                lines.Add("AutoAttach=" + AutoAttach.ToString(CultureInfo.InvariantCulture));
                lines.Add("KeepAttached=" + KeepAttached.ToString(CultureInfo.InvariantCulture));
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (IOException)
            {
            }
        }

        private static bool ParseBool(string value)
        {
            bool result;
            return bool.TryParse(value, out result) && result;
        }
    }
}
