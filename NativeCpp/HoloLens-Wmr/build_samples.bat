call "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat"

set ROOT=%~dp0\..\..\..\..\

%HAVOK_THIRDPARTY_DIR%\sdks\win32\NuGet\4.9.2\nuget restore "Source\Samples\NativeCpp\HoloLens-Wmr\HolographicApp.sln"

msbuild.exe "Source\Samples\NativeCpp\HoloLens-Wmr\HolographicApp.sln" /t:Build /p:AppxPackageSigningEnabled=false /clp:NoSummary;NoItemAndPropertyList;Verbosity=minimal /p:Configuration=Release;Platform="ARM64"

