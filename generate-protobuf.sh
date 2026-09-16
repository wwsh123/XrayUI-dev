#!/bin/bash
# XrayUI Protocol Buffer Code Generation Script
# This script regenerates the gRPC and Protocol Buffer C# code from stats_service.proto

set -e

PROTOBUF_DIR="${1:-Services/Traffic}"
PROTO_FILE="${2:-stats_service.proto}"
GRPC_TOOLS_VERSION="2.71.0"

echo "XrayUI Protocol Buffer Code Generator"
echo "======================================"
echo ""
echo "Protobuf Directory: $PROTOBUF_DIR"
echo "Proto File: $PROTO_FILE"
echo ""

# Determine the platform and set the gRPC plugin path
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    PLUGIN_PATH="$HOME/.nuget/packages/grpc.tools/$GRPC_TOOLS_VERSION/tools/linux_x64/grpc_csharp_plugin"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    PLUGIN_PATH="$HOME/.nuget/packages/grpc.tools/$GRPC_TOOLS_VERSION/tools/macosx_x64/grpc_csharp_plugin"
else
    echo "ERROR: Unsupported operating system: $OSTYPE"
    exit 1
fi

# Verify protoc is available
if ! command -v protoc &> /dev/null; then
    echo "ERROR: protoc not found in PATH"
    echo "Install protobuf-compiler:"
    echo "  Linux:   sudo apt-get install protobuf-compiler"
    echo "  macOS:   brew install protobuf"
    exit 1
fi

PROTOC_VERSION=$(protoc --version | cut -d' ' -f2)
echo "Found protoc version: $PROTOC_VERSION"

# Verify gRPC plugin is available
if [ ! -f "$PLUGIN_PATH" ]; then
    echo "ERROR: gRPC C# plugin not found at $PLUGIN_PATH"
    echo "Make sure Grpc.Tools $GRPC_TOOLS_VERSION is restored in NuGet packages"
    exit 1
fi

echo "Found gRPC plugin: $PLUGIN_PATH"
chmod +x "$PLUGIN_PATH"
echo ""

# Change to the protobuf directory
cd "$PROTOBUF_DIR"

# Generate C# code
echo "Generating Protocol Buffer C# code..."
protoc --csharp_out=. --grpc_out=. --plugin=protoc-gen-grpc="$PLUGIN_PATH" "$PROTO_FILE"

# Verify generated files
echo ""
echo "Generated files:"
GENERATED_FILES=("StatsService.cs" "StatsServiceGrpc.cs")
for file in "${GENERATED_FILES[@]}"; do
    if [ -f "$file" ]; then
        SIZE=$(wc -c < "$file")
        echo "  ✓ $file ($SIZE bytes)"
    else
        echo "  ✗ $file (NOT FOUND)"
        exit 1
    fi
done

echo ""
echo "SUCCESS: Protocol Buffer code regenerated successfully!"
echo "The generated files are ready for compilation."
