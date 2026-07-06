# scripts/start-headless-clients.ps1
param(
    [Parameter(Mandatory = $true)]
    [int]$Count,

    [string]$GodotExe = "C:/Program Files (x86)/Godot/Godot_v4.6.1-stable_mono_win64.exe",

    [int]$Chunk,

    [switch]$NoWait
)

# $ProjectPath = Join-Path $PSScriptRoot "..\Client.Godot"
# $ProjectPath = (Resolve-Path $ProjectPath).Path
$ProjectPath = "Client.Godot"

Write-Host "Starting $Count headless clients..."
Write-Host "Godot executable: $GodotExe"
Write-Host "Project path: $ProjectPath"

$processes = @()

for ($i = 1; $i -le $Count; $i++) {
    $clientName = "HeadlessClient_$i_$([Guid]::NewGuid().ToString("N").Substring(0, 8))"

    $argumentList = "--headless --path `"$ProjectPath`" --loginName `"$clientName`""

    if ($PSBoundParameters.ContainsKey("Chunk")) {
        $argumentList += " --chunk $Chunk"
    }
    
    Write-Host "Arguments: $argumentList"

    if ($PSBoundParameters.ContainsKey("Chunk")) {
        $godotArgs += @("--chunk", $Chunk)
    }

    Write-Host "Starting client $i as '$clientName'..."

    $process = Start-Process `
        -FilePath $GodotExe `
        -RedirectStandardOutput "NUL" `
        -ArgumentList $argumentList `
        -PassThru `
        -NoNewWindow

    $processes += $process
}

Write-Host "Started $($processes.Count) clients."

if (!$NoWait) {
    Write-Host "Waiting for clients to exit. Press Ctrl+C to stop."
    $processes | Wait-Process
}