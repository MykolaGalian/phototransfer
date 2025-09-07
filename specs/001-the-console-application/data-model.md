# Data Model: PhotoTransfer

## Core Entities

### PhotoMetadata
Represents metadata information for a single photo file.

**Fields**:
- `FilePath` (string): Full path to the photo file
- `FileName` (string): Original filename with extension
- `CreationDate` (DateTime): Photo creation date from EXIF or file system
- `FileSize` (long): File size in bytes
- `Extension` (string): File extension (e.g., ".jpg", ".png")
- `Hash` (string): SHA-256 hash for duplicate detection
- `IsTransferred` (bool): Whether this photo has been transferred
- `TransferredTo` (string?): Destination path if transferred

**Validation Rules**:
- FilePath must be valid absolute path
- FileName must not be empty
- CreationDate must be valid DateTime
- FileSize must be > 0
- Extension must be in supported formats list
- Hash must be valid SHA-256 string

### PhotoIndex
Represents the complete collection of indexed photos.

**Fields**:
- `IndexedAt` (DateTime): When the index was created
- `WorkingDirectory` (string): Base directory that was indexed
- `Photos` (List<PhotoMetadata>): Collection of all indexed photos
- `Version` (string): Metadata format version (for future migrations)
- `TotalCount` (int): Total number of photos indexed
- `SupportedExtensions` (string[]): Extensions that were processed

**Validation Rules**:
- IndexedAt must be valid DateTime
- WorkingDirectory must be valid path
- Photos collection must not be null
- Version must follow semantic versioning format
- TotalCount must match Photos.Count
- SupportedExtensions must contain valid extensions

### TransferOperation
Represents a single photo transfer operation.

**Fields**:
- `SourcePath` (string): Original photo location
- `DestinationPath` (string): Target location for photo
- `OperationType` (TransferType): Move or Copy operation
- `Status` (OperationStatus): Pending, InProgress, Completed, Failed
- `ErrorMessage` (string?): Error details if operation failed
- `ProcessedAt` (DateTime?): When operation completed

**Enumerations**:
```csharp
public enum TransferType
{
    Move,
    Copy
}

public enum OperationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}
```

### TimePeriod
Represents a year-month time period for photo filtering.

**Fields**:
- `Year` (int): Four-digit year
- `Month` (int): Month number (1-12)
- `DisplayName` (string): Formatted string (YYYY-MM)

**Validation Rules**:
- Year must be between 1900 and current year + 1
- Month must be between 1 and 12
- DisplayName must match YYYY-MM format

### ProgressIndicator
Represents a pulsating progress visualization for console operations.

**Fields**:
- `IsActive` (bool): Whether the indicator is currently running
- `Character` (string): Display character (default: "*")
- `IntervalMs` (int): Pulse interval in milliseconds (default: 500)
- `Position` (ConsolePosition): Current cursor position for the indicator

**Methods**:
- `Start()`: Begin pulsating animation
- `Stop()`: End animation and clear character
- `Update()`: Toggle visibility state

## Relationships

- PhotoIndex contains many PhotoMetadata
- PhotoMetadata can have one TransferOperation
- TimePeriod filters PhotoMetadata by CreationDate
- TransferOperation references PhotoMetadata via file paths

## State Transitions

### PhotoMetadata Lifecycle
1. **Discovered**: Found during directory scan
2. **Indexed**: Metadata extracted and stored
3. **Selected**: Matches transfer criteria (time period)
4. **Transferred**: Successfully moved to destination
5. **Error**: Transfer failed, remains in original location

### TransferOperation Lifecycle
1. **Pending**: Operation queued for processing
2. **InProgress**: Currently being executed
3. **Completed**: Successfully finished
4. **Failed**: Error occurred, operation aborted

## Data Storage

### Metadata File Structure
```json
{
  "indexedAt": "2025-09-07T10:30:00Z",
  "workingDirectory": "C:\\Photos",
  "version": "1.0.0",
  "totalCount": 1250,
  "supportedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".cr3", ".crw", ".cr2", ".avi", ".mp4", ".3gp", ".m4a", ".mov"],
  "photos": [
    {
      "filePath": "C:\\Photos\\Vacation\\IMG_001.jpg",
      "fileName": "IMG_001.jpg",
      "creationDate": "2012-01-15T14:30:00Z",
      "fileSize": 2048576,
      "extension": ".jpg",
      "hash": "a1b2c3d4e5f6...",
      "isTransferred": false,
      "transferredTo": null
    }
  ]
}
```

### File Naming Convention
- Metadata file: `.phototransfer-index.json`
- Target directories: `phototransfer/YYYY-MM/`
- Duplicate handling: `filename(N).ext` where N starts from 0