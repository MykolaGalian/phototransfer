#!/bin/bash

# PhotoTransfer Test Script for Unix/Linux/macOS
# Cross-platform test script with coverage and filtering options

set -euo pipefail

# Default values
CATEGORY="all"
CONFIGURATION="Debug"
COVERAGE=false
VERBOSE=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Help function
show_help() {
    cat << EOF
PhotoTransfer Test Script

Usage: ./test.sh [OPTIONS]

Options:
    -c, --category CATEGORY       Test category: unit, integration, contract, all (default: all)
    --config CONFIGURATION        Build configuration: Debug or Release (default: Debug)
    --coverage                    Generate code coverage report
    -v, --verbose                Show detailed test output
    -h, --help                   Show this help message

Examples:
    ./test.sh                            Run all tests in Debug mode
    ./test.sh --category unit --coverage Run unit tests with coverage
    ./test.sh --verbose --config Release Run all tests in Release with detailed output
EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--category)
            CATEGORY="$2"
            shift 2
            ;;
        --config)
            CONFIGURATION="$2"
            shift 2
            ;;
        --coverage)
            COVERAGE=true
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
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

# Validate inputs
if [[ ! "$CATEGORY" =~ ^(unit|integration|contract|all)$ ]]; then
    echo -e "${RED}Error: Category must be 'unit', 'integration', 'contract', or 'all'${NC}"
    exit 1
fi

if [[ ! "$CONFIGURATION" =~ ^(Debug|Release)$ ]]; then
    echo -e "${RED}Error: Configuration must be 'Debug' or 'Release'${NC}"
    exit 1
fi

echo -e "${GREEN}PhotoTransfer Test Script${NC}"
echo -e "${GREEN}========================${NC}"

SOLUTION_FILE="PhotoTransfer.sln"
TEST_PROJECT="tests/PhotoTransfer.Tests/PhotoTransfer.Tests.csproj"
COVERAGE_DIR="coverage"

if [[ ! -f "$SOLUTION_FILE" ]]; then
    echo -e "${RED}Error: Solution file '$SOLUTION_FILE' not found. Run from repository root.${NC}"
    exit 1
fi

if [[ ! -f "$TEST_PROJECT" ]]; then
    echo -e "${RED}Error: Test project '$TEST_PROJECT' not found.${NC}"
    exit 1
fi

# Build solution first
echo -e "${YELLOW}Building solution ($CONFIGURATION)...${NC}"
dotnet build "$SOLUTION_FILE" --configuration "$CONFIGURATION" --verbosity minimal

# Prepare test arguments
test_args=(
    "test"
    "$TEST_PROJECT"
    "--configuration" "$CONFIGURATION"
    "--no-build"
)

# Add verbosity
if [[ "$VERBOSE" == "true" ]]; then
    test_args+=("--verbosity" "normal")
else
    test_args+=("--verbosity" "minimal")
fi

# Add category filter
if [[ "$CATEGORY" != "all" ]]; then
    case "$CATEGORY" in
        unit)
            test_args+=("--filter" "TestCategory=Unit")
            ;;
        integration)
            test_args+=("--filter" "TestCategory=Integration")
            ;;
        contract)
            test_args+=("--filter" "TestCategory=Contract")
            ;;
    esac
    echo -e "${YELLOW}Running $CATEGORY tests...${NC}"
else
    echo -e "${YELLOW}Running all tests...${NC}"
fi

# Add coverage collection
if [[ "$COVERAGE" == "true" ]]; then
    echo -e "${YELLOW}Coverage reporting enabled${NC}"
    test_args+=("--collect:XPlat Code Coverage")
    test_args+=("--results-directory" "$COVERAGE_DIR")
fi

# Add logger for better output
test_args+=("--logger" "console;verbosity=normal")

# Run tests
echo -e "${GRAY}Test command: dotnet ${test_args[*]}${NC}"
dotnet "${test_args[@]}"
test_exit_code=$?

if [[ $test_exit_code -eq 0 ]]; then
    echo ""
    echo -e "${GREEN}All tests passed!${NC}"
    
    # Process coverage if requested
    if [[ "$COVERAGE" == "true" && -d "$COVERAGE_DIR" ]]; then
        echo -e "${YELLOW}Processing coverage report...${NC}"
        
        # Find coverage files
        coverage_files=($(find "$COVERAGE_DIR" -name "coverage.cobertura.xml" -type f))
        
        if [[ ${#coverage_files[@]} -gt 0 ]]; then
            echo -e "${GREEN}Coverage files found:${NC}"
            for file in "${coverage_files[@]}"; do
                echo -e "  ${GREEN}$file${NC}"
            done
            
            # Try to generate HTML report if reportgenerator is available
            if command -v dotnet-reportgenerator-globaltool &> /dev/null; then
                html_report="$COVERAGE_DIR/html"
                dotnet reportgenerator \
                    -reports:"${coverage_files[0]}" \
                    -targetdir:"$html_report" \
                    -reporttypes:Html
                
                echo -e "${GREEN}HTML coverage report: $html_report/index.html${NC}"
            else
                echo -e "${YELLOW}Install dotnet-reportgenerator-globaltool for HTML reports${NC}"
                echo -e "${YELLOW}  dotnet tool install -g dotnet-reportgenerator-globaltool${NC}"
            fi
        fi
    fi
else
    echo ""
    echo -e "${RED}Tests failed!${NC}"
    echo -e "${RED}Exit code: $test_exit_code${NC}"
fi

exit $test_exit_code