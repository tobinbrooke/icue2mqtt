@echo off
rem Double-click to build and redeploy the icue2mqtt service (asks for administrator rights).
rem Options are forwarded to deploy.ps1, e.g.: deploy.cmd -Pull
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1" %*
