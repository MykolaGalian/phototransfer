# CLI Interface Contract

## Application: PhotoTransfer

### Command Structure
Single executable with flag-based commands following the pattern:
```
phototransfer <command> [options]
```

## Commands

### Index Command
**Signature**: `phototransfer --index [options]`

**Purpose**: Scan current directory and subdirectories for media files, extract metadata, and create local index file with incremental saving every 5000 records.

**Options**:
- `--directory <path>`: Specify directory to index (default: current directory)
- `--output <path>`: Specify metadata file location (default: `.phototransfer-index.json`)
- `--verbose`: Show detailed progress information and per-file processing
- `--stat`: Show statistics table after indexing completion
- `--update-base`: Recreate base-index with new formats while preserving existing metadata (incremental update)
- `--help`: Show command help

**Exit Codes**:
- `0`: Success - indexing completed
- `1`: Error - directory not found or inaccessible
- `2`: Error - insufficient permissions
- `3`: Error - metadata file write failed

**Output Format**:
```
Indexing photos in: C:\Photos
Found 1,250 image files
Processing * (pulsating asterisk shows progress)
...
Index complete: 1,250 photos indexed
Metadata saved to: .phototransfer-index.json
```

**Progress Visualization**:
- Pulsating asterisk (*) during processing operations
- Asterisk appears/disappears in 500ms intervals
- Shows application is actively working during long operations

**Error Output**:
```
ERROR: Directory not found: C:\InvalidPath
ERROR: Permission denied: Cannot write to .phototransfer-index.json
ERROR: Invalid image file: corrupted_image.jpg
```

### Transfer Command
**Signature**: `phototransfer --transfer <YYYY-MM> [options]`

**Purpose**: Transfer photos from specified time period to organized directory structure.

**Examples**:
- `phototransfer --transfer 2012-01`
- `phototransfer --transfer 2023-12`

**Options**:
- `--copy`: Copy files instead of moving them
- `--dry-run`: Show what would be transferred without moving files
- `--target <path>`: Base directory for transfers (default: `phototransfer`)
- `--verbose`: Show detailed progress information
- `--help`: Show command help

**Exit Codes**:
- `0`: Success - all photos transferred
- `1`: Error - metadata file not found or invalid
- `2`: Error - no photos found for specified period
- `3`: Error - transfer operation failed
- `4`: Error - insufficient disk space or permissions

**Output Format**:
```
Loading metadata from: .phototransfer-index.json
Found 45 photos for period: 2012-01
Target directory: phototransfer\2012-01\
Transferring: IMG_001.jpg -> phototransfer\2012-01\IMG_001.jpg
Transferring: IMG_002.jpg -> phototransfer\2012-01\IMG_002(0).jpg (duplicate)
...
Transfer complete: 45 photos moved to phototransfer\2012-01\
```

**Error Output**:
```
ERROR: Metadata file not found: .phototransfer-index.json
ERROR: No photos found for period: 2012-13 (invalid month)
ERROR: Transfer failed: IMG_001.jpg (file in use)
ERROR: Insufficient disk space for transfer
```

### Statistics Command
**Signature**: `phototransfer --stat [options]`

**Purpose**: Display statistics by date periods from existing index file.

**Options**:
- `--input <path>`: Specify metadata file location (default: latest in current directory)
- `--help`: Show command help

**Output Format**:
```
Statistics from: .phototransfer-index-0001.json

Date Period | Photo Count
------------|------------
2021-03     | 45
2021-06     | 128
2022-01     | 67
2023-12     | 234

Total: 474 photos indexed
```

### File Types Command
**Signature**: `phototransfer --types [options]`

**Purpose**: Analyze all file types in directory structure.

**Options**:
- `--directory <path>`: Directory to analyze (default: current directory)
- `--help`: Show command help

**Output Format**:
```
File type analysis for: C:\Photos

Extension | Count
----------|------
.jpg      | 1,245
.png      | 387
.mp4      | 156
.m4a      | 23
.mov      | 12

Total: 1,823 files analyzed
```

### Global Options
Available for all commands:

- `--version`: Show application version
- `--help`: Show general help information

**Version Output**:
```
PhotoTransfer v1.0.0
.NET 9.0 Console Application
Copyright (c) 2025
```

**Help Output**:
```
PhotoTransfer - Photo organization by date

Usage: phototransfer <command> [options]

Commands:
  --index              Index media files in current directory
  --transfer <period>  Transfer photos from specified month
  --stat              Show statistics by date periods
  --types             Analyze file types in directory

Examples:
  phototransfer --index
  phototransfer --transfer 2012-01
  phototransfer --index --directory C:\Photos --verbose --stat
  phototransfer --transfer 2023-12 --copy --dry-run
  phototransfer --stat --input .phototransfer-index-0001.json
  phototransfer --types --directory C:\Photos
  phototransfer --index --update-base --verbose

For command-specific help:
  phototransfer --index --help
  phototransfer --transfer 2012-01 --help
  phototransfer --stat --help
  phototransfer --types --help
```

## Data Validation

### Time Period Format
- Pattern: `YYYY-MM` (used with --transfer command)
- Year range: 1900-2025 (current year + 1)
- Month range: 01-12
- Invalid examples: `2012-13`, `12-01`, `2012-1`

### File Path Validation
- Must be absolute or relative valid paths
- Cannot contain invalid characters for target OS
- Must have appropriate permissions

### Extension Validation
- Must start with dot (.)
- Case insensitive matching
- Default supported: .jpg, .jpeg, .png, .gif, .bmp, .tiff, .tif, .cr3, .crw, .cr2, .avi, .mp4, .3gp, .m4a, .mov, .jpg_128x96, .mp4_128x96

## Error Handling

### Graceful Degradation
- Continue processing on individual file errors
- Report errors at end of operation
- Preserve partial results where possible

### Recovery Scenarios
- Corrupted metadata file: Offer to rebuild index
- Permission errors: Suggest running as administrator
- Disk space issues: Show required vs available space
- Network path issues: Suggest local directory usage