# MediaChrono (PhotoTransfer)

A .NET 9 console application for intelligent media organization by creation date. MediaChrono scans directories for photo and video files, extracts comprehensive metadata including EXIF dates, and transfers media to organized date-based directory structures with smart duplicate handling.

## Features

### 🎯 **Smart Media Processing**
- **Comprehensive Metadata Extraction**: Extract dates from EXIF, video metadata, and filesystem timestamps
- **Intelligent Date Selection**: Prioritize EXIF DateTimeOriginal over corrupted filesystem dates
- **Placeholder Date Detection**: Automatically ignore common placeholder dates (1970-01-01, 2001-01-01, etc.)
- **Period-Based Duplicate Filtering**: Statistics filter duplicates within each period (same as transfer logic)

### 📊 **Advanced Analysis**
- **Multi-Source Date Collection**: Store all discovered dates with their sources for debugging
- **Accurate Duplicate Handling**: Statistics match transfer behavior - filter duplicates per time period
- **Comprehensive Statistics**: View file statistics by date periods with precise counts and total file sizes
- **File Type Analysis**: Detailed breakdown of formats in your collection

### 🗂️ **Intelligent Organization**
- **Photo Indexing**: Recursively scan directories for supported image/video/audio formats
- **Multiple Directory Support**: Index multiple directories in a single operation (comma-separated paths)
- **Incremental Indexing**: Progressive processing with 5000-record saves and resume capability
- **Date-based Organization**: Transfer media by year-month periods (YYYY-MM)
- **Camera-based Grouping**: Automatically organize files by camera model within each period
- **Source Directory Preservation**: Maintain original folder structure as subdirectories within camera directories
- **Smart Duplicate Handling**: Select largest file when duplicates exist by filename within each time period
- **Operation Modes**: Move (default) or copy files with dry-run support

### ⚡ **Performance & Reliability**
- **Incremental Updates**: Add new formats without reprocessing existing files
- **Progress Visualization**: Visual feedback during all operations
- **Resume Capability**: Continue interrupted indexing from where it stopped
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

## Smart Date Selection

MediaChrono uses an intelligent algorithm to select the most reliable date from multiple sources:

### 📅 **Date Sources (in priority order):**

1. **EXIF.DateTimeOriginal** - When the photo/video was originally captured
2. **EXIF.DateTime** - General EXIF timestamp  
3. **EXIF.DateTimeDigitized** - When the media was digitized
4. **Video.CreationTime** - Video container creation time
5. **FileSystem.Creation** - File creation date
6. **FileSystem.LastWrite** - File modification date

### 🧠 **Smart Selection Logic:**

- **Placeholder Detection**: Ignores common placeholder dates (1970-01-01, 1980-01-01, 2000-01-01, 2001-01-01)
- **EXIF Priority**: If filesystem date is >1 year older than EXIF date, prefers EXIF (handles corrupted filesystem dates)
- **Oldest Valid Date**: When all dates are reliable, selects the oldest
- **Fallback Strategy**: If only placeholder dates exist, selects oldest placeholder

### 📊 **Date Information Storage:**

All discovered dates are stored in the index with their sources:

```json
"allDates": [
  {"date": "2025-09-10T00:07:26", "source": "FileSystem.Creation", "isPlaceholder": false},
  {"date": "2006-04-27T16:15:41", "source": "EXIF.DateTimeOriginal", "isPlaceholder": false},
  {"date": "2001-01-01T00:00:00", "source": "EXIF.DateTime", "isPlaceholder": true}
],
"effectiveDate": "2006-04-27T16:15:41"
```

This allows you to see exactly which dates were found and why a specific date was selected as the effective date.

## Installation

### Prerequisites
- .NET 9.0 or later

### Build from Source

```bash
# Clone or download the source code
cd PhotoTransfer

# Build the application
dotnet build src/PhotoTransfer/PhotoTransfer.csproj -c Release

# Run the executable (Windows)
./src/PhotoTransfer/bin/Release/net9.0/win-x64/phototransfer.exe

# Or on Linux/macOS
./src/PhotoTransfer/bin/Release/net9.0/linux-x64/phototransfer
```

