@echo off
cd /d "%~dp0"
taskkill /F /IM pythonw.exe >nul 2>nul
where pythonw >nul 2>nul
if %errorlevel%==0 (
    start "" pythonw "%~dp0main.py"
    exit /b 0
)
where pyw >nul 2>nul
if %errorlevel%==0 (
    start "" pyw "%~dp0main.py"
    exit /b 0
)
if exist "C:\Users\Orkhan\AppData\Local\Programs\Python\Python312\pythonw.exe" (
    start "" "C:\Users\Orkhan\AppData\Local\Programs\Python\Python312\pythonw.exe" "%~dp0main.py"
    exit /b 0
)
start "" python "%~dp0main.py"
exit /b 0
