@echo off

set WORKSPACE=..\..\..
set LUBAN_DLL=%WORKSPACE%\LubanConfig\Tools\Luban\Luban.dll
set CONF_ROOT=.

set OUTPUT_JSON_DIR=%WORKSPACE%\Unity\Assets\Resources\moba
set OUTPUT_BYTES_DIR=%WORKSPACE%\Unity\Assets\Resources\moba_bytes

dotnet %LUBAN_DLL% ^
    -t all ^
    -d json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputDataDir=%OUTPUT_JSON_DIR%

dotnet %LUBAN_DLL% ^
    -t all ^
    -d msgpack ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputDataDir=%OUTPUT_BYTES_DIR%

pause