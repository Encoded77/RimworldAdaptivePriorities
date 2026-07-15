<#
.SYNOPSIS
    Switches which RimSort instance config (ModsConfig.xml, mod settings, etc.)
    RimWorld actually loads, by junctioning the game's real (LocalLow) config
    folder to the desired instance's config folder.

.WHY
    RimWorld always reads/writes its config from the fixed LocalLow path,
    ignoring RimSort's per-instance "Config location" setting at launch time
    (see https://github.com/RimSort/RimSort/issues/1036). This script makes
    the LocalLow path a directory junction that points at whichever
    instance's config you want active, so switching instances actually
    switches the mod list RimWorld loads.

.USAGE
    .\switch-rimworld-config.ps1 -To Dev
    .\switch-rimworld-config.ps1 -To Default
    .\switch-rimworld-config.ps1 -Status
#>

param(
    [ValidateSet('Default', 'Dev')]
    [string]$To,

    [switch]$Status
)

$ErrorActionPreference = 'Stop'

$LiveConfig   = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config'
$BackupConfig = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config.default'
$DevConfig    = Join-Path $env:USERPROFILE 'AppData\Local\RimSort\instances\Mod dev\InstanceData\Config'

function Get-LiveLinkInfo {
    if (-not (Test-Path -LiteralPath $LiveConfig)) {
        return $null
    }
    return Get-Item -LiteralPath $LiveConfig -Force
}

function Show-Status {
    $item = Get-LiveLinkInfo
    if (-not $item) {
        Write-Host "Live config folder does not exist: $LiveConfig"
        return
    }
    if ($item.LinkType -eq 'Junction') {
        Write-Host "Live config is a junction -> $($item.Target)"
        if ($item.Target -eq $DevConfig) {
            Write-Host "Active instance: Dev"
        } elseif ($item.Target -eq $BackupConfig) {
            Write-Host "Active instance: Default"
        } else {
            Write-Host "Active instance: unknown (points somewhere unexpected)"
        }
    } else {
        Write-Host "Live config is a real folder (never switched yet). Active instance: Default"
    }
}

if ($Status -or -not $To) {
    Show-Status
    return
}

$item = Get-LiveLinkInfo

# First run: preserve the real default config before turning the live path into a junction.
if ($item -and $item.LinkType -ne 'Junction') {
    if (Test-Path -LiteralPath $BackupConfig) {
        throw "Backup path already exists at '$BackupConfig' but live config is a real folder. Resolve manually before continuing."
    }
    Write-Host "First run: moving real default config to backup at '$BackupConfig'"
    Move-Item -LiteralPath $LiveConfig -Destination $BackupConfig
} elseif (-not $item) {
    throw "Live config folder not found at '$LiveConfig'. Launch RimWorld once via the Default instance to create it, then retry."
}

$target = if ($To -eq 'Dev') { $DevConfig } else { $BackupConfig }

if (-not (Test-Path -LiteralPath $target)) {
    throw "Target config folder not found: $target"
}

# Remove the existing junction (this only removes the link, not the target's contents).
if (Test-Path -LiteralPath $LiveConfig) {
    (Get-Item -LiteralPath $LiveConfig -Force).Delete()
}

New-Item -ItemType Junction -Path $LiveConfig -Target $target | Out-Null

Write-Host "Switched active RimWorld config to '$To' -> $target"
