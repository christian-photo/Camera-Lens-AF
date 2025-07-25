dotnet build C:\Users\Christian\source\repos\NINA\LensAF\LensAF\NINA.Plugin.Template\LensAF.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary /p:Configuration=Release /p:Platform="AnyCPU"
set /p "beta=Should this be a beta release? y/n: "
echo "%beta%"
set /p "version=Version: "
echo "%version%"
set /p "b=Beta Version (leave empty for no beta release): "
if "%beta%" == "y" (
    pwsh.exe CreateNET7Manifest.ps1 -file "C:\Users\Christian\AppData\Local\NINA\Plugins\3.0.0\Lens AF\LensAF.dll" -beta -installerUrl "https://github.com/christian-photo/Camera-Lens-AF/releases/download/%version%-b.%b%/LensAF.dll"
    gh release create "%version%-b.%b%" -t "Lens AF %version%-beta %b%" -p -R christian-photo/Camera-Lens-AF "C:\Users\Christian\AppData\Local\NINA\Plugins\3.0.0\Lens AF\LensAF.dll"
) ELSE (
    pwsh.exe CreateNET7Manifest.ps1 -file "C:\Users\Christian\AppData\Local\NINA\Plugins\3.0.0\Lens AF\LensAF.dll" -installerUrl "https://github.com/christian-photo/Camera-Lens-AF/releases/download/%version%/LensAF.dll"
    gh release create %version% -t "Lens AF %version%" -R christian-photo/Camera-Lens-AF "C:\Users\Christian\AppData\Local\NINA\Plugins\3.0.0\Lens AF\LensAF.dll"
)
cd ..
cd ..
cd ..
cd nina.plugin.manifests
git pull
git pull https://github.com/isbeorn/nina.plugin.manifests.git
mkdir "manifests\l\Lens AF\3.0.0\"
copy "..\LensAF\LensAF\NINA.Plugin.Template\manifest.json" "manifests\l\Lens AF\3.0.0\manifest.json"
echo "Testing the manifest"
node gather
echo "Please verify that the test ran successfully"
pause
git add "manifests\l\Lens AF\3.0.0\*"
git commit -m "Added LensAF manifest for version %version%"
git push origin main
pause