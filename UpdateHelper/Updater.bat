@echo off
setlocal

set "tempFilePath=%1"
set "currentFilePath=%2"

:: Wait for the main application to close
timeout /t 5 /nobreak >nul

:: Replace the old version with the new version
del "%currentFilePath%"
timeout /t 1 /nobreak >nul
move "%tempFilePath%" "%currentFilePath%"

:: Start the new version
start "" "%currentFilePath%"