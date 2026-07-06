@echo off
setlocal
REM RaceKnight 一键编译：自动把 DALAMUD_HOME 指向 Soil 启动器的 Dalamud dev 目录
REM （Soil 的路径是 XIVLauncherCN\addon\Hooks\dev，和 vanilla 的 XIVLauncher\addon\Hooks\dev 不同）
set DALAMUD_HOME=C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\addon\Hooks\dev
dotnet build -c Debug %*
endlocal
