# ref-test Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-09-07

## Active Technologies
- C# .NET 9 Console Application (001-the-console-application)
- System.Text.Json for metadata storage with source generation (trimming-safe)
- System.CommandLine for CLI interface  
- System.Drawing for EXIF metadata extraction
- NUnit for testing framework
- SHA256 for file hash generation
- Progressive indexing with 5000-record saves

## Project Structure
```
src/PhotoTransfer/
├── Commands/        # IndexCommand, TransferCommand, StatCommand, TypesCommand
├── Models/          # PhotoMetadata, PhotoIndex, IndexingProgress, BaseIndex
├── Services/        # PhotoIndexer, PhotoTransfer, MetadataStore
├── JsonContext.cs   # JSON source generation context
└── Program.cs       # Application entry point

tests/PhotoTransfer.Tests/
├── ContractTests/   # CLI interface contract tests
├── IntegrationTests/# File system operation tests
└── UnitTests/       # Individual component tests
```

## Commands
# PhotoTransfer console application commands:
# phototransfer --index [--directory <path>] [--verbose] [--stat] [--update-base] [--output <file>]
# phototransfer --stat [--input <file>]
# phototransfer --types [--directory <path>]
# phototransfer --transfer <YYYY-MM> [--copy] [--dry-run] [--target <path>] [--verbose]
# phototransfer --YYYY-MM [--copy] [--dry-run] [--target <path>] (legacy syntax)
# phototransfer --help
# phototransfer --version

## Supported File Formats
- Images: .jpg, .jpeg, .png, .gif, .bmp, .tiff, .tif
- RAW: .cr3, .crw, .cr2
- Video: .avi, .mp4, .3gp, .mov
- Audio: .m4a
- Thumbnails: .jpg_128x96, .mp4_128x96

## Code Style
C#: Follow standard .NET conventions, minimal dependencies approach
- Use System.Text.Json with source generation for trimming-safe serialization
- JsonContext.Default for all serialization operations
- Direct .NET APIs, no wrapper classes
- Structured logging via console output with progress callbacks
- Progressive indexing with base-index and incremental saves
- TDD workflow: tests first, must fail before implementation

## Recent Changes
- 001-the-console-application: Added PhotoTransfer console app with photo indexing and date-based transfer functionality
- Added .3gp, .m4a, .mov, .jpg_128x96, .mp4_128x96 format support
- Implemented dual-date indexing (creation + modification dates, uses earliest as effective date)
- Added --stat command for date-period statistics
- Added --types command for file type analysis
- Added --update-base flag for incremental format updates (avoids reprocessing existing files when adding new formats)
- Implemented progressive indexing with 5000-record saves and resume capability
- Added incremental file naming (.phototransfer-index-XXXX.json)
- Implemented JSON source generation for trimming compatibility
- Added base-index.json for file path tracking

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->