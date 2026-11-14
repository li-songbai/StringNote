param(
    [switch]$SelfContained,
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot 'app\Xianpu.Desktop\Xianpu.Desktop.csproj'
$publishPath = if ($SelfContained) {
    Join-Path $repoRoot 'app\Xianpu.Desktop\bin\Release\net10.0-windows\win-x64\publish'
} else {
    Join-Path $repoRoot 'app\Xianpu.Desktop\bin\Release\net10.0-windows\publish'
}
$distPath = Join-Path $repoRoot 'dist'
$issPath = Join-Path $repoRoot 'installer\StringNote.iss'

$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $repoRoot '.nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

if (Test-Path $publishPath) {
    Remove-Item -Path $publishPath -Recurse -Force
}

if ($SelfContained) {
    dotnet restore $projectPath -r win-x64 -p:NuGetAudit=false
} else {
    dotnet restore $projectPath -p:NuGetAudit=false
}
if ($LASTEXITCODE -ne 0) {
    throw "弦笺 (StringNote) 依赖还原失败，dotnet 退出码：$LASTEXITCODE"
}

if ($SelfContained) {
    dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version --no-restore -o $publishPath
} else {
    dotnet publish $projectPath -c Release -p:Version=$Version --no-restore -o $publishPath
}
if ($LASTEXITCODE -ne 0) {
    throw "弦笺 (StringNote) 发布失败，dotnet 退出码：$LASTEXITCODE"
}

Copy-Item (Join-Path $repoRoot 'README.md') (Join-Path $publishPath 'README.txt') -Force
New-Item -ItemType Directory -Path $distPath -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot '启动弦谱.cmd') -Destination (Join-Path $publishPath 'Start-Xianpu.cmd') -Force

$zipPath = Join-Path $distPath 'StringNote-Portable-win-x64.zip'
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath -Force

Write-Host "发布完成：$publishPath\Xianpu.Desktop.exe"
Write-Host "便携压缩包：$zipPath"

$isccPath = $null
$innoDefault = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (Test-Path $innoDefault) {
    $isccPath = $innoDefault
} else {
    $isccCmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($isccCmd) {
        $isccPath = $isccCmd.Source
    }
}

if ($isccPath -and (Test-Path $issPath)) {
    & $isccPath "/DAppVersion=$Version" "/DPublishDir=$publishPath" $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup 安装包编译失败，ISCC 退出码：$LASTEXITCODE"
    }
    $setupPath = Join-Path $distPath "StringNote-Setup-$Version.exe"
    Write-Host "安装程序：$setupPath"
} else {
    Write-Host "未检测到 Inno Setup 6 (ISCC.exe)，已跳过安装程序生成。"
}
