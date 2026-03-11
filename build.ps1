param(
    [ValidateSet("win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")]
    [string]$Runtime = "win-x64",

    [string]$Configuration = "Release",
    [string]$OutputDir = "./publish"
)

$ErrorActionPreference = "Stop"

$project = "src/NoMercyBot.Server/NoMercyBot.Server.csproj"

Write-Host "Building NoMercyBot ($Configuration | $Runtime)..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $OutputDir `
    /p:PublishSingleFile=true `
    /p:EnableCompressionInSingleFile=true `
    /p:IncludeAllContentForSelfExtract=true `
    /p:DebugType=None `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

$exe = if ($Runtime.StartsWith("win")) { "NoMercyBot.exe" } else { "NoMercyBot" }
$path = Join-Path $OutputDir $exe

if (Test-Path $path) {
    $size = (Get-Item $path).Length / 1MB
    Write-Host "Build succeeded: $path ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "Build succeeded but binary not found at expected path." -ForegroundColor Yellow
}
