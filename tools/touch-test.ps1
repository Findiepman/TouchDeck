<#
.SYNOPSIS
    Proves that tapping a TouchDeck button presses that button and leaves the mouse pointer
    exactly where it was.

.DESCRIPTION
    Starts TouchDeck against a throwaway config holding two harmless buttons, then injects
    real touch contacts with InjectTouchInput and checks three things:
      1. a tap on a button runs that button's action
      2. a tap on an empty square runs nothing
      3. the mouse pointer is in the same place afterwards as before

    The third is the point. A window that has not asked Windows for touch input gets mouse
    emulation instead, which moves the pointer to the contact. The panel is on a second
    screen, so that used to drag the pointer off whatever the user was looking at, and a
    game steering its camera by mouse movement read it as an enormous flick.

.NOTES
    Injected touch goes down the same path as a finger, which is why this works when
    ordinary synthetic clicks do not land on this machine at all.

    Nothing of yours is touched: the config folder is thrown away at the end, the buttons
    only set a variable inside it, and the running deck is left stopped for you to restart.
#>
[CmdletBinding()]
param(
    [string] $Exe,
    [string] $Root
)

$ErrorActionPreference = 'Stop'

# Resolved here rather than in the param block: a script that opens with a comment based
# help block gets an empty $PSScriptRoot inside a parameter default, which turns the default
# into an error instead of a path.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Exe) { $Exe = Join-Path $here '..\TouchDeck.App\bin\Debug\net9.0-windows\TouchDeck.exe' }
if (-not $Root) { $Root = Join-Path ([System.IO.Path]::GetTempPath()) 'touchdeck-touch-test' }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Inject
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINTER_INFO
    {
        public uint pointerType, pointerId, frameId, pointerFlags;
        public IntPtr sourceDevice, hwndTarget;
        public POINT ptPixelLocation, ptHimetricLocation, ptPixelLocationRaw, ptHimetricLocationRaw;
        public uint dwTime, historyCount;
        public int inputData;
        public uint dwKeyStates;
        public ulong PerformanceCount;
        public int ButtonChangeType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINTER_TOUCH_INFO
    {
        public POINTER_INFO pointerInfo;
        public uint touchFlags, touchMask;
        public RECT rcContact, rcContactRaw;
        public uint orientation, pressure;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool InitializeTouchInjection(uint maxCount, uint mode);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool InjectTouchInput(uint count, [In] POINTER_TOUCH_INFO[] info);

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);

    private const uint Touch = 2, Down = 0x00010000, Update = 0x00020000, Up = 0x00040000;
    private const uint InRange = 0x00000002, InContact = 0x00000004;
    private const uint MaskContact = 0x00000001, MaskPressure = 0x00000004;

    private static POINTER_TOUCH_INFO At(int x, int y, uint flags)
    {
        var info = new POINTER_TOUCH_INFO();
        info.pointerInfo.pointerType = Touch;
        info.pointerInfo.pointerFlags = flags;
        info.pointerInfo.ptPixelLocation.X = x;
        info.pointerInfo.ptPixelLocation.Y = y;
        info.touchMask = MaskContact | MaskPressure;
        info.rcContact.L = x - 4; info.rcContact.T = y - 4;
        info.rcContact.R = x + 4; info.rcContact.B = y + 4;
        info.pressure = 32000;
        return info;
    }

    /// <summary>Taps once and reports where the mouse pointer was before and after.</summary>
    public static int[] Tap(int x, int y)
    {
        POINT before, after;
        GetCursorPos(out before);

        InitializeTouchInjection(1, 1);
        InjectTouchInput(1, new[] { At(x, y, Down | InRange | InContact) });

        // Held for a few frames, because a contact that lands and leaves in the same frame
        // is not what a finger does.
        var hold = new[] { At(x, y, Update | InRange | InContact) };
        for (var i = 0; i < 6; i++)
        {
            InjectTouchInput(1, hold);
            System.Threading.Thread.Sleep(16);
        }

        InjectTouchInput(1, new[] { At(x, y, Up) });
        System.Threading.Thread.Sleep(250);

        GetCursorPos(out after);
        return new[] { before.X, before.Y, after.X, after.Y };
    }
}
'@

function Fail([string] $why) {
    Write-Host "FAIL  $why" -ForegroundColor Red
    $script:Failures++
}

function Pass([string] $what) {
    Write-Host "ok    $what" -ForegroundColor Green
}

$script:Failures = 0

