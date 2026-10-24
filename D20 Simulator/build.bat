@echo off
rem Rebuilds D20.exe using the C# compiler that ships with Windows (.NET Framework 4).
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:"%~dp0D20.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0D20.cs"
