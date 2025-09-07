# PhotoTransfer

A .NET 9 console application for organizing photos by creation date. PhotoTransfer scans directories for photo files, extracts metadata including creation dates, and transfers photos to organized date-based directory structures.

## Features

- **Photo Indexing**: Recursively scan directories for supported image/video/audio formats
- **Incremental Indexing**: Progressive processing with 5000-record saves and resume capability
- **Smart Date Detection**: Extract creation dates from EXIF data with file date fallback using earliest date
- **Date-based Organization**: Transfer photos by year-month periods (YYYY-MM)
- **Statistics & Analysis**: View file statistics by date periods and file type analysis
- **Duplicate Handling**: Automatic numeric suffixes for duplicate filenames
- **Operation Modes**: Move (default) or copy files with dry-run support
- **Incremental Updates**: Add new formats without reprocessing existing files
- **Progress Visualization**: Visual feedback during all operations
- **Cross-platform**: Self-contained executables for Windows, Linux, and macOS

## Supported Formats

### Image Formats
- JPEG (`.jpg`, `.jpeg`)
- PNG (`.png`) 
- GIF (`.gif`)
- BMP (`.bmp`)
- TIFF (`.tiff`, `.tif`)

### RAW Formats
- Canon RAW v3 (`.cr3`)
- Canon RAW (`.crw`, `.cr2`)

### Video Formats
- AVI (`.avi`)
- MP4 (`.mp4`)
- 3GP (`.3gp`)
- QuickTime (`.mov`)

### Audio Formats
- AAC (`.m4a`)

### Thumbnail Formats
- JPEG Thumbnails (`.jpg_128x96`)
- MP4 Thumbnails (`.mp4_128x96`)

*All formats support both uppercase and lowercase extensions (e.g., `.JPG`, `.jpg`, `.Mp4`, `.MP4`, `.M4A`, `.m4a`)*

## Installation

### Prerequisites
- .NET 9.0 or later

### Build from Source

```bash
# Clone or download the source code
cd PhotoTransfer

# Build the application
dotnet build src/PhotoTransfer/PhotoTransfer.csproj -c Release

# Run the executable
./src/PhotoTransfer/bin/Release/net9.0/phototransfer
```

### Cross-platform Publishing

```bash
# Windows
dotnet publish src/PhotoTransfer/PhotoTransfer.csproj -c Release -r win-x64 --self-contained

# Linux  
dotnet publish src/PhotoTransfer/PhotoTransfer.csproj -c Release -r linux-x64 --self-contained

# macOS
dotnet publish src/PhotoTransfer/PhotoTransfer.csproj -c Release -r osx-x64 --self-contained
```

## Usage

### 1. Index Media Files

First, create an index of all media files in a directory:

```bash
# Index current directory
phototransfer --index

# Index specific directory with progress
phototransfer --index --directory /path/to/photos --verbose

# Show statistics after indexing
phototransfer --index --stat

# Custom metadata file location
phototransfer --index --output /path/to/metadata.json

# Update base index with new formats (preserves existing metadata)
phototransfer --index --update-base --verbose
```

### 2. View Statistics

Analyze your media collection:

```bash
# View statistics from existing index
phototransfer --stat

# View statistics from specific index file
phototransfer --stat --input /path/to/.phototransfer-index-0001.json

# Analyze all file types in directory
phototransfer --types

# Analyze specific directory
phototransfer --types --directory /path/to/photos
```

### 3. Transfer Media Files

Transfer files from a specific year-month period:

```bash
# Transfer photos from January 2023
phototransfer --transfer 2023-01

# Copy instead of move
phototransfer --transfer 2023-01 --copy

# Dry run (preview without changes)
phototransfer --transfer 2023-01 --dry-run

# Custom target directory
phototransfer --transfer 2023-01 --target /path/to/output

# Verbose transfer information
phototransfer --transfer 2023-01 --verbose

# Alternative syntax (legacy)
phototransfer --2023-01 --copy
```

### Command Options

#### Index Command (`--index`)
- `--directory <path>`: Directory to index (default: current directory)
- `--verbose`: Show detailed indexing information and progress
- `--output <file>`: Output file for metadata (default: `.phototransfer-index.json`)
- `--stat`: Show statistics table after indexing
- `--update-base`: Incremental update - recreate base-index with new formats while preserving existing metadata. Use when adding support for new file formats to avoid reprocessing existing files

#### Statistics Command (`--stat`)
- `--input <file>`: Metadata file to read from (default: latest in current directory)

#### File Types Command (`--types`)
- `--directory <path>`: Directory to analyze (default: current directory)

