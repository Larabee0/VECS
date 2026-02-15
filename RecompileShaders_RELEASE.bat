echo off
setlocal enabledelayedexpansion
set solutionDir=%CD%
set netversion=net9.0



echo clearing Assets/Shaders

del /q /S %CD%\bin\Release\%netversion%\Assets\Shaders\*



echo clearing CompiledShaders

del /q /S %CD%\CompiledShaders\*

echo Compiling Shaders...
pushd "%CD%\Shaders\"
(
    for /r %%a in (*) do (

        if NOT "%%~xa"==".glsl" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a -o %solutionDir%\CompiledShaders\%%~na%%~xa.spv
        )
    )
)
popd

echo Compiliation Complete

echo Copying Shaders to Assets/Shaders...

XCOPY %solutionDir%\CompiledShaders\* %CD%\bin\Release\%netversion%\Assets\Shaders

echo Finished

pause
