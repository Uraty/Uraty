# Conventions

## 適用範囲
新規コードすべてに適用する。

## 最終決定者
リードプログラマー

















## 命名規則

## 目的
プロジェクト内の命名を統一し、可読性・保守性を向上させることを目的とする。

## 基本方針
- 名前は意味が分かるものにする
- 略語は一般的なもののみ使用する（例：ID、UI、APIなど）
- １文字変数はループカウンタなど限定的な用途でのみ許可する
- 単数形 / 複数形を正しく使う
  - 単体: `user`
  - 配列・複数: `users`

### クラス名
- 形式: `PascalCase`
- ルール:
  - 名詞を使う
  - 役割が分かる名前にする
  - `MonoBehaviour` は「コンポーネントとして何をするか」が分かる名前にする
  - `Manager`, `Controller`, `Service` は責務が明確な場合のみ使う
- 例:
  - OK: `PlayerManager`, `TextureLoader`, `EnemySpawner`, `AudioPlaybackService`
  - NG: `manager`, `dataClass`, `Common`, `Util1`

### 構造体名
- 形式: `PascalCase`
- ルール:
  - 名詞を使う
  - 役割が分かる名前にする
  - 値オブジェクト / 軽量データであることを意識した名前にする
- 例:
  - OK: `DamageInfo`, `SpawnRequest`, `PlayerStatus`
  - NG: `manager`, `dataClass`, `TempStruct`

### Enum 名
- 形式: `PascalCase`
- ルール:
  - 名詞を使う
  - 単数形を基本にする
  - `Enum` などの接尾辞は付けない
  - 状態を表す場合も「状態集合の型名」として自然な名詞にする
- 例:
  - OK: `Color`, `Direction`, `PlayerState`, `GamePhase`
  - NG: `Colors`, `DirectionEnum`, `EPlayerState`

### Enum メンバ名
- 形式: `PascalCase`
- ルール:
  - 名詞を使う（状態名は慣用的に形容詞/分詞も可）
  - 型名プレフィックスを付けない（冗長）
  - `[Flags]` を使う場合は `None = 0` を必須にする
- 例:
  - OK: `None`, `Idle`, `Moving`, `Dead`
  - NG: `PLAYER_STATE_IDLE`, `PlayerStateIdle`, `A`

### 関数名
- 形式: `PascalCase`
- ルール:
  - 動詞を使う
  - 役割が分かる名前にする
  - `bool` を返す関数は `Is / Has / Can / Should` で始める
- 例:
  - OK: `LoadTexture`, `ApplyDamage`, `GetSpawnPoint`, `IsGrounded`, `CanAttack`
  - NG: `Do`, `Run`, `ProcessData`, `Func1`

### メンバ変数名
- 形式: `private` は `_camelCase` / `public` は `PascalCase`
- ルール:
  - 原則: フィールドは `private` とし `public` は禁止とする
  - Inspector公開は `[SerializeField] private`
  - `bool` は `is / has / can / should` を含める
- 例:
  - OK:
    - `[SerializeField] private int _maxHp;`
    - `private Rigidbody _rigidbody;`
    - `private bool _isInvincible;`
  - NG:
    - `public int hp;`
    - `private int MaxHP;`（privateでPascalCase）
    - `private GameObject go;`

### ローカル変数名
- 形式: `camelCase`
- ルール:
  - スコープが短くても意味を持たせる
  - 省略しすぎない（`x`, `y` の乱用禁止）
  - `var` を使う場合も変数名で型や役割が推測できるようにする
- 例:
  - OK: `currentHp`, `targetEnemy`, `spawnPosition`, `isWithinRange`
  - NG: `tmp`, `data`, `obj`, `x1`

### グローバル変数名
- 原則: 禁止

### 定数名
- 形式: `PascalCase`
- ルール:
  - `const` / `static readonly` を区別する
  - 単位や意味を名前に含める（`Seconds`, `Meters`, `Count` など）
  - マジックナンバーを直接書かない
- 例:
  - OK:
    - `private const int MaxPartyMemberCount = 4;`
    - `private static readonly string SaveFileName = "save.json";`
    - `private const float InvincibleDurationSeconds = 1.5f;`
  - NG:
    - `ConstValue`
    - `A = 4`

### インターフェース名
- 形式: `PascalCase` + `I` プレフィックス
- ルール:
  - `I` を接頭してインターフェースであることを明示する
  - 役割が分かる名前にする

- 例:
  - OK: `IEnemy`, `IDamageable`, `ISaveable`, `IAudioPlaybackService`
  - NG: `EnemyInterface`, `Damageable`, `Saveable`, `AudioService`

### マクロ名
- 形式: `UPPER_SNAKE_CASE`
- ルール:
  - `#if`, `#define` のシンボルは大文字スネークケース
- 例:
  - OK: `UNITY_EDITOR`, `DEVELOPMENT_BUILD`, `GAME_ENABLE_LOG`
  - NG: `UnityEditor`, `gameEnableLog`, `EnableLog`

### 名前空間名
- 形式: `PascalCase` を `.` で連結
- ルール:
  - `Uraty.機能名` の順で整理
  - フォルダ構造と概ね一致させる
  - `Scripts`, `Common`, `Utils` の乱用を避ける（責務単位に分ける）
- 例:
  - OK:
    - `Uraty.Player`
    - `Uraty.UI.Inventory`
    - `Uraty.Core.Audio`
  - NG:
    - `game`
    - `MyNamespace`
    - `Common.Utils.Helpers`

