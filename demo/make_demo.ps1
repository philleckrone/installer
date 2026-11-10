# Demo video for Install_Phils_tools.exe, following tools\demo_video\Demo_Video_Standard.md (a copy
# of Custom_Stylesheet_Editor\demo\make_demo.ps1 with its own capture section).
#
#   powershell -ExecutionPolicy Bypass -File Installer\demo\make_demo.ps1 -Lang en [-NoVoice] [-Reuse] [-CaptureOnly]
#
# The capture drives the real installer on this computer's real Paratext (the user's choice):
# Paratext must be closed. The demo plugin ($Plugin) is removed first and installed again in the
# window; the demo CMS tool ($CmsTool) is removed from the live cms folder and installed again, and
# afterwards put back in the languages it had. With an admin-only plugins folder (Paratext 9 Beta)
# Windows asks for administrator permission at the removal and at the install: someone must click
# Yes. The pictures of Google Drive, the browser and Windows' warnings are the user's screenshots
# (install_*_en.png in this folder); a step whose picture is missing is left out.
param(
    [string]$Lang = 'en',
    [string]$Work = '',
    # The installer as it is shipped.
    [string]$Exe = '',
    # The plugin installed in the video (a key of make_installers.TOOLS) and the CMS tool (catalog id).
    [string]$Plugin = 'BulkNotes',
    [string]$CmsTool = 'short_v_1',
    # An Azure voice (a name ending in 'Neural'; key and region from AZURE_SPEECH_KEY /
    # AZURE_SPEECH_REGION, see azure_tts.ps1) or a Windows voice (System.Speech).
    [string]$Voice = '',
    [int]$Rate = -1,
    [string]$AzureRate = '0%',
    [string]$FFmpeg = '',
    # Use the pictures of the last capture in $Work (nothing is installed or removed).
    [switch]$Reuse,
    # Only capture (then build with -Reuse).
    [switch]$CaptureOnly,
    # Only the plugin steps (ticked, installed) for the plugin videos' install section: the CMS
    # tool is not touched. Use with -CaptureOnly.
    [switch]$PluginOnly,
    [switch]$Zoom,
    [switch]$NoVoice,
    [string]$Music = 'C:\Users\Pleckrone\Dropbox\Camtasia work\Background Music Files\Aetheric - Coconut Kind of Love (freetouse.com).mp3',
    [double]$MusicVolume = 0.30,
    [double]$MusicUnderVoice = 0.025,
    [double]$CoverIntro = 3.5,
    [string]$Series = 'Plugins by Phil',
    [double]$Dissolve = 1.0,
    [double]$Wipe = 2.0,
    [double]$Dip = 1.4
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
if (-not $Exe) { $Exe = Join-Path (Split-Path $here) 'Install_Phils_tools.exe' }
if (-not $Work) { $Work = Join-Path $env:TEMP $(if ($PluginOnly) { "philstools_plugin_${Plugin}_$Lang" } else { "philstools_demo_$Lang" }) }
if (-not $Voice) { $Voice = @{ 'en' = 'en-US-JennyNeural'; 'es' = 'es-MX-JorgeNeural'; 'fr' = 'fr-FR-RemyMultilingualNeural' }[$Lang] }   # the standard's voices

$azure = $Voice -match 'Neural$'
$musicOn = $Music -and (Test-Path -LiteralPath $Music)
if ($Music -and -not $musicOn) { "music file not found, none used: $Music" }
$es = $Lang -like 'es*'; $isFr = $Lang -like 'fr*'
# The window's (and the slides') text in the video's language: English, Spanish, French
# (the editor's own words are in CustomStyEditor\Resources\Strings.<lang>.xaml).
function T($en, $sp, $frText) { if ($es) { $sp } elseif ($isFr -and $null -ne $frText) { $frText } else { $en } }

$narr = Get-Content -LiteralPath (Join-Path $here 'narration.json') -Raw -Encoding UTF8 | ConvertFrom-Json
function Text($id) {
    $t = ($narr.steps | Where-Object { $_.id -eq $id } | Select-Object -First 1).$Lang
    if (-not $t) { throw "narration.json has no '$Lang' text for step $id" }
    $t
}
# Box cues in the narration (the standard, section 8). "{1}" before a phrase shows box 1 (the
# step's boxes, in order) from there on, "{0,2}" boxes 0 and 2, "{}" none; the pointer goes on the
# first box named. Before the first cue no box is shown. A text without cues shows all its boxes
# the whole time. Cues are left out of the subtitles and the voice.
$cueRx = '\{\s*(\d+(\s*,\s*\d+)*)?\s*\}'
function Clean($raw) { if ($raw) { [regex]::Replace($raw, $cueRx, '') } else { $raw } }
# The parts of a step: @{ Pos (in the clean text); Show (box numbers) }, or none without cues.
function Cues($raw) {
    $clean = ''; $marks = @(); $last = 0
    if (-not $raw) { return , @() }
    foreach ($m in [regex]::Matches($raw, $cueRx)) {
        $clean += $raw.Substring($last, $m.Index - $last)
        $show = @()
        if ($m.Groups[1].Success) { $show = @($m.Groups[1].Value -split '\s*,\s*' | ForEach-Object { [int]$_ }) }
        $marks += [pscustomobject]@{ Pos = $clean.Length; Show = $show }
        $last = $m.Index + $m.Length
    }
    if ($marks.Count -and $marks[0].Pos -gt 0) { $marks = @([pscustomobject]@{ Pos = 0; Show = @() }) + $marks }
    , $marks
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms, System.Speech
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DmWin {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    /// <summary>Per-monitor aware, so window rectangles, DWM frames and screen copies all use
    /// physical pixels on monitors with different scales.</summary>
    public static void BeAware() { try { if (SetProcessDpiAwarenessContext((IntPtr)(-4))) return; } catch { } SetProcessDPIAware(); }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out Rect r, int size);
    /// <summary>The visible frame (without the invisible resize borders), in screen pixels.</summary>
    public static Rect Frame(IntPtr h) {
        Rect r;
        if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(Rect))) != 0) GetWindowRect(h, out r);
        return r;
    }
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, int flags);
    /// <summary>Keeps a window above all others (no move, no size), so screen copies show it.</summary>
    public static void TopMost(IntPtr h) { SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); }
    /// <summary>Moves a window (no size change, no activation).</summary>
    public static void MoveTo(IntPtr h, int x, int y) { SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); }
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo {
        public int cb; public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string app, System.Text.StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, int flags, IntPtr env, string dir, ref StartupInfo si, out ProcessInfo pi);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    /// <summary>Starts a program whose first window is shown without being activated
    /// (SW_SHOWNOACTIVATE), so it does not take the keyboard from whatever the user is typing in.</summary>
    public static int StartNoActivate(string exe, string args) {
        var si = new StartupInfo(); si.cb = Marshal.SizeOf(typeof(StartupInfo));
        si.dwFlags = 1; si.wShowWindow = 4;
        ProcessInfo pi;
        if (!CreateProcess(exe, new System.Text.StringBuilder("\"" + exe + "\" " + args), IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out pi))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
        return pi.dwProcessId;
    }
}
'@
[DmWin]::BeAware()
$AE = [Windows.Automation.AutomationElement]
$Scope = [Windows.Automation.TreeScope]
$CT = [Windows.Automation.ControlType]
$root = $AE::RootElement


Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
/// <summary>Window messages to the installer (a WinForms window: UI Automation sees its controls
/// only as panes, so they are driven with messages).</summary>
public static class InMsg {
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, StringBuilder l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R4 r);
    struct R4 { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref Pt p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, IntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p, IntPtr a, IntPtr size, uint type);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, IntPtr n, out IntPtr done);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr a, byte[] b, IntPtr n, out IntPtr done);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    struct Pt { public int X, Y; }

    public static string Text(IntPtr h) {
        int n = (int)SendMessage(h, 0x000E, IntPtr.Zero, IntPtr.Zero);
        StringBuilder sb = new StringBuilder(n + 1); SendMessage(h, 0x000D, (IntPtr)(n + 1), sb); return sb.ToString();
    }
    /// <summary>A window's rectangle on the screen: x, y, width, height.</summary>
    public static int[] Screen(IntPtr h) { R4 r; GetWindowRect(h, out r); return new[] { r.L, r.T, r.R - r.L, r.B - r.T }; }

    /// <summary>Sends a message whose LPARAM points to a buffer in the target process (a RECT or an
    /// LVITEM): the buffer is filled with init, sent, and read back.</summary>
    static byte[] Remote(IntPtr h, uint msg, IntPtr w, byte[] init) {
        uint pid; GetWindowThreadProcessId(h, out pid);
        IntPtr p = OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid);
        if (p == IntPtr.Zero) throw new Exception("OpenProcess failed");
        IntPtr mem = VirtualAllocEx(p, IntPtr.Zero, (IntPtr)init.Length, 0x3000, 0x04);
        try {
            IntPtr done;
            WriteProcessMemory(p, mem, init, (IntPtr)init.Length, out done);
            SendMessage(h, msg, w, mem);
            byte[] back = new byte[init.Length];
            ReadProcessMemory(p, mem, back, (IntPtr)back.Length, out done);
            return back;
        } finally { VirtualFreeEx(p, mem, IntPtr.Zero, 0x8000); CloseHandle(p); }
    }
    /// <summary>A rectangle a control gives in client coordinates (LVM_GETITEMRECT,
    /// LVM_GETSUBITEMRECT, TCM_GETITEMRECT), on the screen: x, y, width, height.</summary>
    public static int[] ClientRect(IntPtr h, uint msg, int index, int left, int top) {
        byte[] b = new byte[16];
        BitConverter.GetBytes(left).CopyTo(b, 0); BitConverter.GetBytes(top).CopyTo(b, 4);
        b = Remote(h, msg, (IntPtr)index, b);
        Pt a = new Pt(); a.X = BitConverter.ToInt32(b, 0); a.Y = BitConverter.ToInt32(b, 4);
        Pt z = new Pt(); z.X = BitConverter.ToInt32(b, 8); z.Y = BitConverter.ToInt32(b, 12);
        ClientToScreen(h, ref a); ClientToScreen(h, ref z);
        return new[] { a.X, a.Y, z.X - a.X, z.Y - a.Y };
    }
    /// <summary>Ticks (or unticks) a list row and makes it the selected, focused row (LVM_SETITEMSTATE).</summary>
    public static void Tick(IntPtr lv, int i, bool on) {
        byte[] b = new byte[96];
        BitConverter.GetBytes(0x0008).CopyTo(b, 0);                                 // mask = LVIF_STATE
        BitConverter.GetBytes((on ? 0x2000 : 0x1000) | 0x3).CopyTo(b, 12);         // checked/unchecked image, selected, focused
        BitConverter.GetBytes(0xF000 | 0x3).CopyTo(b, 16);
        Remote(lv, 0x102B, (IntPtr)i, b);
    }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Pt p, uint f);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr m, int t, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
    /// <summary>The scale (dpi) of the monitor holding a point.</summary>
    public static uint MonitorDpi(int x, int y) {
        Pt p = new Pt(); p.X = x; p.Y = y; uint dx, dy;
        GetDpiForMonitor(MonitorFromPoint(p, 2), 0, out dx, out dy); return dx;
    }
    public static bool Ticked(IntPtr lv, int i) { return (((int)SendMessage(lv, 0x102C, (IntPtr)i, (IntPtr)0xF000)) >> 12) == 2; }
}
'@

