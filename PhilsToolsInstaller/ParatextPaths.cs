using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace PhilsTools
{
    /// <summary>Paratext 9 installations and projects folders, found as Install-Plugin.ps1 finds them.</summary>
    public static class ParatextPaths
    {
        /// <summary>Testing only: Paratext folders to use instead of searching (";"-separated). Also passed
        /// to Install-Plugin.ps1 as -ParatextDir with -NoElevate, which the script honours only when it
        /// does not run as administrator.</summary>
        public static string TestInstalls;

        /// <summary>Folders holding Paratext.exe and PluginInterfaces.dll (Paratext 9 or later).</summary>
        public static List<string> FindInstalls()
        {
            List<string> candidates = new List<string>();
            if (!string.IsNullOrEmpty(TestInstalls))
            {
                foreach (string d in TestInstalls.Split(';')) if (d.Trim().Length > 0) candidates.Add(d.Trim());
            }
            else
            {
                List<string> roots = new List<string>();
                foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    try
                    {
                        using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                        {
                            using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Paratext\8"))
                            {
                                if (key != null)
                                {
                                    foreach (string name in key.GetValueNames())
                                    {
                                        if (name.IndexOf("AppPath", StringComparison.OrdinalIgnoreCase) < 0 &&
                                            !name.StartsWith("Program_Files_Directory", StringComparison.OrdinalIgnoreCase)) continue;
                                        string value = key.GetValue(name) as string;
                                        if (value != null && value.Length > 3) candidates.Add(value);
                                    }
                                }
                            }
                            using (RegistryKey cv = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion"))
                            {
                                if (cv != null)
                                {
                                    foreach (string name in new[] { "ProgramFilesDir", "ProgramFilesDir (x86)", "ProgramW6432Dir" })
                                    {
                                        string value = cv.GetValue(name) as string;
                                        if (value != null && value.Length > 3) roots.Add(value);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception) { }
                }
                foreach (string pf in roots)
                {
                    try
                    {
                        if (Directory.Exists(pf)) candidates.AddRange(Directory.GetDirectories(pf, "Paratext*"));
                    }
                    catch (Exception) { }
                }
            }
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string c in candidates)
            {
                string full;
                try { full = Path.GetFullPath(c.Trim().Trim('"')).TrimEnd('\\'); }
                catch (Exception) { continue; }
                if (!seen.Add(full)) continue;
                if (File.Exists(Path.Combine(full, "Paratext.exe")) && File.Exists(Path.Combine(full, "PluginInterfaces.dll")))
                    result.Add(full);
            }
            return result;
        }

        /// <summary>Where a program that runs as administrator is installed: Program Files\Folder
        /// (in a test run, the folder Install-Plugin.ps1 uses: ProgramFiles beside the first install).</summary>
        public static string ProtectedExeFolder(string folder, List<string> installs)
        {
            if (!string.IsNullOrEmpty(TestInstalls) && installs.Count > 0)
                return Path.Combine(Path.Combine(Path.GetDirectoryName(installs[0]), "ProgramFiles"), folder);
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey cv = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion"))
                    {
                        if (cv == null) continue;
                        foreach (string name in new[] { "ProgramW6432Dir", "ProgramFilesDir" })
                        {
                            string value = cv.GetValue(name) as string;
                            if (value != null && Directory.Exists(value)) return Path.Combine(value.TrimEnd('\\'), folder);
                        }
                    }
                }
                catch (Exception) { }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), folder);
        }

        /// <summary>Paratext projects folders (Settings_Directory) from HKLM (both views), then HKCU.
        /// The cms folder is inside.</summary>
        public static List<string> ProjectsFolders()
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<RegistryKey> roots = new List<RegistryKey>();
            try { roots.Add(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)); } catch (Exception) { }
            try { roots.Add(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)); } catch (Exception) { }
            try { roots.Add(RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)); } catch (Exception) { }
            foreach (RegistryKey root in roots)
            {
                try
                {
                    using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Paratext\8"))
                    {
                        if (key == null) continue;
                        string value = key.GetValue("Settings_Directory") as string;
                        if (string.IsNullOrEmpty(value)) continue;
                        string full = Path.GetFullPath(value.Trim().Trim('"')).TrimEnd('\\');
                        if (Directory.Exists(full) && seen.Add(full)) result.Add(full);
                    }
                }
                catch (Exception) { }
                finally { root.Dispose(); }
            }
            return result;
        }

        public static bool ParatextRunning()
        {
            Process[] list = Process.GetProcessesByName("Paratext");
            bool running = list.Length > 0;
            foreach (Process p in list) p.Dispose();
            return running;
        }

        public static string Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(data);
                StringBuilder sb = new StringBuilder(64);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>The SHA-256 of a file, or null when it is missing or cannot be read.</summary>
        public static string FileSha(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return Sha256(File.ReadAllBytes(path));
            }
            catch (Exception) { return null; }
        }
    }
}
