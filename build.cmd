@echo off
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:"%~dp0FocusControl.exe" "%~dp0FocusControl.cs"