# ------------------------------------------------------------ the capture
$shots = Join-Path $Work 'shots'
$frames = Join-Path $Work 'frames'
$stepsFile = Join-Path $Work 'steps.xml'
if ($Reuse) {
    if (-not (Test-Path -LiteralPath $stepsFile)) { throw "-Reuse: no capture in $Work (run once without -Reuse)" }
    $script:steps = @(Import-Clixml -LiteralPath $stepsFile)
    if (Test-Path $frames) { Remove-Item -Recurse -Force $frames }
    New-Item -ItemType Directory -Force $frames | Out-Null
    Get-ChildItem -LiteralPath $Work -File | Where-Object { $_.Name -ne 'steps.xml' } | Remove-Item -Force
    "reusing the capture of $((Get-Item -LiteralPath $stepsFile).LastWriteTime): $(($script:steps | ForEach-Object { $_.Id }) -join ' ')"
}
else {
if (Get-Process -Name Paratext -ErrorAction SilentlyContinue) { throw 'Paratext is running: close it first (the demo installs into the real Paratext).' }
if (Test-Path $Work) { Remove-Item -Recurse -Force $Work }
New-Item -ItemType Directory -Force $shots, $frames | Out-Null

# What the command line says is installed now: the row order of each list, and the CMS tool's
# languages (to put back afterwards).
function Status {
    $lines = @(& $Exe --cli status | ForEach-Object { "$_" })
    $p = @($lines | Where-Object { $_ -like "P`t*" } | ForEach-Object { ($_ -split "`t")[1] })
    $ps = @($lines | Where-Object { $_ -like "P`t*" } | ForEach-Object { ($_ -split "`t")[3] })
    $c = @($lines | Where-Object { $_ -like "C`t*" })
    @{ Plugins = $p; PluginStatus = $ps; Cms = @($c | ForEach-Object { ($_ -split "`t")[1] }); CmsLines = $c }
}
$st0 = Status
$pluginRow = [array]::IndexOf($st0.Plugins, $Plugin)
$cmsRow = [array]::IndexOf($st0.Cms, $CmsTool)
if ($pluginRow -lt 0) { throw "no plugin $Plugin in the installer" }
if ($cmsRow -lt 0) { throw "no CMS tool $CmsTool in the installer" }
$pluginBefore = $st0.PluginStatus[$pluginRow]
$cmsLangsBefore = @((($st0.CmsLines[$cmsRow] -split "`t") + @('', '', ''))[3] -split ',' | Where-Object { $_ })
"before: $Plugin row $pluginRow; $CmsTool row $cmsRow, languages '$($cmsLangsBefore -join ',')'"

# The demo starts from "not installed": both are removed first (the plugin's removal may ask for
# administrator permission: click Yes).
"removing $Plugin (Windows may ask for administrator permission: click Yes)" | Out-Host
& $Exe --cli remove $Plugin | Out-Null
if ($LASTEXITCODE -ne 0) { throw "could not remove $Plugin first" }
if (-not $PluginOnly) { & $Exe --cli remove $CmsTool | Out-Null }

$script:proc = $null
$script:hwnd = [IntPtr]::Zero
function Restore {
    # Everything back as it was: the CMS tool in the languages it had (or removed, if it had none),
    # and the plugin removed again if it was not installed before.
    if (-not $PluginOnly) {
        & $Exe --cli remove $CmsTool | Out-Null
        foreach ($l in $cmsLangsBefore) { & $Exe --cli install $CmsTool --cmslang $l | Out-Null }
    }
    if ($pluginBefore -eq 'NotInstalled') { & $Exe --cli remove $Plugin | Out-Null }
    elseif ((Status).PluginStatus[$pluginRow] -ne 'Installed') { & $Exe --cli install $Plugin | Out-Null }
}
trap {
    if ($script:proc -and -not $script:proc.HasExited) { $script:proc.Kill(); $script:proc.WaitForExit(5000) | Out-Null }
    Restore
    throw $_
}

$script:proc = Get-Process -Id ([DmWin]::StartNoActivate($Exe, "--lang $Lang"))
for ($i = 0; $i -lt 100 -and $script:hwnd -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Milliseconds 200; $script:proc.Refresh(); $script:hwnd = $script:proc.MainWindowHandle
}
if ($script:hwnd -eq [IntPtr]::Zero) { throw 'the installer window did not appear' }
# On a monitor at the system scale: the installer is system-DPI aware, so on a monitor with another
# scale Windows stretches its picture while the positions it reports for list rows and tabs stay
# unstretched (the boxes came out wrong). Preferably one without the mouse cursor (hover effects).
$cursor = [Windows.Forms.Cursor]::Position
$sysDpi = [InMsg]::GetDpiForSystem()
$atScale = @([Windows.Forms.Screen]::AllScreens | Where-Object { [InMsg]::MonitorDpi($_.Bounds.X + 10, $_.Bounds.Y + 10) -eq $sysDpi })
$screen = $atScale | Where-Object { -not $_.Bounds.Contains($cursor) } | Select-Object -First 1
if (-not $screen) { $screen = $atScale | Select-Object -First 1; 'the mouse cursor is on the capture monitor: keep it away from the installer window' | Out-Host }
if ($screen) { [DmWin]::MoveTo($script:hwnd, $screen.WorkingArea.X + 20, $screen.WorkingArea.Y + 20) }
[DmWin]::TopMost($script:hwnd)
Start-Sleep -Milliseconds 2500

# The window's controls (visible ones only): @{ Class; Name; H; Rect }.
function Controls {
    $all = $AE::FromHandle($script:hwnd).FindAll($Scope::Descendants, [Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) {
        $c = $e.Current
        if ($c.IsOffscreen -or $c.NativeWindowHandle -eq 0) { continue }
        [pscustomobject]@{ Class = $c.ClassName; Name = $c.Name; H = [IntPtr]$c.NativeWindowHandle; Rect = ([InMsg]::Screen([IntPtr]$c.NativeWindowHandle)) }
    }
}
function Ctl($classLike, $nameLike = '*') {
    $c = Controls | Where-Object { $_.Class -like $classLike -and $_.Name -like $nameLike } | Select-Object -First 1
    if (-not $c) { throw "control $classLike '$nameLike' not found" }
    $c
}
function ListView { Ctl '*SysListView32*' }
# The edit boxes of the visible tab: the description box (higher) and the log (lowest).
function Edits { @(Controls | Where-Object { $_.Class -like '*EDIT*' } | Sort-Object { $_.Rect[1] }) }
function LogText { [InMsg]::Text((Edits)[-1].H) }
function Click($h) { [InMsg]::PostMessage($h, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 700 }

$installText = T 'Install / update ticked' 'Instalar / actualizar los marcados' "Installer / mettre $([char]0xE0) jour"
$removeText = T 'Remove ticked' 'Quitar los marcados' "Supprimer les coch$([char]0xE9)s"
$foundText = T 'Paratext 9 found*' 'Paratext 9 encontrado*' 'Paratext 9 trouv*'
$restartRx = 'Start \(or restart\) Paratext|Inicie \(o reinicie\) Paratext|D.marrez \(ou red.marrez\) Paratext'

function Row($lv, $i) { [InMsg]::ClientRect($lv.H, 0x100E, $i, 0, 0) }                  # LVM_GETITEMRECT, LVIR_BOUNDS
function Cell($lv, $i, $col) { [InMsg]::ClientRect($lv.H, 0x1038, $i, 0, $col) }        # LVM_GETSUBITEMRECT
# A column of the list: the header strip above the rows (in the column's x range) and the cells
# of the rows on screen.
function Column($lv, $col) {
    $top = [int][InMsg]::SendMessage($lv.H, 0x1027, [IntPtr]::Zero, [IntPtr]::Zero)      # LVM_GETTOPINDEX
    $per = [int][InMsg]::SendMessage($lv.H, 0x1028, [IntPtr]::Zero, [IntPtr]::Zero)      # LVM_GETCOUNTPERPAGE
    $rects = @()
    for ($i = $top; $i -lt $top + $per; $i++) { $rects += , (Cell $lv $i $col) }
    $first = $rects[0]
    $rects += , @($first[0], $lv.Rect[1], $first[2], ($first[1] - $lv.Rect[1]))
    , $rects
}
function Show($lv, $i) { [InMsg]::SendMessage($lv.H, 0x1013, [IntPtr]$i, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 300 }   # LVM_ENSUREVISIBLE

# ------------------------------------------------------------ capturing a step
$script:steps = @()
function Capture {
    $f = [DmWin]::Frame($script:hwnd)
    $bmp = New-Object Drawing.Bitmap ($f.R - $f.L), ($f.B - $f.T)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($f.L, $f.T, 0, 0, $bmp.Size)
    $g.Dispose()
    @{ Bmp = $bmp; Rect = $f }
}
# A box round some screen rectangles (x, y, w, h), in the capture's pixels.
function Box($rects, $f) {
    $x1 = 1e9; $y1 = 1e9; $x2 = -1e9; $y2 = -1e9
    foreach ($r in $rects) {
        $x1 = [Math]::Min($x1, $r[0]); $y1 = [Math]::Min($y1, $r[1])
        $x2 = [Math]::Max($x2, $r[0] + $r[2]); $y2 = [Math]::Max($y2, $r[1] + $r[3])
    }
    , @([int]($x1 - $f.L), [int]($y1 - $f.T), [int]($x2 - $x1), [int]($y2 - $y1))
}
# $groups: a list of rectangle lists, one orange box each. $pointer: the box pointed at, or -1.
function Step($id, $groups, $pointer = -1, $dialog = $null) {
    Start-Sleep -Milliseconds 500
    $shot = Capture
    $boxes = @()
    foreach ($grp in $groups) { $boxes += , (Box @($grp) $shot.Rect) }
    $file = Join-Path $shots "$id.png"
    $shot.Bmp.Save($file, [Drawing.Imaging.ImageFormat]::Png)
    $shot.Bmp.Dispose()
    $dl = $null
    if ($dialog) { $dl = Box @(, $dialog) $shot.Rect }
    $script:steps += [pscustomobject]@{ Id = $id; Shot = $file; Boxes = $boxes; Pointer = [int]$pointer; Dialog = $dl; Rows = -1; PointAt = $null }
    "captured $id"
}
function WaitForLog {
    for ($i = 0; $i -lt 600; $i++) {
        Start-Sleep -Milliseconds 500
        if ((LogText) -match $restartRx) { Start-Sleep -Milliseconds 1500; return }
    }
    throw 'the install did not finish within 5 minutes'
}

# ------------------------------------------------------------ the demo
# The window as it opens: the language box, the Paratext line, the two tabs.
$tab = Ctl '*SysTabControl32*'
$langBox = @(Controls | Where-Object { $_.Class -like '*COMBOBOX*' } | Sort-Object { $_.Rect[1] })[0]
$found = Ctl '*STATIC*' $foundText
$tabs = @(([InMsg]::ClientRect($tab.H, 0x130A, 0, 0, 0)), ([InMsg]::ClientRect($tab.H, 0x130A, 1, 0, 0)))
Step 'w1_window' @(@(, $langBox.Rect), @(, $found.Rect), $tabs) -1

# The Status column.
$lv = ListView
Step 'w2_status' @(, (Column $lv 2)) -1

# The plugin ticked: its row, the description box, the Install button.
Show $lv $pluginRow
[InMsg]::Tick($lv.H, $pluginRow, $true)
Start-Sleep -Milliseconds 600
if (-not [InMsg]::Ticked($lv.H, $pluginRow)) { throw "the row of $Plugin is not ticked" }
$install = Ctl '*BUTTON*' $installText
Step 'w3_tick' @(@(, (Row $lv $pluginRow)), @(, (Edits)[0].Rect), @(, $install.Rect)) 0

# Install: Windows may ask for administrator permission (click Yes).
"installing $Plugin (Windows may ask for administrator permission: click Yes)" | Out-Host
Click $install.H
WaitForLog
if ([InMsg]::Ticked($lv.H, $pluginRow)) { throw 'the row is still ticked after the install' }
Show $lv $pluginRow
Step 'w5_done' @(@(, (Cell $lv $pluginRow 2)), @(, (Edits)[-1].Rect)) -1

if (-not $PluginOnly) {
# The CMS tab: the cms folder and the language of the tools.
[InMsg]::SendMessage($tab.H, 0x1330, [IntPtr]1, [IntPtr]::Zero) | Out-Null       # TCM_SETCURFOCUS
Start-Sleep -Milliseconds 1000
$lv = ListView
if ($lv.Rect[1] -lt 0) { throw 'the CMS tab did not open' }
$folder = @(Controls | Where-Object { $_.Class -like '*COMBOBOX*' } | Sort-Object { $_.Rect[1] })[1]
$folderLabel = Ctl '*STATIC*' (T 'cms folder:' 'Carpeta cms:' 'Dossier cms :')
$rbEn = Ctl '*BUTTON*' 'English'
$rbEs = Ctl '*BUTTON*' "Espa$([char]0xF1)ol"
$langLabel = Ctl '*STATIC*' (T 'Install the CMS tools in:' 'Instalar las herramientas CMS en:' 'Installer les outils CMS en :')
Click $(if ($es) { $rbEs.H } else { $rbEn.H })
Step 'w6_cms' @(@($folderLabel.Rect, $folder.Rect), @($langLabel.Rect, $rbEn.Rect, $rbEs.Rect)) -1

# A CMS tool ticked and installed.
Show $lv $cmsRow
[InMsg]::Tick($lv.H, $cmsRow, $true)
Start-Sleep -Milliseconds 600
Click $install.H
WaitForLog
Show $lv $cmsRow
Step 'w7_cmsdone' @(@(, (Row $lv $cmsRow)), @(, (Edits)[-1].Rect)) 0

# Remove: the tool ticked, Remove clicked, the question shown; answered No.
[InMsg]::Tick($lv.H, $cmsRow, $true)
Start-Sleep -Milliseconds 500
$remove = Ctl '*BUTTON*' $removeText
Click $remove.H
Start-Sleep -Milliseconds 1200
$dlg = $null
for ($i = 0; $i -lt 30 -and -not $dlg; $i++) {
    $byProcess = New-Object Windows.Automation.PropertyCondition ($AE::ProcessIdProperty, $script:proc.Id)
    $dlg = @($root.FindAll($Scope::Descendants, $byProcess)) | Where-Object { $_.Current.ClassName -eq '#32770' } | Select-Object -First 1
    if (-not $dlg) { Start-Sleep -Milliseconds 200 }
}
if (-not $dlg) { throw 'the question before removing did not appear' }
$dlgH = [IntPtr]$dlg.Current.NativeWindowHandle
$df = [DmWin]::Frame($dlgH); $dlgRect = @($df.L, $df.T, ($df.R - $df.L), ($df.B - $df.T))
Step 'w8_remove' @(@(, (Row $lv $cmsRow)), @(, $remove.Rect), @(, $dlgRect)) 1
[InMsg]::PostMessage($dlgH, 0x0111, [IntPtr]7, [IntPtr]::Zero) | Out-Null      # WM_COMMAND IDNO
Start-Sleep -Milliseconds 800
}

$script:proc.CloseMainWindow() | Out-Null
$script:proc.WaitForExit(10000) | Out-Null
if (-not $script:proc.HasExited) { $script:proc.Kill(); $script:proc.WaitForExit(5000) | Out-Null }
Restore
$after = Status
"after: $(($after.CmsLines[$cmsRow]))"
$script:steps | Export-Clixml -LiteralPath $stepsFile

# The short install section for the plugin videos (the standard, section 9): this language's
# window pictures and boxes, beside the shared screenshots.
$shared = Join-Path (Split-Path (Split-Path $here)) 'tools\demo_video\install'
New-Item -ItemType Directory -Force $shared | Out-Null
foreach ($s in $script:steps | Where-Object { $_.Id -in 'w3_tick', 'w5_done' }) {
    Copy-Item -LiteralPath $s.Shot -Destination (Join-Path $shared ("{0}_{1}_{2}.png" -f $s.Id, $Plugin, $Lang)) -Force
    $s | Select-Object Id, Boxes, Pointer | ConvertTo-Json -Depth 5 -Compress |
        Set-Content -LiteralPath (Join-Path $shared ("{0}_{1}_{2}.json" -f $s.Id, $Plugin, $Lang)) -Encoding UTF8
}
"short-section pictures written to $shared"
}
if ($CaptureOnly) { "captured in ${Work}; build the video with -Reuse"; return }

# The user's screenshots (English; every language uses them) and the Paratext Tools menu. Boxes
# are in each picture's pixels (x, y, width, height); Pointer = the box pointed at.
$pictures = @{
    'd1_folder'    = @{ File = 'install_1_folder';    Boxes = @(@(526, 26, 254, 42), @(44, 350, 262, 44)); Pointer = 1 }
    'd2_download'  = @{ File = 'install_1_folder';    Boxes = @(@(44, 350, 262, 44), @(380, 94, 34, 34)); Pointer = 1 }
    'd3_dangerous' = @{ File = 'install_2_dangerous'; Boxes = @(, @(472, 174, 194, 42)); Pointer = 0 }
    'd4_save'      = @{ File = 'install_3_save';      Boxes = @(@(156, 440, 1042, 28), @(947, 574, 112, 34)); Pointer = 1 }
    'd5_warning'   = @{ File = 'install_4_warning';   Boxes = @(@(284, 210, 212, 30), @(518, 190, 88, 26)); Pointer = 1 }
    'd6_runanyway' = @{ File = 'install_5_runanyway'; Boxes = @(@(30, 166, 262, 54), @(352, 550, 142, 48)); Pointer = 1 }
    'w4_uac'       = @{ File = 'install_6_uacprompt';       Boxes = @(); Pointer = -1 }
}
# Bulk Note Editing's Tools menu (Bulk_Notes\demo\tools_menu_<lang>.png), with its boxes there.
$menuBoxes = @{
    'en' = @(@(1036, 102, 54, 24), @(1038, 304, 144, 24))
    'es' = @(@(1126, 102, 112, 24), @(1128, 304, 186, 24))
    'fr' = @(@(1284, 102, 52, 24), @(1286, 304, 204, 24))
}
$menuShot = Join-Path (Split-Path (Split-Path $here)) "Bulk_Notes\demo\tools_menu_$Lang.png"
$order0 = @('d1_folder', 'd2_download', 'd3_dangerous', 'd4_save', 'd5_warning', 'd6_runanyway',
            'w1_window', 'w2_status', 'w3_tick', 'w4_uac', 'w5_done', 'w6_cms', 'w7_cmsdone', 'w8_remove', 'w9_ready')
$all = @()
foreach ($id in $order0) {
    $cap = $script:steps | Where-Object { $_.Id -eq $id } | Select-Object -First 1
    if ($cap) { $all += $cap; continue }
    if ($id -eq 'w9_ready') {
        if (Test-Path -LiteralPath $menuShot) {
            $mb = @(); foreach ($b in $menuBoxes[$Lang]) { $mb += , $b }
            $all += [pscustomobject]@{ Id = $id; Shot = $menuShot; Boxes = $mb; Pointer = 1; Dialog = $null; Rows = -1 }
        }
        else { "no $menuShot : step $id is left out" }
        continue
    }
    $p = $pictures[$id]
    if (-not $p) { continue }
    $shot = Join-Path $here ("{0}_{1}.png" -f $p.File, $Lang)
    if (-not (Test-Path -LiteralPath $shot)) { $shot = Join-Path $here ("{0}_en.png" -f $p.File) }
    if (-not (Test-Path -LiteralPath $shot)) { "no $shot : step $id is left out"; continue }
    $bx = @(); foreach ($b in $p.Boxes) { $bx += , $b }
    $all += [pscustomobject]@{ Id = $id; Shot = $shot; Boxes = $bx; Pointer = $p.Pointer; Dialog = $null; Rows = -1 }
}
$script:steps = $all


# ------------------------------------------------------------ frames
$W = 1920; $H = 1080; $top = 96; $bottomBand = 190
# The "Sky" design (the user's choice): deep blue cover, light-to-mid blue steps, gold accents.
function Hex($h) { [Drawing.ColorTranslator]::FromHtml($h) }
$navy = Hex '#1F3A5F'          # headline bar
$orange = [Drawing.Color]::FromArgb(255, 140, 0)   # boxes round the controls
$gold = Hex '#F2B544'          # accents
$band = Hex '#18222E'          # subtitle band
$bgTop = Hex '#EEF7FF'; $bgBottom = Hex '#94BEE4'          # steps: the gradient
$coverTop = Hex '#2C5A9A'; $coverBottom = Hex '#0B1830'    # cover and closing slide
function Gradient($g, $x, $y, $w, $h, $c1, $c2) {
    $br = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.RectangleF $x, ($y - 1), $w, ($h + 2)), $c1, $c2, 90.0
    $g.FillRectangle($br, [float]$x, [float]$y, [float]$w, [float]$h)
    $br.Dispose()
}
function RoundRect($x, $y, $w, $h, $r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc([float]$x, [float]$y, $d, $d, 180, 90); $p.AddArc([float]($x + $w - $d), [float]$y, $d, $d, 270, 90)
    $p.AddArc([float]($x + $w - $d), [float]($y + $h - $d), $d, $d, 0, 90); $p.AddArc([float]$x, [float]($y + $h - $d), $d, $d, 90, 90)
    $p.CloseFigure(); $p
}
function NewFrame { $f = New-Object Drawing.Bitmap $W, $H; $g = [Drawing.Graphics]::FromImage($f); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'; @{ F = $f; G = $g } }
function Centered($g, $text, $font, $brush, $rect) {
    $sf = New-Object Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString($text, $font, $brush, $rect, $sf)
}
# A font of the given size, or smaller until the text fits on one line in $maxWidth pixels
# (long names, e.g. "Edicion de notas en lote", were cut off on the cover).
function FitFont($g, $text, $family, $size, $maxWidth) {
    $f = New-Object Drawing.Font $family, $size
    while ($size -gt 36 -and $g.MeasureString($text, $f).Width -gt $maxWidth) {
        $f.Dispose(); $size -= 2; $f = New-Object Drawing.Font $family, $size
    }
    $f
}
function Card($id, $file) {
    $t = Text $id
    $fr = NewFrame; $g = $fr.G
    Gradient $g 0 0 $W $H $coverTop $coverBottom
    $light = New-Object Drawing.SolidBrush $gold
    $grey = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(190, 205, 225))
    if ($id -eq 'title') {
        # The cover: a framed panel (gold border) with a tag on its top edge; the plugin's name
        # large and white, a gold rule, then the series smaller (the user's order).
        $px = 360; $py = 290; $pw = $W - 720; $ph = 470
        $panel = RoundRect $px $py $pw $ph 28
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 255, 255, 255))), $panel)
        $g.DrawPath((New-Object Drawing.Pen $gold, 4), $panel)
        $tagFont = New-Object Drawing.Font 'Segoe UI Semibold', 20
        $tagText = T 'PARATEXT TOOLS' 'HERRAMIENTAS DE PARATEXT' 'OUTILS PARATEXT'
        $tw = $g.MeasureString($tagText, $tagFont).Width + 44; $th = 48; $tx = ($W - $tw) / 2; $ty = $py - $th / 2
        $g.FillPath((New-Object Drawing.SolidBrush $gold), (RoundRect $tx $ty $tw $th 24))
        Centered $g $tagText $tagFont (New-Object Drawing.SolidBrush $coverBottom) (New-Object Drawing.RectangleF $tx, $ty, $tw, $th)
        Centered $g $t.title (FitFont $g $t.title 'Segoe UI Semibold' 80 ($pw - 60)) ([Drawing.Brushes]::White) (New-Object Drawing.RectangleF $px, ($py + 60), $pw, 170)
        $g.FillRectangle($light, [float]($W / 2 - 160), [float]($py + 250), 320, 5)
        Centered $g $Series (New-Object Drawing.Font 'Segoe UI', 50) $light (New-Object Drawing.RectangleF $px, ($py + 280), $pw, 120)
    }
    elseif ($id -like 'sec_*') {
        # A section slide (the user's "subtitle" slide): the cover's panel, its title large and
        # white, a gold rule, then the plugin's name smaller.
        $px = 360; $py = 290; $pw = $W - 720; $ph = 470
        $panel = RoundRect $px $py $pw $ph 28
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 255, 255, 255))), $panel)
        $g.DrawPath((New-Object Drawing.Pen $gold, 4), $panel)
        Centered $g $t.title (FitFont $g $t.title 'Segoe UI Semibold' 80 ($pw - 60)) ([Drawing.Brushes]::White) (New-Object Drawing.RectangleF $px, ($py + 60), $pw, 170)
        $g.FillRectangle($light, [float]($W / 2 - 160), [float]($py + 250), 320, 5)
        # The plugin's name, smaller when it would not fit on one line (the French name was cut).
        $name = (Text 'title').title
        Centered $g $name (FitFont $g $name 'Segoe UI' 50 ($pw - 60)) $light (New-Object Drawing.RectangleF $px, ($py + 280), $pw, 120)
    }
    else {
        $f1 = New-Object Drawing.Font 'Segoe UI Semibold', 72
        $f2 = New-Object Drawing.Font 'Segoe UI', 34
        # Smaller when a long name would not fit on one line (the French name of this tool).
        $f1 = FitFont $g $t.title 'Segoe UI Semibold' 72 ($W - 200)
        Centered $g $t.title $f1 ([Drawing.Brushes]::White) (New-Object Drawing.RectangleF 80, 300, ($W - 160), 160)
        $g.FillRectangle($light, [float]($W / 2 - 160), 462, 320, 5)
        # The key points on one line, or each on its own line when they don't fit (a wrap fell in
        # the middle of a point in Spanish and French).
        $sub = $t.sub
        $subH = 120
        if ($g.MeasureString($sub, $f2).Width -gt ($W - 160)) { $sub = ($sub -split ('\s+' + [char]0xB7 + '\s+')) -join "`n"; $subH = 200 }
        Centered $g $sub $f2 $light (New-Object Drawing.RectangleF 80, 480, ($W - 160), $subH)
        # Credits, in small print.
        $credits = @(T 'Created with Claude AI' 'Creado con Claude AI' "Cr$([char]0xE9)$([char]0xE9) avec Claude AI")
        if (-not $NoVoice -and $azure) { $credits += T 'Narration: AI-generated voice (Microsoft Azure AI Speech)' "Narraci$([char]0xF3)n: voz generada por IA (Microsoft Azure AI Speech)" "Narration : voix g$([char]0xE9)n$([char]0xE9)r$([char]0xE9)e par IA (Microsoft Azure AI Speech)" }
        if ($musicOn) { $credits += T 'Music: Coconut Kind of Love by Aetheric (freetouse.com)' "M$([char]0xFA)sica: Coconut Kind of Love de Aetheric (freetouse.com)" 'Musique : Coconut Kind of Love par Aetheric (freetouse.com)' }
        $f3 = New-Object Drawing.Font 'Segoe UI', 22
        $y = $H - 70 - 42 * $credits.Count
        foreach ($c in $credits) { Centered $g $c $f3 $grey (New-Object Drawing.RectangleF 80, $y, ($W - 160), 42); $y += 42 }
    }
    $g.Dispose(); $fr.F.Save($file, [Drawing.Imaging.ImageFormat]::Png); $fr.F.Dispose()
}
function Pointer($g, $x, $y) {
    # A mouse arrow with its tip at (x, y).
    $s = 1.6
    $pts = @(@(0, 0), @(0, 25), @(6, 19), @(11, 30), @(15, 28), @(10, 18), @(18, 18)) | ForEach-Object { New-Object Drawing.PointF ($x + $_[0] * $s), ($y + $_[1] * $s) }
    $g.FillPolygon([Drawing.Brushes]::White, [Drawing.PointF[]]$pts)
    $pen = New-Object Drawing.Pen ([Drawing.Color]::Black), 2
    $g.DrawPolygon($pen, [Drawing.PointF[]]$pts)
}
# The window area of a frame: the band between the headline and the subtitles, less margins.
$areaX = 40; $areaY = $top + 22; $areaW = $W - 80; $areaH = $H - $top - $bottomBand - 44
# The view of the whole window: the shot widened (or heightened) to the area's shape.
function FullView($iw, $ih) {
    $ar = $areaW / $areaH
    if ($iw / $ih -lt $ar) { $w0 = $ih * $ar; , @((($iw - $w0) / 2), 0, $w0, $ih) }
    else { $h0 = $iw / $ar; , @(0, (($ih - $h0) / 2), $iw, $h0) }
}
# The close-up of a step: the boxes (and the dialog) with a margin, in the area's shape, at most
# twice the size of the full view; $null when that would hardly be closer than the full view.
function ZoomView($st, $iw, $ih) {
    $rects = @($st.Boxes)
    if ($st.Dialog) { $rects += , $st.Dialog }
    if ($rects.Count -eq 0) { return $null }
    $x1 = 1e9; $y1 = 1e9; $x2 = -1e9; $y2 = -1e9
    foreach ($r in $rects) {
        $x1 = [Math]::Min($x1, $r[0]); $y1 = [Math]::Min($y1, $r[1])
        $x2 = [Math]::Max($x2, $r[0] + $r[2]); $y2 = [Math]::Max($y2, $r[1] + $r[3])
    }
    $pad = 0.05 * $iw
    $x1 -= $pad; $y1 -= $pad; $x2 += $pad; $y2 += $pad
    $ar = $areaW / $areaH
    $w = $x2 - $x1; $h = $y2 - $y1
    if ($w / $h -lt $ar) { $w = $h * $ar } else { $h = $w / $ar }
    $full = FullView $iw $ih
    $maxZoom = 2.0
    if ($full[2] / $w -gt $maxZoom) { $w = $full[2] / $maxZoom; $h = $w / $ar }
    if ($full[2] / $w -lt 1.15) { return $null }
    $cx = ($x1 + $x2) / 2; $cy = ($y1 + $y2) / 2
    $x = $cx - $w / 2; $y = $cy - $h / 2
    if ($w -le $iw) { $x = [Math]::Max(0, [Math]::Min($x, $iw - $w)) } else { $x = ($iw - $w) / 2 }
    if ($h -le $ih) { $y = [Math]::Max(0, [Math]::Min($y, $ih - $h)) } else { $y = ($ih - $h) / 2 }
    , @($x, $y, $w, $h)
}
# Between two views, at s (0..1): the centre moves in a straight line, the size changes
# geometrically, so the zoom feels even.
function MixView($a, $b, $s) {
    $w = $a[2] * [Math]::Pow($b[2] / $a[2], $s); $h = $a[3] * [Math]::Pow($b[3] / $a[3], $s)
    $cx = ($a[0] + $a[2] / 2) * (1 - $s) + ($b[0] + $b[2] / 2) * $s
    $cy = ($a[1] + $a[3] / 2) * (1 - $s) + ($b[1] + $b[3] / 2) * $s
    , @(($cx - $w / 2), ($cy - $h / 2), $w, $h)
}
# $part: one part of a step with cues (its Show = the boxes shown); $null = all boxes.
function StepFrame($st, $num, $total, $file, $view = $null, $part = $null) {
    $t = Text $st.Id
    $fr = NewFrame; $g = $fr.G
    Gradient $g 0 $top $W ($H - $top - $bottomBand) $bgTop $bgBottom
    # headline, with a gold line under it
    $g.FillRectangle((New-Object Drawing.SolidBrush $navy), 0, 0, $W, $top)
    $g.FillRectangle((New-Object Drawing.SolidBrush $gold), 0, ($top - 5), $W, 5)
    $fh = New-Object Drawing.Font 'Segoe UI Semibold', 30
    $g.DrawString($t.title, $fh, [Drawing.Brushes]::White, 40, 22)
    $fn = New-Object Drawing.Font 'Segoe UI', 20
    $sf = New-Object Drawing.StringFormat; $sf.Alignment = 'Far'
    $g.DrawString("$num / $total", $fn, (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(190, 205, 225))), (New-Object Drawing.RectangleF 0, 34, ($W - 40), 40), $sf)
    # the window: the view (a rectangle of the shot) fills the area; float coordinates, so a
    # zoom moves smoothly. Whatever falls outside the band between headline and subtitles is cut.
    $img = [Drawing.Image]::FromFile($st.Shot)
    if (-not $view) { $view = FullView $img.Width $img.Height }
    $sc = [Math]::Min($areaW / $view[2], $areaH / $view[3])
    $ox = $areaX + ($areaW - $view[2] * $sc) / 2 - $view[0] * $sc
    $oy = $areaY + ($areaH - $view[3] * $sc) / 2 - $view[1] * $sc
    $iw = $img.Width * $sc; $ih = $img.Height * $sc
    $g.SetClip((New-Object Drawing.RectangleF 0, $top, $W, ($H - $top - $bottomBand)))
    # a soft shadow (spread, faint layers) and a white frame, so the grey window floats on the page
    for ($k = 12; $k -ge 2; $k -= 2) {
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(12, 10, 30, 60))), (RoundRect ($ox - $k + 6) ($oy - $k + 10) ($iw + 2 * $k) ($ih + 2 * $k) (10 + $k)))
    }
    $g.FillPath([Drawing.Brushes]::White, (RoundRect ($ox - 6) ($oy - 6) ($iw + 12) ($ih + 12) 10))
    $g.DrawImage($img, [float]$ox, [float]$oy, [float]$iw, [float]$ih)
    $pen = New-Object Drawing.Pen $orange, 5
    if ($st.Dialog) {
        $d = $st.Dialog
        $g.DrawRectangle((New-Object Drawing.Pen ([Drawing.Color]::FromArgb(31, 58, 95)), 4), [float]($ox + $d[0] * $sc - 4), [float]($oy + $d[1] * $sc - 4), [float]($d[2] * $sc + 8), [float]($d[3] * $sc + 8))
    }
    $i = 0; $pointAtShown = -not $part
    foreach ($b in $st.Boxes) {
        $shown = (-not $part) -or ($part.Show -contains $i)
        if ($shown) {
            $bx = $ox + $b[0] * $sc - 6; $by = $oy + $b[1] * $sc - 6; $bwid = $b[2] * $sc + 12; $bhei = $b[3] * $sc + 12
            $g.DrawRectangle($pen, [float]$bx, [float]$by, [float]$bwid, [float]$bhei)
            # With cues, the pointer (if the step has one) is on the first box named.
            $pointHere = if ($part) { $st.Pointer -ge 0 -and $part.Show[0] -eq $i } else { $i -eq $st.Pointer }
            if ($pointHere) { Pointer $g ($bx + $bwid * 0.6) ($by + $bhei * 0.6) }
            if ($st.PointAt) {
                $pa = $st.PointAt; $cx = $pa[0] + $pa[2] / 2; $cy = $pa[1] + $pa[3] / 2
                if ($cx -ge $b[0] -and $cx -le $b[0] + $b[2] -and $cy -ge $b[1] -and $cy -le $b[1] + $b[3]) { $pointAtShown = $true }
            }
        }
        $i++
    }
    if ($st.PointAt -and $pointAtShown) {
        $pa = $st.PointAt
        # at the right end of the item, so its text stays readable
        Pointer $g ($ox + ($pa[0] + $pa[2] * 0.88) * $sc) ($oy + ($pa[1] + $pa[3] * 0.5) * $sc)
    }
    $g.ResetClip()
    $iwShot = $img.Width; $ihShot = $img.Height
    $img.Dispose()
    # subtitles
    $g.FillRectangle((New-Object Drawing.SolidBrush $band), 0, ($H - $bottomBand), $W, $bottomBand)
    $fs = New-Object Drawing.Font 'Segoe UI', 27
    Centered $g (Clean $t.text) $fs ([Drawing.Brushes]::White) (New-Object Drawing.RectangleF 70, ($H - $bottomBand + 8), ($W - 140), ($bottomBand - 16))
    $g.Dispose(); $fr.F.Save($file, [Drawing.Imaging.ImageFormat]::Png); $fr.F.Dispose()
}

