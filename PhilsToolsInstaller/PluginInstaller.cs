using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace PhilsTools
{
    /// <summary>
    /// Installs and removes a plugin inside this program. It is a port of
    /// tools\plugin_installer\Install-Plugin.template.ps1, with the same rules, but it starts no
    /// PowerShell: an unsigned download that starts a hidden PowerShell with an encoded command is what
    /// malware does, and Windows Defender quarantined the installer for it (2026-11-12).
    ///
    /// The rules:
    /// - Every Paratext 9 found is used (ParatextPaths.Candidates). A folder counts only if it holds
    ///   Paratext.exe and PluginInterfaces.dll AND only administrators can change it (owner and
    ///   write permissions), so an administrator step is never steered into a folder a user controls.
    /// - Paratext gives every user Full Control of its plugins folder. Such a "shared" folder is
    ///   written by this normal process; nothing running as administrator writes there (a link swapped
    ///   in at the right moment could redirect the write). Folders only administrators can change (the
    ///   plugins folder of Paratext 9 Beta, Program Files\Folder for a program that runs as
    ///   administrator) are written by a second copy of this program started with "Run as
    ///   administrator" (--elevated), which also runs Paratext's UpdateParatextPluginCache.exe there.
    /// - The bytes installed are the bytes checked against the bundle's SHA-256 (Bundle.Read). Files are
    ///   written as new files (an old entry is deleted first: it could be a link), carry no "from the
    ///   internet" mark, and are removed one by one, never with a recursive delete.
    /// - Afterwards every installed copy is hashed again and loaded as Paratext loads it, in a separate
    ///   process (--loadcheck), from a private copy.
    /// - Test folders (--paratext) are used only when this process is not an administrator, and then
    ///   everything runs here, without asking for administrator rights.
    /// </summary>
    public static class PluginInstaller
    {
        /// <summary>Installs or removes the plugin from the normal window. Returns 0 when all went well.</summary>
        public static int Run(Bundle bundle, PluginItem p, bool uninstall, Action<string> log)
        {
            return new Job(bundle, p, uninstall, false, log).Run();
        }

        /// <summary>The administrator step (Install_Phils_tools.exe --elevated install|remove KEY): only
        /// the folders that ordinary users cannot change. Problems are shown in a message box, since this
        /// process has no window of its own.</summary>
        public static int RunElevated(Bundle bundle, string verb, string key)
        {
            StringBuilder messages = new StringBuilder();
            Action<string> log = delegate(string s) { messages.AppendLine(s); };
            int code;
            PluginItem p = bundle.FindPlugin(key);
            if (p == null || (verb != "install" && verb != "remove"))
            {
                log("Unknown request: " + verb + " " + key);
                code = 2;
            }
            else code = new Job(bundle, p, verb == "remove", true, log).Run();
            if (code != 0)
                System.Windows.Forms.MessageBox.Show(messages.ToString(), Strings.T("Caption"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return code;
        }

        // -- One install or removal ------------------------------------------------------------

        sealed class Job
        {
            readonly Bundle bundle;
            readonly PluginItem p;
            readonly bool uninstall, elevated;
            readonly Action<string> log;
            readonly bool isAdmin, useGivenFolders;
            readonly List<string> folderFiles = new List<string>();   // what goes into plugins\Folder
            readonly List<string> packageFiles = new List<string>();  // plugin file + any exe
            readonly Dictionary<string, BundleFile> files = new Dictionary<string, BundleFile>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, byte[]> bytes;
            List<string> installs;
            string protectedExeDir;

            public Job(Bundle bundle, PluginItem p, bool uninstall, bool elevated, Action<string> log)
            {
                this.bundle = bundle;
                this.p = p;
                this.uninstall = uninstall;
                this.elevated = elevated;
                this.log = log;
                isAdmin = IsAdmin();
                // Folders given for a test are trusted only WITHOUT administrator rights: an
                // administrator run must never be steered into a folder a non-administrator controls.
                useGivenFolders = !string.IsNullOrEmpty(ParatextPaths.TestInstalls) && !isAdmin && !elevated;
                packageFiles.Add(p.PluginFile);
                folderFiles.Add(p.PluginFile);
                if (p.ExeFile.Length > 0)
                {
                    packageFiles.Add(p.ExeFile);
                    if (!p.ExeProtected) folderFiles.Add(p.ExeFile);
                }
                foreach (BundleFile f in p.Files)
                {
                    string prefix = p.Folder + "\\";
                    if (f.Target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        files[f.Target.Substring(prefix.Length)] = f;
                }
            }

            public int Run()
            {
                try
                {
                    if (!string.IsNullOrEmpty(ParatextPaths.TestInstalls) && (isAdmin || elevated))
                        throw new InvalidOperationException("Test folders (--paratext) cannot be used by a program running as administrator. Nothing was changed.");
                    installs = FindInstalls();
                    if (installs.Count == 0)
                        throw new InvalidOperationException("No Paratext 9 installation was found (looked in the Paratext registry entries and in Program Files). Reinstalling Paratext 9 records its folder again.");
                    if (p.ExeProtected)
                        protectedExeDir = useGivenFolders
                            ? Path.Combine(Path.Combine(Path.GetDirectoryName(installs[0]), "ProgramFiles"), p.Folder)
                            : Path.Combine(ProgramFilesFolder(), p.Folder);
                    List<string> shared = new List<string>(), adminOnly = new List<string>();
                    foreach (string i in installs) (IsSharedPluginsFolder(i) ? shared : adminOnly).Add(i);
                    if (!elevated)
                    {
                        log("  Paratext installations found:");
                        foreach (string i in installs) log("    " + i);
                    }
                    AssertParatextClosed();
                    bytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                    if (!uninstall)
                    {
                        // Only the listed package files, each checked against its checksum (Bundle.Read).
                        foreach (string f in packageFiles)
                        {
                            BundleFile bf;
                            if (!files.TryGetValue(f, out bf)) throw new InvalidOperationException("The installer is incomplete: " + f + " is missing.");
                            bytes[f] = bundle.Read(bf);
                        }
                    }

                    if (elevated)
                    {
                        // The administrator step: only the folders ordinary users cannot change, then
                        // Paratext's plugin-list tool (its manifest requires administrator rights).
                        if (uninstall) { RemovePluginFiles(adminOnly); if (p.ExeProtected) RemoveProtectedExe(); }
                        else { InstallPluginFiles(adminOnly); if (p.ExeProtected) InstallProtectedExe(); }
                        foreach (string i in adminOnly) UpdatePluginCache(i);
                        return 0;
                    }
                    if (isAdmin || useGivenFolders)
                    {
                        // Already an administrator (or a test run): everything here. An administrator
                        // must not write into a plugins folder that ordinary users control.
                        if (isAdmin && shared.Count > 0)
                            throw new InvalidOperationException("This program runs as administrator, and every user can change the plugins folder of " +
                                string.Join(", ", shared.ToArray()) + ", so the plugin must be copied there without administrator rights. " +
                                "Close the installer and start it again normally (not with \"Run as administrator\"); it asks for administrator rights only where they are needed. Nothing was changed.");
                        if (uninstall)
                        {
                            RemovePluginFiles(installs);
                            if (p.ExeProtected) RemoveProtectedExe();
                            foreach (string i in adminOnly) UpdatePluginCache(i);
                            return 0;
                        }
                        InstallPluginFiles(installs);
                        if (p.ExeProtected) InstallProtectedExe();
                        foreach (string i in adminOnly) UpdatePluginCache(i);
                        return CompleteInstall();
                    }
                    if (adminOnly.Count == 0 && !p.ExeProtected)
                    {
                        // Only shared plugins folders: no administrator rights needed at all.
                        if (uninstall)
                        {
                            RemovePluginFiles(shared);
                            log("  Removed from " + installs.Count + " Paratext installation(s).");
                            return 0;
                        }
                        InstallPluginFiles(shared);
                        return CompleteInstall();
                    }
                    AssertNotMappedDrive();
                    // 1. This (normal) process: the shared plugins folders.
                    if (uninstall) RemovePluginFiles(shared); else InstallPluginFiles(shared);
                    // 2. A copy of this program as administrator: the folders only administrators can change.
                    log("  Administrator rights are needed for folders that only administrators can change. Asking Windows...");
                    RunAsAdministrator();
                    log("  The administrator step finished.");
                    // 3. Here again: checksums of what is installed, then the load check.
                    if (uninstall)
                    {
                        log("  Removed from " + installs.Count + " Paratext installation(s).");
                        return 0;
                    }
                    return CompleteInstall();
                }
                catch (Exception ex)
                {
                    log("  " + ex.Message);
                    return 1;
                }
            }

            // -- Finding Paratext ------------------------------------------------------------------

            List<string> FindInstalls()
            {
                List<string> result = new List<string>();
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string c in ParatextPaths.Candidates(useGivenFolders))
                {
                    string full = ParatextPaths.LongPath(c);
                    if (full == null || !seen.Add(full)) continue;
                    if (!ParatextPaths.IsParatext9(full)) continue;
                    if (!useGivenFolders && !IsAdminOnlyFolder(full))
                    {
                        if (!elevated) log("  Skipped " + full + ": ordinary users can change this folder, so it is not treated as a Paratext installation.");
                        continue;
                    }
                    result.Add(full);
                }
                return result;
            }

            /// <summary>Paratext's own installer gives every user Full Control of plugins: such an install
            /// is written from the normal process. One whose plugins folder only administrators can change
            /// (or that has none yet) is handled as administrator.</summary>
            static bool IsSharedPluginsFolder(string install)
            {
                string plugins = Path.Combine(install, "plugins");
                if (!Directory.Exists(plugins)) return false;
                return !IsAdminOnlyFolder(plugins);
            }

            void AssertParatextClosed()
            {
                List<string> running = new List<string>();
                foreach (Process proc in Process.GetProcessesByName("Paratext"))
                {
                    try
                    {
                        string path = ProcessPath(proc.Id);
                        if (string.IsNullOrEmpty(path)) { running.Add("(a Paratext window)"); continue; }
                        string dir = ParatextPaths.LongPath(Path.GetDirectoryName(path));
                        foreach (string i in installs)
                            if (string.Equals(dir, i, StringComparison.OrdinalIgnoreCase) && !running.Contains(i)) running.Add(i);
                    }
                    finally { proc.Dispose(); }
                }
                if (running.Count > 0)
                    throw new InvalidOperationException("Paratext is running (" + string.Join(", ", running.ToArray()) + "). Close Paratext, then run this again.");
            }

            /// <summary>A mapped network drive (Z:) is not visible to the administrator copy that Windows
            /// starts, so it would fail to start there.</summary>
            static void AssertNotMappedDrive()
            {
                string root = Path.GetPathRoot(ExePath());
                if (root != null && root.Length == 3 && root[1] == ':')
                {
                    DriveInfo d = new DriveInfo(root);
                    if (d.DriveType == DriveType.Network)
                        throw new InvalidOperationException("The installer is on the network drive " + root + ". Copy it to a folder on this computer (for example Downloads), then run it again from there.");
                }
            }

            // -- Writing and removing ----------------------------------------------------------------

            void InstallPluginFiles(List<string> list)
            {
                foreach (string i in list)
                {
                    log("  " + i);
                    string plugins = Path.Combine(i, "plugins");
                    string target = Path.Combine(plugins, p.Folder);
                    foreach (string d in new[] { i, plugins, target }) AssertNotReparsePoint(d);
                    if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                    foreach (string f in folderFiles) WriteOwnFile(target, f, bytes[f]);
                    foreach (string f in p.OldFiles)
                        if (RemoveOwnFile(target, f)) log("    Removed " + f + ", left by an older version.");
                    // A protected program left in the plugins folder by an older version is removed.
                    if (p.ExeProtected && RemoveOwnFile(target, p.ExeFile)) log("    Removed the old copy of " + p.ExeFile + " from the plugins folder.");
                }
            }

            /// <summary>Only this plugin's own files are deleted, one by one: never a recursive delete.</summary>
            void RemovePluginFiles(List<string> list)
            {
                foreach (string i in list)
                {
                    string plugins = Path.Combine(i, "plugins");
                    string target = Path.Combine(plugins, p.Folder);
                    if (!Directory.Exists(target)) { log("  Not installed in " + i); continue; }
                    foreach (string d in new[] { i, plugins, target }) AssertNotReparsePoint(d);
                    foreach (string f in packageFiles) RemoveOwnFile(target, f);
                    foreach (string f in p.OldFiles) RemoveOwnFile(target, f);
                    string[] left = Directory.GetFileSystemEntries(target);
                    if (left.Length == 0)
                    {
                        AssertNotReparsePoint(target);
                        Directory.Delete(target, false);
                        log("  Removed from " + i);
                    }
                    else
                    {
                        List<string> names = new List<string>();
                        foreach (string l in left) names.Add(Path.GetFileName(l));
                        log("  Removed from " + i + " (the folder " + target + " was kept: it also holds " + string.Join(", ", names.ToArray()) + ")");
                    }
                }
            }

            /// <summary>The protected program's folder must be one that only administrators can change, under
            /// a parent only administrators can change (Program Files). Test runs use a scratch folder.</summary>
            void InstallProtectedExe()
            {
                string parent = Path.GetDirectoryName(protectedExeDir);
                if (!useGivenFolders && !IsAdminOnlyFolder(parent))
                    throw new InvalidOperationException(parent + " can be changed by ordinary users; " + p.ExeFile + " is not installed there.");
                AssertNotReparsePoint(protectedExeDir);
                if (!Directory.Exists(protectedExeDir)) Directory.CreateDirectory(protectedExeDir);
                if (!useGivenFolders && !IsAdminOnlyFolder(protectedExeDir))
                    throw new InvalidOperationException(protectedExeDir + " can be changed by ordinary users; " + p.ExeFile + " is not installed there.");
                log("  " + protectedExeDir);
                WriteOwnFile(protectedExeDir, p.ExeFile, bytes[p.ExeFile]);
            }

            void RemoveProtectedExe()
            {
                if (!Directory.Exists(protectedExeDir)) return;
                RemoveOwnFile(protectedExeDir, p.ExeFile);
                if (Directory.GetFileSystemEntries(protectedExeDir).Length == 0)
                {
                    AssertNotReparsePoint(protectedExeDir);
                    Directory.Delete(protectedExeDir, false);
                }
                log("  Removed " + protectedExeDir);
            }

            /// <summary>Writes checked bytes as a NEW file: an existing entry is deleted first, since it
            /// could be a link or a hard link to some other file. The new file carries no "from the
            /// internet" mark, which Paratext would refuse.</summary>
            void WriteOwnFile(string folder, string name, byte[] data)
            {
                RemoveOwnFile(folder, name);
                AssertNotReparsePoint(folder);
                string dest = Path.Combine(folder, name);
                using (FileStream fs = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    fs.Write(data, 0, data.Length);
                if (HasInternetMark(dest)) DeleteFile(dest + ":Zone.Identifier");
                if (HasInternetMark(dest)) throw new InvalidOperationException("Could not remove the 'from the internet' mark from " + dest);
                log("    Installed " + dest);
            }

            /// <summary>Deletes a file of this plugin (link-checked). True when there was one.</summary>
            static bool RemoveOwnFile(string folder, string name)
            {
                string file = Path.Combine(folder, name);
                if (!File.Exists(file) && !Directory.Exists(file)) return false;
                AssertNotReparsePoint(folder);
                AssertNotReparsePoint(file);
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                return true;
            }

            /// <summary>Paratext keeps a plugin cache (plugins\AddIns.store); its own tool rebuilds it. Its
            /// output lists "Could not find a valid add-in" for every plugin folder, Paratext's own included,
            /// so it is shown only when the tool fails. Only as administrator (its manifest requires it), and
            /// only for plugins folders ordinary users cannot change: it rewrites AddIns.store, which a user
            /// could redirect with a link. Paratext 9 does not need it. A failure never stops the install.</summary>
            void UpdatePluginCache(string paratext)
            {
                string updater = Path.Combine(paratext, "UpdateParatextPluginCache.exe");
                if (!File.Exists(updater)) return;
                if (!isAdmin) { log("  Paratext's plugin list was not updated (that needs administrator rights)."); return; }
                ProcessStartInfo psi = new ProcessStartInfo(updater);
                psi.WorkingDirectory = paratext;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                try
                {
                    using (Process proc = Process.Start(psi))
                    {
                        System.Threading.Tasks.Task<string> err = proc.StandardError.ReadToEndAsync();
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit();
                        if (proc.ExitCode != 0)
                        {
                            log("  UpdateParatextPluginCache.exe reported exit code " + proc.ExitCode + ":");
                            foreach (string text in new[] { output, err.Result })
                                foreach (string l in text.Split('\n')) if (l.Trim().Length > 0) log("    " + l.TrimEnd('\r'));
                        }
                        else log("  " + paratext + ": updated Paratext's plugin list.");
                    }
                }
                catch (Exception ex) { log("  Could not update Paratext's plugin list: " + ex.Message); }
            }

            // -- The administrator step ----------------------------------------------------------------

            void RunAsAdministrator()
            {
                ProcessStartInfo psi = new ProcessStartInfo(ExePath(),
                    "--elevated " + (uninstall ? "remove" : "install") + " " + p.Key + " --lang " + new[] { "en", "es", "fr" }[Strings.Lang]);
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Path.GetDirectoryName(ExePath());
                Process proc;
                try { proc = Process.Start(psi); }
                catch (Win32Exception)
                {
                    if (uninstall) throw new InvalidOperationException("Administrator permission was not given: the plugin was removed from the shared plugins folder(s), but not from the folders that only administrators can change. Remove it again.");
                    throw new InvalidOperationException("Administrator permission was not given: the plugin is not completely installed. Install it again.");
                }
                using (proc)
                {
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                        throw new InvalidOperationException("The administrator step reported a problem (see its message); the plugin may not be completely installed or removed.");
                }
            }

            // -- After the files are in place: checksums and the load check ----------------------------

            bool InstalledFileOk(string path, string name)
            {
                if (!File.Exists(path)) { log("    " + name + " was not installed."); return false; }
                if (ParatextPaths.FileSha(path) != files[name].Sha) { log("    The installed " + name + " differs from the package."); return false; }
                return true;
            }

            int CompleteInstall()
            {
                int failed = 0;
                log("  Checking:");
                if (p.ExeProtected)
                {
                    log("  " + protectedExeDir);
                    if (InstalledFileOk(Path.Combine(protectedExeDir, p.ExeFile), p.ExeFile)) log("    " + p.ExeFile + " is in place.");
                    else failed++;
                }
                foreach (string i in installs)
                {
                    log("  " + i);
                    string target = Path.Combine(Path.Combine(i, "plugins"), p.Folder);
                    bool ok = true;
                    foreach (string f in folderFiles) if (!InstalledFileOk(Path.Combine(target, f), f)) ok = false;
                    if (!ok) { failed++; continue; }
                    string result = LoadCheck(bytes[p.PluginFile], i, isAdmin);
                    if (result.StartsWith("OK ")) log("    Paratext can load it (" + result.Substring(3) + ")");
                    else { log("    " + result.Substring(5)); failed++; }
                }
                if (failed == 0)
                {
                    log("  Installed in " + installs.Count + " Paratext installation(s).");
                    return 0;
                }
                log("  Something is missing or wrong in " + failed + " place(s): see the messages above.");
                return 1;
            }
        }

        // -- The load check --------------------------------------------------------------------------

        /// <summary>Loads a private copy of the plugin as Paratext's plugin manager does, in a separate
        /// process (this program with --loadcheck), so the loaded files are not kept open here. Returns
        /// "OK types" or "FAIL reason".</summary>
        static string LoadCheck(byte[] pluginBytes, string paratext, bool isAdmin)
        {
            string dir = null;
            try
            {
                dir = NewPrivateFolder(isAdmin);
                string copy = Path.Combine(dir, "plugin.dll");
                File.WriteAllBytes(copy, pluginBytes);
                ProcessStartInfo psi = new ProcessStartInfo(ExePath(), "--loadcheck " + B64(copy) + " " + B64(paratext));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.WorkingDirectory = dir;
                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    if (!proc.WaitForExit(120000)) { try { proc.Kill(); } catch (Exception) { } return "FAIL The check did not finish."; }
                    string line = null;
                    foreach (string l in output.Split('\n'))
                    {
                        string t = l.TrimEnd('\r');
                        if (t.StartsWith("OK ") || t.StartsWith("FAIL ")) line = t;
                    }
                    return line ?? ("FAIL The check did not run: " + output.Trim());
                }
            }
            catch (Exception ex) { return "FAIL The check did not run: " + ex.Message; }
            finally
            {
                if (dir != null) try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }

        /// <summary>The child side of the load check (Install_Phils_tools.exe --loadcheck PLUGIN PARATEXT,
        /// both base64 UTF-8): the same calls as Paratext's plugin manager. Writes one line.</summary>
        public static int LoadCheckMain(string pluginB64, string paratextB64)
        {
            string result;
            try
            {
                string plugin = Encoding.UTF8.GetString(Convert.FromBase64String(pluginB64));
                string paratext = Encoding.UTF8.GetString(Convert.FromBase64String(paratextB64));
                result = CheckLoads(plugin, paratext);
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                result = "FAIL " + ex.GetType().Name + ": " + ex.Message;
            }
            Console.Out.WriteLine(result);
            Console.Out.Flush();
            return 0;
        }

        static string CheckLoads(string plugin, string paratext)
        {
            Version installed = AssemblyName.GetAssemblyName(Path.Combine(paratext, "PluginInterfaces.dll")).Version;
            foreach (AssemblyName r in Assembly.ReflectionOnlyLoadFrom(plugin).GetReferencedAssemblies())
                if (r.Name == "PluginInterfaces" && r.Version > installed)
                    return "FAIL This Paratext is too old for the plugin: it needs PluginInterfaces " + r.Version + ", this Paratext has " + installed + ".";
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
            {
                string path = Path.Combine(paratext, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            // The same two calls Paratext's plugin manager makes.
            Assembly asm = Assembly.LoadFrom(plugin);
            List<string> found = new List<string>();
            foreach (Type t in asm.GetExportedTypes())
            {
                if (t.IsAbstract || t.IsInterface || t.GetConstructor(Type.EmptyTypes) == null) continue;
                foreach (Type i in t.GetInterfaces())
                    if (i.Name == "IParatextPlugin") { found.Add(t.FullName); break; }
            }
            if (found.Count == 0) return "FAIL The file loads, but contains no Paratext plugin class.";
            return "OK " + string.Join(", ", found.ToArray());
        }

        // -- Helpers ---------------------------------------------------------------------------------

        /// <summary>The path of this program (the copy that is running).</summary>
        static string ExePath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        static string B64(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        }

        public static bool IsAdmin()
        {
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Administrators, SYSTEM and TrustedInstaller.
        static readonly string[] TrustedSids = { "S-1-5-32-544", "S-1-5-18", "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464" };

        static bool Trusted(string sid)
        {
            return Array.IndexOf(TrustedSids, sid) >= 0;
        }

        /// <summary>True when only administrators can change the folder: owned by a trusted account, no
        /// write-type permission for anyone else, and not a link. A Paratext folder under Program Files
        /// passes; a folder ordinary users can write to (the projects folder, or a Paratext installed in
        /// C:\Paratext9) does not.</summary>
        public static bool IsAdminOnlyFolder(string path)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(path);
                if (!di.Exists || (di.Attributes & FileAttributes.ReparsePoint) != 0) return false;
                DirectorySecurity ds = di.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
                IdentityReference owner = ds.GetOwner(typeof(SecurityIdentifier));
                if (owner == null || !Trusted(owner.Value)) return false;
                // WriteData, AppendData, DeleteSubdirectoriesAndFiles, Delete, ChangePermissions,
                // TakeOwnership, GenericAll, GenericWrite.
                long writeMask = 0x2 | 0x4 | 0x40 | 0x10000 | 0x40000 | 0x80000 | 0x10000000 | 0x40000000;
                foreach (FileSystemAccessRule r in ds.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (r.AccessControlType != AccessControlType.Allow) continue;
                    if ((r.PropagationFlags & PropagationFlags.InheritOnly) != 0) continue;
                    if (((long)(int)r.FileSystemRights & writeMask) == 0) continue;
                    if (!Trusted(r.IdentityReference.Value)) return false;
                }
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Junctions and symbolic links could redirect a copy or a delete elsewhere.</summary>
        static void AssertNotReparsePoint(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Refusing to change " + path + ": it is a link (junction or symbolic link) to another place.");
        }

        /// <summary>Program Files from the machine registry (the 64-bit folder on 64-bit Windows), not from
        /// environment variables, which a user can set for their own account.</summary>
        static string ProgramFilesFolder()
        {
            foreach (Microsoft.Win32.RegistryView view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
            {
                try
                {
                    using (Microsoft.Win32.RegistryKey root = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view))
                    using (Microsoft.Win32.RegistryKey cv = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion"))
                    {
                        if (cv == null) continue;
                        foreach (string name in new[] { "ProgramW6432Dir", "ProgramFilesDir" })
                        {
                            string value = cv.GetValue(name) as string;
                            if (value != null && Directory.Exists(value)) return value.TrimEnd('\\');
                        }
                    }
                }
                catch (Exception) { }
            }
            throw new InvalidOperationException("The Program Files folder was not found in the registry.");
        }

        /// <summary>A new private folder for the load check. As administrator it is in the Windows temp
        /// folder with permissions for Administrators and SYSTEM only, so nobody else can swap the file
        /// the (administrator) check loads; otherwise in the user's temp folder, for this user,
        /// Administrators and SYSTEM.</summary>
        static string NewPrivateFolder(bool isAdmin)
        {
            string name = "PluginCheck_" + Guid.NewGuid().ToString("N");
            InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            DirectorySecurity sec = new DirectorySecurity();
            sec.SetAccessRuleProtection(true, false);
            sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            string dir;
            if (isAdmin) dir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), name);
            else
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                    sec.AddAccessRule(new FileSystemAccessRule(id.User, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
                dir = Path.Combine(Path.GetTempPath(), name);
            }
            Directory.CreateDirectory(dir, sec);
            return dir;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFileAttributes(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool DeleteFile(string path);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        /// <summary>True when the file carries the "from the internet" mark (a Zone.Identifier stream).</summary>
        static bool HasInternetMark(string path)
        {
            return GetFileAttributes(path + ":Zone.Identifier") != 0xFFFFFFFF;
        }

        /// <summary>The program file of a process, also of a 64-bit process seen from this 32-bit one;
        /// null when it cannot be read.</summary>
        static string ProcessPath(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return null;
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
            }
            finally { CloseHandle(h); }
        }
    }
}