### Using Build Script (Recommended)

```bash
# Build for current platform
./build.ps1

# Clean build for current platform
./build.ps1 -Clean

# Build for all platforms (Windows, Linux, macOS)
./build.ps1 -Runtime all

# Debug build
./build.ps1 -Configuration Debug
```

### Manual Cross-platform Publishing

```bash
# Windows
dotnet publish src/PhotoTransfer/PhotoTransfer.csproj -c Release -r win-x64 --self-contained

# Linux  
dotnet publish src/PhotoTransfer/PhotoTransfer.csproj -c Release -r linux-x64 --self-contained

# macOS
dotnet publish src/PhotoTransfer/PhotoTransfer.csproj -c Release -r osx-x64 --self-contained
```

## Usage

### Getting Help

View help information for commands:

```bash
# General help
phototransfer --help

# Help for index command
phototransfer --index --help

# Help for transfer command
phototransfer --transfer --help

# Help for statistics command
phototransfer --stat --help

# Help for types command
phototransfer --types --help
```

**Note:** When using the legacy date pattern syntax (e.g., `--2012-01`), the `--help` flag may not work as expected since the date pattern is processed before command parsing. Use the standard syntax instead: `phototransfer --transfer 2012-01 --help`

### 1. Index Media Files

First, create an index of all media files in a directory:

```bash
# Index current directory
phototransfer --index

# Index specific directory with progress
phototransfer --index --directory /path/to/photos --verbose

# Index multiple directories at once (comma-separated)
phototransfer --index --directory "/path/to/photos,/path/to/videos,D:/Backup/Media" --verbose

# Show statistics after indexing
phototransfer --index --stat

# Custom metadata file location
phototransfer --index --output /path/to/metadata.json

# Update base index with new formats (preserves existing metadata)
phototransfer --index --update-base --verbose
```

### 2. View Statistics

Analyze your media collection with file counts and total sizes:

```bash
# View statistics from existing index (shows count and total size per period)
phototransfer --stat

# View statistics from specific index file
phototransfer --stat --input /path/to/.phototransfer-index-0001.json

# Analyze all file types in directory
phototransfer --types

# Analyze specific directory
phototransfer --types --directory /path/to/photos
```

**Example Output:**
```
Statistics by Period:
====================
Date       |   Amount | Total Size
-----------------------------------
2023-01    |       45 |    1.23 GB
2023-02    |       67 |    2.45 GB
2023-03    |       23 |    891 MB
-----------------------------------
Total      |      135 |    4.54 GB
```

### 3. Transfer Media Files

#### Transfer Specific Period

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

# Alternative syntax (legacy, not recommended - --help doesn't work with this syntax)
phototransfer --2023-01 --copy
```

#### Transfer All Periods

Transfer all photos organized automatically by their monthly periods:

```bash
# Transfer all photos organized by periods
phototransfer --transfer --all

# Copy all photos (preserve originals)
phototransfer --transfer --all --copy

# Preview all transfers without changes
phototransfer --transfer --all --dry-run

# Transfer all to custom directory with verbose output
phototransfer --transfer --all --target /path/to/organized --copy --verbose
```

**Example Output:**
```
Found photos in 3 periods:
  2023-01: 45 photos
  2023-02: 67 photos  
  2023-03: 23 photos

Transferring 45 files for period 2023-01...
Period 2023-01 transfer complete - 45 files transferred successfully

Transferring 67 files for period 2023-02...
Period 2023-02 transfer complete - 67 files transferred successfully

Transferring 23 files for period 2023-03...
Period 2023-03 transfer complete - 23 files transferred successfully