$order = @('title')
foreach ($st in $script:steps) {
    $order += $st.Id
}
$order += 'end'
# Slides (cover, section slides, closing slide) have no step number.
function IsCard($id) { $id -eq 'title' -or $id -eq 'end' -or $id -like 'sec_*' }
$stepIds = @($order | Where-Object { -not (IsCard $_) })
# Zoom timing: it starts when the dissolve into the step is over, and takes $zoomTime seconds.
$zoomStart = 1.3; $zoomTime = 1.6; $zoomFrames = [int]($zoomTime * 30)
$zooms = @{}; $partFrames = @{}
$n = 0
foreach ($id in $order) {
    $file = Join-Path $frames ('{0:D2}_{1}.png' -f $n, $id)
    if (IsCard $id) { Card $id $file }
    else {
        $st = $script:steps | Where-Object { $_.Id -eq $id }
        $num = [array]::IndexOf($stepIds, $id) + 1
        StepFrame $st $num $stepIds.Count $file
        $cues = Cues (Text $id).text
        if ($cues.Count) {
            $pf = @()
            for ($k = 0; $k -lt $cues.Count; $k++) {
                $cfile = Join-Path $frames ('{0:D2}_{1}_c{2}.png' -f $n, $id, $k)
                StepFrame $st $num $stepIds.Count $cfile $null $cues[$k]
                $pf += $cfile
            }
            $partFrames[$n] = $pf
            "cues $id : $(($cues | ForEach-Object { '{' + ($_.Show -join ',') + '}' }) -join ' ')"
        }
        elseif ($Zoom) {
            $img = [Drawing.Image]::FromFile($st.Shot); $iw0 = $img.Width; $ih0 = $img.Height; $img.Dispose()
            $v1 = ZoomView $st $iw0 $ih0
            if ($v1) {
                $v0 = FullView $iw0 $ih0
                $zf = @()
                for ($k = 1; $k -le $zoomFrames; $k++) {
                    $pp = $k / $zoomFrames
                    $ss = $pp * $pp * (3 - 2 * $pp)     # ease in and out
                    $zfile = Join-Path $frames ('{0:D2}_z{1:D3}.png' -f $n, $k)
                    StepFrame $st $num $stepIds.Count $zfile (MixView $v0 $v1 $ss)
                    $zf += $zfile
                }
                $zooms[$n] = $zf
                "zoom $id"
            }
        }
    }
    $n++
}

