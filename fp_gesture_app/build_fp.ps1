$ErrorActionPreference = "Stop"

$SDK = "C:\Users\Orkhan\AppData\Local\Android\Sdk"
$BUILD_TOOLS = "$SDK\build-tools\35.0.0"
$PLATFORM = "$SDK\platforms\android-34\android.jar"
$JAVA_BIN = "C:\Program Files\Java\jdk-21\bin"

$APP_DIR = "$PSScriptRoot"
$BUILD_DIR = "$APP_DIR\build"
$OUT_APK = "$PSScriptRoot\..\fp_panel.apk"

if (Test-Path $BUILD_DIR) { Remove-Item -Recurse -Force $BUILD_DIR }
New-Item -ItemType Directory -Force -Path "$BUILD_DIR\compiled_res", "$BUILD_DIR\gen", "$BUILD_DIR\classes", "$BUILD_DIR\dex" | Out-Null

Write-Host "1. Compiling resources with aapt2..."
& "$BUILD_TOOLS\aapt2.exe" compile --dir "$APP_DIR\res" -o "$BUILD_DIR\compiled_res\res.zip"

Write-Host "2. Linking resources and AndroidManifest.xml..."
& "$BUILD_TOOLS\aapt2.exe" link `
    -I "$PLATFORM" `
    --manifest "$APP_DIR\AndroidManifest.xml" `
    --java "$BUILD_DIR\gen" `
    -o "$BUILD_DIR\unaligned.apk" `
    "$BUILD_DIR\compiled_res\res.zip"

Write-Host "3. Compiling Java sources with javac..."
$javaFiles = Get-ChildItem -Recurse -Path "$APP_DIR\src", "$BUILD_DIR\gen" -Filter "*.java" | ForEach-Object { $_.FullName }
& "$JAVA_BIN\javac.exe" -source 11 -target 11 -encoding UTF-8 -cp "$PLATFORM" -d "$BUILD_DIR\classes" $javaFiles

Write-Host "4. Converting classes to DEX with d8..."
$classFiles = Get-ChildItem -Recurse -Path "$BUILD_DIR\classes" -Filter "*.class" | ForEach-Object { $_.FullName }
& "$BUILD_TOOLS\d8.bat" --lib "$PLATFORM" --min-api 26 --output "$BUILD_DIR\dex" $classFiles

Write-Host "5. Adding classes.dex into APK..."
Copy-Item "$BUILD_DIR\dex\classes.dex" "$BUILD_DIR\classes.dex" -Force
Push-Location $BUILD_DIR
& "$JAVA_BIN\jar.exe" uf "$BUILD_DIR\unaligned.apk" "classes.dex"
Pop-Location

Write-Host "6. Zipaligning APK..."
& "$BUILD_TOOLS\zipalign.exe" -f -p 4 "$BUILD_DIR\unaligned.apk" "$BUILD_DIR\aligned.apk"

$KEYSTORE = "$PSScriptRoot\..\android_app\debug.keystore"
Write-Host "7. Signing APK with apksigner..."
& "$BUILD_TOOLS\apksigner.bat" sign `
    --ks $KEYSTORE `
    --ks-pass pass:android `
    --key-pass pass:android `
    --out $OUT_APK `
    "$BUILD_DIR\aligned.apk"

Write-Host "SUCCESS! APK built at: $OUT_APK"
Get-Item $OUT_APK | Select-Object FullName, Length
