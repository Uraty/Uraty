# Uraty Roslyn Analyzers 運用メモ

このリポジトリでは、C# コード規約を Roslyn Analyzer で検査する。

## フォルダ構成

- `repo/Analyzers/`  
  Analyzer 本体（ソース、csproj、ビルド）

- `repo/Assets/Analyzers/`  
  Unity が読み込む Analyzer DLL 配布先（生成物置き場）  

## 重要：解析対象

Analyzer はホワイトリスト方式で、以下配下のみ解析する：

- `Assets/_Features/`
- `Assets/_Shared/`
- `Assets/_Platform/`

`Packages/` や `Library/` 等は解析しない。

## Rules を修正したときにやること（必須）
- 1) `Analyzer.csproj` を `visual studio 2022` で起動しリビルド
※コピー、Unity内でのRoslynAnalyzerのタグ付けは自動化済