All periods transfer complete - 135 files transferred successfully
```

### Command Options

**All commands support `--help` flag to display detailed usage information.**

#### Index Command (`--index`)
- `--directory <path>`: Directory or directories to index (default: current directory). **Supports multiple directories separated by comma**, e.g., `--directory "path1,path2,path3"`
- `--verbose`: Show detailed indexing information and progress
- `--output <file>`: Output file for metadata (default: `.phototransfer-index.json`)
- `--stat`: Show statistics table after indexing
- `--update-base`: Incremental update - recreate base-index with new formats while preserving existing metadata. Use when adding support for new file formats to avoid reprocessing existing files
- `--help`: Display help for this command

#### Statistics Command (`--stat`)
- `--input <file>`: Metadata file to read from (default: latest in current directory)
- `--help`: Display help for this command

#### File Types Command (`--types`)
- `--directory <path>`: Directory to analyze (default: current directory)
- `--help`: Display help for this command

#### Transfer Command (`--transfer [<period>]`)
- `<period>`: Date period in YYYY-MM format (optional when using --all)
- `--all`: Transfer all photos organized by their monthly periods
- `--copy`: Copy files instead of moving them
- `--dry-run`: Preview transfers without making changes
- `--target <directory>`: Target directory (default: `./phototransfer`)
- `--verbose`: Show detailed transfer information
- `--help`: Display help for this command

**Note:** The legacy date pattern syntax (`--YYYY-MM`) bypasses the standard command parser, so `--help` won't work with it. Use the standard syntax `--transfer YYYY-MM` instead.

## Examples

### Complete Workflow

```bash
# 1. Analyze directory structure
phototransfer --types --directory ~/Photos

# 2. Index all media files with progress
phototransfer --index --directory ~/Photos --verbose --stat

# 3. View statistics by date (with file counts and sizes)
phototransfer --stat

# 4. Preview transfers for all periods
phototransfer --transfer --all --target ~/Organized --dry-run

# 5. Perform actual transfer (copy mode) for all periods
phototransfer --transfer --all --target ~/Organized --copy --verbose
```

### Indexing Multiple Directories

Index multiple directories in a single operation:

```bash
# Index photos from multiple sources (external drives, network shares, etc.)
phototransfer --index --directory "/mnt/usb/Photos,/home/user/Pictures,/media/backup/Photos" --verbose

# Index with custom output location
phototransfer --index --directory "C:/Photos,D:/Videos,E:/Backup" --output ./combined-index.json --stat

# Update existing index with files from new directories
phototransfer --index --directory "~/Photos,~/Videos,~/Downloads" --update-base --verbose
```

**Note:** Metadata files (index files, base-index.json, and .progress files) are stored in the directory from which the application is run (current working directory), not in the indexed directories.

### Single Period Workflow

```bash
# Preview transfers for specific period
phototransfer --transfer 2023-06 --target ~/Organized --dry-run

# Perform actual transfer (copy mode) for specific period
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

#### Manual approach (period by period)
```bash
# Transfer photos from different months to yearly folders
phototransfer --transfer 2023-01 --target ~/Photos/2023 --copy
phototransfer --transfer 2023-02 --target ~/Photos/2023 --copy
phototransfer --transfer 2023-03 --target ~/Photos/2023 --copy
```

#### Automated approach (all periods at once)
```bash
# Transfer all photos organized by periods automatically
phototransfer --transfer --all --target ~/Photos --copy

# This creates:
# ~/Photos/2023-01/
# ~/Photos/2023-02/
# ~/Photos/2023-03/
# etc.
```

## File Organization

PhotoTransfer creates an organized structure with automatic grouping by camera model and source directories.

### Camera Model Organization

Files are automatically organized by camera model within each time period. Each camera gets its own directory, and within that directory, subdirectories are created for each source folder.

#### Single Period Transfer
```
target-directory/
└── 2023-06/
    ├── Canon EOS 5D/
    │   ├── Summer/
    │   │   ├── IMG001.jpg
    │   │   └── IMG002.jpg
    │   ├── Trip/
    │   │   └── IMG003.jpg
    │   └── Vacation/
    │       └── IMG004.jpg
    ├── Nikon D850/
    │   └── Holiday/
    │       ├── DSC001.jpg
    │       └── DSC002.jpg
    └── Unknown/                          # Files without camera metadata
        └── phone-photo.jpg
```

