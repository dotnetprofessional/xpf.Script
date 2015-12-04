cd xpf.Script
..\.nuget\nuget.exe pack -Prop Configuration=Release
xcopy *.nupkg "D:\dev\NuGet" /F /Y
cd ..

cd xpf.Script.SQLServer
..\.nuget\nuget.exe pack -Prop Configuration=Release
xcopy *.nupkg "D:\dev\NuGet" /F /Y
cd ..