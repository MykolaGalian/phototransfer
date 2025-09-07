#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Build PhotoTransfer console application

.DESCRIPTION
    Cross-platform build script for PhotoTransfer that supports Debug/Release configurations
    and can create platform-specific executables.

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release)

.PARAMETER Runtime
    Target runtime: win-x64, linux-x64, osx-x64, or all (default: current platform)

.PARAMETER Clean
    Clean build artifacts before building

.EXAMPLE
    ./build.ps1
    Build for current platform in Release mode

.EXAMPLE
    ./build.ps1 -Configuration Debug -Clean
    Clean and build for current platform in Debug mode

.EXAMPLE
    ./build.ps1 -Runtime all
    Build for all supported platforms
#>

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'all', 'current')]
    [string]$Runtime = 'current',
    
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

Write-Host "PhotoTransfer Build Script" -ForegroundColor Green
Write-Host "=========================" -ForegroundColor Green

$SolutionFile = "PhotoTransfer.sln"
$PublishDir = "publish"

if (-not (Test-Path $SolutionFile)) {
    throw "Solution file '$SolutionFile' not found. Run from repository root."
}

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }
    dotnet clean $SolutionFile --configuration $Configuration --verbosity minimal
}

# Restore dependencies
Write-Host "Restoring dependencies..." -ForegroundColor Yellow
dotnet restore $SolutionFile --verbosity minimal

# Build solution
Write-Host "Building solution ($Configuration)..." -ForegroundColor Yellow
dotnet build $SolutionFile --configuration $Configuration --no-restore --verbosity minimal

# Define target runtimes
$runtimes = @()
if ($Runtime -eq 'all') {
    $runtimes = @('win-x64', 'linux-x64', 'osx-x64')
} elseif ($Runtime -eq 'current') {
    # Detect current platform
    if ($env:OS -eq "Windows_NT" -or [Environment]::OSVersion.Platform -eq "Win32NT") {
        $runtimes = @('win-x64')
    } elseif ($PSVersionTable.OS -like "*Linux*" -or [Environment]::OSVersion.Platform -eq "Unix") {
        $runtimes = @('linux-x64')
    } elseif ($PSVersionTable.OS -like "*Darwin*" -or $PSVersionTable.OS -like "*macOS*") {
        $runtimes = @('osx-x64')
    } else {
        # Fallback to Windows if detection fails
        Write-Warning "Could not detect platform, defaulting to Windows"
        $runtimes = @('win-x64')
    }
} else {
    $runtimes = @($Runtime)
}

# Publish for each runtime
foreach ($rid in $runtimes) {
    Write-Host "Publishing for $rid..." -ForegroundColor Yellow
    $outputDir = "$PublishDir/$rid"
    
    dotnet publish src/PhotoTransfer/PhotoTransfer.csproj `
        --configuration $Configuration `
        --runtime $rid `
        --self-contained true `
        --output $outputDir `
        --no-build `
        --verbosity minimal
        
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Published to: $outputDir" -ForegroundColor Green
        
        # Show executable info
        $exeName = if ($rid.StartsWith('win')) { 'phototransfer.exe' } else { 'phototransfer' }
        $exePath = Join-Path $outputDir $exeName
        if (Test-Path $exePath) {
            $size = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
            Write-Host "  Executable: $exeName ($size MB)" -ForegroundColor Green
        }
    } else {
        throw "Publish failed for $rid"
    }
}

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Artifacts available in: $PublishDir/" -ForegroundColor Green