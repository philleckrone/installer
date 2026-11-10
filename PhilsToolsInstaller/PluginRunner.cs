using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace PhilsTools
{
    public enum ToolStatus { NotInstalled, Installed, UpdateAvailable, NoParatext }

    /// <summary>Installs and removes plugins by running each one's own Install-Plugin.ps1 (the same
    /// script as in its zip, with its checksums, Paratext search, trust tests and load check).</summary>
    public static class PluginRunner
    {
        public static ToolStatus Status(PluginItem p, List<string> installs)
        {
            if (installs.Count == 0) return ToolStatus.NoParatext;
            int present = 0, matching = 0, places = 0;
            List<string> folderFiles = new List<string>();
            folderFiles.Add(p.PluginFile);
            if (p.ExeFile.Length > 0 && !p.ExeProtected) folderFiles.Add(p.ExeFile);
            foreach (string install in installs)
            {
                places++;
                string dir = Path.Combine(Path.Combine(install, "plugins"), p.Folder);
                bool any = false, all = true;
                foreach (string f in folderFiles)
                {
                    string h = ParatextPaths.FileSha(Path.Combine(dir, f));
                    if (h != null) any = true;
                    if (h != p.ShaOf(p.Folder + "\\" + f)) all = false;
                }
                if (any) present++;
                if (all) matching++;
            }
            if (p.ExeProtected)
            {
                places++;
                string h = ParatextPaths.FileSha(Path.Combine(ParatextPaths.ProtectedExeFolder(p.Folder, installs), p.ExeFile));
                if (h != null) present++;
                if (h == p.ShaOf(p.Folder + "\\" + p.ExeFile)) matching++;
            }
            if (present == 0) return ToolStatus.NotInstalled;
            return matching == places ? ToolStatus.Installed : ToolStatus.UpdateAvailable;
        }

        /// <summary>Runs Install-Plugin.ps1 (with -Uninstall to remove). Returns its exit code: 0 = done.</summary>
        public static int Run(Bundle bundle, PluginItem p, bool uninstall, Action<string> log)
        {
            string dir = NewPrivateFolder();
            try
            {
                foreach (BundleFile f in p.Files)
                {
                    string dest = Path.Combine(dir, f.Target);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.WriteAllBytes(dest, bundle.Read(f));
                }
                string script = Path.Combine(dir, "Install-Plugin.ps1");
                // Passed in memory (-EncodedCommand), so no folder name can break the quoting. The
                // output is UTF-8, so paths with accents arrive intact.
                StringBuilder cmd = new StringBuilder();
                // No progress records: with stderr redirected they arrive as CLIXML.
                cmd.Append("$ProgressPreference = 'SilentlyContinue'; [Console]::OutputEncoding = [Text.Encoding]::UTF8; & ");
                cmd.Append(Quote(script));
                if (uninstall) cmd.Append(" -Uninstall");
                if (!string.IsNullOrEmpty(ParatextPaths.TestInstalls))
                    cmd.Append(" -NoElevate -ParatextDir ").Append(Quote(ParatextPaths.TestInstalls));
                cmd.Append("; exit $LASTEXITCODE");
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(cmd.ToString()));

                ProcessStartInfo psi = new ProcessStartInfo(PowerShellPath(),
                    "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.WorkingDirectory = dir;
                using (Process proc = new Process())
                {
                    proc.StartInfo = psi;
                    proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) log("  " + e.Data); };
                    proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data == null || e.Data.Trim().Length == 0) return;
                        if (e.Data.StartsWith("#< CLIXML") || e.Data.StartsWith("<Objs")) return;
                        log("  " + e.Data);
                    };
                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                    return proc.ExitCode;
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }

        static string Quote(string s)
        {
            return "'" + s.Replace("'", "''") + "'";
        }

        /// <summary>Windows PowerShell by its real path, never through PATH or the current folder.</summary>
        static string PowerShellPath()
        {
            return Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        }

        /// <summary>A new folder in the user's temp folder that only this user, Administrators (for the
        /// administrator window the script may open) and SYSTEM can open.</summary>
        static string NewPrivateFolder()
        {
            string dir = Path.Combine(Path.GetTempPath(), "PhilsTools_" + Guid.NewGuid().ToString("N"));
            DirectorySecurity sec = new DirectorySecurity();
            sec.SetAccessRuleProtection(true, false);
            InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            sec.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.CreateDirectory(dir, sec);
            return dir;
        }
    }
}
