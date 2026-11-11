using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PhilsTools
{
    /// <summary>Finds and removes the CMS tools that earlier versions of this installer copied into a
    /// Paratext cms folder: the checks now come with the Phils Custom (cms) Checks plugin, so there is no
    /// install any more. The folder is shared with other people's tools, so only files this installer
    /// knows are ever deleted: a file whose content matches no version in the bundle and no version the
    /// installer wrote (it was changed by hand) is kept as a .bak copy.</summary>
    public class CmsInstaller
    {
        /// <summary>Name and SHA-256 of every file this installer wrote, kept in the cms folder.</summary>
        public const string RecordFile = "PhilsTools_installed.txt";

        readonly Bundle bundle;
        readonly string dir;
        readonly Dictionary<string, string> record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Target name -> every SHA-256 that name has in the bundle (any tool, any language).</summary>
        readonly Dictionary<string, HashSet<string>> known = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public CmsInstaller(Bundle bundle, string cmsDir)
        {
            this.bundle = bundle;
            dir = cmsDir;
            foreach (CmsItem c in bundle.Cms)
                foreach (List<BundleFile> list in c.Files.Values)
                    foreach (BundleFile f in list) Know(f);
            foreach (List<BundleFile> list in bundle.Shared.Values)
                foreach (BundleFile f in list) Know(f);
            ReadRecord();
        }

        void Know(BundleFile f)
        {
            HashSet<string> set;
            if (!known.TryGetValue(f.Target, out set)) { set = new HashSet<string>(); known[f.Target] = set; }
            set.Add(f.Sha);
        }

        void ReadRecord()
        {
            string path = Path.Combine(dir, RecordFile);
            if (!File.Exists(path)) return;
            try
            {
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int tab = line.IndexOf('\t');
                    if (tab > 0) record[line.Substring(0, tab)] = line.Substring(tab + 1).Trim();
                }
            }
            catch (Exception) { }
        }

        void WriteRecord()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Files installed by Install_Phils_tools.exe (name, SHA-256). Used to tell them from files changed by hand.");
            List<string> names = new List<string>(record.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string n in names) sb.Append(n).Append('\t').Append(record[n]).AppendLine();
            File.WriteAllText(Path.Combine(dir, RecordFile), sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>Tools never offered for removal: Project Checker is not in the plugin (the user's
        /// choice), so whoever installed its launcher keeps it.</summary>
        public static readonly string[] Kept = { "project_checker" };

        /// <summary>The bundle's CMS tools found in this folder (any language), minus <see cref="Kept"/>.</summary>
        public List<CmsItem> OldCopies()
        {
            List<CmsItem> found = new List<CmsItem>();
            foreach (CmsItem c in bundle.Cms)
                if (Array.IndexOf(Kept, c.Id) < 0 && InstalledLanguages(c).Count > 0) found.Add(c);
            return found;
        }

        /// <summary>The cms folder of every Paratext projects folder, with the old copies in it (only folders that have some).</summary>
        public static List<KeyValuePair<string, List<CmsItem>>> FindOldCopies(Bundle bundle, string onlyDir)
        {
            List<KeyValuePair<string, List<CmsItem>>> result = new List<KeyValuePair<string, List<CmsItem>>>();
            List<string> dirs = new List<string>();
            if (!string.IsNullOrEmpty(onlyDir)) dirs.Add(onlyDir);
            else foreach (string folder in ParatextPaths.ProjectsFolders()) dirs.Add(Path.Combine(folder, "cms"));
            foreach (string d in dirs)
            {
                try
                {
                    if (!Directory.Exists(d)) continue;
                    List<CmsItem> found = new CmsInstaller(bundle, d).OldCopies();
                    if (found.Count > 0) result.Add(new KeyValuePair<string, List<CmsItem>>(d, found));
                }
                catch (Exception) { }
            }
            return result;
        }

        /// <summary>Languages whose .cms is in the folder.</summary>
        public List<string> InstalledLanguages(CmsItem c)
        {
            List<string> result = new List<string>();
            foreach (string l in c.Languages)
                if (File.Exists(Path.Combine(dir, c.Files[l][0].Target))) result.Add(l);
            return result;
        }

        public ToolStatus Status(CmsItem c, out List<string> languages)
        {
            languages = InstalledLanguages(c);
            if (languages.Count == 0) return ToolStatus.NotInstalled;
            // A file name used by two installed languages with different content (the two
            // project_BT_reference_tool.py) can hold only one of them: either one counts as current.
            Dictionary<string, HashSet<string>> ok = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in languages)
                foreach (BundleFile f in FilesFor(c, l))
                {
                    HashSet<string> set;
                    if (!ok.TryGetValue(f.Target, out set)) { set = new HashSet<string>(); ok[f.Target] = set; }
                    set.Add(f.Sha);
                }
            foreach (KeyValuePair<string, HashSet<string>> kv in ok)
            {
                string h = ParatextPaths.FileSha(Path.Combine(dir, kv.Key));
                if (h == null || !kv.Value.Contains(h)) return ToolStatus.UpdateAvailable;
            }
            return ToolStatus.Installed;
        }

        /// <summary>A tool's files in one language, plus the shared files it needs (not other tools).</summary>
        List<BundleFile> FilesFor(CmsItem c, string lang)
        {
            List<BundleFile> list = new List<BundleFile>(c.Files[lang]);
            foreach (string n in c.Needs)
            {
                List<BundleFile> shared;
                if (bundle.Shared.TryGetValue(n, out shared)) list.AddRange(shared);
            }
            return list;
        }

        void AddToPlan(List<KeyValuePair<CmsItem, string>> plan, HashSet<string> seen, CmsItem c, string lang)
        {
            if (!seen.Add(c.Id + "|" + lang)) return;
            plan.Add(new KeyValuePair<CmsItem, string>(c, lang));
            foreach (string n in c.Needs)
            {
                CmsItem dep = bundle.FindCms(n);
                if (dep != null) AddToPlan(plan, seen, dep, dep.LanguageFor("en"));
            }
        }

        /// <summary>True when a file's content is a version from the bundle or one this installer wrote.</summary>
        bool IsOurs(string target, string sha)
        {
            if (sha == null) return false;
            HashSet<string> set;
            if (known.TryGetValue(target, out set) && set.Contains(sha)) return true;
            string mine;
            return record.TryGetValue(target, out mine) && mine == sha;
        }

        /// <summary>Renames a file to name.yyyyMMdd-HHmmss.bak; returns the new name.</summary>
        static string Backup(string path)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string bak = path + "." + stamp + ".bak";
            for (int i = 2; File.Exists(bak); i++) bak = path + "." + stamp + "-" + i + ".bak";
            File.Copy(path, bak);
            return Path.GetFileName(bak);
        }

        /// <summary>Removes the tools (every language). Files still used by a tool that stays are kept,
        /// and files changed by hand are kept as .bak copies. Returns the number of failures.</summary>
        public int Remove(IEnumerable<CmsItem> items, Action<string> log)
        {
            int errors = 0;
            HashSet<string> removing = new HashSet<string>();
            foreach (CmsItem c in items) removing.Add(c.Id);

            // Who still uses each file after the removal: every tool that stays installed, in each of
            // its installed languages, with the tools it needs (in English) and their shared files.
            Dictionary<string, string> usedBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (CmsItem other in bundle.Cms)
            {
                if (removing.Contains(other.Id)) continue;
                List<string> langs = InstalledLanguages(other);
                if (langs.Count == 0) continue;
                List<KeyValuePair<CmsItem, string>> uses = new List<KeyValuePair<CmsItem, string>>();
                HashSet<string> seen = new HashSet<string>();
                foreach (string l in langs) AddToPlan(uses, seen, other, l);
                foreach (KeyValuePair<CmsItem, string> u in uses)
                    foreach (BundleFile f in FilesFor(u.Key, u.Value))
                        if (!usedBy.ContainsKey(f.Target)) usedBy[f.Target] = other.Name[Strings.Lang];
            }

            HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CmsItem c in items)
            {
                log(Strings.F("Removing", c.Name[Strings.Lang]));
                foreach (string l in c.Languages)
                {
                    foreach (BundleFile f in FilesFor(c, l))
                    {
                        if (!done.Add(f.Target)) continue;
                        string path = Path.Combine(dir, f.Target);
                        if (!File.Exists(path)) continue;
                        string user;
                        if (usedBy.TryGetValue(f.Target, out user))
                        {
                            log(Strings.F("Kept", f.Target, user));
                            continue;
                        }
                        try
                        {
                            string h = ParatextPaths.FileSha(path);
                            if (!IsOurs(f.Target, h)) log(Strings.F("BackedUp", f.Target, Backup(path)));
                            File.Delete(path);
                            record.Remove(f.Target);
                            log(Strings.F("Deleted", f.Target));
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            log(Strings.F("FileError", f.Target, ex.Message));
                        }
                    }
                }
            }
            WriteRecord();
            return errors;
        }
    }
}
