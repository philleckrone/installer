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
        /// --paratext is for tests: these folders are used instead of searching, and the plugin
        /// scripts run with -NoElevate (they honour it only when not running as administrator).
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            string lang = null, cmsDir = null, cli = null;
            List<string> ids = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--lang" && i + 1 < args.Length) lang = args[++i];
                else if (a == "--cmsdir" && i + 1 < args.Length) cmsDir = args[++i];
                else if (a == "--paratext" && i + 1 < args.Length) ParatextPaths.TestInstalls = args[++i];
                else if (a == "--cli" && i + 1 < args.Length) cli = args[++i];
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
            if (cli != null) return Cli.Run(bundle, cli, ids, cmsDir);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(bundle, cmsDir));
            return 0;
        }
    }
}