if (-not (Test-Path $Exe)) {
    throw "No TouchDeck.exe at $Exe. Build first."
}

Get-Process TouchDeck -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

if (Test-Path $Root) { Remove-Item $Root -Recurse -Force }
New-Item -ItemType Directory -Path "$Root\profiles" -Force | Out-Null

# A deck of two buttons on a 2x2 grid, so there is a square that should do nothing. Both
# buttons only write a variable inside the throwaway config folder.
@'
{
  "version": 1,
  "display": { "select": "byResolution", "value": "1024x600", "fullscreen": true },
  "behaviour": { "startWithWindows": false, "singleInstance": false, "defaultProfile": "default" },
  "logging": { "level": "Debug" }
}
'@ | Set-Content "$Root\config.json" -Encoding utf8

@'
{
  "id": "default",
  "grid": { "columns": 2, "rows": 2 },
  "pages": [
    { "id": "main", "buttons": [
      { "col": 0, "row": 0, "label": "A",
        "action": { "type": "setVariable", "name": "probe", "value": "A", "scope": "session" } },
      { "col": 1, "row": 1, "label": "B",
        "action": { "type": "setVariable", "name": "probe", "value": "B", "scope": "session" } }
    ] }
  ]
}
'@ | Set-Content "$Root\profiles\default.json" -Encoding utf8

Start-Process -FilePath $Exe -ArgumentList '--config', $Root
Start-Sleep -Seconds 5

$log = Get-ChildItem "$Root\logs" -Filter *.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $log) { throw "The deck wrote no log; it may not have started." }

if ((Get-Content $log.FullName) -match 'Taking touch input directly') {
    Pass 'the panel asked Windows for touch input'
} else {
    Fail 'the panel is not taking touch input, so taps will move the pointer'
}

$placed = (Get-Content $log.FullName | Select-String 'Panel placed on' | Select-Object -Last 1).Line
if ($placed -notmatch 'at (-?\d+),(-?\d+)') { throw "Could not read the panel position from: $placed" }
$left = [int]$Matches[1]
$top = [int]$Matches[2]
if ($placed -notmatch '(\d+)x(\d+) at') { throw "Could not read the panel size from: $placed" }
$width = [int]$Matches[1]
$height = [int]$Matches[2]

Write-Host "panel at $left,$top ${width}x${height}"

$squares = @(
    @{ Name = 'button A'; X = $left + [int]($width * 0.25); Y = $top + [int]($height * 0.25); Fires = $true }
    @{ Name = 'button B'; X = $left + [int]($width * 0.75); Y = $top + [int]($height * 0.75); Fires = $true }
    @{ Name = 'an empty square'; X = $left + [int]($width * 0.75); Y = $top + [int]($height * 0.25); Fires = $false }
)

foreach ($square in $squares) {
    $ranBefore = (Get-Content $log.FullName | Select-String 'Action setVariable finished').Count
    $cursor = [Inject]::Tap($square.X, $square.Y)
    Start-Sleep -Milliseconds 500
    $ranAfter = (Get-Content $log.FullName | Select-String 'Action setVariable finished').Count

    $fired = $ranAfter -gt $ranBefore

    if ($fired -eq $square.Fires) {
        Pass ("a tap on {0} {1}" -f $square.Name, $(if ($square.Fires) { 'ran its action' } else { 'ran nothing' }))
    } else {
        Fail ("a tap on {0} {1}" -f $square.Name, $(if ($fired) { 'ran an action it should not have' } else { 'ran nothing' }))
    }

    # Whether the pointer landed on the panel, not whether it moved at all. A game with the
    # mouse nudges the pointer by a pixel or two of its own accord, and that is not this.
    $x = $cursor[2]
    $y = $cursor[3]
    $onPanel = $x -ge $left -and $x -lt ($left + $width) -and $y -ge $top -and $y -lt ($top + $height)

    if ($onPanel) {
        Fail ("tapping {0} dragged the mouse pointer onto the panel, to {1},{2}" -f $square.Name, $x, $y)
    } else {
        Pass ("tapping {0} left the mouse pointer off the panel, at {1},{2} (was {3},{4})" -f
            $square.Name, $x, $y, $cursor[0], $cursor[1])
    }
}

Get-Process TouchDeck -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Remove-Item $Root -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
if ($script:Failures -gt 0) {
    Write-Host "$($script:Failures) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'All good. Start your own deck again when you are ready.' -ForegroundColor Green
exit 0
