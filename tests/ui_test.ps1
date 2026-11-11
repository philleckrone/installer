<#
.SYNOPSIS
  UI test of Install_Phils_tools.exe (1.3+): no CMS tab, plugins sorted by name in EN/ES/FR, ticks kept
  across a language switch, the CLI refusing CMS installs, and the removal of old CMS copies from a
  scratch cms folder (hand-edited file kept as .bak; Project Checker and foreign files untouched).
  Installs nothing. powershell -ExecutionPolicy Bypass -File Installer\tests\ui_test.ps1 [-Exe PATH]
#>
param([string]$Exe = (Join-Path $PSScriptRoot '..\Install_Phils_tools.exe'))
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path $Exe).Path
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$work = Join-Path $env:TEMP 'inst_test_cms'
Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
New-Item -ItemType Directory $work | Out-Null
$fail = 0
$env:PHILSTOOLS_NOOPEN = $null   # 1.6: the User guide button opens the guide in the installer's own viewer
function Check($ok, $what) { if ($ok) { "ok   $what" } else { "FAIL $what"; $script:fail++ } }

# -- old copies: two checks (+ ptx_goto), Project Checker, a hand-edited file, a foreign file
foreach ($f in 'CMS\find_titles\Find_Titles.cms', 'CMS\find_titles\Find_Titles.py', 'CMS\find_titles\Find_Titles.rtf',
               'CMS\marker_space\MarkerSpace.cms', 'CMS\marker_space\MarkerSpace.py', 'CMS\marker_space\MarkerSpace.rtf',
               'CMS\ptx_goto\ptx_goto.py', 'CMS\ptx_goto\ParatextGoTo.exe',
               'Project_Checker\cms\projectchecker.cms', 'Project_Checker\cms\projectchecker.py') {
  Copy-Item (Join-Path $repo $f) $work
}
Add-Content (Join-Path $work 'MarkerSpace.py') "`n# changed by hand"
Set-Content (Join-Path $work 'someone_else.cms') '\check Someone else'

# -- CLI
$st = & $exe --cli status --cmsdir $work | Out-String
Check ($st -match "C`tfind_titles`tInstalled`ten" -and $st -match "C`tmarker_space`tUpdateAvailable`ten") "cli status sees the old copies"
$ErrorActionPreference = 'Continue'; $out = & $exe --cli install marker_space --cmsdir $work 2>&1 | Out-String; $ErrorActionPreference = 'Stop'
Check ($LASTEXITCODE -ne 0 -and $out -match 'no longer installed') "cli install of a CMS tool is refused"

# -- GUI
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, StringBuilder l);
  public static string Text(IntPtr h) { int n = (int)SendMessage(h, 0x0E, IntPtr.Zero, IntPtr.Zero); var sb = new StringBuilder(n + 1); SendMessage(h, 0x0D, (IntPtr)(n + 1), sb); return sb.ToString(); }
}
'@
function All($el) { $el.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition) }
function ByName($el, $name) { All $el | ? { $_.Current.Name -eq $name } | Select -First 1 }
Add-Type -Path (Join-Path $PSScriptRoot 'lvtext.cs')
function ListHandle($win) { [IntPtr](All $win | ? { $_.Current.ClassName -like '*SysListView32*' } | Select -First 1).Current.NativeWindowHandle }
function Names($win) { $h = ListHandle $win; @(0..([LvText]::Count($h) - 1) | % { [LvText]::ItemText($h, $_) }) }
function Sorted($names, $culture) {
  $c = [StringComparer]::Create([Globalization.CultureInfo]::new($culture), $true)
  $copy = [string[]]$names.Clone(); [Array]::Sort($copy, $c); ($copy -join '|') -eq ($names -join '|')
}

