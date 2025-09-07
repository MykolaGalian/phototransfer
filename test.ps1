#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Run PhotoTransfer tests

.DESCRIPTION
    Cross-platform test script for PhotoTransfer that runs all test categories
    with coverage reporting and detailed output options.

.PARAMETER Category
    Test category to run: unit, integration, contract, all (default: all)

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Debug for testing)

.PARAMETER Coverage
    Generate code coverage report

.PARAMETER Verbose
    Show detailed test output

.EXAMPLE
    ./test.ps1
    Run all tests in Debug mode

.EXAMPLE
    ./test.ps1 -Category unit -Coverage
    Run unit tests with coverage report

.EXAMPLE
    ./test.ps1 -Verbose -Configuration Release
    Run all tests in Release mode with detailed output
#>

param(
    [ValidateSet('unit', 'integration', 'contract', 'all')]
    [string]$Category = 'all',
    
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    
    [switch]$Coverage,
    
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'

Write-Host "PhotoTransfer Test Script" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green

$SolutionFile = "PhotoTransfer.sln"
$TestProject = "tests/PhotoTransfer.Tests/PhotoTransfer.Tests.csproj"
$CoverageDir = "coverage"

if (-not (Test-Path $SolutionFile)) {
    throw "Solution file '$SolutionFile' not found. Run from repository root."
}

if (-not (Test-Path $TestProject)) {
    throw "Test project '$TestProject' not found."
}

# Build solution first
Write-Host "Building solution ($Configuration)..." -ForegroundColor Yellow
dotnet build $SolutionFile --configuration $Configuration --verbosity minimal

# Prepare test command
$testArgs = @(
    'test'
    $TestProject
    '--configuration', $Configuration
    '--no-build'
)

# Add verbosity
if ($Verbose) {
    $testArgs += '--verbosity', 'normal'
} else {
    $testArgs += '--verbosity', 'minimal'
}

# Add category filter
if ($Category -ne 'all') {
    switch ($Category) {
        'unit' { $testArgs += '--filter', 'TestCategory=Unit' }
        'integration' { $testArgs += '--filter', 'TestCategory=Integration' }
        'contract' { $testArgs += '--filter', 'TestCategory=Contract' }
    }
    Write-Host "Running $Category tests..." -ForegroundColor Yellow
} else {
    Write-Host "Running all tests..." -ForegroundColor Yellow
}

# Add coverage collection
if ($Coverage) {
    Write-Host "Coverage reporting enabled" -ForegroundColor Yellow
    $testArgs += '--collect:"XPlat Code Coverage"'
    $testArgs += '--results-directory', $CoverageDir
}

# Add logger for better output
$testArgs += '--logger', 'console;verbosity=normal'

# Run tests
Write-Host "Test command: dotnet $($testArgs -join ' ')" -ForegroundColor Gray
& dotnet $testArgs

$testExitCode = $LASTEXITCODE

if ($testExitCode -eq 0) {
    Write-Host ""
    Write-Host "All tests passed!" -ForegroundColor Green
    
    # Process coverage if requested
    if ($Coverage -and (Test-Path $CoverageDir)) {
        Write-Host "Processing coverage report..." -ForegroundColor Yellow
        
        # Find coverage files
        $coverageFiles = Get-ChildItem -Path $CoverageDir -Recurse -Filter "coverage.cobertura.xml"
        
        if ($coverageFiles.Count -gt 0) {
            Write-Host "Coverage files found:" -ForegroundColor Green
            foreach ($file in $coverageFiles) {
                Write-Host "  $($file.FullName)" -ForegroundColor Green
            }
            
            # Try to generate HTML report if reportgenerator is available
            try {
                $reportGenerator = Get-Command dotnet-reportgenerator-globaltool -ErrorAction SilentlyContinue
                if ($reportGenerator) {
                    $htmlReport = "$CoverageDir/html"
                    dotnet reportgenerator `
                        -reports:"$($coverageFiles[0].FullName)" `
                        -targetdir:$htmlReport `
                        -reporttypes:Html
                    
                    Write-Host "HTML coverage report: $htmlReport/index.html" -ForegroundColor Green
                } else {
                    Write-Host "Install dotnet-reportgenerator-globaltool for HTML reports" -ForegroundColor Yellow
                }
            } catch {
                Write-Host "Could not generate HTML coverage report" -ForegroundColor Yellow
            }
        }
    }
} else {
    Write-Host ""
    Write-Host "Tests failed!" -ForegroundColor Red
    Write-Host "Exit code: $testExitCode" -ForegroundColor Red
}

exit $testExitCode