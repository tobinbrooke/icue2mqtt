<#
.SYNOPSIS
    Builds icue2mqtt and redeploys the Windows service in one step.

.DESCRIPTION
    1. Publishes the project to a clean temporary folder (the running service is not touched if the build fails).
    2. Stops the service (and kills a leftover icue2mqtt.exe).
    3. Empties the destination folder, keeping the existing properties.json (MQTT URL, credentials...).
    4. Copies the new build, then restarts the service and shows its latest log entries.

    The script relaunches itself as administrator when needed.
    Keep it at the root of the repository (next to icue2mqtt.sln).

.PARAMETER Destination
    Folder the service runs from. Default: C:\MQTT

.PARAMETER ServiceName
    Name of the Windows service. Default: icue2mqttService

.PARAMETER Pull
    Runs "git pull --ff-only" in the repository before building.

.EXAMPLE
    .\deploy.ps1
    .\deploy.ps1 -Pull
    .\deploy.ps1 -Destination D:\icue2mqttRelease
#>
[CmdletBinding()]
param(
    [string]$Destination = 'C:\MQTT',
    [string]$ServiceName = 'icue2mqttService',
    [switch]$Pull
)

$ErrorActionPreference = 'Stop'
$Destination = $Destination.TrimEnd('\')

# ---- relaunch as administrator -------------------------------------------------------------
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
                 '-Destination', "`"$Destination`"", '-ServiceName', "`"$ServiceName`"")
    if ($Pull) { $argList += '-Pull' }
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs
    exit
}

function Write-Step([string]$text) {
    Write-Host "==> $text" -ForegroundColor Cyan
}

$failed = $false
try {
    $repoDir    = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
    $projectDir = Join-Path $repoDir 'icue2mqtt'
    $project    = Join-Path $projectDir 'icue2mqtt.csproj'
    if (-not (Test-Path $project)) {
        throw "icue2mqtt.csproj not found in $projectDir. Keep deploy.ps1 at the root of the repository."
    }

    # The destination is emptied: never allow a drive root or a folder that contains the repository.
    $destFull = [IO.Path]::GetFullPath($Destination).TrimEnd('\')
    $driveRoot = [IO.Path]::GetPathRoot($destFull).TrimEnd('\')
    if ($destFull -eq $driveRoot -or $repoDir -eq $destFull -or $repoDir.StartsWith($destFull + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to empty '$destFull': it is a drive root or contains the repository."
    }

    if ($Pull) {
        Write-Step 'git pull'
        git -C $repoDir pull --ff-only
        if ($LASTEXITCODE -ne 0) { throw 'git pull failed.' }
    }

    # ---- build first: if it fails, the running service is left untouched ----------------------
    Write-Step 'dotnet publish'
    $publishDir = Join-Path ([IO.Path]::GetTempPath()) 'icue2mqtt-publish'
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    dotnet publish $project -r win-x64 -c Release -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed. The service was left untouched.' }

    # ---- stop the service ---------------------------------------------------------------------
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -ne 'Stopped') {
        Write-Step "Stopping $ServiceName"
        Stop-Service -Name $ServiceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    Get-Process -Name 'icue2mqtt' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1

    # ---- replace the files, keeping properties.json -------------------------------------------
    Write-Step "Updating $Destination"
    $propsPath   = Join-Path $Destination 'properties.json'
    $propsBackup = Join-Path ([IO.Path]::GetTempPath()) 'icue2mqtt-properties.json.bak'
    $hadProps    = Test-Path $propsPath
    if ($hadProps) { Copy-Item $propsPath $propsBackup -Force }

    if (-not (Test-Path $Destination)) { New-Item -ItemType Directory -Path $Destination | Out-Null }
    Get-ChildItem -Path $Destination -Force | Remove-Item -Recurse -Force
    Copy-Item -Path (Join-Path $publishDir '*') -Destination $Destination -Recurse -Force

    if ($hadProps) {
        Copy-Item $propsBackup $propsPath -Force
        Write-Host 'properties.json kept.'
    } else {
        Write-Warning "No properties.json existed in $Destination. Edit the new one (mqttUrl, credentials) before using the service."
    }

    # ---- restart ------------------------------------------------------------------------------
    $startedAt = Get-Date
    if ($service) {
        Write-Step "Starting $ServiceName"
        Start-Service -Name $ServiceName
        try {
            $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
            Write-Host "$ServiceName is running." -ForegroundColor Green
        } catch {
            Write-Warning "$ServiceName did not stay in the Running state. Check the log below and properties.json."
        }

        Start-Sleep -Seconds 4
        try {
            $events = Get-WinEvent -LogName 'icue2mqttLog' -MaxEvents 15 -ErrorAction Stop |
                Where-Object { $_.TimeCreated -ge $startedAt } |
                Sort-Object TimeCreated
            if ($events) {
                Write-Step 'Service log'
                foreach ($e in $events) {
                    $firstLine = ($e.Message -split "`r?`n")[0]
                    Write-Host ("{0:HH:mm:ss}  {1}" -f $e.TimeCreated, $firstLine)
                }
            }
        } catch {
            # event log not available: nothing to show
        }
    } else {
        Write-Warning "Service '$ServiceName' not found. Create it with:"
        Write-Host "  sc.exe create $ServiceName BinPath=`"$Destination\icue2mqtt.exe`""
    }
} catch {
    Write-Host ''
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $failed = $true
}

Write-Host ''
Read-Host 'Press Enter to close'
if ($failed) { exit 1 }
