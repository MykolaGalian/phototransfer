#!/bin/bash

# PhotoTransfer Build Script for Unix/Linux/macOS
# Cross-platform build script that supports Debug/Release configurations

set -euo pipefail

# Default values
CONFIGURATION="Release"
RUNTIME="current"
CLEAN=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Help function
show_help() {
    cat << EOF
PhotoTransfer Build Script

Usage: ./build.sh [OPTIONS]

Options:
    -c, --config CONFIGURATION    Build configuration: Debug or Release (default: Release)
    -r, --runtime RUNTIME        Target runtime: linux-x64, osx-x64, win-x64, all, or current (default: current)
    --clean                       Clean build artifacts before building
    -h, --help                   Show this help message

Examples:
    ./build.sh                             Build for current platform in Release mode
    ./build.sh --config Debug --clean     Clean and build in Debug mode
    ./build.sh --runtime all              Build for all supported platforms
EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--config)
            CONFIGURATION="$2"
            shift 2
            ;;
        -r|--runtime)
            RUNTIME="$2"
            shift 2
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            show_help
            exit 1
            ;;
    esac
done

# Validate configuration
if [[ ! "$CONFIGURATION" =~ ^(Debug|Release)$ ]]; then
    echo -e "${RED}Error: Configuration must be 'Debug' or 'Release'${NC}"
    exit 1
fi

echo -e "${GREEN}PhotoTransfer Build Script${NC}"
echo -e "${GREEN}=========================${NC}"

SOLUTION_FILE="PhotoTransfer.sln"
PUBLISH_DIR="publish"

if [[ ! -f "$SOLUTION_FILE" ]]; then
    echo -e "${RED}Error: Solution file '$SOLUTION_FILE' not found. Run from repository root.${NC}"
    exit 1
fi

# Clean if requested
if [[ "$CLEAN" == "true" ]]; then
    echo -e "${YELLOW}Cleaning build artifacts...${NC}"
    if [[ -d "$PUBLISH_DIR" ]]; then
        rm -rf "$PUBLISH_DIR"
    fi
    dotnet clean "$SOLUTION_FILE" --configuration "$CONFIGURATION" --verbosity minimal
fi

# Restore dependencies
echo -e "${YELLOW}Restoring dependencies...${NC}"
dotnet restore "$SOLUTION_FILE" --verbosity minimal

# Build solution
echo -e "${YELLOW}Building solution ($CONFIGURATION)...${NC}"
dotnet build "$SOLUTION_FILE" --configuration "$CONFIGURATION" --no-restore --verbosity minimal

# Define target runtimes
declare -a runtimes=()
if [[ "$RUNTIME" == "all" ]]; then
    runtimes=("linux-x64" "osx-x64" "win-x64")
elif [[ "$RUNTIME" == "current" ]]; then
    case "$(uname -s)" in
        Linux*)     runtimes=("linux-x64");;
        Darwin*)    runtimes=("osx-x64");;
        MINGW*|MSYS*|CYGWIN*) runtimes=("win-x64");;
        *)          echo -e "${RED}Error: Unsupported platform$(NC)" && exit 1;;
    esac
else
    runtimes=("$RUNTIME")
fi

# Publish for each runtime
for rid in "${runtimes[@]}"; do
    echo -e "${YELLOW}Publishing for $rid...${NC}"
    output_dir="$PUBLISH_DIR/$rid"
    
    dotnet publish src/PhotoTransfer/PhotoTransfer.csproj \
        --configuration "$CONFIGURATION" \
        --runtime "$rid" \
        --self-contained false \
        --output "$output_dir" \
        --verbosity minimal
        
    if [[ $? -eq 0 ]]; then
        echo -e "  ${GREEN}Published to: $output_dir${NC}"
        
        # Show executable info
        if [[ "$rid" == win-* ]]; then
            exe_name="phototransfer.exe"
        else
            exe_name="phototransfer"
        fi
        
        exe_path="$output_dir/$exe_name"
        if [[ -f "$exe_path" ]]; then
            size=$(du -k "$exe_path" | cut -f1)
            echo -e "  ${GREEN}Executable: $exe_name (${size} KB)${NC}"
        fi
    else
        echo -e "${RED}Error: Publish failed for $rid${NC}"
        exit 1
    fi
done

echo ""
echo -e "${GREEN}Build completed successfully!${NC}"
echo -e "${GREEN}Artifacts available in: $PUBLISH_DIR/${NC}"