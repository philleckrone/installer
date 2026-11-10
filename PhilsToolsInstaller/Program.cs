using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PhilsTools
{
    static class Program
    {
        /// <summary>
        /// Install_Phils_tools.exe [--lang en|es|fr] [--tab cms] [--cmsdir DIR] [--paratext "DIR;DIR"]
        /// Install_Phils_tools.exe --cli status|install|remove [ID...] [--cmslang en|es] [--cmsdir DIR] [--paratext ...]
        /// --paratext is for tests: these folders are used instead of searching, and the plugin
        /// scripts run with -NoElevate (they honour it only when not running as administrator).
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            string lang = null, cmsDir = null, cmsLang = null, cli = null;
            bool cmsTab = false;
            List<string> ids = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--lang" && i + 1 < args.Length) lang = args[++i];
                else if (a == "--cmsdir" && i + 1 < args.Length) cmsDir = args[++i];
                else if (a == "--cmslang" && i + 1 < args.Length) cmsLang = args[++i];
                else if (a == "--paratext" && i + 1 < args.Length) ParatextPaths.TestInstalls = args[++i];
                else if (a == "--tab" && i + 1 < args.Length) cmsTab = args[++i] == "cms";
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
            if (cli != null) return Cli.Run(bundle, cli, ids, cmsDir, cmsLang);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(bundle, cmsDir, cmsLang, cmsTab));
            return 0;
        }
    }
}
