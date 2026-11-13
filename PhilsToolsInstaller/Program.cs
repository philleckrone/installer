using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PhilsTools
{
    static class Program
    {
        /// <summary>
        /// Install_Phils_tools.exe [--lang en|es|fr] [--cmsdir DIR] [--paratext "DIR;DIR"]
        /// Install_Phils_tools.exe --cli status|install|remove [ID...] [--cmsdir DIR] [--paratext ...]
        /// --cmsdir is for tests: the only cms folder searched for old CMS copies.
        /// --paratext is for tests: these folders are used instead of searching, and nothing asks for
        /// administrator rights (honoured only when not running as administrator).
        /// Started by the program itself (PluginInstaller), never by hand:
        /// --loadcheck PLUGIN PARATEXT (both base64): loads a private copy of a plugin as Paratext does.
        /// --elevated install|remove KEY [--lang L]: the administrator step of an install or removal.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 3 && args[0] == "--loadcheck")
            {
                Cli.Output();
                return PluginInstaller.LoadCheckMain(args[1], args[2]);
            }
            string lang = null, cmsDir = null, cli = null, elevated = null;
            List<string> ids = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--lang" && i + 1 < args.Length) lang = args[++i];
                else if (a == "--cmsdir" && i + 1 < args.Length) cmsDir = args[++i];
                else if (a == "--paratext" && i + 1 < args.Length) ParatextPaths.TestInstalls = args[++i];
                else if (a == "--cli" && i + 1 < args.Length) cli = args[++i];
                else if (a == "--elevated" && i + 1 < args.Length) elevated = args[++i];
                else ids.Add(a);
            }
            Strings.Lang = Strings.FromCulture(lang);

            Bundle bundle;
            try { bundle = Bundle.Load(); }
            catch (Exception ex)
            {
                if (cli != null) { Cli.Attach(); Console.Error.WriteLine(ex.Message); }
                else MessageBox.Show(ex.Message, Strings.T("Caption"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }
            if (elevated != null)
            {
                if (ids.Count != 1 || !string.IsNullOrEmpty(ParatextPaths.TestInstalls)) return 2;
                return PluginInstaller.RunElevated(bundle, elevated, ids[0]);
            }
            if (cli != null) return Cli.Run(bundle, cli, ids, cmsDir);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(bundle, cmsDir));
            return 0;
        }
    }
}
