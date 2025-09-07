# Research: PhotoTransfer Console Application

## Research Findings

### Image File Extensions Support
**Decision**: Support jpg, jpeg, png, gif, bmp, tiff formats  
**Rationale**: These are the most common image formats, .NET has built-in support for detecting these via file headers, minimal processing required  
**Alternatives considered**: All image formats (complex), only jpg/png (too restrictive)

### Undated Photo Handling
**Decision**: Use file system creation date as fallback, skip files with no date information  
**Rationale**: File system dates are reliable fallback, prevents data loss by not making assumptions about photo dates  
**Alternatives considered**: Prompt user for date (breaks automation), create "unknown" directory (clutters output)

### Duplicate File Resolution
**Decision**: Add numeric suffixes (0), (1), (2) etc. to filename before extension when transferring  
**Rationale**: User requirement specified this approach, prevents file overwrites, maintains original names  
**Alternatives considered**: Overwrite (data loss risk), timestamp suffix (less readable)

### .NET 9 Minimal Dependencies Approach
**Decision**: Use System.Text.Json, System.IO, System.CommandLine only  
**Rationale**: All required functionality available in .NET BCL, reduces deployment complexity, improves startup performance  
**Alternatives considered**: Third-party JSON library (unnecessary), file system watcher (not required), image processing libraries (not needed for metadata only)

### Metadata File Format
**Decision**: JSON format with structured schema for photo records  
**Rationale**: Human-readable, .NET native support, easy to version and extend, debugging friendly  
**Alternatives considered**: Binary format (not readable), XML (verbose), database (overkill for local storage)

### Photo Metadata Extraction
**Decision**: Use System.Drawing.Image.PropertyItems for EXIF creation date, fall back to file system date  
**Rationale**: Built-in .NET support, handles most common image metadata standards  
**Alternatives considered**: Third-party EXIF libraries (violates minimal dependencies), file date only (less accurate)

### CLI Command Structure
**Decision**: Single executable with flag-based commands: `phototransfer --index` and `phototransfer --YYYY-MM`  
**Rationale**: Matches user requirements exactly, simple interface, familiar pattern  
**Alternatives considered**: Subcommands (more complex), separate executables (deployment complexity)

### File Operations Safety
**Decision**: Atomic move operations using temp directory, rollback on failure  
**Rationale**: Prevents data loss during transfer operations, ensures operation integrity  
**Alternatives considered**: Direct move (failure risk), copy-then-delete (slower, more disk space)

### Performance Optimization
**Decision**: Stream-based processing for large directories, lazy enumeration, parallel file operations where safe  
**Rationale**: Handles large photo collections efficiently, minimal memory footprint  
**Alternatives considered**: Load all files to memory (memory issues), single-threaded (slower)

### Progress Visualization During Indexing
**Decision**: Pulsating asterisk animation using Console.SetCursorPosition and Timer-based updates  
**Rationale**: User requirement for visual feedback during long operations, minimal CPU overhead, works on all console platforms  
**Alternatives considered**: Progress bar (more complex), percentage counter (requires knowing total upfront), spinner (multiple characters)

## Technology Stack Confirmation

- **Runtime**: .NET 9 (cross-platform, modern APIs)
- **CLI Framework**: System.CommandLine (official Microsoft library)
- **JSON Processing**: System.Text.Json (fastest, built-in)
- **Image Metadata**: System.Drawing (EXIF support)
- **File Operations**: System.IO (reliable, atomic operations)
- **Testing**: NUnit (mature, .NET standard)

## Architecture Decisions

- **Library Structure**: Three core libraries (PhotoIndexer, PhotoTransfer, MetadataStore)
- **Data Flow**: Index → Metadata File → Transfer (two-phase operation)
- **Error Handling**: Fail-fast with detailed error messages, partial operation recovery
- **Logging**: Structured console output with operation progress

All NEEDS CLARIFICATION items from feature specification have been resolved with research-backed decisions.