# ------------------------------------------------------------ speech and video
# Spoken text is kept between runs ($Work is emptied each time), so a rebuild asks Azure only for
# what changed (the free tier allows few requests a minute).
$ttsCache = Join-Path $env:TEMP 'bulknotes_demo_tts'
New-Item -ItemType Directory -Force $ttsCache | Out-Null
function Say($text, $wav) {
    if (-not $azure) { $tts.SetOutputToWaveFile($wav); $tts.Speak($text); $tts.SetOutputToNull(); return }
    $keyText = "$Voice|$AzureRate|$text|" + ($script:SayAs | ConvertTo-Json -Compress -Depth 4)
    $sha = [Security.Cryptography.SHA1]::Create()
    $hash = ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($keyText)) | ForEach-Object { $_.ToString('x2') }) -join ''
    $cached = Join-Path $ttsCache "$hash.wav"
    if (-not (Test-Path -LiteralPath $cached)) { Speak-Azure -Text $text -Voice $Voice -OutFile $cached -Rate $AzureRate }
    Copy-Item -LiteralPath $cached $wav -Force
}
# Seconds from the start of a WAV (16-bit PCM, data at byte 44) to the end of the speech in it.
function SpeechEnd($wav) {
    $bytes = [IO.File]::ReadAllBytes($wav)
    $byteRate = [BitConverter]::ToInt32($bytes, 28)
    $i = $bytes.Length - 2
    while ($i -ge 44 -and [Math]::Abs([int][BitConverter]::ToInt16($bytes, $i)) -lt 700) { $i -= 2 }
    [Math]::Max(0.0, ($i - 44) / $byteRate)
}
if (-not $NoVoice) {
    if ($azure) { . (Join-Path $here 'azure_tts.ps1') }
    else {
        $tts = New-Object System.Speech.Synthesis.SpeechSynthesizer
        $tts.SelectVoice($Voice)
        $tts.Rate = $Rate
    }
    "voice: $Voice"
}
else { 'voice: none' }
$ffmpeg = $FFmpeg
if (-not $ffmpeg) {
    $ffmpeg = Get-ChildItem 'C:\ffmpeg-*' -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ChildItem $_.FullName -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $ffmpeg) { $ffmpeg = (Get-Command ffmpeg).Source }
