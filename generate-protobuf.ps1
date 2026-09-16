#!/usr/bin/env pwsh
# XrayUI Protocol Buffer Code Generation Script
# This script regenerates the gRPC and Protocol Buffer C# code from stats_service.proto

param(
    [string]$ProtobufDir = "Services\Traffic",
    [string]$ProtoFile = "stats_service.proto"
)

$ErrorActionPreference = "Stop"

# Determine platform and find Grpc.Tools
$NugetPackages = Join-Path $env:USERPROFILE ".nuget\packages"
$GrpcToolsVersion = "2.71.0"

if ($IsLinux -or $IsOSX) {
    $PluginName = "grpc_csharp_plugin"
    $PluginPath = Join-Path $NugetPackages "grpc.tools\$GrpcToolsVersion\tools\linux_x64\$PluginName"
    if (-not (Test-Path $PluginPath)) {
        $PluginPath = Join-Path $NugetPackages "grpc.tools\$GrpcToolsVersion\tools\macosx_x64\$PluginName"
    }
} else {
    $PluginName = "grpc_csharp_plugin.exe"
    if ([Environment]::Is64BitOperatingSystem) {
        $PluginPath = Join-Path $NugetPackages "grpc.tools\$GrpcToolsVersion\tools\windows_x64\$PluginName"
    } else {
        $PluginPath = Join-Path $NugetPackages "grpc.tools\$GrpcToolsVersion\tools\windows_x86\$PluginName"
    }
}

Write-Host "XrayUI Protocol Buffer Code Generator" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Protobuf Directory: $ProtobufDir"
Write-Host "Proto File: $ProtoFile"
Write-Host "gRPC Plugin: $PluginPath"
Write-Host ""

# Verify protoc is available
$ProtocCmd = Get-Command protoc -ErrorAction SilentlyContinue
if (-not $ProtocCmd) {
    Write-Host "ERROR: protoc not found in PATH" -ForegroundColor Red
    Write-Host "Install protobuf-compiler:"
    Write-Host "  Windows: choco install protoc"
    Write-Host "  Linux:   apt-get install protobuf-compiler"
    Write-Host "  macOS:   brew install protobuf"
    exit 1
}

Write-Host "Found protoc: $($ProtocCmd.Source)" -ForegroundColor Green

# Verify gRPC plugin is available
if (-not (Test-Path $PluginPath)) {
    Write-Host "ERROR: gRPC C# plugin not found at $PluginPath" -ForegroundColor Red
    Write-Host "Make sure Grpc.Tools $GrpcToolsVersion is restored in NuGet packages"
    exit 1
}

Write-Host "Found gRPC plugin: $PluginPath" -ForegroundColor Green
Write-Host ""

# Change to the protobuf directory
Push-Location $ProtobufDir

try {
    # Generate C# code
    Write-Host "Generating Protocol Buffer C# code..." -ForegroundColor Yellow
    
    if ($IsLinux -or $IsOSX) {
        & protoc --csharp_out=. --grpc_out=. --plugin=protoc-gen-grpc=$PluginPath $ProtoFile
    } else {
        & protoc --csharp_out=. --grpc_out=. "--plugin=protoc-gen-grpc=$PluginPath" $ProtoFile
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: protoc generation failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
    
    # Verify generated files
    $GeneratedFiles = @("StatsService.cs", "StatsServiceGrpc.cs")
    Write-Host ""
    Write-Host "Generated files:" -ForegroundColor Green
    foreach ($file in $GeneratedFiles) {
        if (Test-Path $file) {
            $size = (Get-Item $file).Length
            Write-Host "  ✓ $file ($size bytes)"
        } else {
            Write-Host "  ✗ $file (NOT FOUND)" -ForegroundColor Red
            exit 1
        }
    }
    
    Write-Host ""
    Write-Host "SUCCESS: Protocol Buffer code regenerated successfully!" -ForegroundColor Green
    Write-Host "The generated files are ready for compilation."
    
} finally {
    Pop-Location
}