$p = Start-Process $exe -ArgumentList '--lang', 'en', '--cmsdir', "`"$work`"" -PassThru
try {
  for ($i = 0; $i -lt 50 -and $p.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 200; $p.Refresh() }
  Start-Sleep -Milliseconds 800
  $win = [Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
  $tabs = All $win | ? { $_.Current.ControlType -eq [Windows.Automation.ControlType]::Tab -or $_.Current.ClassName -like '*SysTabControl32*' }
  Check (@($tabs).Count -eq 0) "no tabs any more"
  $en = Names $win
  "     EN: $($en -join ' | ')"
  Check ($en.Count -eq 13 -and (Sorted $en 'en-US')) "13 plugins, alphabetical in English"

  # tick one row (space on the focused first row), switch language, check the tick follows the plugin
  $lvH = ListHandle $win
  $first = $en[0]
  [LvText]::Tick($lvH, 0, $true)
  $cbo = All $win | ? { $_.Current.ClassName -like '*COMBOBOX*' } | Select -First 1
  foreach ($pair in @(@(1, 'es-ES', 'ES'), @(2, 'fr-FR', 'FR'))) {
    [W]::SendMessage([IntPtr]$cbo.Current.NativeWindowHandle, 0x014E, [IntPtr]$pair[0], [IntPtr]::Zero) | Out-Null   # CB_SETCURSEL
    $ctrlId = 0 # CBN_SELCHANGE to the parent
    $parent = [Windows.Automation.TreeWalker]::RawViewWalker.GetParent($cbo)
    $id = [int](Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr h);' -Name G$($pair[2]) -PassThru)::GetDlgCtrlID([IntPtr]$cbo.Current.NativeWindowHandle)
    [W]::SendMessage([IntPtr]$parent.Current.NativeWindowHandle, 0x0111, [IntPtr](($id -band 0xFFFF) -bor (1 -shl 16)), [IntPtr]$cbo.Current.NativeWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    $names = Names $win
    "     $($pair[2]): $($names -join ' | ')"
    Check ($names.Count -eq 13 -and (Sorted $names $pair[1])) "alphabetical in $($pair[2])"
  }
  $now = Names $win
  $ticked = @(0..($now.Count - 1) | ? { [LvText]::Ticked($lvH, $_) } | % { $now[$_] })
  "     ticked now: $($ticked -join ', ') (was row 0 in English: $first)"
  Check ($ticked.Count -eq 1 -and $ticked[0] -ne $null) "the tick is kept across language switches"

  # 1.4: a click anywhere on a row ticks or unticks it; a double-click is two clicks.
  [LvText]::Tick($lvH, 0, $false)
  $row = 2
  $r = [LvText]::ItemRect($lvH, $row)
  $y = [int](($r[1] + $r[3]) / 2)
  $nameX = $r[0] + 80; $statusX = $r[2] - 40; $boxX = $r[0] + 10   # the tick box starts the row
  function ClickWait($kind, $x) {
    if ($kind -eq 'double') { [LvText]::DoubleClick($lvH, $x, $y) } else { [LvText]::Click($lvH, $x, $y) }
    Start-Sleep -Milliseconds 500
    [LvText]::Ticked($lvH, $row)
  }
  Check ((ClickWait 'single' $nameX) -eq $true) "a click on the name ticks the row"
  Check ((ClickWait 'single' $statusX) -eq $false) "a click on the status unticks it"
  Check ((ClickWait 'single' $boxX) -eq $true) "a click on the box ticks it once (not twice)"
  Check ((ClickWait 'double' $nameX) -eq $true) "a double-click on the name leaves it ticked (two toggles)"
  Check (@(0..12 | ? { [LvText]::Ticked($lvH, $_) }).Count -eq 1) "no other row was ticked"
  [LvText]::Tick($lvH, $row, $false)

  # 1.5: the User guide button writes the selected plugin's guide in the window's language (French
  # now), or the English one when there is none (Style Guide). 1.6: it opens in the installer's own viewer.
  $names = Names $win
  $btnGuide = All $win | ? { $_.Current.Name -eq "Guide de l'utilisateur" } | Select -First 1
  $logH = [IntPtr]((All $win | ? { $_.Current.ClassName -like '*EDIT*' } | Select -Last 1).Current.NativeWindowHandle)
  $guideDir = Join-Path $env:LOCALAPPDATA 'PhilsTools\guides'
  foreach ($case in @(@('Modification de notes en lot*', 'BulkNotes_UserGuide_fr.rtf', 'Bulk_Notes\plugin\BulkNotes_UserGuide_fr.rtf'),
                      @('Style Guide*', 'StyleGuide_UserGuide.rtf', 'Style_Guide\plugin\StyleGuide_UserGuide.rtf'))) {
    $i = [array]::IndexOf($names, ($names | ? { $_ -like $case[0] } | Select -First 1))
    [LvText]::Select($lvH, $i)
    Start-Sleep -Milliseconds 300
    [W]::PostMessage([IntPtr]$btnGuide.Current.NativeWindowHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 1200
    $path = Join-Path $guideDir $case[1]
    $logged = [W]::Text($logH) -match [regex]::Escape("guide: $path")
    $same = (Test-Path $path) -and ((Get-FileHash $path).Hash -eq (Get-FileHash (Join-Path $repo $case[2])).Hash)
    Check ($logged -and $same) "User guide for '$($names[$i])' gives $($case[1])"
    $viewer = $null
    for ($k = 0; $k -lt 20 -and -not $viewer; $k++) {
      # The viewer is owned by the window; UI Automation lists it under its owner.
      $viewer = $win.FindAll('Children', [Windows.Automation.Condition]::TrueCondition) | ? { $_.Current.Name -like "* - Guide de l'utilisateur" } | Select -First 1
      if (-not $viewer) { Start-Sleep -Milliseconds 200 }
    }
    $rich = if ($viewer) { All $viewer | ? { $_.Current.ClassName -like '*RICHEDIT50W*' } | Select -First 1 }
    $len = if ($rich) { [int][W]::SendMessage([IntPtr]$rich.Current.NativeWindowHandle, 0x0E, [IntPtr]::Zero, [IntPtr]::Zero) } else { 0 }
    Check ($viewer -and $len -gt 1000) "the guide shows in the installer's viewer: '$(if ($viewer) { $viewer.Current.Name })', $len characters"
    if ($viewer) { [W]::PostMessage([IntPtr]$viewer.Current.NativeWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 400 }
  }
  Check ([bool]$btnGuide) "the User guide button is there"

  # old copies (window is in French now)
  $lblOld = All $win | ? { $_.Current.Name -like '*v*rification(s) CMS*' } | Select -First 1
  Check ($lblOld -and $lblOld.Current.Name -match '^2 ' -and $lblOld.Current.Name -like "*$work*") "old-copies line: $($lblOld.Current.Name)"
  $btn = All $win | ? { $_.Current.Name -like 'Supprimer les anciennes copies CMS*' } | Select -First 1
  # A posted BM_CLICK can fail when the window is not active: activate it, then a posted mouse click.
  $bh = [IntPtr]$btn.Current.NativeWindowHandle
  [W]::SendMessage([IntPtr]$p.MainWindowHandle, 0x0006, [IntPtr]1, [IntPtr]::Zero) | Out-Null   # WM_ACTIVATE
  [W]::PostMessage($bh, 0x0201, [IntPtr]1, [IntPtr]((8 -shl 16) -bor 8)) | Out-Null           # WM_LBUTTONDOWN
  [W]::PostMessage($bh, 0x0202, [IntPtr]0, [IntPtr]((8 -shl 16) -bor 8)) | Out-Null           # WM_LBUTTONUP
  $dlg = $null
  for ($i = 0; $i -lt 50 -and -not $dlg; $i++) {
    Start-Sleep -Milliseconds 200
    # UI Automation lists a message box under the window that owns it.
    $dlg = $win.FindAll('Children', [Windows.Automation.Condition]::TrueCondition) | ? { $_.Current.ClassName -eq '#32770' } | Select -First 1
  }
  if (-not $dlg) { "     no dialog; button found: $([bool]$btn) '$($btn.Current.Name)'"; throw 'no confirmation dialog' }
  # A Win32 message box: its text is the static control 0xFFFF; Yes = WM_COMMAND IDYES (6).
  Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr h, int id);' -Name D -Namespace Dlg
  $dh = [IntPtr]$dlg.Current.NativeWindowHandle
  $msg = [W]::Text([Dlg.D]::GetDlgItem($dh, 0xFFFF))
  Check ($msg -match 'Supprimer les 2 anciennes copies' -and $msg -like "*$work*") "confirmation asked: $(($msg -replace "`r?`n", ' / ').Substring(0, 100))..."
  [W]::SendMessage($dh, 0x0111, [IntPtr]6, [IntPtr]::Zero) | Out-Null
  Start-Sleep -Seconds 2
  $log = [W]::Text([IntPtr]((All $win | ? { $_.Current.ClassName -like '*EDIT*' } | Select -Last 1).Current.NativeWindowHandle))
  "     log: " + ($log -replace "`r`n", ' / ')
} finally { if (-not $p.HasExited) { $p.Kill() } }

$left = @(Get-ChildItem $work -Name)
"     left: $($left -join ', ')"
Check (-not (Test-Path "$work\Find_Titles.py") -and -not (Test-Path "$work\MarkerSpace.cms") -and -not (Test-Path "$work\ptx_goto.py")) "the checks and ptx_goto were removed"
Check (@($left | ? { $_ -like 'MarkerSpace.py.*.bak' }).Count -eq 1) "the hand-edited file was kept as .bak"
Check ((Test-Path "$work\someone_else.cms") -and (Test-Path "$work\projectchecker.cms") -and (Test-Path "$work\projectchecker.py")) "foreign file and Project Checker untouched"
$st = & $exe --cli status --cmsdir $work | Out-String
Check ($st -notmatch "C`t(find_titles|marker_space)`t(Installed|UpdateAvailable)") "status: no old copies left"
Remove-Item -Recurse -Force $work
if ($fail) { "$fail failure(s)" } else { 'ALL PASSED' }


