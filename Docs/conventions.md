# 規約

- バージョン: v1.0.0
- 最終更新日: 2026-03-01
- 文書オーナー: リードプログラマー

## 改訂履歴
- v1.0.2 (2026-03-06)
  - `Feature` 内の標準フォルダ名を追加
- v1.0.1 (2026-03-01): 
  - 目次機能の修正
- v1.0.0 (2026-02-28): 
  - 初版作成

## 目次
- [適用範囲](#適用範囲)
- [ルール強度の定義](#ルール強度の定義)
- [規約の例外](#規約の例外)
- [用語定義](#用語定義)
- [クイックリファレンス](#クイックリファレンス)
- [アクセス修飾子規定](#アクセス修飾子規定)
- [命名規則](#命名規則)
- [通知（Signal/Stream）規約](#通知（Signal/Stream）規約)
- [Unity 固有メソッド規約](#unity-固有メソッド規約)
- [フォルダ構造](#フォルダ構造)
- [自動チェック対象](#自動チェック対象)

## 最終決定者 
リードプログラマー 

- 本文書の最終解釈および例外承認の最終責任者はリードプログラマーとする。
- 解釈に迷う場合は、PR 上で論点を明記し、リードプログラマーの判断を仰ぐ。

## 適用範囲
- 新規コードすべてに適用する。
  - 自動生成コードは対象外
  - サードパーティ製コードは原則対象外
  - サードパーティを包むラッパーコード / アダプターコードは対象とする
- フォルダ構造規約はプロジェクト全体に適用する。

### 補足
- 既存コードの改修時に当該規約へ合わせるかどうかは、変更規模・影響範囲・工数を踏まえて PR で判断する。
- 対象外のコードであっても、新規追加・改修するラッパー / アダプター部分は本規約の対象とする。

## ルール強度の定義
この文書では、ルールの強さを以下で表す。

- MUST（必須）: 守らなければならない。違反は修正対象とする。
- SHOULD（原則）: 原則として守る。例外は認めるが、例外とする理由を説明する。
- MAY（任意）: 状況に応じて選択してよい。

また、禁止系は以下の意味で用いる。

- MUST NOT（禁止）: 行ってはならない。違反は修正対象とする。
- SHOULD NOT（原則禁止）: 原則として行わない。例外は認めるが、例外とする理由を説明する。

### 記述ルール（本文の表現）
- MUST / MUST NOT の本文は「〜しなければならない / 〜してはならない」で記述する。
- SHOULD / SHOULD NOT の本文は「原則として〜する / 原則として〜しない」で記述する。
- MAY の本文は「〜してよい」で記述する。

## 規約の例外
- SHOULD / SHOULD NOT の例外を適用する場合は、PR に理由を記載する。
- MUST / MUST NOT の例外は、リードプログラマーの合意を必要とする。

### PR に記載する例外理由（最低限）
- 対象ルール（どの見出し・どのルールか）
- 例外とする理由（技術的制約 / 既存資産との整合 / 工数対効果 など）
- 代替策または影響範囲
- 将来的に解消予定かどうか（Yes / No）

## 用語定義
- **Feature（機能）**: ユーザー価値またはゲーム上の機能単位でまとまる実装群。例: `Inventory`, `Player`, `Quest`
- **Shared**: 2つ以上の Feature から利用され、特定 Feature に依存しない共通要素
- **Platform**: 入力、保存、通信、アセット読み込み等の基盤機能。具体的なゲーム機能を知らない層
- **ゲームロジック**: ゲーム固有のルール・状態遷移・勝敗条件・進行制御等
- **ラッパー / アダプターコード**: サードパーティまたは外部 API の差異を吸収し、プロジェクト側のインターフェースへ変換するコード
- **主要型**: 当該ファイルの主目的となる public 型、または Unity の `MonoBehaviour` / `ScriptableObject` としてアセットに紐づく型
- **横断置き場**: 特定の Feature に属さない名前で、複数 Feature の専用物を混在して置くフォルダ（例: `Assets/Scripts`, `Assets/Prefabs`）

## クイックリファレンス

### 命名
- クラス / 構造体 / Enum / プロパティ / 関数: `PascalCase`
- 引数 / ローカル変数: `camelCase`
- フィールド（`const` を除く）: _camelCase
- `const`: PascalCase
- `bool` 命名:
  - フィールド `_is / _has / _can / _should`
  - 引数 / ローカル変数 `is / has / can / should`
  - プロパティ / 関数 `Is / Has / Can / Should`
- インターフェース: `I` + `PascalCase`
- マクロ: `UPPER_SNAKE_CASE`

### Unity ライフサイクル
- `Awake`: 自身の内部初期化
- `OnEnable` / `OnDisable`: 購読 / 解除
- `Start`: 他オブジェクト依存の初期化
- `Update`: フレーム制御
- `FixedUpdate`: 物理
- `LateUpdate`: 後追い処理
- `OnDestroy`: 最終クリーンアップ

### フォルダ
- `Assets/_Features`
- `Assets/_Shared`
- `Assets/_Platform`
- `Assets/_ThirdParty`
- `Assets/_Generated`

## アクセス修飾子規定

### 目的
公開 API と内部実装の境界を明確にし、意図しない依存・書き換え・保守性低下を防止する。

### 基本方針
- 外部公開は最小限にする
- フィールドではなく、プロパティ / 関数で公開する
- Unity Inspector 公開のために `public` を使用しない

### 共通ルール
- MUST:
  - Inspector 公開が必要なフィールドは `[SerializeField] private` を使用しなければならない
- SHOULD:
  - 原則としてフィールドは `private` とする
  - 外部公開が必要な値は、原則としてプロパティで公開する
- MUST NOT:
  - `MonoBehaviour / ScriptableObject` の可変フィールドを `public` にしてはならない（Inspector 公開目的を含む）
- SHOULD NOT:
  - 原則として `public readonly` フィールドを使用しない

### 補足
- `readonly` は再代入禁止を表すが、命名規則は変更しない（フィールドはフィールドとして扱う）
- `const` の命名は「定数名」項目に従う

## 命名規則

### 目的
プロジェクト内の命名を統一し、可読性・保守性を向上させることを目的とする。

### 判定の補助基準（レビュー時）
以下の語は方針語であり、レビュー時は次の観点で補助判定する。

- 「意味が分かる」:
  名前から少なくとも「責務・対象・結果」のいずれかが推測できること。
- 「役割が分かる」:
  型名 / 関数名から、何を保持・処理・提供するかが推測できること。
- 「一般的な略語」:
  チーム内で共通認識があり、初見でも誤解しにくい略語（例: `ID`, `UI`, `API`, `URL`）

### 基本方針
- 略語は一般的なもののみ使用するが、命名規則に則るものとする（例: `playerId`, `characterUi`, `serverApi`, `homeUrl`） 
- 1文字変数は、ループカウンタなど限定的な用途でのみ許可する
- 単数形 / 複数形を正しく使う
  - 単体: `user`
  - 配列・複数: `users`

### bool 命名
- MUST:
  - `bool` を表す名前は、状態 / 可否 / 必要性が分かる語を用いなければならない
  - `bool` の名前は `is / has / can / should`（プロパティ・関数は `Is / Has / Can / Should`）で始めなければならない
- MUST NOT:
  - `flag`, `value`, `status` など意味の薄い語のみで命名してはならない

### クラス名
- 形式: `PascalCase`
- ルール:
  - MUST:
    - 名詞を使わなければならない
- 例:
  - OK: `PlayerManager`, `TextureLoader`, `EnemySpawner`, `AudioPlaybackService`
  - NG: `manager`, `dataClass`, `Common`, `Util1`

### 構造体名
- 形式: `PascalCase`
- ルール:
  - MUST:
    - 名詞を使わなければならない
- 例:
  - OK: `DamageInfo`, `SpawnRequest`, `PlayerStatus`
  - NG: `manager`, `dataClass`, `TempStruct`

### Enum 名
- 形式: `PascalCase`
- ルール:
  - MUST:
    - 名詞を使わなければならない
    - 単数形を基本にしなければならない
    - `Enum` などの接尾辞を付けてはならない
- 例:
  - OK: `Color`, `Direction`, `PlayerState`, `GamePhase`
  - NG: `Colors`, `DirectionEnum`, `EPlayerState`

### Enum メンバ名
- 形式: `PascalCase`
- ルール:
  - MUST:
    - 名詞を使わなければならない（状態名は慣用的に形容詞 / 分詞も可）
    - 型名プレフィックスを付けてはならない（冗長）
    - `[Flags]` を使う場合は `None = 0` を定義しなければならない
- 例:
  - OK: `None`, `Idle`, `Moving`, `Dead`
  - NG: `PLAYER_STATE_IDLE`, `PlayerStateIdle`, `A`

### 関数名
- 形式: `PascalCase`
- ルール:
  - MUST:
    - 動詞を使わなければならない
- 例:
  - OK: `LoadTexture`, `ApplyDamage`, `GetSpawnPoint`, `IsGrounded`, `CanAttack`
  - NG: `Do`, `Run`, `Func1`

### 引数名
- 形式: `camelCase`
- ルール:
  - MUST NOT:
    - `_` プリフィックスをつけてはならない
- 例:
  - OK: `targetEnemy`, `damageAmount`, `isCritical`
  - NG: `_targetEnemy`, `TargetEnemy`, `tmp`

### フィールド名
- 形式: `_` プリフィックス + `camelCase`
- ルール:
  - MUST:
    - `const` を使用する場合は 定数名項目に従わなければならない
- 例:
  - OK:
    - `[SerializeField] private int _maxHp;`
    - `private Rigidbody _rigidbody;`
    - `private bool _isInvincible;`
  - NG:
    - `public int hp;`
    - `private int MaxHp;`
    - `private GameObject go;`

### プロパティ名
- 形式: `PascalCase`
- ルール:
  - SHOULD:
    - 原則としてメンバ変数名を `PascalCase` に置き換えたものとする
  - MUST NOT:
    - `Get / Set` プレフィックスを付けてはならない
- 例:
  - OK: `CurrentHp`, `MaxHp`, `DisplayName`, `IsDead`, `CanAttack`
  - NG: `currentHp`, `_currentHp`, `GetHp`（プロパティなのに関数名風）

### ローカル変数名
- 形式: `camelCase`
- ルール:
  - MUST:
    - `var` を使う場合も、変数名で型や役割が推測できるようにしなければならない
    - `tmp` / `data` / `obj` は使用してはならない（役割が伝わる語に置換する）
  - SHOULD:
    - 原則として 1文字変数は `for` / `foreach` のカウンタ、および数学的文脈（`x`, `y`, `z`）のみにする
- 例:
  - OK: `currentHp`, `targetEnemy`, `spawnPosition`, `isWithinRange`
  - NG: `tmp`, `data`, `obj`, `x1`

### グローバル状態（static 可変状態）
- ルール:
  - MUST NOT:
    - static な可変状態を導入してはならない

### 定数
- 形式: `PascalCase`
- ルール:
  - MUST:
    - 単位や意味を名前に含めなければならない（`Seconds`, `Meters`, `Count` など）
    - マジックナンバーを直接書いてはならない（`0`, `1`, `-1` などの自明値は例外とする）
- 例:
  - OK:
    - `private const int MaxPartyMemberCount = 4;`
    - `private const string SaveFileName = "save.json";`
    - `private const float InvincibleDurationSeconds = 1.5f;`
  - NG:
    - `ConstValue`
    - `A = 4`

### インターフェース名
- 形式: `I` プレフィックス + `PascalCase`
- ルール:
  - MUST:
    - `I` を接頭してインターフェースであることを明示しなければならない
- 例:
  - OK: `IEnemy`, `IDamageable`, `ISaveable`, `IAudioPlaybackService`
  - NG: `EnemyInterface`, `Damageable`, `Saveable`, `AudioService`

### マクロ名
- 形式: `UPPER_SNAKE_CASE`
- ルール:
  - MUST:
    - `#if`, `#define` のシンボルは大文字スネークケースにしなければならない
- 例:
  - OK: `UNITY_EDITOR`, `DEVELOPMENT_BUILD`, `GAME_ENABLE_LOG`
  - NG: `UnityEditor`, `gameEnableLog`, `EnableLog`

### 名前空間名
- 形式: `PascalCase` を `.` で連結
- ルール:
  - MUST:
    - `Uraty.機能名` の順で整理しなければならない
  - SHOULD:
    - 原則としてフォルダ構造と概ね一致させる
  - MUST NOT:
    - 新規に `Common` / `Utils` / `Helper` 命名の名前空間・クラスを追加してはならない
- 例:
  - OK:
    - `Uraty.Player`
    - `Uraty.UI.Inventory`
    - `Uraty.Platform.Audio`
  - NG:
    - `game`
    - `MyNamespace`
    - `Common.Utils.Helpers`

### 通知（Stream）名
- 形式: `PascalCase`
- ルール:
  - SHOULD:
    - 原則として状態変化（変更後）は `...Changed` を使用する
    - 原則として状態変化（変更前）は `...Changing` を使用する
    - 原則として処理の開始 / 完了は `...Started` / `...Completed` を使用する
    - 原則として要求・依頼は `...Requested` を使用する
  - MUST:
    - 外部公開する通知は R3 の Stream（Observable）として公開し、名前に `Stream` 接尾辞を付けなければならない（例: `HpChangedStream`）
  - MUST NOT:
    - `On` プレフィックスを通知名に付けてはならない（`OnHpChangedStream` など）
    - `Event` など意味の薄い接尾辞を付けてはならない（`HpChangedEventStream` など）
- 例:
  - OK: `HpChangedStream`, `AttackStartedStream`, `SaveCompletedStream`, `RetryRequestedStream`
  - NG: `OnHpChangedStream`, `HpChangedEventStream`, `Event1Stream`

### 購読ハンドラ名
- 形式: `PascalCase`
- ルール:
  - SHOULD:
    - 原則として `Handle` プレフィックスを使用する
  - MUST NOT:
    - 通知（Stream）名と同名にしてはならない（`HpChangedStream` と `HpChangedStream` など）
    - `On` プレフィックスをハンドラ名に使用してはならない
- 例:
  - OK: `HandleHpChanged`, `HandleRetryRequested`, `HandleButtonClicked`
  - NG: `HpChangedStream`, `OnHpChanged`

### 通知送出メソッド名
- 形式: `PascalCase`
- ルール:
  - SHOULD:
    - 原則として `Publish` プレフィックスを使用する
  - MUST NOT:
    - 通知（Stream）名と同名にしてはならない
- 例:
  - OK: `PublishHpChanged`, `PublishRetryRequested`, `PublishButtonClicked`
  - NG: `HpChangedStream`, `OnHpChanged`

### 補足（R3 / UniTask）
- 通知（Stream）は「継続的に流れる値・状態変化」を表すものとする。
- 単発で完了する処理（ロード・通信・演出待ち等）は原則として `UniTask / UniTask<T>` を使用してよい。
  - `UniTask / UniTask<T>` を返す関数は、原則として `Async` 接尾辞を付ける（例: `LoadAsync`, `SaveAsync`）。

### ファイル名
- 形式: `PascalCase`
- ルール:
  - MUST:
    - ツールや慣習などで名前が固定されているディレクトリは、その慣習に従わなければならない
    - public class / 主要型名とファイル名を一致させなければならない
    - Unity の `MonoBehaviour` / `ScriptableObject` は型名とファイル名を一致させなければならない
  - SHOULD:
    - 原則として 1 ファイル 1 主要型とする
  - SHOULD NOT:
    - 原則として 1 ファイルに複数の public 型を定義しない
- 例:
  - OK: `PlayerController.cs`, `DamageInfo.cs`, `InventoryPresenter.cs`, `PlayerControllerEditor.cs`
  - NG: `playercontroller.cs`, `Player.cs`（中身が `PlayerManager`）, `script1.cs`

### ディレクトリ名
- 形式: `PascalCase`
- ルール:
  - MUST:
    - ツールや慣習などで名前が固定されているディレクトリは、その慣習に従わなければならない
    - 機能単位またはレイヤ単位で整理しなければならない（同一階層で混在させてはならない）
    - `Misc`, `Temp`, `NewFolder` を放置してはならない
    - Unity 予約フォルダ名は正確に使用しなければならない（例: `Editor`, `Resources`, `StreamingAssets`, `Plugins`）
  - SHOULD:
    - 概念・機能を表すディレクトリ名は単数形を使用する（例: `Player`, `Inventory`, `Audio`）
    - 同種の要素を複数まとめる分類ディレクトリは複数形を使用する（例: `Scripts`, `Textures`, `Materials`, `Animations`）
  - MAY:
    - 常に上に表示させることを目的に、プレフィックスとして `_` を使用してよい
- 例:
  - OK:
    - `Player`
    - `Inventory`
    - `UI`
    - `Editor`（Unity 予約）
  - NG:
    - `misc`
    - `aaa`
    - `New Folder`

### ブランチ名
- 形式: `<type[(scope)]>/[<ticket-id>-]<short-description>`（`kebab-case`）
- ルール:
  - MUST:
    - 種別を先頭に付けなければならない
      - `feature` 新機能
      - `fix` バグ修正
      - `refactor` 振る舞いを変えない整理
      - `chore` 雑務 / 設定更新（機能追加・バグ修正・設計変更を含まない変更に限る）
      - `docs` ドキュメント修正
      - `test` テストコードの追加 / 修正
      - `ci` CI / CD の修正
    - short-description を含めなければならない
  - SHOULD:
    - `feature / fix / refactor` は原則として Issue 番号を含める
  - MUST NOT:
    - 変更の主目的が機能追加・不具合修正・設計整理であるにもかかわらず `chore` を使用してはならない
- 例:
  - OK:
    - `feature/123-add-player-jump`
    - `feature(player)/123-add-player-jump`
    - `fix/456-crash-on-startup`
    - `docs/update-readme-installation`
    - `ci(github)/add-windows-build`
  - NG:
    - `test`
    - `mybranch`
    - `feature/AddPlayerJump`
    - `feature/add_player_jump`
    - `chore/add-player-jump`

## 通知（Signal/Stream）規約

### 目的
通知の表現を統一し、意図しない依存・購読解除漏れ・命名の混乱を防止する。

### 基本方針
- 通知は R3 の Stream（Observable）として公開する。

### 共通ルール
- MUST NOT:
  - 新規コードで C# の `event` を使用してはならない
- MAY:
  - サードパーティ API の外部契約として `event` が必須であるラッパー / アダプターでは使用してよい（ただし MUST NOT の例外としてリードプログラマーの合意を必要とする）

## Unity 固有メソッド規約

### 目的
Unity ライフサイクルメソッドの責務を統一し、初期化漏れ・二重初期化・依存順序のバグを防止する。

### 前提
- 本章は主に `MonoBehaviour` を対象とする。
- 例外的に本章の原則に従えない場合は、「規約の例外」に従って PR に理由を記載する。

### Awake
- 目的: 依存関係のない初期化処理を行う
- ルール:
  - MUST:
    - 自身の内部初期化のみを行わなければならない
      - 参照のキャッシュ（`GetComponent` など）
      - フィールド初期化
      - 必須の依存オブジェクトの取得（事前に参照が注入されている場合を含む）
  - MUST NOT:
    - 他オブジェクトの状態に依存する処理を行ってはならない
    - ゲーム全体を探索する高コスト API（例: `FindObjectOfType`, `FindAnyObjectByType`, `FindFirstObjectByType`）を初期化目的で常用してはならない

### 補足
- `Awake` での取得可否は「他オブジェクトの状態に依存しないか」を基準に判断する。

### OnEnable / OnDisable
- 目的: イベントの購読・登録 / 解除を行う
- ルール:
  - SHOULD:
    - 原則としてイベント・通知・入力の購読 / 登録と解除を `OnEnable` / `OnDisable` に記述する
      - 通知（Stream）の `Subscribe` / `Dispose`
      - Input、MessageBus、Observable 等への登録 / 解除
  - SHOULD NOT:
    - 原則として購読解除処理を `OnDestroy` のみに記述しない（無効化中のリークを防ぐため）

### Start
- 目的: 依存関係のある初期化処理を行う
- ルール:
  - MUST:
    - 他オブジェクトの状態に依存する初期化処理は `Start` に記述しなければならない
      - 例: 他オブジェクトのプロパティを参照して自身の状態を初期化する
  - MUST NOT:
    - 依存関係のない初期化処理を `Start` に記述してはならない

### 判断基準
- 他オブジェクトの状態・初期化結果・設定値を参照して自身の初期状態を決める処理は `Start` に記述する。
- 自身のみで完結する参照キャッシュ・初期値設定は `Awake` に記述する。

### Update / FixedUpdate / LateUpdate
- 目的: フレームごとの処理を行う
- ルール:
  - SHOULD:
    - 原則として `Update` は入力・AI・状態遷移など、フレーム単位の制御に使用する
    - 原則として `FixedUpdate` は物理（`Rigidbody` 操作）に使用する
    - 原則として `LateUpdate` は追従カメラなど、他更新後に必要な処理に使用する
  - MUST NOT:
    - `Update` 内で高コスト探索 API（`GetComponent`, `Find`, `FindObjectOfType` 等）を常用してはならない

### 補足
- 一時的なデバッグコードはレビュー前に除去する。
- 必要な参照は `Awake` / `Start` でキャッシュする。

### OnDestroy
- 目的: オブジェクトが完全に破棄される際の最終クリーンアップ
- ルール:
  - MUST:
    - オブジェクト破棄時の最終クリーンアップを行わなければならない
      - ネイティブリソースの解放
      - static 参照の解放
    - `OnDisable` で行うべき購読解除処理を代替として集約してはならない
  - SHOULD:
    - 原則としてイベント解除は `OnDisable` に記述する

## フォルダ構造

### 目的
プロジェクトのフォルダ構造を統一し、変更影響範囲の局所化・責務の明確化・依存爆発の防止を行う。

### 基本方針
「Feature（機能）縦割り」＋「Platform / Shared」

- Feature を第一の配置基準とする
- `Shared` は再利用実績または再利用計画が明確なものに限定する
- `Platform` はゲーム固有ルールを持たない基盤に限定する

### 判定原則
- フォルダ配置に迷う場合は、まず「その要素がどの Feature に属するか」を判定する。
- 特定 Feature に属する場合は `_Features/<FeatureName>/...` に配置する。
- 2つ以上の Feature で再利用され、かつ特定 Feature に依存しない場合のみ `_Shared` を検討する。

### Assets
- 目的: Unity プロジェクトのルートフォルダで、すべてのゲームアセットとコードを格納する
- ルール:
  - MUST:
    - 横断置き場（`Assets` 直下の `Scenes` / `Prefabs` / `Scripts` 等）を新設してはならない
    - `Assets` 直下のトップレベルフォルダは以下を使用しなければならない
      - `_Features` 機能
      - `_Shared` 複数機能で共有するコードやアセット
      - `_Platform` アプリ基盤（ゲームロジック禁止）
      - `_ThirdParty` 外部アセット
      - `_Generated` 自動生成物

### 依存ルール
- MAY:
  - `Feature` → `Shared`
  - `Feature` → `Platform`
  - `Shared` → `Platform`
- MUST NOT:
  - `Shared` → `Feature`
  - `Platform` → `Feature`
- SHOULD NOT:
  - 原則として `Feature` → `Feature` に依存しない（必要な場合は `Shared` で抽象化する）

### 依存の判断に迷う場合
- 依存先が特定 Feature 名を知っている時点で、`Shared` / `Platform` への配置は不適切である可能性が高い。

### Features
- 目的: ゲームの個々の機能を実装するコードとアセットを格納する
- ルール:
  - MUST:
    - 機能内にその機能専用の `Scenes`, `Prefabs`, `Scripts` などを配置しなければならない
    - `Feature` 直下は以下の標準フォルダ名から作成しなければならない
      - `Scenes/`
      - `Prefabs/`
      - `Scripts/`
      - `UI/`
      - `Audio/`
        - `BGM/`
        - `SFX/`
        - `Voice`
      - `Textures/`
      - `Models/`
      - `Animations/`
      - `VFX/`
      - `Materials/`
      - `Shaders/`
      - `Fonts/`
      - `Docs/`
      - `Data/`（ScriptableObject）

### 補足
- 「完結」とは、当該 Feature 専用物を原則として当該 Feature 配下に配置することを指す。
- 共通化の必要が明確になった時点で `_Shared` への移動を検討する。

### Shared
- 目的: 複数の機能で共有されるコードやアセットを格納する
- ルール:
  - MUST:
    - `_Shared` に配置するのは、2つ以上の Feature で共通に必要なものに限らなければならない
  - MUST NOT:
    - 特定 Feature に依存するものを配置してはならない

### 補足
- 単なる「将来使うかもしれない」は `_Shared` 配置の根拠にしない。
- 共通化により依存関係が複雑化する場合は、Feature 内に留める判断を優先してよい（`SHOULD` の例外として PR で理由を記載）。

### Platform
- 目的: プラットフォーム固有のコードやアセットを格納する
- ルール:
  - MUST:
    - 入力、セーブ、ネットワーク、アセット読み込みなどの共通基盤を配置しなければならない
  - MUST NOT:
    - 具体的なゲーム機能を知ってはならない

### 判断例
- OK: `SaveRepository`, `InputAdapter`, `AssetLoader`
- NG: `QuestSaveManager`（特定 Feature の概念を知っている）

### ThirdParty
- 目的: サードパーティ製のアセットやプラグインを格納する
- ルール:
  - SHOULD NOT:
    - サードパーティ資産は原則として改変しない
  - SHOULD:
    - 原則として必要な拡張はラッパー / アダプターコードで吸収する

### Generated
- 目的: 自動生成されたコードを格納する
- ルール:
  - MUST NOT:
    - 自動生成コード以外を置いてはならない
  - SHOULD:
    - 原則として手修正が必要なコードは生成対象外に分離するか、生成後処理で対応する

## 自動チェック対象
以下は静的解析・Lint・CI での自動検査対象候補とする。

- 命名規則の一部（private フィールド `_camelCase`、型名 `PascalCase`）
- `MonoBehaviour` の public 可変フィールド禁止（`[SerializeField] private` 推奨）
- フォルダトップレベル構造（`Assets/_Features` 等）
- 禁止名前（`Common`, `Utils`, `Helper`, `Misc`, `Temp`, `NewFolder`）