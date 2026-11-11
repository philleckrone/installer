using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PhilsTools
{
    /// <summary>The command line, for tests: --cli status | install ID... | remove ID...
    /// IDs are plugin keys (BulkNotes). remove also takes CMS tool ids (marker_space), for old copies in
    /// the cms folder (--cmsdir, otherwise the first projects folder's): CMS tools are no longer installed,
    /// they come with the Phils Custom (cms) Checks plugin.</summary>
    static class Cli
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);

        /// <summary>Output goes to the redirected stdout when there is one, otherwise to the console
        /// of the window that started the program.</summary>
        public static void Attach()
        {
            if (Console.IsOutputRedirected) return;
            AttachConsole(-1);
        }

        public static int Run(Bundle bundle, string verb, List<string> ids, string cmsDir)
        {
            Attach();
            if (Console.IsOutputRedirected)
            {
                // A pipe or file: UTF-8, so names with accents arrive intact. (Setting
                // Console.OutputEncoding fails here, since a GUI program has no console.)
                StreamWriter w = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
                w.AutoFlush = true;
                Console.SetOut(w);
            }
            Action<string> log = delegate(string s) { Console.WriteLine(s); };
            if (cmsDir == null)
            {
                List<string> folders = ParatextPaths.ProjectsFolders();
                if (folders.Count > 0) cmsDir = Path.Combine(folders[0], "cms");
            }
            List<string> installs = ParatextPaths.FindInstalls();

            if (verb == "status")
            {
                Console.WriteLine("Paratext: " + string.Join("; ", installs.ToArray()));
                foreach (PluginItem p in bundle.Plugins)
                    // The name (in the --lang language) lets a test find the plugin's row in the
                    // window, which sorts the list by name.
                    Console.WriteLine("P\t" + p.Key + "\t" + p.Version + "\t" + PluginRunner.Status(p, installs) + "\t" + p.Name[Strings.Lang]);
                Console.WriteLine("cms: " + cmsDir);
                if (cmsDir != null && Directory.Exists(cmsDir))
                {
                    CmsInstaller cms = new CmsInstaller(bundle, cmsDir);
                    foreach (CmsItem c in bundle.Cms)
                    {
                        List<string> langs;
                        ToolStatus st = cms.Status(c, out langs);
                        Console.WriteLine("C\t" + c.Id + "\t" + st + "\t" + string.Join(",", langs.ToArray()));
                    }
                }
                return 0;
            }
            if (verb != "install" && verb != "remove")
            {
                Console.Error.WriteLine("Unknown command: " + verb);
                return 2;
            }

            bool remove = verb == "remove";
            int errors = 0;
            List<CmsItem> cmsItems = new List<CmsItem>();
            foreach (string id in ids)
            {
                PluginItem p = bundle.FindPlugin(id);
                if (p != null)
                {
                    log(Strings.F(remove ? "Removing" : "Installing", p.Name[Strings.Lang]));
                    if (PluginRunner.Run(bundle, p, remove, log) != 0) errors++;
                    continue;
                }
                CmsItem c = bundle.FindCms(id);
                if (c == null) { Console.Error.WriteLine("Unknown tool: " + id); errors++; continue; }
                if (!remove) { Console.Error.WriteLine("CMS tools are no longer installed (they come with Phils Custom (cms) Checks): " + id); errors++; continue; }
                cmsItems.Add(c);
            }
            if (cmsItems.Count > 0)
            {
                if (cmsDir == null || !Directory.Exists(cmsDir)) { Console.Error.WriteLine("No cms folder: " + cmsDir); return 1; }
                CmsInstaller cms = new CmsInstaller(bundle, cmsDir);
                errors += cms.Remove(cmsItems, log);
            }
            return errors == 0 ? 0 : 1;
        }
    }
}
