# QuickStart: PhotoTransfer Console Application

## Prerequisites

- .NET 9 SDK installed
- Command line access (Terminal, Command Prompt, PowerShell)
- Directory containing photos to organize

## Installation

1. Build the application:
```bash
dotnet build --configuration Release
```

2. Optionally install globally:
```bash
dotnet pack
dotnet tool install -g --add-source ./nupkg phototransfer
```

## Basic Usage

### Step 1: Index Your Photos

Navigate to the directory containing your photos and run:

```bash
# Index current directory and subdirectories
phototransfer --index

# Or specify a different directory
phototransfer --index --directory "C:\My Photos"
```

**Expected Output**:
```
Indexing photos in: C:\My Photos
Found 1,250 image files
Processing: IMG_001.jpg (1/1250)
Processing: vacation_sunset.png (2/1250)
...
Index complete: 1,250 photos indexed
Metadata saved to: .phototransfer-index.json
```

**Verification**: Check that `.phototransfer-index.json` file was created in the working directory.

### Step 2: Transfer Photos by Date

Transfer all photos from a specific month:

```bash
# Transfer photos from January 2012
phototransfer --2012-01

# Transfer photos from December 2023
phototransfer --2023-12
```

**Expected Output**:
```
Loading metadata from: .phototransfer-index.json
Found 45 photos for period: 2012-01
Target directory: phototransfer\2012-01\
Transferring: IMG_001.jpg -> phototransfer\2012-01\IMG_001.jpg
Transferring: IMG_002.jpg -> phototransfer\2012-01\IMG_002.jpg
Transferring: family_photo.jpg -> phototransfer\2012-01\family_photo(0).jpg (duplicate)
...
Transfer complete: 45 photos moved to phototransfer\2012-01\
```

**Verification**: 
1. Check that `phototransfer/2012-01/` directory was created
2. Verify photos are present in the new location
3. Confirm photos are no longer in original locations

## Advanced Usage

### Verbose Output
Get detailed progress information:
```bash
phototransfer --index --verbose
phototransfer --2012-01 --verbose
```

### Copy Instead of Move
Keep original photos in place:
```bash
phototransfer --2012-01 --copy
```

### Dry Run
Preview what would be transferred:
```bash
phototransfer --2012-01 --dry-run
```

### Custom Target Directory
Specify different output location:
```bash
phototransfer --2012-01 --target "C:\Organized Photos"
```

## Testing Scenarios

### Scenario 1: Basic Photo Organization
**Setup**: 
- Create test directory with photos from different dates
- Mix of JPG and PNG files
- Some subdirectories

**Steps**:
1. `cd test-photos`
2. `phototransfer --index`
3. `phototransfer --2023-01`
4. Verify 2023 January photos moved to `phototransfer/2023-01/`

**Expected**: All January 2023 photos organized, other photos remain

### Scenario 2: Duplicate Handling  
**Setup**:
- Two photos with same filename in different subdirectories
- Both from same month

**Steps**:
1. Index photos
2. Transfer for that month
3. Check both photos copied with numeric suffixes

**Expected**: `photo.jpg` and `photo(0).jpg` in target directory

### Scenario 3: Mixed File Types
**Setup**: Directory with JPG, PNG, GIF, BMP, TIFF files

**Steps**:
1. Index directory
2. Transfer photos from specific month

**Expected**: All supported image types transferred, other files ignored

### Scenario 4: No Photos Found
**Setup**: Index directory, attempt to transfer from empty time period

**Steps**:
1. `phototransfer --index`  
2. `phototransfer --1995-01` (assuming no photos from 1995)

**Expected**: "No photos found for period: 1995-01" message

### Scenario 5: Corrupted Metadata
**Setup**: Delete or corrupt `.phototransfer-index.json`

**Steps**:
1. Attempt transfer command
2. Should get error about missing/invalid metadata file

**Expected**: Clear error message, suggestion to re-run index

## Troubleshooting

### "Directory not found"
- Verify path exists and is accessible
- Check for typos in directory path
- Ensure proper permissions

### "Permission denied"  
- Run as administrator (Windows) or with sudo (Linux/Mac)
- Check directory write permissions
- Verify antivirus not blocking file operations

### "No photos found for period"
- Verify date format is YYYY-MM
- Check photos actually exist for that time period
- Re-run index if photos were added after indexing

### "Transfer failed"
- Check available disk space
- Ensure target directory is writable
- Verify source files are not in use by other applications

## File Structure After Completion

```
original-photos/                 # Original directory (after transfers)
├── .phototransfer-index.json   # Metadata file
├── 2024/                       # Remaining photos (not transferred)
│   └── march/
└── vacation/                   # Remaining photos

phototransfer/                   # Organized photos
├── 2012-01/                    # January 2012 photos
│   ├── IMG_001.jpg
│   ├── IMG_002.jpg
│   └── family_photo(0).jpg     # Duplicate handling
└── 2023-12/                    # December 2023 photos
    ├── sunset.png
    └── celebration.gif
```

## Next Steps

1. Verify all photos transferred correctly
2. Backup organized directories
3. Clean up empty original directories if desired
4. Update photo management workflows to use organized structure