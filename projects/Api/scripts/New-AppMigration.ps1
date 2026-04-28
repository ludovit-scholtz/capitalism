[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Name,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$Environment = 'Development',

    [Parameter()]
    [string[]]$AdditionalArguments = @()
)

$ErrorActionPreference = 'Stop'

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDirectory = Split-Path -Parent $scriptDirectory
$projectFile = Join-Path $projectDirectory 'Api.csproj'
$migrationsDirectory = 'Data/Migrations'

if (-not (Test-Path $projectFile)) {
    throw "Could not find Api.csproj at '$projectFile'."
}

$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:ASPNETCORE_ENVIRONMENT = $Environment

try {
    Push-Location $projectDirectory

    $arguments = @(
        'ef',
        'migrations',
        'add',
        $Name,
        '--context', 'Api.Data.AppDbContext',
        '--project', $projectFile,
        '--startup-project', $projectFile,
        '--output-dir', $migrationsDirectory,
        '--'
    ) + $AdditionalArguments

    Write-Host "Scaffolding PostgreSQL migration '$Name' for AppDbContext..." -ForegroundColor Cyan
    Write-Host ("dotnet " + ($arguments -join ' ')) -ForegroundColor DarkGray

    & dotnet @arguments
    $exitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } elseif ($?) { 0 } else { 1 }
    if ($exitCode -ne 0) {
        throw "dotnet ef migrations add failed with exit code $exitCode."
    }
}
finally {
    Pop-Location

    if ($null -eq $previousEnvironment) {
        Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    }
    else {
        $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    }
}
