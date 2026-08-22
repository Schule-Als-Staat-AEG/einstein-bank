@echo off
setlocal enabledelayedexpansion

SET SCRIPT_DIR=%~dp0%
SET FULL_DIR=%SCRIPT_DIR%frontend\build

echo %SCRIPT_DIR%
echo %FULL_DIR%/

FOR /F "tokens=* USEBACKQ" %%F IN (`wsl wslpath -a -u "%FULL_DIR%"`) DO (
SET WSL_DIR=%%F
)
ECHO %WSL_DIR%

IF "%rsync_passwd%"=="" (
    echo Please set the rsync_passwd environment variable to the password for rsync.
    pause
    exit /b 1
)

IF "%rsync_user%"=="" (
    echo Please set the rsync_user environment variable to the username for rsync.
    set /p rsync_user=Enter rsync username: 
    if "!rsync_user!"=="" (
        echo No username provided. Exiting.
        pause
        exit /b 1
    )
    setx rsync_user "!rsync_user!" >nul
)
if "%rsync_server%"=="" (
    echo Please set the rsync_server environment variable to the server address for rsync.
    set /p rsync_server=Enter rsync server address: 
    if "!rsync_server!"=="" (
        echo No server address provided. Exiting.
        pause
        exit /b 1
    )
    setx rsync_server "!rsync_server!" >nul
)
wsl sshpass -p "!rsync_passwd!" rsync -avr -I --progress "%WSL_DIR%/" "!rsync_user!"@!rsync_server!:/opt/sites/aeg-site/
pause