# CONTRIBUTING

## ブランチ戦略
- main: 常にビルド可能な状態を保つ
- develop: 開発中のコードを統合するブランチ

## Pull Request
- PRは必ず１つの目的に限定する
- main への 直接Push は禁止とする
- レビュー承認後にマージする

## コードレビュー基準
- 命名規則に従っているか
- 依存方向を破っていないか
- publicフィールド がないか

## Issue運用
- タスクはIssueで管理する
- Issueには必ず担当者を割り当てる
- Issueは完了したらクローズする
- Issueのタイトルは簡潔に、内容は具体的に記述する

## asmdef の運用

- `Feature` は機能ごとに asmdef を分割する
- `Application` は実際に利用する `Feature` の asmdef のみを個別に参照する
- `Feature` 間で直接参照してはならない
- `Shared` は `Feature` / `Application` / `System` に依存してはならない
- 詳細な依存ルールと命名規約は `docs/conventions` を参照する