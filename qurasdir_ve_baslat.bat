@echo off
cd /d "%~dp0"
python -m pip install -q -r "%~dp0requirements.txt" <nul
cmd /c "%~dp0baslat.bat" <nul
