echo off
setlocal enabledelayedexpansion
set solutionDir=%CD%
set netversion=net9.0


echo Compiling Shaders...
pushd "%CD%\Assets\Shaders"
(
    for /r %%a in (*) do (

        if "%%~xa"==".vert" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".frag" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".geom" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".comp" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".tesc" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".tese" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".rgen" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".rint" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".rahit" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".rchit" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".rmiss" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".rcall" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".mesh" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
        if "%%~xa"==".task" (

           echo %%a
           %VULKAN_SDK%\bin\glslc --target-env=vulkan1.4 %%a
        )
    )
)
popd

echo Compiliation Complete