#### All Periods Transfer (--all flag)
```
target-directory/
├── 2023-01/
│   ├── Canon EOS 5D/
│   │   └── Winter/
│   │       ├── photo1.jpg
│   │       └── photo2.jpg
│   └── iPhone 12/
│       └── Work/
│           └── photo3.jpg
├── 2023-02/
│   ├── Canon EOS 5D/
│   │   └── Vacation/
│   │       ├── video1.mp4
│   │       └── photo4.jpg
│   └── Nikon D850/
│       ├── Beach/
│       │   └── photo5.jpg
│       └── Mountains/
│           └── photo6.jpg
└── 2023-03/
    └── Unknown/                          # Mixed devices without metadata
        ├── photo7.jpg
        └── largest-duplicate.jpg         # Only largest duplicate per period
```

### Organization Rules

1. **Camera Model Directories**: Each camera model gets its own subdirectory within the period folder
2. **Source Directory Subdirectories**: Within each camera directory, subdirectories are created for each source folder
3. **Preserved Source Structure**: Files maintain their original source folder organization
4. **Unknown Category**: Files without camera metadata are placed in an "Unknown" subdirectory
5. **Duplicate Handling**: Only the largest file is kept when duplicates exist within the same period

**Example**: If you have Canon EOS 5D photos in folders "Vacation", "Summer", and "Trip", they will be organized into:
```
2023-06/
└── Canon EOS 5D/
    ├── Summer/
    │   └── photos...
    ├── Trip/
    │   └── photos...
    └── Vacation/
        └── photos...
```

This makes it easy to see:
- Which camera was used
- Which source folders the files came from
- Files organized by both camera and original location

## Metadata Files

### Index Files

The indexing process creates incremental `.phototransfer-index-XXXX.json` files containing:

```json
{
  "indexedAt": "2025-09-07T09:57:57.893Z",
  "workingDirectory": "/path/to/photos",
  "workingDirectories": ["/path/to/photos", "/path/to/videos"],
  "version": "1.0.0",
  "totalCount": 150,
  "supportedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".cr3", ".crw", ".cr2", ".avi", ".mp4", ".3gp", ".m4a", ".mov", ".jpg_128x96", ".mp4_128x96"],
  "photos": [
    {
      "filePath": "/path/to/Vacation/photo.jpg",
      "fileName": "photo.jpg",
      "extension": ".jpg",
      "fileSize": 2048576,
      "hash": "sha256hash",
      "cameraModel": "Canon EOS 5D",
      "sourceDirectory": "Vacation",
      "creationDate": "2023-06-15T14:30:00Z",
      "modificationDate": "2023-06-15T14:25:00Z",
      "effectiveDate": "2023-06-15T14:25:00Z",
      "allDates": [
        {"date": "2023-06-15T14:30:00Z", "source": "FileSystem.Creation", "isPlaceholder": false},
        {"date": "2023-06-15T14:25:00Z", "source": "EXIF.DateTimeOriginal", "isPlaceholder": false},
        {"date": "2023-06-15T14:25:00Z", "source": "EXIF.DateTime", "isPlaceholder": false}
      ],
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
  "workingDirectories": ["/path/to/photos", "/path/to/videos"],
  "totalFiles": 150,
  "filePaths": [
    "/path/to/photos/photo1.jpg",
    "/path/to/videos/video.mov",
    "/path/to/photos/audio.m4a"
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

- **System.CommandLine**: CLI interface framework
- **System.Text.Json**: JSON serialization with source generation for trimming support
- **MetadataExtractor**: EXIF and video metadata extraction library
- **NUnit**: Testing framework (dev dependency)

## License

This project is provided as-is for educational and personal use.