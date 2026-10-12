@echo off
rem Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (echo .NET Framework 4 compiler not found & exit /b 1)
cd /d "%~dp0"

"%CSC%" /nologo /target:winexe /optimize+ /out:GalaxyTaskbar\GalaxyTaskbar.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll GalaxyTaskbar\*.cs || exit /b 1
echo Built GalaxyTaskbar\GalaxyTaskbar.exe
