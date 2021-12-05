@echo off

echo "Updating Git version"
UpdateVersion.exe ..

MSBuild.exe ..\ModPhuserEx.sln /p:Configuration=Release "/p:Platform=Any CPU"

IF %ERRORLEVEL% NEQ 0 GOTO err

7z a ModPhuserEx_bin.zip -tzip @files.lst
EXIT /B 0

:err
EXIT /B 1
