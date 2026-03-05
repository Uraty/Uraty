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

echo === Checking Git LFS ===
git lfs version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Git LFS が使えません（git lfs が認識されない）。
    echo Git LFS をインストールして PATH を通してください。
    exit /b 1
)

echo === Setting up Git LFS ===
rem core.hooksPath を設定した後に実行する（pre-push が .githooks に入る）
git lfs install --local
if errorlevel 1 (
    echo [ERROR] git lfs install 失敗
    exit /b 1
)

echo === Setting commit.template ===
git config --local commit.template .gitmessage
if errorlevel 1 (
    echo [ERROR] commit.template 設定失敗
    exit /b 1
)

echo === Setting fetch.prune ===
git config --local fetch.prune true
if errorlevel 1 (
    echo [ERROR] fetch.prune 設定失敗
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
echo.
echo fetch.prune:
git config --local fetch.prune

echo.
echo Git LFS (version):
git lfs version

exit /b 0