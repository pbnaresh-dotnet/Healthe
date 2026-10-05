[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [switch]$SkipInstall,
    [switch]$NoNewWindows
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$BackendRoot = Join-Path $RepoRoot 'backend'
$FrontendRoot = Join-Path $RepoRoot 'frontend\healthapp-saas'
$SolutionPath = Join-Path $BackendRoot 'HealthApp.sln'
$ApiProjectPath = Join-Path $BackendRoot 'src\HealthApp.Api\HealthApp.Api.csproj'

function Test-CommandExists {
    param([Parameter(Mandatory = $true)][string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Resolve-GitExecutable {
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($git) { return $git.Source }

    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Git\cmd\git.exe'),
        (Join-Path $programFilesX86 'Git\cmd\git.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }

    if ($candidates.Count -gt 0) {
        return $candidates[0]
    }

    throw "Git was not found. Install Git for Windows or add git.exe to PATH."
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $false)][string[]]$ArgumentList = @()
    )

    Write-Host ''
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    Write-Host "$FilePath $($ArgumentList -join ' ')" -ForegroundColor DarkGray

    Push-Location $WorkingDirectory
    try {
        & $FilePath @ArgumentList
        if ($LASTEXITCODE -ne 0) {
            throw "'$Name' failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Start-DevProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$Command
    )

    $PowerShellExe = (Get-Process -Id $PID).Path
    $childCommand = '$Host.UI.RawUI.WindowTitle = ''' + $Title + '''; Set-Location -LiteralPath ''' + $WorkingDirectory + '''; ' + $Command
    $arguments = @('-NoLogo', '-NoExit', '-Command', $childCommand)

    if ($NoNewWindows) {
        return Start-Process -FilePath $PowerShellExe -ArgumentList $arguments -WorkingDirectory $WorkingDirectory -PassThru
    }

    return Start-Process -FilePath $PowerShellExe -ArgumentList $arguments -WorkingDirectory $WorkingDirectory -WindowStyle Normal -PassThru
}

Write-Host 'HealthApp local development startup' -ForegroundColor Green
Write-Host "Repository   : $RepoRoot"
Write-Host "Configuration: $Configuration"

$GitExecutable = Resolve-GitExecutable
Test-CommandExists 'dotnet'
Test-CommandExists 'node'
Test-CommandExists 'npm'

Write-Host ''
Write-Host '=== Pulling latest code from Git ===' -ForegroundColor Cyan
Push-Location $RepoRoot
try {
    & $GitExecutable pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        throw "Git pull failed with exit code $LASTEXITCODE. Resolve the repository state and run start-dev.ps1 again."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $SolutionPath)) { throw "Backend solution not found: $SolutionPath" }
if (-not (Test-Path $FrontendRoot)) { throw "Frontend workspace not found: $FrontendRoot" }
if (-not (Test-Path $ApiProjectPath)) { throw "API project not found: $ApiProjectPath" }

if (-not $SkipBuild) {
    $step = @{ Name = 'Restore .NET solution'; WorkingDirectory = $BackendRoot; FilePath = 'dotnet'; ArgumentList = @('restore', $SolutionPath) }
    Invoke-Step @step

    $step = @{ Name = 'Build .NET solution'; WorkingDirectory = $BackendRoot; FilePath = 'dotnet'; ArgumentList = @('build', $SolutionPath, '--configuration', $Configuration, '--no-restore') }
    Invoke-Step @step
}

$FrontendNodeModules = Join-Path $FrontendRoot 'node_modules'
if (-not $SkipInstall -and -not (Test-Path $FrontendNodeModules)) {
    $step = @{ Name = 'Install React workspace dependencies'; WorkingDirectory = $FrontendRoot; FilePath = 'npm'; ArgumentList = @('install') }
    Invoke-Step @step
} elseif (-not $SkipInstall) {
    Write-Host 'React dependencies already installed. Skipping npm install.' -ForegroundColor DarkGray
} else {
    Write-Host 'Skipping npm install because -SkipInstall was supplied.' -ForegroundColor Yellow
}

if (-not $SkipBuild) {
    $step = @{ Name = 'Build React workspaces'; WorkingDirectory = $FrontendRoot; FilePath = 'npm'; ArgumentList = @('run', 'build') }
    Invoke-Step @step
}

Write-Host ''
Write-Host '=== Starting local services ===' -ForegroundColor Cyan
$processes = @()

$service = @{ Title = 'HealthApp API'; WorkingDirectory = $BackendRoot; Command = "dotnet run --project `"$ApiProjectPath`" --launch-profile https" }
$processes += Start-DevProcess @service

$service = @{ Title = 'HealthApp Customer Web'; WorkingDirectory = $FrontendRoot; Command = 'npm run dev:customer' }
$processes += Start-DevProcess @service

$service = @{ Title = 'HealthApp Outlet Web'; WorkingDirectory = $FrontendRoot; Command = 'npm run dev:outlet' }
$processes += Start-DevProcess @service

$service = @{ Title = 'HealthApp Admin Web'; WorkingDirectory = $FrontendRoot; Command = 'npm run dev:admin' }
$processes += Start-DevProcess @service

Write-Host ''
Write-Host 'HealthApp development environment started.' -ForegroundColor Green
Write-Host ''
Write-Host 'API      : https://localhost:50447/swagger'
Write-Host 'API HTTP : http://localhost:50448/api'
Write-Host 'Customer : http://localhost:5173'
Write-Host 'Outlet   : http://localhost:5174'
Write-Host 'Admin    : http://localhost:5175'
Write-Host ''
Write-Host "Child process IDs: $($processes.Id -join ', ')" -ForegroundColor DarkGray
Write-Host 'Close the individual PowerShell windows to stop the services.' -ForegroundColor Yellow