"ffmpeg: $ffmpeg"
if (-not ((& $ffmpeg -hide_banner -filters 2>&1 | Out-String) -match '\bxfade\b')) {
    throw "This ffmpeg has no xfade filter (it needs ffmpeg 4.3 or later): $ffmpeg"
}
$inv = [Globalization.CultureInfo]::InvariantCulture
function Num($x) { ([double]$x).ToString('0.###', $inv) }

# The joins: a dip to black after the cover and before the closing slide, dissolves elsewhere.
$count = $order.Count
$joinType = @{}; $joinLen = @{}
for ($k = 1; $k -lt $count; $k++) {
    if ((IsCard $order[$k - 1]) -or (IsCard $order[$k])) { $joinType[$k] = 'fadeblack'; $joinLen[$k] = $Dip }
    else {
        $ra = ($script:steps | Where-Object { $_.Id -eq $order[$k - 1] }).Rows
        $rb = ($script:steps | Where-Object { $_.Id -eq $order[$k] }).Rows
        if ($ra -ge 0 -and $ra -eq $rb) { $joinType[$k] = 'wipeleft'; $joinLen[$k] = $Wipe }
        else { $joinType[$k] = 'fade'; $joinLen[$k] = $Dissolve }
    }
    "join $($order[$k - 1]) -> $($order[$k]): $($joinType[$k]) $($joinLen[$k]) s"
}
$fadeInFirst = 0.8; $endHold = 1.5
# Each step: its join in, a breath, the voice (or the reading time), a pause, its join out.
$args2 = @('-hide_banner', '-loglevel', 'error', '-y')
$durs = @(); $leads = @(); $voiceIdx = @{}; $videoIdx = @{}
$idx = 0
for ($n = 0; $n -lt $count; $n++) {
    $id = $order[$n]
    $frame = Join-Path $frames ('{0:D2}_{1}.png' -f $n, $id)
    $in = if ($n -eq 0) { $fadeInFirst } else { $joinLen[$n] }
    $out = if ($n -eq $count - 1) { $endHold } else { $joinLen[$n + 1] }
    $lead = $in + 0.3; $tail = 0.8
    if ($n -eq 0) { $lead += $CoverIntro }
    $text = Clean (Text $id).text
    if ($NoVoice) {
        # About 2.6 words a second: time to read the subtitle.
        $words = @($text -split '\s+' | Where-Object { $_ }).Count
        $secs = [Math]::Max(2.5, $words / 2.6)
        # The cover and closing slides show no subtitle: a short, fixed time.
        if ($id -eq 'title') { $secs = 1.5 } elseif ($id -eq 'end') { $secs = 4.5 } elseif ($id -like 'sec_*') { $secs = 2.5 }
        $wav = $null
    }
    else {
        $wav = Join-Path $Work ('{0:D2}.wav' -f $n)
        Say $text $wav
        $fs = [IO.File]::OpenRead($wav); $br = New-Object IO.BinaryReader $fs; $fs.Position = 28; $rate = $br.ReadInt32(); $br.Close()
        $secs = ((Get-Item $wav).Length - 44) / $rate
    }
    $dur = [Math]::Round([Math]::Max($lead + $secs + $tail + $out, 4.5), 2)
    $durs += $dur; $leads += $lead
    if ($partFrames.ContainsKey($n)) {
        # When each part starts: where its cue is spoken (the speech of the text before the cue,
        # spoken on its own), or without a voice, its share of the words.
        $cues = Cues (Text $id).text
        $starts = @()
        for ($k = 0; $k -lt $cues.Count; $k++) {
            $pos = $cues[$k].Pos
            if ($pos -eq 0) { $starts += 0.0; continue }
            $before = $text.Substring(0, $pos).Trim()
            if ($wav) {
                $pw = Join-Path $Work ('{0:D2}_cue{1}.wav' -f $n, $k)
                Say $before $pw
                $t = SpeechEnd $pw
            }
            else {
                $all = @($text -split '\s+' | Where-Object { $_ }).Count
                $t = $secs * @($before -split '\s+' | Where-Object { $_ }).Count / [Math]::Max(1, $all)
            }
            $starts += [Math]::Min($dur - 0.5, $lead + $t)
        }
        "  cues at $(($starts | ForEach-Object { Num $_ }) -join ', ') s"
        $q = { param($x) "file '" + ($x -replace '\\', '/') + "'" }
        $lines = @('ffconcat version 1.0')
        $pf = $partFrames[$n]
        for ($k = 0; $k -lt $pf.Count; $k++) {
            $end = if ($k -lt $pf.Count - 1) { $starts[$k + 1] } else { $dur + 1 }
            $lines += (& $q $pf[$k]); $lines += "duration $(Num ([Math]::Max(0.034, $end - $starts[$k])))"
        }
        $lines += (& $q $pf[-1])     # the concat reader ignores the last duration
        $listFile = Join-Path $Work ('{0:D2}_cues.txt' -f $n)
        [IO.File]::WriteAllLines($listFile, $lines)
        # Made into a clip of its own first: many image-list inputs in the one big join ran ffmpeg
        # out of memory.
        $clip = Join-Path $Work ('{0:D2}_cues.mp4' -f $n)
        & $ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i $listFile -vf 'fps=30,format=yuv420p' -t (Num $dur) -c:v libx264 -preset veryfast -crf 12 -tune stillimage -threads 4 $clip
        if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed on the cues of $id" }
        # One decoding thread each: with frame threads, every clip's decoder holds many 1080p
        # frames, and the big join ran out of memory.
        $args2 += @('-threads', '1', '-i', $clip)
    }
    elseif ($zooms.ContainsKey($n)) {
        $zf = $zooms[$n]
        $q = { param($x) "file '" + ($x -replace '\\', '/') + "'" }
        $zs = $in + 0.3
        $lines = @('ffconcat version 1.0', (& $q $frame), "duration $(Num $zs)")
        foreach ($z in $zf) { $lines += (& $q $z); $lines += 'duration 0.0333333' }
        $lines += (& $q $zf[-1]); $lines += "duration $(Num ($dur - $zs - $zoomTime + 1))"
        $lines += (& $q $zf[-1])     # the concat reader ignores the last duration
        $listFile = Join-Path $Work ('{0:D2}_zoom.txt' -f $n)
        [IO.File]::WriteAllLines($listFile, $lines)
        $args2 += @('-f', 'concat', '-safe', '0', '-i', $listFile)
    }
    else { $args2 += @('-loop', '1', '-framerate', '30', '-t', (Num $dur), '-i', $frame) }
    $videoIdx[$n] = $idx; $idx++
    if ($wav) { $args2 += @('-i', $wav); $voiceIdx[$n] = $idx; $idx++ }
    "step $id $(Num $dur)s"
}
if (-not $NoVoice -and -not $azure) { $tts.Dispose() }
if ($musicOn) { $args2 += @('-stream_loop', '-1', '-i', $Music); $musicIdx = $idx; $idx++ }

