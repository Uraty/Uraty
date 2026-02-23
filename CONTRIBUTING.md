# CONTRIBUTING

## ブランチ戦略
- main: 常にビルド可能な状態を保つ
- develop: 開発中のコードを統合するブランチ
- feature/xxx: 新機能の開発用ブランチ
- fix/xxx: バグ修正用ブランチ
- refactor/xxx: コードのリファクタリング用ブランチ
- release/xxx: リリース準備用ブランチ

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