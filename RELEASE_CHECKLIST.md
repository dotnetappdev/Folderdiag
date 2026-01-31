# Release Checklist

Use this checklist when preparing a new release of FoldersDiag.

## Pre-Release

### Code Quality
- [ ] All features implemented and working
- [ ] No compiler warnings in Release build
- [ ] No memory leaks detected
- [ ] Code reviewed and approved
- [ ] All known bugs fixed or documented

### Testing
- [ ] Tested on Windows 10
- [ ] Tested on Windows 11
- [ ] Tested with small folders (< 1,000 files)
- [ ] Tested with medium folders (1,000 - 10,000 files)
- [ ] Tested with large folders (> 10,000 files)
- [ ] Tested with different drive types (HDD, SSD, Network)
- [ ] Tested theme switching (light to dark and back)
- [ ] Tested all menu items and shortcuts
- [ ] Tested toolbar buttons
- [ ] Tested column sorting
- [ ] Tested window resizing
- [ ] Tested with long paths (> 260 characters)
- [ ] Tested with special characters in filenames
- [ ] Tested access denied scenarios
- [ ] Tested with empty folders

### Documentation
- [ ] README.md updated with new features
- [ ] USER_GUIDE.md updated
- [ ] ARCHITECTURE.md reflects current design
- [ ] QUICKSTART.md is accurate
- [ ] CHANGELOG.md updated (if exists)
- [ ] Version number updated in CMakeLists.txt
- [ ] All documentation links working

### Build
- [ ] Clean build successful (Debug)
- [ ] Clean build successful (Release)
- [ ] No unnecessary files in build output
- [ ] Executable size reasonable
- [ ] All dependencies included or documented

## Release Process

### Version Management
- [ ] Update version number in:
  - [ ] CMakeLists.txt
  - [ ] resource.h (if version resource added)
  - [ ] README.md
  
### Build Release
```bash
# Clean build
rm -rf build
mkdir build
cd build

# Configure for Release
cmake .. -G "Visual Studio 16 2019" -A x64

# Build Release
cmake --build . --config Release

# Test the executable
.\bin\Release\FoldersDiag.exe
```

- [ ] Release build completed successfully
- [ ] Executable tested and working
- [ ] No console window appears (WIN32 subsystem)
- [ ] Icon appears correctly (if icon added)

### Package Release
- [ ] Create release folder structure:
  ```
  FoldersDiag-v1.0.0/
  ├── FoldersDiag.exe
  ├── README.txt (simplified readme)
  └── LICENSE.txt
  ```

- [ ] Copy executable to release folder
- [ ] Copy LICENSE to release folder (as .txt)
- [ ] Create simplified README.txt for users
- [ ] Create ZIP archive
- [ ] Test extracted archive on clean machine

### Create GitHub Release
- [ ] Commit all changes
- [ ] Push to GitHub
- [ ] Create Git tag: `git tag -a v1.0.0 -m "Version 1.0.0"`
- [ ] Push tag: `git push origin v1.0.0`
- [ ] Go to GitHub Releases page
- [ ] Create new release
- [ ] Select the version tag
- [ ] Write release notes (see template below)
- [ ] Upload ZIP file
- [ ] Mark as pre-release if beta
- [ ] Publish release

### Release Notes Template
```markdown
## FoldersDiag v1.0.0

### New Features
- Feature 1 description
- Feature 2 description

### Improvements
- Improvement 1
- Improvement 2

### Bug Fixes
- Fixed bug 1
- Fixed bug 2

### Known Issues
- Issue 1 (workaround if any)
- Issue 2 (workaround if any)

### System Requirements
- Windows 10 or later
- No additional dependencies required

### Installation
1. Download FoldersDiag-v1.0.0.zip
2. Extract to any folder
3. Run FoldersDiag.exe

### Checksums
SHA256: [calculate and insert]
```

## Post-Release

### Verification
- [ ] Download release from GitHub
- [ ] Extract and test on clean machine
- [ ] Verify all features working
- [ ] Check download statistics after a few days

### Communication
- [ ] Update project website (if exists)
- [ ] Announce on social media (if applicable)
- [ ] Update any package managers (if applicable)
- [ ] Respond to user feedback

### Monitoring
- [ ] Monitor GitHub issues for bug reports
- [ ] Collect user feedback
- [ ] Plan next release based on feedback

## Hotfix Release

If a critical bug is found:
- [ ] Create hotfix branch from release tag
- [ ] Fix the bug
- [ ] Test thoroughly
- [ ] Increment patch version (e.g., 1.0.0 → 1.0.1)
- [ ] Follow release process above
- [ ] Merge hotfix back to main branch

## Version Numbering

Follow Semantic Versioning (SemVer):
- **Major.Minor.Patch** (e.g., 1.0.0)
- **Major**: Breaking changes
- **Minor**: New features, backwards compatible
- **Patch**: Bug fixes, backwards compatible

Examples:
- 1.0.0 → 1.0.1: Bug fix
- 1.0.1 → 1.1.0: New feature added
- 1.1.0 → 2.0.0: Breaking change

## Checklist for v1.0.0 (Initial Release)

### Must Have
- [x] Folder scanning functionality
- [x] Size calculation and display
- [x] ListView with columns
- [x] Dark/Light theme support
- [x] Basic documentation

### Nice to Have (Future)
- [ ] Export to CSV
- [ ] Custom filters
- [ ] Chart visualization
- [ ] Settings persistence
- [ ] Multiple folder comparison

### Stretch Goals (v2.0+)
- [ ] Duplicate file detection
- [ ] File type analysis
- [ ] Scheduled scans
- [ ] Command-line interface
- [ ] Plugins/extensions

## Notes

- Always test on a clean Windows installation if possible
- Keep release archives for historical reference
- Document all known issues in release notes
- Be transparent about limitations
- Respond promptly to critical bug reports

## Automation Opportunities

For future releases, consider automating:
- Version number updates
- Build process
- ZIP packaging
- Checksum calculation
- GitHub release creation
- Release notes generation
