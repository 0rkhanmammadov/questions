@echo off
cd /d "%~dp0"
python -m pip install -q -r "%~dp0requirements.txt"
call "%~dp0baslat.bat"
