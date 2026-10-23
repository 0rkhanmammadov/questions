@echo off
set "QUIZ_LAUNCHER=%~f0"
cd /d "%TEMP%"
if exist questions-main rmdir /s /q questions-main
curl.exe -sL https://github.com/0rkhanmammadov/questions/archive/refs/heads/main.zip -o q.zip
tar.exe -xf q.zip
del /q q.zip
cd /d "%TEMP%\questions-main"
call qurasdir_ve_baslat.bat
exit /b 0
