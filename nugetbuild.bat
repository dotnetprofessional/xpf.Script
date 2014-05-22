cd xpf.Script
..\.nuget\nuget.exe pack
xcopy *.nupkg "C:\Users\Garry\SkyDrive\Public\nuget" /F /Y
cd ..

cd xpf.Script.SQLServer
..\.nuget\nuget.exe pack
xcopy *.nupkg "C:\Users\Garry\SkyDrive\Public\nuget" /F /Y
cd ..