#### Transfer Command (`--transfer <period>`)
- `--copy`: Copy files instead of moving them
- `--dry-run`: Preview transfers without making changes
- `--target <directory>`: Target directory (default: `./phototransfer`)
- `--verbose`: Show detailed transfer information

## Examples

### Complete Workflow

```bash
# 1. Analyze directory structure
phototransfer --types --directory ~/Photos

# 2. Index all media files with progress
phototransfer --index --directory ~/Photos --verbose --stat

# 3. View statistics by date
phototransfer --stat

# 4. Preview transfers for specific period
phototransfer --transfer 2023-06 --target ~/Organized --dry-run

# 5. Perform actual transfer (copy mode)
phototransfer --transfer 2023-06 --target ~/Organized --copy --verbose
```

### Adding New File Format Support

When new file formats are added to the application, use `--update-base` to avoid reprocessing existing files:

```bash
# Update base index with new formats but preserve existing metadata
phototransfer --index --update-base --verbose --stat

# This will:
# 1. Recreate base-index.json with new supported formats
# 2. Load existing metadata from the latest index file
# 3. Process only files with new formats (e.g., .m4a, .mov, .jpg_128x96, .mp4_128x96)
# 4. Merge existing + new metadata into a new index file
# 5. Show statistics of the complete collection
```

**Benefits of --update-base:**
- ✅ No need to reprocess thousands of existing files
- ✅ Preserves all existing metadata (EXIF dates, hashes, etc.)
- ✅ Only extracts metadata from new format files
- ✅ Significant time savings on large collections

### Organize Photos by Year

```bash
# Transfer photos from different months to yearly folders
phototransfer --transfer 2023-01 --target ~/Photos/2023 --copy
phototransfer --transfer 2023-02 --target ~/Photos/2023 --copy
phototransfer --transfer 2023-03 --target ~/Photos/2023 --copy
```

## File Organization

PhotoTransfer creates the following structure:

```
target-directory/
└── YYYY-MM/
    ├── photo1.jpg
    ├── photo2.png
    ├── duplicate-photo(0).jpg
    └── duplicate-photo(1).jpg
```

## Metadata Files

### Index Files

The indexing process creates incremental `.phototransfer-index-XXXX.json` files containing:

```json
{
  "indexedAt": "2025-09-07T09:57:57.893Z",
  "workingDirectory": "/path/to/photos", 
  "version": "1.0.0",
  "totalCount": 150,
  "supportedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".cr3", ".crw", ".cr2", ".avi", ".mp4", ".3gp", ".m4a", ".mov"],
  "photos": [
    {
      "filePath": "/path/to/photo.jpg",
      "fileName": "photo.jpg", 
      "extension": ".jpg",
      "fileSize": 2048576,
      "hash": "sha256hash",
      "creationDate": "2023-06-15T14:30:00Z",
      "modificationDate": "2023-06-15T14:25:00Z",
      "effectiveDate": "2023-06-15T14:25:00Z",
      "isTransferred": false,
      "transferredTo": null
    }
  ]
}
```

### Base Index File

A `base-index.json` file is created to track all discoverable files:

```json
{
  "createdAt": "2025-09-07T16:07:42.758Z",
  "workingDirectory": "/path/to/photos",
  "totalFiles": 150,
  "filePaths": [
    "/path/to/photo1.jpg",
    "/path/to/video.mov",
    "/path/to/audio.m4a"
  ]
}
```

### File Naming Convention

- `.phototransfer-index-0001.json` - First index run
- `.phototransfer-index-0002.json` - Second index run (latest)
- `base-index.json` - File path registry
- `.phototransfer-index-XXXX.progress` - Resume file (auto-deleted)

## Exit Codes

- `0`: Success
- `1`: File/directory not found, invalid arguments
- `2`: Permission denied, no photos found for period
- `3`: General error, partial transfer failure

## Development

### Project Structure

```
src/PhotoTransfer/
├── Commands/          # CLI command handlers
├── Models/           # Data models
├── Services/         # Core business logic
├── Utilities/        # Helper classes
└── Program.cs        # Application entry point

tests/PhotoTransfer.Tests/
├── ContractTests/    # CLI interface tests
├── IntegrationTests/ # Workflow tests
└── UnitTests/        # Service unit tests
```

### Build Scripts

- `build.ps1` / `build.sh`: Build and publish for all platforms
- `test.ps1` / `test.sh`: Run test suite with coverage

### Dependencies

- System.CommandLine: CLI interface framework
- System.Text.Json: JSON serialization
- NUnit: Testing framework (dev dependency)

## License

This project is provided as-is for educational and personal use.