# Video: xfade joins. Each join starts $joinLen before the end of what is joined so far.
$f = @()
for ($n = 0; $n -lt $count; $n++) {
    $f += "[$($videoIdx[$n]):v]fps=30,format=yuv420p,setsar=1,trim=0:$(Num $durs[$n]),settb=AVTB,setpts=PTS-STARTPTS[v$n]"
}
$length = $durs[0]; $vPrev = 'v0'
for ($k = 1; $k -lt $count; $k++) {
    $f += "[$vPrev][v$k]xfade=transition=$($joinType[$k]):duration=$(Num $joinLen[$k]):offset=$(Num ($length - $joinLen[$k]))[xv$k]"
    $vPrev = "xv$k"
    $length += $durs[$k] - $joinLen[$k]
}
$total = $length
$f += "[$vPrev]fade=t=in:st=0:d=$(Num $fadeInFirst),fade=t=out:st=$(Num ($total - 1.2)):d=1.2[vout]"

# Audio: the voices, each delayed to its place, joined with the same overlaps; the music under it.
$mix = $null
if (-not $NoVoice) {
    for ($n = 0; $n -lt $count; $n++) {
        $ms = [int]($leads[$n] * 1000)
        $f += "[$($voiceIdx[$n]):a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,adelay=$ms|$ms,apad,atrim=0:$(Num $durs[$n]),asetpts=PTS-STARTPTS[a$n]"
    }
    $aPrev = 'a0'
    for ($k = 1; $k -lt $count; $k++) {
        $f += "[$aPrev][a$k]acrossfade=d=$(Num $joinLen[$k]):c1=tri:c2=tri[xa$k]"
        $aPrev = "xa$k"
    }
    $mix = $aPrev
}
if ($musicOn) {
    # The music alone at $MusicVolume, then down to $MusicUnderVoice over the second before the
    # narration starts (where it would start, without a voice), and there for the rest.
    $t1 = $leads[0]; $t0 = [Math]::Max(0.0, $t1 - 1.0)
    $hi = Num $MusicVolume; $lo = Num $MusicUnderVoice
    $level = "if(lt(t,$(Num $t0)),$hi,if(lt(t,$(Num $t1)),$hi-($hi-$lo)*(t-$(Num $t0))/$(Num ($t1 - $t0)),$lo))"
    $f += "[$($musicIdx):a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,atrim=0:$(Num $total),asetpts=PTS-STARTPTS,volume='$level':eval=frame,afade=t=in:st=0:d=2,afade=t=out:st=$(Num ($total - 3)):d=3[music]"
    if ($mix) {
        $f += "[$mix][music]amix=inputs=2:duration=first:normalize=0[amixed]"
        $mix = 'amixed'
    }
    else { $mix = 'music' }
}
if (-not $mix) { $f += "anullsrc=r=48000:cl=stereo,atrim=0:$(Num $total)[silence]"; $mix = 'silence' }
$f += "[$mix]afade=t=out:st=$(Num ($total - 1.2)):d=1.2[aout]"