### ファイル名
- 形式: `PascalCase`
- ルール:
  - public class / main type 名とファイル名を一致させる
  - 1ファイル1主要型を原則にする
  - Unityの `MonoBehaviour` / `ScriptableObject` は一致必須で扱う
- 例:
  - OK: `PlayerController.cs`, `DamageInfo.cs`, `InventoryPresenter.cs`, `PlayerControllerEditor.cs`
  - NG: `playercontroller.cs`, `Player.cs`（中身が `PlayerManager`）, `script1.cs`

### ディレクトリ名
- 形式: `PascalCase`
- ルール:
  - 機能単位 or レイヤ単位で整理する（混在させない）
  - `Misc`, `Temp`, `NewFolder` を放置しない
  - Unity予約フォルダ名は正確に使用する（例: `Editor`, `Resources`, `StreamingAssets`, `Plugins`）
  - ただし常に上に表示させることを目的に、プレフィックスとして`_`を使用することは許可する
- 例:
  - OK:
    - `Player`
    - `Inventory`
    - `UI`
    - `Editor`（Unity予約）
  - NG:
    - `misc`
    - `aaa`
    - `New Folder`

### ブランチ名
- 形式: `<type>/<ticket-id>-<short-description>`（`kebab-case`）
- ルール:
  - 種別を先頭に付ける（`feature`, `fix`, `refactor`, `chore`, `hotfix`, `docs`）
    - `feature` 新機能
    - `fix` バグ修正
    - `refactor` 振る舞いを変えない整理
    - `chore` 雑務/設定更新（乱用禁止）
    - `hotfix` 緊急修正
    - `docs` ドキュメント修正
  - Issue番号を原則含めること
  - 何をしたブランチかが一目で分かるようにする
  - 日本語は避ける
- 例:
  - OK:
    - `feature/123-player-jump`
  - NG:
    - `test`
    - `mybranch`
    - `修正`
    - `player`













## Unity 固有メソッド規約

## 目的
Unity ライフサイクルメソッドの責務を統一し、初期化漏れ・二重初期化・依存順序のバグを防止する。

### Awake
- 目的: 依存関係のない初期化処理を行う
- ルール:
  - 自身の内部初期化のみを行う
    - 参照のキャッシュ（`GetComponent` など）
    - フィールド初期化
    - 必須の依存オブジェクトの取得
  - 他オブジェクトの状態に依存する処理を行わない
    - FindObjectOfType でゲーム全体を探索して初期化する、などは禁止（依存順序の問題を引き起こすため）

### OnEnable / OnDisable
- 目的: イベントの購読・登録 / 解除を行う
- ルール:
  - 購読・登録 / 解除のみを行う
    - event += / event -=
    - Input、MessageBus、Observable 等への登録
  - 初期化処理は行わない（Awake に記述すること）

### Start
- 目的: 依存関係のある初期化処理を行う
- ルール:
  - 他オブジェクトの状態に依存する初期化処理を行う
    - 例: 他オブジェクトのプロパティを参照して自身の状態を初期化する、など
  - 依存関係のない初期化は行わない（Awake に記述すること）

### Update / FixedUpdate / LateUpdate
- 目的: フレームごとの処理を行う
- ルール: 
  - Update：入力・AI・状態遷移など、フレーム単位の制御（GetComponent, Find などの GC/負荷 問題がある関数の常用は禁止）
  - FixedUpdate：物理（Rigidbody 操作）に限定
  - LateUpdate：追従カメラなど「他の更新後に行う必要があるもの」に限定
  
### OnDestroy
- 目的: オブジェクトが完全に破棄される際の最終クリーンアップ
- ルール:
  - ネイティブリソースや static 参照の解放のみを行う
  - イベント解除は原則 OnDisable に記述する














## フォルダ構造

## 目的
プロジェクトのフォルダ構造を統一し、変更影響範囲の局所化・責務の明確化・依存爆発の防止を行う。

## 基本方針
「Feature（機能）縦割り」＋「Platform/Shared」

### Assets
- 目的: Unity プロジェクトのルートフォルダで、すべてのゲームアセットとコードを格納する
- ルール:
  - `_Features` 機能
  - `_Shared` 複数機能で共有するコードやアセット
  - `_Platform` アプリ基盤（ゲームロジック禁止）
  - `_ThirdParty` 外部アセット
  - `_Generated` 自動生成物
- 依存:
  - 許可
    - `Feature` → `Shared`
    - `Feature` → `Platform`
    - `Shared`  → `Platform`
  - 禁止
    - `Shared`   → `Feature`
    - `Platform` → `Feature`
    - `Feature` → `Feature` （原則禁止。櫃うような場合は Shared で抽象化）

#### Features
- 目的: ゲームの個々の機能を実装するコードとアセットを格納する
- ルール:
  - 各機能は１フォルダで完結させる（機能の縦割り）
  - 機能内にその機能専用の Scenes, Prefabs, Scripts などを配置する
  - ※横断置き場（Assets 直下の Scenes/Prefabs/Scripts など）を作らないこと

#### Shared
- 目的: 複数の機能で共有されるコードやアセットを格納する
- ルール:
  - 「２つ以上の Feature が共通で必要」になったときのみ追加を検討する
  - 特定 Feature に依存するものは禁止

#### Platform
- 目的: プラットフォーム固有のコードやアセットを格納する
- ルール:
  - 入力、セーブ、ネットワーク、アセット読み込みなど
  - 具体的なゲーム機能を知ってはならない

#### ThirdParty
- 目的: サードパーティ製のアセットやプラグインを格納する
- ルール:
  - 原則として改変しない

#### Generated
- 目的: 自動生成されたコードを格納する
- ルール:
  -　自動生成コード以外は置かない