@echo off
setlocal

where git >nul 2>&1
if errorlevel 1 (
    echo [ERROR] git が見つかりません。
    echo Git for Windows をインストールし、PATHに追加してください。
    exit /b 1
)

echo === Setting core.hooksPath ===
git config --local core.hooksPath .githooks
if errorlevel 1 (
    echo [ERROR] hooksPath 設定失敗
    exit /b 1
)

echo === Setting commit.template ===
git config --local commit.template .gitmessage
if errorlevel 1 (
    echo [ERROR] commit.template 設定失敗
    exit /b 1
)

echo.
echo [OK] 設定完了
echo.
echo core.hooksPath:
git config --local core.hooksPath
echo.
echo commit.template:
git config --local commit.template

exit /b 0