$script = Join-Path $Work 'filter.txt'
[IO.File]::WriteAllText($script, ($f -join ";`n"))
$suffix = $(if ($Zoom) { '_zoom' } else { '' }) + $(if ($NoVoice) { '_novoice' } else { '' })
$outFile = Join-Path $here ("PhilsTools_Install_{0}{1}.mp4" -f $Lang.ToUpper(), $suffix)
# Newer ffmpeg reads an option's value from a file with -/option; older ones had -filter_complex_script.
$fromFile = if ((& $ffmpeg -hide_banner -h long 2>&1 | Out-String) -match 'filter_complex_script') { '-filter_complex_script' } else { '-/filter_complex' }
$args2 += @($fromFile, $script, '-map', '[vout]', '-map', '[aout]', '-t', (Num $total),
    # Four encoder threads: fewer buffers, so the join fits when memory is short.
    '-c:v', 'libx264', '-preset', 'medium', '-tune', 'stillimage', '-crf', '18', '-r', '30', '-threads', '4',
    '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart', $outFile)
& $ffmpeg @args2
if ($LASTEXITCODE -ne 0) { throw 'ffmpeg failed' }
"written $outFile ($(Num $total)s)"

# The YouTube description (the standard, section 12), with each step's question ('q' in
# narration.json) at the time its step is fully on screen in this video: the end of the join into
# it, rounded up. Only for the voiced video, whose times differ from the test's. English is
# youtubetext.txt (the user's name for it), other languages youtubetext_<lang>.txt.
if (-not $NoVoice) {
    $yt = $narr.youtube.$Lang
    if (-not $yt) { "narration.json has no youtube text for '$Lang': no youtubetext written" }
    else {
        $lines = @($yt.about, '', $yt.download, $yt.link, '')
        $at = $durs[0]
        for ($k = 1; $k -lt $count; $k++) {
            $q = (Text $order[$k]).q
            if ($q) { $sec = [int][Math]::Ceiling($at); $lines += ('{0}:{1:D2} {2}' -f [int][Math]::Floor($sec / 60), ($sec % 60), $q) }
            $at += $durs[$k] - $joinLen[$k]
        }
        $lines += ''
        $lines += @($yt.credits)
        $ytFile = Join-Path $here $(if ($Lang -eq 'en') { 'youtubetext.txt' } else { "youtubetext_$Lang.txt" })
        [IO.File]::WriteAllText($ytFile, (($lines -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding $true))
        "written $ytFile"
    }
}
