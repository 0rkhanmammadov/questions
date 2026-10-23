@echo off
cd /d "%TEMP%"
if exist q rmdir /s /q q
git clone https://github.com/0rkhanmammadov/questions q
cd /d "%TEMP%\q"
call qurasdir_ve_baslat.bat
