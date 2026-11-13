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

    /// <summary>A plugin's status in the Paratext installations (installing and removing is done by
    /// PluginInstaller).</summary>
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
    }
}
