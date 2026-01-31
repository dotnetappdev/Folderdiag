cmake_minimum_required(VERSION 3.15)
project(FoldersDiag VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Set output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Source files
set(SOURCES
    src/main.cpp
    src/MainWindow.cpp
    src/FolderScanner.cpp
    src/ThemeManager.cpp
    src/FileSystemItem.cpp
)

set(HEADERS
    src/MainWindow.h
    src/FolderScanner.h
    src/ThemeManager.h
    src/FileSystemItem.h
    src/resource.h
)

# Resources
set(RESOURCES
    src/FoldersDiag.rc
)

# Create executable
if(WIN32)
    add_executable(${PROJECT_NAME} WIN32 ${SOURCES} ${HEADERS} ${RESOURCES})
    target_link_libraries(${PROJECT_NAME} comctl32 shlwapi uxtheme dwmapi)
else()
    message(FATAL_ERROR "This project is designed for Windows only")
endif()

# Set subsystem to Windows
if(MSVC)
    set_target_properties(${PROJECT_NAME} PROPERTIES
        LINK_FLAGS "/SUBSYSTEM:WINDOWS"
    )
endif()
