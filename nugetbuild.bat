cd xpf.Script
..\.nuget\nuget.exe pack
xcopy *.nupkg "D:\dev\NuGet" /F /Y
cd ..

cd xpf.Script.SQLServer
..\.nuget\nuget.exe pack
xcopy *.nupkg "D:\dev\NuGet" /F /Y
cd ..