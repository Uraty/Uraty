@echo off
setlocal

where git >nul 2>&1
if errorlevel 1 (
    echo [ERROR] git が見つかりません。
    echo Git for Windows をインストールし、PATHに追加してください。
    exit /b 1
)

git config --local core.hooksPath .githooks
if errorlevel 1 (
    echo [ERROR] hooksPath 設定失敗
    exit /b 1
)

echo [OK] core.hooksPath = .githooks に設定しました。

git config --local core.hooksPath

exit /b 0