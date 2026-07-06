@echo off
setlocal
REM 一键打包：编译 Release 并把 Dalamud 自动生成的插件包 latest.zip 复制为发布用的 RaceKnight.zip
set DALAMUD_HOME=C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\addon\Hooks\dev

dotnet build -c Release %*
if errorlevel 1 exit /b 1

if not exist "dist" mkdir dist
copy /Y "bin\Release\RaceKnight\latest.zip" "dist\RaceKnight.zip" >nul

echo.
echo 已生成 dist\RaceKnight.zip  （含 RaceKnight.dll / .json / .deps.json，可直接发布）
echo 下一步：把 dist\RaceKnight.zip 上传到 GitHub Release，再把下载直链填进 ..\repo\pluginmaster.json 的 DownloadLinkInstall。
endlocal
