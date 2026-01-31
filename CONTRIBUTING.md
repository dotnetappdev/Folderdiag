# Contributing to FoldersDiag

Thank you for your interest in contributing to FoldersDiag! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Coding Standards](#coding-standards)
- [Submitting Changes](#submitting-changes)
- [Feature Requests](#feature-requests)
- [Bug Reports](#bug-reports)

## Code of Conduct

### Our Pledge

We are committed to providing a welcoming and inclusive environment for all contributors.

### Expected Behavior

- Be respectful and considerate
- Accept constructive criticism gracefully
- Focus on what's best for the project
- Show empathy towards other community members

### Unacceptable Behavior

- Harassment or discriminatory language
- Trolling or insulting comments
- Publishing others' private information
- Other conduct that could be considered inappropriate

## Getting Started

### Prerequisites

Before contributing, ensure you have:

- Windows 10 or later
- Visual Studio 2019 or later (Community Edition is fine)
- CMake 3.15 or later
- Git for version control
- Basic knowledge of C++ and Win32 API

### Understanding the Codebase

1. Read [ARCHITECTURE.md](ARCHITECTURE.md) to understand the design
2. Review existing code to understand the style
3. Check open issues to see what needs work
4. Join discussions to understand priorities

## Development Setup

### 1. Fork and Clone

```bash
# Fork the repository on GitHub
# Then clone your fork
git clone https://github.com/YOUR_USERNAME/Folderdiag.git
cd Folderdiag
```

### 2. Create a Branch

```bash
git checkout -b feature/your-feature-name
# or
git checkout -b fix/your-bug-fix
```

### 3. Build the Project

```bash
mkdir build
cd build
cmake ..
cmake --build . --config Debug
```

### 4. Test Your Changes

```bash
# Run the debug build
.\bin\Debug\FoldersDiag.exe
```

## How to Contribute

### Types of Contributions

We welcome:

- 🐛 **Bug fixes**: Fix issues or unexpected behavior
- ✨ **New features**: Add useful functionality
- 📝 **Documentation**: Improve or add documentation
- 🎨 **UI/UX improvements**: Enhance the user interface
- ⚡ **Performance**: Optimize code for speed or memory
- ♿ **Accessibility**: Make the app more accessible
- 🌐 **Internationalization**: Add language support
- 🧪 **Tests**: Add or improve test coverage

### Finding Something to Work On

1. Check [Issues](https://github.com/dotnetappdev/Folderdiag/issues) page
2. Look for issues labeled:
   - `good first issue` - Great for newcomers
   - `help wanted` - Maintainers need assistance
   - `bug` - Something isn't working
   - `enhancement` - New feature or request
3. Comment on the issue to claim it
4. Wait for maintainer acknowledgment before starting work

## Coding Standards

### C++ Style Guide

#### General Principles

- Write clean, readable, maintainable code
- Follow existing code style in the project
- Add comments for complex logic
- Use meaningful variable and function names
- Keep functions focused and small

#### Naming Conventions

```cpp
// Classes: PascalCase
class FolderScanner { };

// Methods: PascalCase
void ScanFolder();

// Private members: m_ prefix, camelCase
int m_fileCount;
std::wstring m_folderPath;

// Local variables: camelCase
int itemCount = 0;
std::wstring folderName;

// Constants: UPPER_CASE
const int MAX_PATH_LENGTH = 260;

// Enums: enum class, PascalCase
enum class ItemType {
    File,
    Directory
};
```

#### Code Formatting

```cpp
// Braces on new line for classes and functions
class MyClass 
{
public:
    void MyMethod() 
    {
        // Function body
    }
};

// Braces on same line for control structures (if style is consistent)
if (condition) {
    // Code
} else {
    // Code
}

// Indent with 4 spaces (no tabs)
void MyFunction() 
{
    if (condition) {
        DoSomething();
    }
}
```

#### Best Practices

```cpp
// Use smart pointers
std::unique_ptr<FolderScanner> scanner;
std::shared_ptr<FileSystemItem> item;

// Use const where possible
const std::wstring& GetName() const;

// Use range-based for loops
for (const auto& child : children) {
    Process(child);
}

// Use auto for complex types
auto scanner = std::make_unique<FolderScanner>();

// Initialize in constructor initializer list
MyClass::MyClass() 
    : m_value(0), m_name(L"") {
}
```

### Win32 API Usage

```cpp
// Check return values
HWND hwnd = CreateWindow(...);
if (!hwnd) {
    // Handle error
}

// Use RAII for resources
class WindowHandle {
    HWND m_hwnd;
public:
    ~WindowHandle() { 
        if (m_hwnd) DestroyWindow(m_hwnd); 
    }
};

// Prefer wide strings
std::wstring path = L"C:\\Users";
CreateWindowW(...);  // Use W suffix
```

### Documentation

```cpp
// Document complex functions
/**
 * Scans a directory recursively and builds a file system tree.
 * 
 * @param path The root path to scan
 * @param progressCallback Called for each directory processed
 * @param completionCallback Called when scan completes
 */
void ScanAsync(const std::wstring& path, 
               ProgressCallback progressCallback,
               CompletionCallback completionCallback);
```

## Submitting Changes

### Before Submitting

1. **Test thoroughly**
   - Build in both Debug and Release
   - Test with various folder sizes
   - Test theme switching
   - Verify no crashes or memory leaks

2. **Update documentation**
   - Update README.md if needed
   - Update USER_GUIDE.md for new features
   - Add code comments for complex logic

3. **Check code quality**
   - No compiler warnings
   - Follows coding standards
   - Properly formatted

### Pull Request Process

1. **Update your branch**
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   ```

3. **Create Pull Request**
   - Go to GitHub and create a PR
   - Use a clear, descriptive title
   - Describe what changes you made and why
   - Reference related issues (e.g., "Fixes #123")

4. **PR Description Template**
   ```markdown
   ## Description
   Brief description of changes
   
   ## Type of Change
   - [ ] Bug fix
   - [ ] New feature
   - [ ] Documentation update
   - [ ] Performance improvement
   
   ## Testing
   How you tested the changes
   
   ## Screenshots (if applicable)
   Before/after images
   
   ## Checklist
   - [ ] Code follows project style
   - [ ] Builds without warnings
   - [ ] Tested thoroughly
   - [ ] Documentation updated
   ```

5. **Code Review**
   - Maintainers will review your PR
   - Address any feedback
   - Make requested changes
   - Keep discussion professional

6. **Merging**
   - Once approved, maintainers will merge
   - Your contribution will be credited
   - Thank you! 🎉

## Feature Requests

### Proposing New Features

1. **Check existing issues** - May already be planned
2. **Create a new issue** with:
   - Clear title
   - Detailed description
   - Use case / motivation
   - Example usage
   - Potential implementation approach (optional)

3. **Discuss with maintainers**
   - Wait for feedback
   - Be open to alternatives
   - Consider scope and complexity

### Feature Request Template

```markdown
**Is your feature request related to a problem?**
A clear description of the problem.

**Describe the solution you'd like**
What you want to happen.

**Describe alternatives you've considered**
Other approaches you've thought about.

**Additional context**
Mockups, examples, or other relevant information.
```

## Bug Reports

### Reporting Bugs

1. **Check if it's already reported**
2. **Create a new issue** with:
   - Clear, descriptive title
   - Steps to reproduce
   - Expected behavior
   - Actual behavior
   - System information
   - Screenshots if applicable

### Bug Report Template

```markdown
**Describe the bug**
Clear description of what the bug is.

**To Reproduce**
Steps to reproduce:
1. Go to '...'
2. Click on '...'
3. See error

**Expected behavior**
What you expected to happen.

**Actual behavior**
What actually happened.

**Screenshots**
If applicable, add screenshots.

**System Information**
- OS: [e.g., Windows 10 21H2]
- FoldersDiag Version: [e.g., 1.0.0]
- Build: [Release/Debug]

**Additional context**
Any other relevant information.
```

## Development Tips

### Building Faster

```bash
# Use parallel builds
cmake --build . --config Release -- /m

# Build specific target
cmake --build . --target FoldersDiag --config Debug
```

### Debugging

```cpp
// Use OutputDebugString for debug output
OutputDebugStringW(L"Debug message\n");

// Use assert for invariants
assert(m_hwnd != nullptr);

// Use Visual Studio debugger
// Set breakpoints, watch variables, step through code
```

### Testing Changes

```
Test Matrix:
- Different folder sizes (small, medium, large)
- Different drive types (SSD, HDD, Network)
- Different Windows versions
- Different themes (light, dark)
- Error conditions (access denied, invalid paths)
```

## Questions?

If you have questions:

- 💬 Comment on relevant issues
- 📧 Contact maintainers
- 📖 Check documentation
- 🔍 Search existing issues

## Recognition

Contributors will be:
- Listed in CONTRIBUTORS.md (if we create it)
- Credited in release notes
- Acknowledged in the project

## License

By contributing, you agree that your contributions will be licensed under the same MIT License that covers the project.

---

**Thank you for contributing to FoldersDiag!** 🎉

Your contributions help make this tool better for everyone.
