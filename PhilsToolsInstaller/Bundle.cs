using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace PhilsTools
{
    /// <summary>A file carried by the installer: its zip entry, the name it is written under, and its SHA-256.</summary>
    public class BundleFile
    {
        public string Entry;
        public string Target;
        public string Sha;
    }

    public class PluginItem
    {
        public string Key, Folder, PluginFile, ExeFile, Version;
        public bool ExeProtected;
        public string[] Name = new string[3];
        public string[] Menu = new string[3];
        public string[] Description = new string[3];
        /// <summary>The package as its zip holds it: Install-Plugin.ps1 and Folder\file.</summary>
        public List<BundleFile> Files = new List<BundleFile>();
        /// <summary>The user guides by language ("en", "es", "fr"); English is always there.</summary>
        public Dictionary<string, BundleFile> Guides = new Dictionary<string, BundleFile>();

        /// <summary>The guide in this language, or the English one.</summary>
        public BundleFile GuideFor(string lang)
        {
            BundleFile f;
            if (Guides.TryGetValue(lang, out f)) return f;
            return Guides.TryGetValue("en", out f) ? f : null;
        }

        public string ShaOf(string target)
        {
            foreach (BundleFile f in Files)
                if (string.Equals(f.Target, target, StringComparison.OrdinalIgnoreCase)) return f.Sha;
            return null;
        }
    }

    public class CmsItem
    {
        public string Id;
        public List<string> Needs = new List<string>();
        public List<string> Languages = new List<string>();
        public string[] Name = new string[3];
        public string[] Description = new string[3];
        /// <summary>Language ("en", "es") -> files; the first is the .cms.</summary>
        public Dictionary<string, List<BundleFile>> Files = new Dictionary<string, List<BundleFile>>();

        /// <summary>The language to install when the user asked for <paramref name="wanted"/>.</summary>
        public string LanguageFor(string wanted)
        {
            if (Languages.Contains(wanted)) return wanted;
            if (Languages.Contains("en")) return "en";
            return Languages[0];
        }
    }

    /// <summary>The bundle.zip embedded in the exe (written by tools\phils_installer\make_bundle.py).</summary>
    public class Bundle
    {
        public readonly List<PluginItem> Plugins = new List<PluginItem>();
        public readonly List<CmsItem> Cms = new List<CmsItem>();
        public readonly Dictionary<string, List<BundleFile>> Shared = new Dictionary<string, List<BundleFile>>();
        readonly ZipArchive zip;
        readonly object gate = new object();

        Bundle(ZipArchive zip)
        {
            this.zip = zip;
        }

        public static Bundle Load()
        {
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("bundle.zip");
            if (s == null) throw new InvalidOperationException("The installer is incomplete: bundle.zip is missing.");
            MemoryStream ms = new MemoryStream();
            s.CopyTo(ms);
            s.Dispose();
            ms.Position = 0;
            Bundle b = new Bundle(new ZipArchive(ms, ZipArchiveMode.Read));
            b.ParseManifest(Encoding.UTF8.GetString(b.ReadEntry("manifest.txt")));
            return b;
        }

        void ParseManifest(string text)
        {
            Dictionary<string, PluginItem> plugins = new Dictionary<string, PluginItem>();
            Dictionary<string, CmsItem> cms = new Dictionary<string, CmsItem>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                string[] f = line.Split('\t');
                switch (f[0])
                {
                    case "P":
                        {
                            PluginItem p = new PluginItem();
                            p.Key = f[1]; p.Folder = f[2]; p.PluginFile = f[3]; p.ExeFile = f[4];
                            p.ExeProtected = f[5] == "1"; p.Version = f[6];
                            for (int i = 0; i < 3; i++)
                            {
                                p.Name[i] = f[7 + i];
                                p.Menu[i] = f[10 + i];
                                p.Description[i] = f[13 + i];
                            }
                            plugins[p.Key] = p;
                            Plugins.Add(p);
                            break;
                        }
                    case "F":
                        plugins[f[1]].Files.Add(NewFile(f[2], f[3], f[4]));
                        break;
                    case "G":
                        plugins[f[1]].Guides[f[2]] = NewFile(f[3], f[4], f[5]);
                        break;
                    case "S":
                        {
                            List<BundleFile> list;
                            if (!Shared.TryGetValue(f[1], out list)) { list = new List<BundleFile>(); Shared[f[1]] = list; }
                            list.Add(NewFile(f[2], f[3], f[4]));
                            break;
                        }
                    case "C":
                        {
                            CmsItem c = new CmsItem();
                            c.Id = f[1];
                            foreach (string n in f[2].Split(',')) if (n.Length > 0) c.Needs.Add(n);
                            foreach (string l in f[3].Split(',')) if (l.Length > 0) c.Languages.Add(l);
                            for (int i = 0; i < 3; i++)
                            {
                                c.Name[i] = f[4 + i];
                                c.Description[i] = f[7 + i];
                            }
                            cms[c.Id] = c;
                            Cms.Add(c);
                            break;
                        }
                    case "L":
                        {
                            CmsItem c = cms[f[1]];
                            List<BundleFile> list;
                            if (!c.Files.TryGetValue(f[2], out list)) { list = new List<BundleFile>(); c.Files[f[2]] = list; }
                            list.Add(NewFile(f[3], f[4], f[5]));
                            break;
                        }
                }
            }
        }

        static BundleFile NewFile(string entry, string target, string sha)
        {
            BundleFile b = new BundleFile();
            b.Entry = entry; b.Target = target; b.Sha = sha;
            return b;
        }

        public CmsItem FindCms(string id)
        {
            foreach (CmsItem c in Cms) if (c.Id == id) return c;
            return null;
        }

        public PluginItem FindPlugin(string key)
        {
            foreach (PluginItem p in Plugins) if (string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        byte[] ReadEntry(string name)
        {
            lock (gate)
            {
                ZipArchiveEntry e = zip.GetEntry(name);
                if (e == null) throw new InvalidOperationException("The installer is incomplete: " + name + " is missing.");
                using (Stream s = e.Open())
                {
                    MemoryStream ms = new MemoryStream();
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>A file's bytes, checked against the SHA-256 in the manifest.</summary>
        public byte[] Read(BundleFile f)
        {
            byte[] data = ReadEntry(f.Entry);
            if (ParatextPaths.Sha256(data) != f.Sha)
                throw new InvalidOperationException("The installer is damaged: " + f.Entry + " does not match its checksum.");
            return data;
        }
    }
}
