using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace PhilsTools
{
    /// <summary>
    /// The installer window: the Paratext plugins as a ticked list with a status column, sorted by
    /// name in the window's language. The CMS checks are no longer installed (they come with the Phils
    /// Custom (cms) Checks plugin); copies an earlier version put in a cms folder can be removed here.
    /// </summary>
    public class MainForm : Form
    {
        /// <summary>The plugin that now carries the CMS checks.</summary>
        const string ChecksPluginKey = "PhilsChecks";

        readonly Bundle bundle;
        readonly string cmsDirOverride;
        List<string> installs = new List<string>();
        List<KeyValuePair<string, List<CmsItem>>> oldCopies = new List<KeyValuePair<string, List<CmsItem>>>();
        bool busy;

        readonly Label lblTitle = new Label();
        readonly Label lblIntro = new Label();
        readonly Label lblNote = new Label();
        readonly Label lblLang = new Label();
        readonly ComboBox cboLang = new ComboBox();
        readonly Label lblWarning = new Label();
        readonly Label lblPlugins = new Label();
        readonly Label lblParatext = new Label();
        readonly ListView lvPlugins = new ToggleListView();
        readonly TextBox txtPluginInfo = new TextBox();
        readonly TableLayoutPanel oldRow = new TableLayoutPanel();
        readonly Label lblOld = new Label();
        readonly Button btnOld = new Button();
        readonly Button btnSelectAll = new Button();
        readonly Button btnSelectNone = new Button();
        readonly Button btnInstall = new Button();
        readonly Button btnRemove = new Button();
        readonly Button btnRefresh = new Button();
        readonly Button btnGuide = new Button();
        readonly Button btnClose = new Button();
        readonly TextBox txtLog = new TextBox();

        /// <param name="cmsDir">For tests: the only cms folder searched for old copies (null = every projects folder's).</param>
        public MainForm(Bundle bundle, string cmsDir)
        {
            this.bundle = bundle;
            cmsDirOverride = cmsDir;
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, SystemFonts.MessageBoxFont.SizeInPoints + 1f);
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(920, 760);
            MinimumSize = new Size(700, 560);
            BuildLayout();

            cboLang.Items.AddRange(new object[] { "English", "Espa\u00F1ol", "Fran\u00E7ais" });
            cboLang.SelectedIndex = Strings.Lang;
            cboLang.SelectedIndexChanged += delegate { Strings.Lang = cboLang.SelectedIndex; ApplyTexts(); RefreshStatus(); };

            foreach (PluginItem p in bundle.Plugins)
            {
                ListViewItem it = new ListViewItem(new[] { "", p.Version, "" });
                it.Tag = p;
                lvPlugins.Items.Add(it);
            }
            ApplyTexts();
            RefreshStatus();
            if (lvPlugins.Items.Count > 0) lvPlugins.Items[0].Selected = true;
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

            // A highlighted note at the top (1.4, the user's request): the CMS checks moved to a plugin.
            TableLayoutPanel top = new TableLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.AutoSize = true;
            top.Margin = new Padding(0);
            top.ColumnCount = 1;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.RowCount = 2;
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lblNote.AutoSize = true;
            lblNote.Dock = DockStyle.Fill;
            lblNote.BackColor = Color.FromArgb(214, 236, 214);
            lblNote.ForeColor = Color.FromArgb(0, 80, 0);
            lblNote.Padding = new Padding(6);
            lblNote.Margin = new Padding(3, 6, 3, 2);
            lblNote.Font = new Font(Font, FontStyle.Bold);
            lblNote.Name = "lblNote";
            lblIntro.AutoSize = true;
            lblIntro.Dock = DockStyle.Fill;
            lblIntro.Margin = new Padding(3, 4, 3, 6);
            top.Controls.Add(lblNote, 0, 0);
            top.Controls.Add(lblIntro, 0, 1);
            root.Controls.Add(top, 0, 1);

            lblWarning.AutoSize = true;
            lblWarning.Dock = DockStyle.Fill;
            lblWarning.BackColor = Color.FromArgb(255, 236, 160);
            lblWarning.Padding = new Padding(6);
            lblWarning.Margin = new Padding(3, 0, 3, 6);
            lblWarning.Font = new Font(Font, FontStyle.Bold);
            lblWarning.Visible = false;
            lblWarning.Name = "lblWarning";
            root.Controls.Add(lblWarning, 0, 2);

            // The plugins
            TableLayoutPanel tp = new TableLayoutPanel();
            tp.Dock = DockStyle.Fill;
            tp.Margin = new Padding(0);
            tp.ColumnCount = 1;
            tp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tp.RowCount = 5;
            tp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tp.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            lblPlugins.AutoSize = true;
            lblPlugins.Font = new Font(Font, FontStyle.Bold);
            lblPlugins.Margin = new Padding(3, 2, 3, 0);
            lblParatext.AutoSize = true;
            lblParatext.AutoEllipsis = true;
            lblParatext.Dock = DockStyle.Fill;
            lblParatext.Margin = new Padding(3, 4, 3, 6);

            // Old CMS copies: shown only when some are found.
            oldRow.Dock = DockStyle.Fill;
            oldRow.AutoSize = true;
            oldRow.ColumnCount = 2;
            oldRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            oldRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            oldRow.BackColor = Color.FromArgb(232, 242, 252);
            oldRow.Margin = new Padding(3, 0, 3, 6);
            oldRow.Visible = false;
            lblOld.AutoSize = true;
            lblOld.Dock = DockStyle.Fill;
            lblOld.Padding = new Padding(4);
            lblOld.Name = "lblOld";
            btnOld.AutoSize = true;
            btnOld.Padding = new Padding(6, 2, 6, 2);
            btnOld.Anchor = AnchorStyles.Right;
            btnOld.Name = "btnOld";
            btnOld.Click += delegate { RemoveOldCopies(); };
            oldRow.Controls.Add(lblOld, 0, 0);
            oldRow.Controls.Add(btnOld, 1, 0);

            SetupList(lvPlugins, "lvPlugins", 3);
            SetupInfo(txtPluginInfo, "txtPluginInfo");
            tp.Controls.Add(lblPlugins, 0, 0);
            tp.Controls.Add(lblParatext, 0, 1);
            tp.Controls.Add(oldRow, 0, 2);
            tp.Controls.Add(lvPlugins, 0, 3);
            tp.Controls.Add(txtPluginInfo, 0, 4);
            root.Controls.Add(tp, 0, 3);
            lvPlugins.SelectedIndexChanged += delegate { ShowPluginInfo(); };

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
            foreach (Button b in new[] { btnSelectAll, btnSelectNone, btnRefresh, btnGuide, btnInstall, btnRemove, btnClose })
            {
                b.AutoSize = true;
                b.Padding = new Padding(6, 2, 6, 2);
            }
            btnSelectAll.Name = "btnSelectAll";
            btnSelectNone.Name = "btnSelectNone";
            btnRefresh.Name = "btnRefresh";
            btnGuide.Name = "btnGuide";
            btnInstall.Name = "btnInstall";
            btnRemove.Name = "btnRemove";
            btnClose.Name = "btnClose";
            btnInstall.Font = new Font(Font, FontStyle.Bold);
            left.Controls.Add(btnSelectAll);
            left.Controls.Add(btnSelectNone);
            left.Controls.Add(btnRefresh);
            left.Controls.Add(btnGuide);
            right.Controls.Add(btnInstall);
            right.Controls.Add(btnRemove);
            right.Controls.Add(btnClose);
            bar.Controls.Add(left, 0, 0);
            bar.Controls.Add(right, 1, 0);
            root.Controls.Add(bar, 0, 4);
            btnSelectAll.Click += delegate { TickAll(true); };
            btnSelectNone.Click += delegate { TickAll(false); };
            btnRefresh.Click += delegate { RefreshStatus(); };
            btnGuide.Click += delegate { OpenGuide(); };
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
            Text = Strings.T("Caption") + " (v. " + Strings.ToolVersion + ")";
            lblTitle.Text = Strings.T("Title");
            lblIntro.Text = Strings.T("Intro");
            lblNote.Text = Strings.T("CmsNote");
            lblLang.Text = Strings.T("Language");
            lblWarning.Text = Strings.T("Running");
            lblPlugins.Text = Strings.T("TabPlugins");
            btnOld.Text = Strings.T("OldRemove");
            btnSelectAll.Text = Strings.T("SelectAll");
            btnSelectNone.Text = Strings.T("SelectNone");
            btnRefresh.Text = Strings.T("Refresh");
            btnGuide.Text = Strings.T("Guide");
            btnInstall.Text = Strings.T("Install");
            btnRemove.Text = Strings.T("Remove");
            btnClose.Text = Strings.T("Close");
            lvPlugins.Columns[0].Text = Strings.T("ColName");
            lvPlugins.Columns[1].Text = Strings.T("ColVersion");
            lvPlugins.Columns[2].Text = Strings.T("ColStatus");
            foreach (ListViewItem it in lvPlugins.Items) it.Text = ((PluginItem)it.Tag).Name[Strings.Lang];
            SortPlugins();
            ShowPluginInfo();
        }

        /// <summary>Puts the plugins in alphabetical order of their names in the window's language,
        /// keeping the ticks and the selected row.</summary>
        void SortPlugins()
        {
            CultureInfo culture = new CultureInfo(Strings.Lang == 1 ? "es-ES" : Strings.Lang == 2 ? "fr-FR" : "en-US");
            StringComparer cmp = StringComparer.Create(culture, true);
            List<ListViewItem> items = new List<ListViewItem>();
            HashSet<ListViewItem> ticked = new HashSet<ListViewItem>();
            foreach (ListViewItem it in lvPlugins.Items)
            {
                items.Add(it);
                if (it.Checked) ticked.Add(it);
            }
            ListViewItem selected = lvPlugins.SelectedItems.Count > 0 ? lvPlugins.SelectedItems[0] : null;
            items.Sort(delegate(ListViewItem a, ListViewItem b) { return cmp.Compare(a.Text, b.Text); });
            lvPlugins.BeginUpdate();
            lvPlugins.Items.Clear();
            lvPlugins.Items.AddRange(items.ToArray());
            foreach (ListViewItem it in items) it.Checked = ticked.Contains(it);
            if (selected != null)
            {
                selected.Selected = true;
                selected.EnsureVisible();
            }
            lvPlugins.EndUpdate();
        }

        void SizeColumns()
        {
            // Widths in pixels, scaled from 96 dpi.
            float k = DeviceDpi / 96f;
            int ver = (int)(80 * k), st = (int)(230 * k);
            int w = lvPlugins.ClientSize.Width;
            lvPlugins.Columns[1].Width = ver;
            lvPlugins.Columns[2].Width = st;
            lvPlugins.Columns[0].Width = Math.Max(150, w - ver - st - 4);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            lvPlugins.Resize += delegate { SizeColumns(); };
            SizeColumns();
        }

        void ShowPluginInfo()
        {
            btnGuide.Enabled = !busy && lvPlugins.SelectedItems.Count > 0;
            if (lvPlugins.SelectedItems.Count == 0) { txtPluginInfo.Text = ""; return; }
            PluginItem p = (PluginItem)lvPlugins.SelectedItems[0].Tag;
            txtPluginInfo.Text = p.Description[Strings.Lang] + Environment.NewLine + Strings.F("Menu", p.Menu[Strings.Lang]);
        }

        /// <summary>
        /// Opens the selected plugin's user guide, in the window's language or else in English. The
        /// guides are not installed: the checked bytes are written to %LOCALAPPDATA%\PhilsTools\guides
        /// (a per-user folder; an existing copy is reused only when it matches) and opened there.
        /// </summary>
        void OpenGuide()
        {
            if (lvPlugins.SelectedItems.Count == 0) return;
            PluginItem p = (PluginItem)lvPlugins.SelectedItems[0].Tag;
            BundleFile f = p.GuideFor(Strings.Lang == 1 ? "es" : Strings.Lang == 2 ? "fr" : "en");
            if (f == null) return;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhilsTools", "guides");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, f.Target);
                if (!File.Exists(path) || ParatextPaths.FileSha(path) != f.Sha)
                {
                    byte[] data = bundle.Read(f);
                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }
                    using (FileStream fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        fs.Write(data, 0, data.Length);
                }
                Log("guide: " + path);
                // Our own viewer (1.6): Windows 11 24H2+ has no WordPad, so without Word an .rtf may
                // not open at all. Tests set PHILSTOOLS_NOOPEN to only log the path.
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PHILSTOOLS_NOOPEN")))
                {
                    var texts = new PhilsShared.RtfViewer.Texts();
                    texts.OpenElsewhere = Strings.T("OpenElsewhere");
                    texts.Close = Strings.T("Close");
                    texts.CannotOpenElsewhere = Strings.T("CannotOpenElsewhere");
                    PhilsShared.RtfViewer.Open(this, path, p.Name[Strings.Lang] + " - " + Strings.T("Guide"), texts);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Strings.F("GuideError", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
            RefreshOldCopies();
        }

        // -- Old CMS copies -------------------------------------------------------------------

        int OldCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<string, List<CmsItem>> kv in oldCopies) n += kv.Value.Count;
                return n;
            }
        }

        string OldFolders
        {
            get
            {
                List<string> dirs = new List<string>();
                foreach (KeyValuePair<string, List<CmsItem>> kv in oldCopies) dirs.Add(kv.Key);
                return string.Join("; ", dirs.ToArray());
            }
        }

        void RefreshOldCopies()
        {
            oldCopies = CmsInstaller.FindOldCopies(bundle, cmsDirOverride);
            oldRow.Visible = OldCount > 0;
            lblOld.Text = OldCount > 0 ? Strings.F("OldCopies", OldCount, OldFolders) : "";
        }

        /// <summary>Asks, then removes the old copies (files changed by hand are kept as .bak).</summary>
        void RemoveOldCopies()
        {
            RefreshOldCopies();
            if (OldCount == 0) return;
            List<string> dirs = new List<string>();
            foreach (KeyValuePair<string, List<CmsItem>> kv in oldCopies) dirs.Add(kv.Key);
            if (MessageBox.Show(this, Strings.F("OldConfirm", OldCount, string.Join(Environment.NewLine, dirs.ToArray())), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            List<KeyValuePair<string, List<CmsItem>>> work = oldCopies;
            txtLog.Clear();
            SetBusy(true);
            Thread t = new Thread(delegate()
            {
                int problems = 0;
                foreach (KeyValuePair<string, List<CmsItem>> kv in work)
                {
                    try
                    {
                        Log(Strings.F("OldRemoving", kv.Key));
                        problems += new CmsInstaller(bundle, kv.Key).Remove(kv.Value, Log);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.Message);
                        problems++;
                    }
                    Log("");
                }
                Log(problems == 0 ? Strings.T("Done") : Strings.F("DoneErrors", problems));
                Log(Strings.T("RestartNote"));
                BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    RefreshStatus();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void TickAll(bool on)
        {
            foreach (ListViewItem it in lvPlugins.Items) it.Checked = on;
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
            foreach (Control c in new Control[] { lvPlugins, btnOld, btnSelectAll, btnSelectNone, btnRefresh, btnGuide, btnInstall, btnRemove, btnClose, cboLang })
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
            List<PluginItem> plugins = new List<PluginItem>();
            List<ListViewItem> handled = new List<ListViewItem>();
            foreach (ListViewItem it in lvPlugins.Items)
            {
                if (!it.Checked) continue;
                handled.Add(it);
                plugins.Add((PluginItem)it.Tag);
            }
            if (plugins.Count == 0)
            {
                MessageBox.Show(this, Strings.T("NothingTicked"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (remove && MessageBox.Show(this, Strings.F("ConfirmRemovePlugins", handled.Count), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            bool paratextRunning = ParatextPaths.ParatextRunning();
            bool checksPlugin = false;
            foreach (PluginItem p in plugins) if (p.Key == ChecksPluginKey) checksPlugin = true;
            txtLog.Clear();
            SetBusy(true);
            Thread t = new Thread(delegate()
            {
                int problems = 0;
                try
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
                BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    if (!paratextRunning) foreach (ListViewItem it in handled) it.Checked = false;
                    RefreshStatus();
                    // The checks plugin is now installed: offer once to remove the old copies it replaces.
                    if (!remove && checksPlugin && !paratextRunning && OldCount > 0 && ChecksPluginInstalled())
                        RemoveOldCopies();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        bool ChecksPluginInstalled()
        {
            PluginItem p = bundle.FindPlugin(ChecksPluginKey);
            return p != null && PluginRunner.Status(p, installs) == ToolStatus.Installed;
        }
    }

    /// <summary>
    /// A ticked list where a click anywhere on a row (name, version, status) ticks or unticks it, not
    /// only a click on its box (1.4, the user's request). The box itself is still toggled by the list.
    /// The press is handled here and not passed on: the list control waits for a drag after a press
    /// and raises a click only if none happened, which proved unreliable. A ticked ListView also
    /// toggles on a double-click, so a double-click counts as a second press: two clicks, two toggles.
    /// </summary>
    public class ToggleListView : ListView
    {
        const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONDBLCLK = 0x0203;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDBLCLK) m.Msg = WM_LBUTTONDOWN;
            if (m.Msg == WM_LBUTTONDOWN && CheckBoxes)
            {
                long l = m.LParam.ToInt64();
                Point pt = new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF));
                ListViewHitTestInfo hit = HitTest(pt);
                if (hit.Item != null && hit.Location != ListViewHitTestLocations.StateImage)
                {
                    if (!Focused) Focus();
                    hit.Item.Selected = true;
                    hit.Item.Focused = true;
                    hit.Item.Checked = !hit.Item.Checked;
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
