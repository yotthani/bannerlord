@echo off
echo ============================================
echo FaceLearner Setup Helper
echo ============================================
echo.
echo NOTE: The mod now auto-downloads everything!
echo       Just launch the game.
echo.
echo This script shows current status.
echo.

set MOD_PATH=%~dp0

echo Mod directory: %MOD_PATH%
echo.

echo Checking status...
echo.

if exist "%MOD_PATH%Data\models\shape_predictor_68_face_landmarks.dat" (
    echo [OK] Dlib model installed
) else (
    echo [PENDING] Dlib model - will auto-download on game launch
)

if exist "%MOD_PATH%Data\datasets\lfw\*.*" (
    echo [OK] LFW dataset installed
) else (
    echo [PENDING] LFW dataset - will auto-download on game launch
)

if exist "%MOD_PATH%Data\datasets\celeba\list_attr_celeba.txt" (
    echo [OK] CelebA attributes installed
) else (
    echo [PENDING] CelebA attributes - will auto-download on game launch
)

dir /b "%MOD_PATH%Data\datasets\celeba\img_align_celeba\*.jpg" >nul 2>&1
if %ERRORLEVEL%==0 (
    echo [OK] CelebA images installed (manual)
) else (
    echo [OPTIONAL] CelebA images - download manually for 200K faces
)

dir /b "%MOD_PATH%Data\datasets\utkface\*.jpg" >nul 2>&1
if %ERRORLEVEL%==0 (
    echo [OK] UTKFace dataset installed (manual)
) else (
    echo [OPTIONAL] UTKFace - download manually for age/ethnicity data
)

echo.
echo ============================================
echo Just launch the game - downloads are automatic!
echo ============================================
echo.
pause
