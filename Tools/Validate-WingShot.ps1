param(
    [string]$UnityExe = "C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe",
    [string]$ValidationFolder = "Temp/WingShotValidationProject",
    [switch]$CaptureScreenshots
)
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot $ValidationFolder))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "Temp")) + [IO.Path]::DirectorySeparatorChar
if (-not $validationRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Validation copies must remain inside the project's Temp directory."
}
if (-not (Test-Path -LiteralPath $UnityExe)) { throw "Unity editor not found: $UnityExe" }
New-Item -ItemType Directory -Path $validationRoot -Force | Out-Null
foreach ($folder in @("Assets", "Packages", "ProjectSettings")) {
    $destination = Join-Path $validationRoot $folder
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    & robocopy (Join-Path $projectRoot $folder) $destination /E /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "Could not copy $folder for validation." }
}
# A separate PlayerPrefs identity prevents tests touching the real game's saved progress.
$settingsPath = Join-Path $validationRoot "ProjectSettings/ProjectSettings.asset"
$settings = Get-Content -LiteralPath $settingsPath -Raw
$settings = $settings -replace '(?m)^  companyName:.*$', '  companyName: WingShotValidation'
$settings = $settings -replace '(?m)^  productName:.*$', '  productName: WingShotValidation'
Set-Content -LiteralPath $settingsPath -Value $settings -Encoding UTF8
$logPath = Join-Path $validationRoot "WingShot-Batch.log"
$arguments = @("-batchmode", "-projectPath", ('"' + $validationRoot + '"'),
    "-executeMethod", "WingShotProjectTools.RunBatchChecks", "-logFile", ('"' + $logPath + '"'))
if ($CaptureScreenshots) { $arguments += @("-force-d3d11", "-wingShotCapture") } else { $arguments += "-nographics" }
$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden
Write-Output ("Validation Unity PID: " + $process.Id)
Write-Output ("Validation log: " + $logPath)
Write-Output ("Report after completion: " + (Join-Path $validationRoot "Logs/WingShotValidation.json"))
# Start is asynchronous so the editor's potentially long first import does not block progress updates.

