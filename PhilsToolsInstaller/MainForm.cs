using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PhilsTools
{
    /// <summary>The installer window: a Plugins tab and a CMS tab, each a ticked list with a status column.</summary>
    public class MainForm : Form
    {
        readonly Bundle bundle;
        List<string> installs = new List<string>();
        bool busy;

        readonly Label lblTitle = new Label();
        readonly Label lblIntro = new Label();
        readonly Label lblLang = new Label();
        readonly ComboBox cboLang = new ComboBox();
        readonly Label lblWarning = new Label();
        readonly TabControl tabs = new TabControl();
        readonly TabPage tabPlugins = new TabPage();
        readonly TabPage tabCms = new TabPage();
        readonly Label lblParatext = new Label();
        readonly ListView lvPlugins = new ListView();
        readonly TextBox txtPluginInfo = new TextBox();
        readonly Label lblCmsDir = new Label();
        readonly ComboBox cboCmsDir = new ComboBox();
        readonly Button btnBrowse = new Button();
        readonly Label lblCmsLang = new Label();
        readonly RadioButton rbEn = new RadioButton();
        readonly RadioButton rbEs = new RadioButton();
        readonly ListView lvCms = new ListView();
        readonly TextBox txtCmsInfo = new TextBox();
        readonly Button btnSelectAll = new Button();
        readonly Button btnSelectNone = new Button();
        readonly Button btnInstall = new Button();
        readonly Button btnRemove = new Button();
        readonly Button btnRefresh = new Button();
        readonly Button btnClose = new Button();
        readonly TextBox txtLog = new TextBox();

        public MainForm(Bundle bundle, string cmsDir, string cmsLang, bool cmsTab)
        {
            this.bundle = bundle;
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, SystemFonts.MessageBoxFont.SizeInPoints + 1f);
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(920, 760);
            MinimumSize = new Size(700, 560);
            BuildLayout();

            cboLang.Items.AddRange(new object[] { "English", "Espa\u00F1ol", "Fran\u00E7ais" });
            cboLang.SelectedIndex = Strings.Lang;
            cboLang.SelectedIndexChanged += delegate { Strings.Lang = cboLang.SelectedIndex; ApplyTexts(); RefreshStatus(); };

            foreach (string folder in ParatextPaths.ProjectsFolders()) cboCmsDir.Items.Add(Path.Combine(folder, "cms"));
            if (!string.IsNullOrEmpty(cmsDir))
            {
                cmsDir = Path.GetFullPath(cmsDir).TrimEnd('\\');
                if (!cboCmsDir.Items.Contains(cmsDir)) cboCmsDir.Items.Insert(0, cmsDir);
                cboCmsDir.SelectedItem = cmsDir;
            }
            else if (cboCmsDir.Items.Count > 0) cboCmsDir.SelectedIndex = 0;
            cboCmsDir.SelectedIndexChanged += delegate { RefreshCmsStatus(); };

            string wanted = string.IsNullOrEmpty(cmsLang) ? (Strings.Lang == 1 ? "es" : "en") : cmsLang;
            if (wanted == "es") rbEs.Checked = true; else rbEn.Checked = true;

            foreach (PluginItem p in bundle.Plugins)
            {
                ListViewItem it = new ListViewItem(new[] { "", p.Version, "" });
                it.Tag = p;
                lvPlugins.Items.Add(it);
            }
            foreach (CmsItem c in bundle.Cms)
            {
                ListViewItem it = new ListViewItem(new[] { "", "" });
                it.Tag = c;
                lvCms.Items.Add(it);
            }
            ApplyTexts();
            RefreshStatus();
            if (lvPlugins.Items.Count > 0) lvPlugins.Items[0].Selected = true;
            if (lvCms.Items.Count > 0) lvCms.Items[0].Selected = true;
            if (cmsTab) tabs.SelectedTab = tabCms;
        }

        // -- Layout -----------------------------------------------------------------------------

        void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 75));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            Controls.Add(root);

            TableLayoutPanel head = new TableLayoutPanel();
            head.Dock = DockStyle.Fill;
            head.AutoSize = true;
            head.ColumnCount = 3;
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font(Font.FontFamily, Font.SizeInPoints + 5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(0, 70, 130);
            lblLang.AutoSize = true;
            lblLang.Anchor = AnchorStyles.Right;
            cboLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLang.Width = 120;
            cboLang.Name = "cboLang";
            head.Controls.Add(lblTitle, 0, 0);
            head.Controls.Add(lblLang, 1, 0);
            head.Controls.Add(cboLang, 2, 0);
            root.Controls.Add(head, 0, 0);

            lblIntro.AutoSize = true;
            lblIntro.Dock = DockStyle.Fill;
            lblIntro.Margin = new Padding(3, 4, 3, 6);
            root.Controls.Add(lblIntro, 0, 1);

            lblWarning.AutoSize = true;
            lblWarning.Dock = DockStyle.Fill;
            lblWarning.BackColor = Color.FromArgb(255, 236, 160);
            lblWarning.Padding = new Padding(6);
            lblWarning.Margin = new Padding(3, 0, 3, 6);
            lblWarning.Font = new Font(Font, FontStyle.Bold);
            lblWarning.Visible = false;
            lblWarning.Name = "lblWarning";
            root.Controls.Add(lblWarning, 0, 2);

            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(tabPlugins);
            tabs.TabPages.Add(tabCms);
            root.Controls.Add(tabs, 0, 3);

            // Plugins tab
            TableLayoutPanel tp = new TableLayoutPanel();
            tp.Dock = DockStyle.Fill;
            tp.ColumnCount = 1;
            tp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tp.RowCount = 3;
            tp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tp.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            lblParatext.AutoSize = true;
            lblParatext.AutoEllipsis = true;
            lblParatext.Dock = DockStyle.Fill;
            lblParatext.Margin = new Padding(3, 6, 3, 6);
            SetupList(lvPlugins, "lvPlugins", 3);
            SetupInfo(txtPluginInfo, "txtPluginInfo");
            tp.Controls.Add(lblParatext, 0, 0);
            tp.Controls.Add(lvPlugins, 0, 1);
            tp.Controls.Add(txtPluginInfo, 0, 2);
            tabPlugins.Controls.Add(tp);
            lvPlugins.SelectedIndexChanged += delegate { ShowPluginInfo(); };

            // CMS tab
            TableLayoutPanel tc = new TableLayoutPanel();
            tc.Dock = DockStyle.Fill;
            tc.ColumnCount = 4;
            tc.RowCount = 4;
            tc.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tc.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tc.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tc.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tc.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tc.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tc.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            lblCmsDir.AutoSize = true;
            lblCmsDir.Anchor = AnchorStyles.Left;
            cboCmsDir.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCmsDir.Dock = DockStyle.Fill;
            cboCmsDir.Name = "cboCmsDir";
            btnBrowse.AutoSize = true;
            btnBrowse.Name = "btnBrowse";
            btnBrowse.Click += delegate { BrowseCms(); };
            tc.Controls.Add(lblCmsDir, 0, 0);
            tc.Controls.Add(cboCmsDir, 1, 0);
            tc.SetColumnSpan(cboCmsDir, 2);
            tc.Controls.Add(btnBrowse, 3, 0);
            FlowLayoutPanel langRow = new FlowLayoutPanel();
            langRow.AutoSize = true;
            langRow.Dock = DockStyle.Fill;
            langRow.WrapContents = false;
            lblCmsLang.AutoSize = true;
            lblCmsLang.Margin = new Padding(3, 6, 10, 3);
            rbEn.AutoSize = true;
            rbEn.Name = "rbEn";
            rbEs.AutoSize = true;
            rbEs.Name = "rbEs";
            langRow.Controls.Add(lblCmsLang);
            langRow.Controls.Add(rbEn);
            langRow.Controls.Add(rbEs);
            tc.Controls.Add(langRow, 0, 1);
            tc.SetColumnSpan(langRow, 4);
            SetupList(lvCms, "lvCms", 2);
            SetupInfo(txtCmsInfo, "txtCmsInfo");
            tc.Controls.Add(lvCms, 0, 2);
            tc.SetColumnSpan(lvCms, 4);
            tc.Controls.Add(txtCmsInfo, 0, 3);
            tc.SetColumnSpan(txtCmsInfo, 4);
            tabCms.Controls.Add(tc);
            lvCms.SelectedIndexChanged += delegate { ShowCmsInfo(); };

            // Buttons
            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.AutoSize = true;
            bar.ColumnCount = 2;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            FlowLayoutPanel left = new FlowLayoutPanel();
            left.AutoSize = true;
            left.WrapContents = false;
            FlowLayoutPanel right = new FlowLayoutPanel();
            right.AutoSize = true;
            right.WrapContents = false;
            right.Anchor = AnchorStyles.Right;
            foreach (Button b in new[] { btnSelectAll, btnSelectNone, btnRefresh, btnInstall, btnRemove, btnClose })
            {
                b.AutoSize = true;
                b.Padding = new Padding(6, 2, 6, 2);
            }
            btnSelectAll.Name = "btnSelectAll";
            btnSelectNone.Name = "btnSelectNone";
            btnRefresh.Name = "btnRefresh";
            btnInstall.Name = "btnInstall";
            btnRemove.Name = "btnRemove";
            btnClose.Name = "btnClose";
            btnInstall.Font = new Font(Font, FontStyle.Bold);
            left.Controls.Add(btnSelectAll);
            left.Controls.Add(btnSelectNone);
            left.Controls.Add(btnRefresh);
            right.Controls.Add(btnInstall);
            right.Controls.Add(btnRemove);
            right.Controls.Add(btnClose);
            bar.Controls.Add(left, 0, 0);
            bar.Controls.Add(right, 1, 0);
            root.Controls.Add(bar, 0, 4);
            btnSelectAll.Click += delegate { TickAll(true); };
            btnSelectNone.Click += delegate { TickAll(false); };
            btnRefresh.Click += delegate { RefreshStatus(); };
            btnInstall.Click += delegate { Start(false); };
            btnRemove.Click += delegate { Start(true); };
            btnClose.Click += delegate { Close(); };

            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Dock = DockStyle.Fill;
            txtLog.BackColor = SystemColors.Window;
            txtLog.Font = new Font(FontFamily.GenericMonospace, Font.SizeInPoints - 1f);
            txtLog.Name = "txtLog";
            root.Controls.Add(txtLog, 0, 5);
            AcceptButton = btnInstall;
        }

        static void SetupList(ListView lv, string name, int columns)
        {
            lv.Name = name;
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.CheckBoxes = true;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.MultiSelect = false;
            for (int i = 0; i < columns; i++) lv.Columns.Add("");
        }

        static void SetupInfo(TextBox t, string name)
        {
            t.Name = name;
            t.Dock = DockStyle.Fill;
            t.Multiline = true;
            t.ReadOnly = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.BackColor = SystemColors.Info;
        }

        // -- Texts ---------------------------------------------------------------------------

        void ApplyTexts()
        {
            Text = Strings.T("Caption") + " " + Strings.ToolVersion;
            lblTitle.Text = Strings.T("Title");
            lblIntro.Text = Strings.T("Intro");
            lblLang.Text = Strings.T("Language");
            lblWarning.Text = Strings.T("Running");
            tabPlugins.Text = Strings.T("TabPlugins");
            tabCms.Text = Strings.T("TabCms");
            lblCmsDir.Text = Strings.T("CmsFolder");
            btnBrowse.Text = Strings.T("Browse");
            lblCmsLang.Text = Strings.T("CmsLang");
            rbEn.Text = Strings.T("LangEn");
            rbEs.Text = Strings.T("LangEs");
            btnSelectAll.Text = Strings.T("SelectAll");
            btnSelectNone.Text = Strings.T("SelectNone");
            btnRefresh.Text = Strings.T("Refresh");
            btnInstall.Text = Strings.T("Install");
            btnRemove.Text = Strings.T("Remove");
            btnClose.Text = Strings.T("Close");
            lvPlugins.Columns[0].Text = Strings.T("ColName");
            lvPlugins.Columns[1].Text = Strings.T("ColVersion");
            lvPlugins.Columns[2].Text = Strings.T("ColStatus");
            lvCms.Columns[0].Text = Strings.T("ColName");
            lvCms.Columns[1].Text = Strings.T("ColStatus");
            foreach (ListViewItem it in lvPlugins.Items) it.Text = ((PluginItem)it.Tag).Name[Strings.Lang];
            foreach (ListViewItem it in lvCms.Items) it.Text = ((CmsItem)it.Tag).Name[Strings.Lang];
            ShowPluginInfo();
            ShowCmsInfo();
        }

        void SizeColumns()
        {
            // Widths in pixels, scaled from 96 dpi.
            float k = DeviceDpi / 96f;
            int ver = (int)(80 * k), st = (int)(230 * k), cst = (int)(290 * k);
            int w = lvPlugins.ClientSize.Width;
            lvPlugins.Columns[1].Width = ver;
            lvPlugins.Columns[2].Width = st;
            lvPlugins.Columns[0].Width = Math.Max(150, w - ver - st - 4);
            w = lvCms.ClientSize.Width;
            lvCms.Columns[1].Width = cst;
            lvCms.Columns[0].Width = Math.Max(150, w - cst - 4);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            lvPlugins.Resize += delegate { SizeColumns(); };
            lvCms.Resize += delegate { SizeColumns(); };
            SizeColumns();
        }

        void ShowPluginInfo()
        {
            if (lvPlugins.SelectedItems.Count == 0) { txtPluginInfo.Text = ""; return; }
            PluginItem p = (PluginItem)lvPlugins.SelectedItems[0].Tag;
            txtPluginInfo.Text = p.Description[Strings.Lang] + Environment.NewLine + Strings.F("Menu", p.Menu[Strings.Lang]);
        }

        void ShowCmsInfo()
        {
            if (lvCms.SelectedItems.Count == 0) { txtCmsInfo.Text = ""; return; }
            CmsItem c = (CmsItem)lvCms.SelectedItems[0].Tag;
            string text = c.Description[Strings.Lang];
            List<string> needs = new List<string>();
            foreach (string n in c.Needs)
            {
                CmsItem dep = bundle.FindCms(n);
                if (dep != null) needs.Add(dep.Name[Strings.Lang]);
            }
            if (needs.Count > 0) text += Environment.NewLine + Strings.F("AlsoInstalls", string.Join(", ", needs.ToArray()));
            if (c.Languages.Count == 1) text += Environment.NewLine + Strings.F("OnlyIn", Strings.LangName(c.Languages[0]));
            txtCmsInfo.Text = text;
        }

        // -- Status ------------------------------------------------------------------------

        static string StatusText(ToolStatus s)
        {
            switch (s)
            {
                case ToolStatus.Installed: return Strings.T("StInstalled");
                case ToolStatus.UpdateAvailable: return Strings.T("StUpdate");
                case ToolStatus.NoParatext: return Strings.T("StNoParatext");
                default: return Strings.T("StNotInstalled");
            }
        }

        static Color StatusColor(ToolStatus s)
        {
            switch (s)
            {
                case ToolStatus.Installed: return Color.FromArgb(0, 110, 0);
                case ToolStatus.UpdateAvailable: return Color.FromArgb(190, 90, 0);
                case ToolStatus.NoParatext: return Color.FromArgb(170, 0, 0);
                default: return SystemColors.WindowText;
            }
        }

        void RefreshStatus()
        {
            installs = ParatextPaths.FindInstalls();
            lblParatext.Text = installs.Count == 0 ? Strings.T("ParatextNone")
                : Strings.F("ParatextFound", string.Join("; ", installs.ToArray()));
            lblParatext.ForeColor = installs.Count == 0 ? Color.FromArgb(170, 0, 0) : SystemColors.ControlText;
            lblWarning.Visible = ParatextPaths.ParatextRunning();
            foreach (ListViewItem it in lvPlugins.Items)
            {
                ToolStatus st = PluginRunner.Status((PluginItem)it.Tag, installs);
                it.UseItemStyleForSubItems = false;
                it.SubItems[2].Text = StatusText(st);
                it.SubItems[2].ForeColor = StatusColor(st);
            }
            RefreshCmsStatus();
        }

        string CmsDir
        {
            get { return cboCmsDir.SelectedItem as string; }
        }

        void RefreshCmsStatus()
        {
            string dir = CmsDir;
            CmsInstaller cms = (dir != null && Directory.Exists(dir)) ? new CmsInstaller(bundle, dir) : null;
            foreach (ListViewItem it in lvCms.Items)
            {
                CmsItem c = (CmsItem)it.Tag;
                List<string> langs = new List<string>();
                ToolStatus st = cms == null ? ToolStatus.NotInstalled : cms.Status(c, out langs);
                string text = StatusText(st);
                if (langs.Count > 0)
                {
                    List<string> names = new List<string>();
                    foreach (string l in langs) names.Add(Strings.LangName(l));
                    text += " (" + string.Join(", ", names.ToArray()) + ")";
                }
                it.UseItemStyleForSubItems = false;
                it.SubItems[1].Text = text;
                it.SubItems[1].ForeColor = StatusColor(st);
            }
        }

        void BrowseCms()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = Strings.T("BrowseTitle");
                dlg.ShowNewFolderButton = false;
                if (CmsDir != null && Directory.Exists(CmsDir)) dlg.SelectedPath = CmsDir;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string dir = dlg.SelectedPath.TrimEnd('\\');
                if (!cboCmsDir.Items.Contains(dir)) cboCmsDir.Items.Add(dir);
                cboCmsDir.SelectedItem = dir;
            }
        }

        void TickAll(bool on)
        {
            ListView lv = tabs.SelectedTab == tabCms ? lvCms : lvPlugins;
            foreach (ListViewItem it in lv.Items) it.Checked = on;
        }

        // -- Install / remove ---------------------------------------------------------------

        void Log(string line)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), line); return; }
            txtLog.AppendText(line + Environment.NewLine);
        }

        void SetBusy(bool on)
        {
            busy = on;
            foreach (Control c in new Control[] { tabs, btnSelectAll, btnSelectNone, btnRefresh, btnInstall, btnRemove, btnClose, cboLang })
                c.Enabled = !on;
            UseWaitCursor = on;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy) e.Cancel = true;
            base.OnFormClosing(e);
        }

        void Start(bool remove)
        {
            // Only the tab that is showing: ticks left on the other tab are never acted on.
            bool onCms = tabs.SelectedTab == tabCms;
            ListView lv = onCms ? lvCms : lvPlugins;
            List<PluginItem> plugins = new List<PluginItem>();
            List<CmsItem> cmsItems = new List<CmsItem>();
            List<ListViewItem> handled = new List<ListViewItem>();
            foreach (ListViewItem it in lv.Items)
            {
                if (!it.Checked) continue;
                handled.Add(it);
                if (onCms) cmsItems.Add((CmsItem)it.Tag); else plugins.Add((PluginItem)it.Tag);
            }
            if (plugins.Count + cmsItems.Count == 0)
            {
                MessageBox.Show(this, Strings.T("NothingTicked"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string cmsDir = CmsDir;
            if (cmsItems.Count > 0)
            {
                if (cmsDir == null)
                {
                    tabs.SelectedTab = tabCms;
                    MessageBox.Show(this, Strings.T("NoCmsDir"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                // The projects folder exists but has no cms folder yet: create it for an install.
                if (!Directory.Exists(cmsDir))
                {
                    string parent = Path.GetDirectoryName(cmsDir);
                    if (remove || parent == null || !Directory.Exists(parent))
                    {
                        MessageBox.Show(this, Strings.F("CmsNotExist", cmsDir), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }
            if (remove && MessageBox.Show(this, Strings.F(onCms ? "ConfirmRemoveCms" : "ConfirmRemovePlugins", handled.Count), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            string cmsLang = rbEs.Checked ? "es" : "en";
            bool paratextRunning = ParatextPaths.ParatextRunning();
            txtLog.Clear();
            SetBusy(true);
            Thread t = new Thread(delegate()
            {
                int problems = 0;
                try
                {
                    if (plugins.Count > 0)
                    {
                        if (paratextRunning)
                        {
                            Log(Strings.T("PluginsSkipped"));
                            problems++;
                        }
                        else
                        {
                            if (!remove) Log(Strings.T("UacNote"));
                            foreach (PluginItem p in plugins)
                            {
                                Log("");
                                Log(Strings.F(remove ? "Removing" : "Installing", p.Name[Strings.Lang]));
                                int code;
                                try { code = PluginRunner.Run(bundle, p, remove, Log); }
                                catch (Exception ex) { Log("  " + ex.Message); code = 1; }
                                if (code != 0) { problems++; Log(Strings.F("ScriptFailed", p.Name[Strings.Lang])); }
                            }
                        }
                    }
                    if (cmsItems.Count > 0)
                    {
                        Log("");
                        if (!Directory.Exists(cmsDir)) Directory.CreateDirectory(cmsDir);
                        CmsInstaller cms = new CmsInstaller(bundle, cmsDir);
                        problems += remove ? cms.Remove(cmsItems, Log) : cms.Install(cmsItems, cmsLang, Log);
                    }
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                    problems++;
                }
                Log("");
                Log(problems == 0 ? Strings.T("Done") : Strings.F("DoneErrors", problems));
                Log(Strings.T("RestartNote"));
                // Untick what was handled (failures too: the log says what went wrong). Plugins skipped
                // because Paratext was open keep their ticks, to try again after closing it.
                bool keepTicks = plugins.Count > 0 && paratextRunning;
                BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    if (!keepTicks) foreach (ListViewItem it in handled) it.Checked = false;
                    RefreshStatus();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}
