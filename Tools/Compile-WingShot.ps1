param([string]$UnityEditor = "C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor")
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$checkRoot = Join-Path $projectRoot "Temp/WingShotChecks"
New-Item -ItemType Directory -Path $checkRoot -Force | Out-Null
[xml]$project = Get-Content (Join-Path $projectRoot "Assembly-CSharp.csproj")
$references = @($project.Project.ItemGroup.Reference.HintPath | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
$compiler = Join-Path $UnityEditor "Data/DotNetSdkRoslyn/csc.dll"
if (-not (Test-Path -LiteralPath $compiler)) {
    $sdkRoot = Join-Path $env:ProgramFiles "dotnet/sdk"
    $compiler = Get-ChildItem -LiteralPath $sdkRoot -Directory | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "Roslyn/bincore/csc.dll" } |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $compiler) { throw "No C# compiler found." }
$runtimeDll = Join-Path $checkRoot "WingShot.Runtime.dll"
$argsFile = Join-Path $checkRoot "Runtime.rsp"
$defines = @($project.Project.PropertyGroup.DefineConstants | Where-Object { $_ })[0]
$arguments = @("-nostdlib+", "-target:library", "-langversion:9.0",
    ("-define:" + $defines), ('-out:"' + $runtimeDll + '"'))
$arguments += $references | ForEach-Object { '-reference:"' + $_ + '"' }
$arguments += Get-ChildItem -LiteralPath (Join-Path $projectRoot "Assets/Scripts") -Filter "*.cs" |
    ForEach-Object { '"' + $_.FullName + '"' }
$arguments | Set-Content -LiteralPath $argsFile -Encoding UTF8
& dotnet $compiler "-noconfig" ("@" + $argsFile)
if ($LASTEXITCODE -ne 0) { throw "Runtime C# compilation failed." }
$editorSources = Get-ChildItem -LiteralPath (Join-Path $projectRoot "Assets/Editor") -Filter "*.cs" -ErrorAction SilentlyContinue
if ($editorSources) {
    [xml]$editorProject = Get-Content (Join-Path $projectRoot "Assembly-CSharp-Editor.csproj")
    $editorReferences = @($editorProject.Project.ItemGroup.Reference.HintPath |
        Where-Object { $_ -and (Test-Path -LiteralPath $_) -and [IO.Path]::GetFileName($_) -ne "Assembly-CSharp.dll" })
    $editorArgs = @("-nostdlib+", "-target:library", "-langversion:9.0",
        ("-define:" + $defines),
        ('-out:"' + (Join-Path $checkRoot "WingShot.Editor.dll") + '"'),
        ('-reference:"' + $runtimeDll + '"'))
    $editorArgs += $editorReferences | ForEach-Object { '-reference:"' + $_ + '"' }
    $editorArgs += $editorSources | ForEach-Object { '"' + $_.FullName + '"' }
    $editorArgsFile = Join-Path $checkRoot "Editor.rsp"
    $editorArgs | Set-Content -LiteralPath $editorArgsFile -Encoding UTF8
    & dotnet $compiler "-noconfig" ("@" + $editorArgsFile)
    if ($LASTEXITCODE -ne 0) { throw "Editor C# compilation failed." }
}
Write-Output "Wing Shot C# checks passed."

