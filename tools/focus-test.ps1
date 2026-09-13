<#
.SYNOPSIS
    Proves that pressing a TouchDeck button never steals focus, and that the keystroke
    lands in the application that did have focus.

.DESCRIPTION
    Starts TouchDeck against a throwaway config, opens a throwaway window to receive the
    keystrokes, then clicks two buttons on the panel and checks three things:
      1. the panel window carries WS_EX_NOACTIVATE and WS_EX_TOOLWINDOW
      2. the foreground window is the same before and after each press
      3. the keystrokes arrived in the foreground window, plain and with modifiers

    No real document is ever touched: the target is a disposable text box.

.NOTES
    A full screen game that holds the foreground will make the test skip its presses and
    say so. Close it and run again.
#>
[CmdletBinding()]
param(
    [string] $Exe,
    [string] $WorkDir
)

$ErrorActionPreference = 'Stop'

# Resolved here rather than in the param block: a script that opens with a comment based
# help block gets an empty $PSScriptRoot inside a parameter default, which turns the default
# into an error instead of a path.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Exe) { $Exe = Join-Path $here '..\TouchDeck.App\bin\Debug\net9.0-windows\TouchDeck.exe' }
if (-not $WorkDir) { $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) 'touchdeck-focus-test' }

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class TdNative {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtrW(IntPtr h, int i);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

  public static IntPtr VisibleWindowOf(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr p) {
      uint owner; GetWindowThreadProcessId(h, out owner);
      if (owner == pid && IsWindowVisible(h)) { found = h; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static long ExStyle(IntPtr h) { return (long)GetWindowLongPtrW(h, -20); }
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(150);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(300);
  }
  public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowTextW(h, sb, 256); return sb.ToString(); }
}
"@

# ---------------------------------------------------------------- throwaway config
$config = Join-Path $WorkDir 'config'
$null = New-Item -ItemType Directory -Force -Path (Join-Path $config 'profiles')

@'
{
  "display": { "select": "primary", "fullscreen": false,
               "bounds": { "x": 60, "y": 60, "width": 520, "height": 340 } },
  "behaviour": { "defaultProfile": "focustest" },
  "logging": { "level": "debug" }
}
'@ | Set-Content (Join-Path $config 'config.json') -Encoding utf8

@'
{
  "id": "focustest",
  "grid": { "columns": 3, "rows": 2 },
  "pages": [ { "id": "main", "buttons": [
    { "col": 0, "row": 0, "label": "Select All", "action": { "type": "hotkey", "keys": "ctrl+a" } },
    { "col": 1, "row": 0, "label": "Type A",     "action": { "type": "hotkey", "keys": "a" } },
    { "col": 0, "row": 1, "label": "Copy",       "action": { "type": "hotkey", "keys": "ctrl+c" } }
  ] } ]
}
'@ | Set-Content (Join-Path $config 'profiles\focustest.json') -Encoding utf8

# ---------------------------------------------------------------- throwaway target window
$typed = Join-Path $WorkDir 'typed.txt'
$targetScript = Join-Path $WorkDir 'target.ps1'
@"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
`$form = New-Object System.Windows.Forms.Form
`$form.Text = 'TouchDeck Test Target'
`$form.StartPosition = 'Manual'
`$form.Left = 700; `$form.Top = 520; `$form.Width = 520; `$form.Height = 220
`$form.TopMost = `$true
`$box = New-Object System.Windows.Forms.TextBox
`$box.Multiline = `$true; `$box.Dock = 'Fill'
`$box.Font = New-Object System.Drawing.Font('Consolas', 14)
`$box.Add_TextChanged({ [System.IO.File]::WriteAllText('$typed', `$box.Text) })
`$form.Controls.Add(`$box)
`$form.Add_Shown({ `$form.Activate(); `$box.Focus() })
[System.IO.File]::WriteAllText('$typed', '')
[void]`$form.ShowDialog()
"@ | Set-Content $targetScript -Encoding utf8

# ---------------------------------------------------------------- run
& $Exe --quit | Out-Null
Start-Sleep -Seconds 1

$deckProc = Start-Process -FilePath $Exe -ArgumentList @('--config', $config) -PassThru
$targetProc = Start-Process powershell -ArgumentList @('-NoProfile', '-WindowStyle', 'Hidden', '-File', $targetScript) -PassThru
Start-Sleep -Seconds 4

$deck = [TdNative]::VisibleWindowOf($deckProc.Id)
$target = [TdNative]::VisibleWindowOf($targetProc.Id)

$ex = [TdNative]::ExStyle($deck)
$noActivate = ($ex -band 0x08000000) -ne 0
$toolWindow = ($ex -band 0x00000080) -ne 0

$rect = New-Object TdNative+RECT
[void][TdNative]::GetWindowRect($deck, [ref]$rect)

# The same layout maths the panel uses: square cells, centred, gap 8, padding 12.
$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
$cols = 3; $rows = 2; $gap = 8; $pad = 12
$cell = [Math]::Min(($w - 2*$pad - ($cols-1)*$gap) / $cols, ($h - 2*$pad - ($rows-1)*$gap) / $rows)
$ox = ($w - ($cols*$cell + ($cols-1)*$gap)) / 2
$oy = ($h - ($rows*$cell + ($rows-1)*$gap)) / 2
function CellCentre($c, $r) {
    @([int]($rect.Left + $ox + $c*($cell+$gap) + $cell/2), [int]($rect.Top + $oy + $r*($cell+$gap) + $cell/2))
}

Set-Clipboard -Value 'clipboard-was-not-touched'
$skipped = 0
$focusKept = $true

function PressDeck($point, $what) {
    [void][TdNative]::SetForegroundWindow($target)
    Start-Sleep -Milliseconds 400
    $before = [TdNative]::GetForegroundWindow()
    if ($before -ne $target) {
        $script:skipped++
        Write-Host "  skipped $what - '$([TdNative]::Title($before))' held the foreground"
        return
    }
    [TdNative]::Click($point[0], $point[1])
    $after = [TdNative]::GetForegroundWindow()
    if ($after -ne $target) { $script:focusKept = $false }
    Write-Host "  pressed $what - focus kept: $($after -eq $target)"
}

1..3 | ForEach-Object { PressDeck (CellCentre 1 0) 'Type A' }
PressDeck (CellCentre 0 0) 'Select All'
PressDeck (CellCentre 0 1) 'Copy'
Start-Sleep -Milliseconds 600

$text = if (Test-Path $typed) { [System.IO.File]::ReadAllText($typed) } else { '' }
$clip = Get-Clipboard

Stop-Process -Id $targetProc.Id -Force -ErrorAction SilentlyContinue
& $Exe --quit | Out-Null

Write-Host ''
Write-Host "WS_EX_NOACTIVATE set            : $noActivate"
Write-Host "WS_EX_TOOLWINDOW set            : $toolWindow"
Write-Host "panel placed at                 : $($rect.Left),$($rect.Top) $w x $h"
Write-Host "presses skipped (foreground lost): $skipped"
Write-Host "focus stayed with the target     : $focusKept"
Write-Host "plain key arrived                : $($text -eq 'aaa')  (got '$text')"
Write-Host "modifier combo arrived           : $($clip -eq 'aaa')  (clipboard '$clip')"

if (-not ($noActivate -and $toolWindow -and $focusKept -and $skipped -eq 0 -and $text -eq 'aaa' -and $clip -eq 'aaa')) {
    exit 1
}
