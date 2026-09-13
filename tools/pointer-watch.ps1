<#
.SYNOPSIS
    Watches the mouse pointer while you tap the deck, and says whether tapping is what moves
    it.

.DESCRIPTION
    Samples the pointer position as fast as it can for a few seconds and reports every jump,
    saying which of them landed on the deck panel. That is the question: a tap that drags the
    pointer onto the panel is what makes a game's camera swing, because the game reads the
    jump as an enormous movement of the mouse.

    Run it, then tap a few buttons on the panel while it counts down.

    A clean reading needs the deck running, so that the panel's position can be read out of
    its log. It is worth running once with a game in front and once without: a game that
    steers by the mouse puts the pointer back every frame, which shows up here as a swarm of
    small movements around one point, and the tap shows up as the one large jump.

.NOTES
    Reads only. Nothing is injected, nothing is moved, nothing is changed.
#>
[CmdletBinding()]
param(
    [int] $Seconds = 15,
    [int] $JumpPixels = 60
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class Pointer
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);

    /// <summary>
    /// Samples the pointer until the time is up, keeping only the moves worth looking at.
    /// Each entry is milliseconds since the start, then where it came from and went to.
    /// </summary>
    public static List<int[]> Watch(int seconds, int jump)
    {
        var jumps = new List<int[]>();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        POINT last;
        GetCursorPos(out last);

        while (clock.ElapsedMilliseconds < seconds * 1000)
        {
            POINT now;
            if (!GetCursorPos(out now))
            {
                continue;
            }

            var dx = now.X - last.X;
            var dy = now.Y - last.Y;

            if ((dx * dx) + (dy * dy) >= jump * jump)
            {
                jumps.Add(new[] { (int)clock.ElapsedMilliseconds, last.X, last.Y, now.X, now.Y });
            }

            last = now;
            System.Threading.Thread.Sleep(1);
        }

        return jumps;
    }
}
'@

$log = Get-ChildItem (Join-Path $env:APPDATA 'TouchDeck\logs') -Filter *.log -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

$left = $null

if ($log) {
    $placed = (Get-Content $log.FullName | Select-String 'Panel placed on' | Select-Object -Last 1).Line
    if ($placed -match '(\d+)x(\d+) at (-?\d+),(-?\d+)') {
        $width = [int]$Matches[1]
        $height = [int]$Matches[2]
        $left = [int]$Matches[3]
        $top = [int]$Matches[4]
        Write-Host "panel at $left,$top ${width}x${height}"
    }

    $claimed = (Get-Content $log.FullName) -match 'Taking touch input directly'
    $stylus = (Get-Content $log.FullName) -match "WPF's own touch stack is off"
    Write-Host ("panel asked Windows for touch: {0}" -f $(if ($claimed) { 'yes' } else { 'NO' }))
    Write-Host ("WPF's own touch stack:          {0}" -f $(if ($stylus) { 'off' } else { 'ON' }))
} else {
    Write-Host 'No TouchDeck log found, so the panel position is unknown.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host "Tap some buttons on the deck now. Watching for $Seconds seconds." -ForegroundColor Cyan

$jumps = [Pointer]::Watch($Seconds, $JumpPixels)

Write-Host ''
if ($jumps.Count -eq 0) {
    Write-Host "The pointer never jumped more than $JumpPixels pixels at once." -ForegroundColor Green
    Write-Host 'If the camera still swung, whatever moved it was not the pointer jumping to the panel.'
    exit 0
}

$onPanel = 0

foreach ($jump in $jumps) {
    $landed = ''
    if ($null -ne $left) {
        $x = $jump[3]
        $y = $jump[4]
        if ($x -ge $left -and $x -lt ($left + $width) -and $y -ge $top -and $y -lt ($top + $height)) {
            $landed = '   <-- ON THE PANEL'
            $onPanel++
        }
    }

    '{0,6} ms  {1},{2} -> {3},{4}{5}' -f $jump[0], $jump[1], $jump[2], $jump[3], $jump[4], $landed
}

Write-Host ''
Write-Host ("{0} jumps, {1} of them onto the panel." -f $jumps.Count, $onPanel)

if ($onPanel -gt 0) {
    Write-Host 'Tapping is still dragging the pointer onto the panel.' -ForegroundColor Red
} else {
    Write-Host 'No tap dragged the pointer onto the panel.' -ForegroundColor Green
}
