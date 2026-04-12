# roguelike

C# Windows Forms製のローグライクゲーム。

## プロジェクト構造

```
roguelike/
├── roguelike.sln
└── Maze/
    ├── Program.cs          # エントリーポイント
    ├── Form1.cs            # UI・キー入力 (RogueLike クラス)
    ├── Logic.cs            # ゲームロジック全体
    ├── Entity.cs           # 全エンティティの基底クラス
    ├── Hero.cs             # プレイヤー
    ├── MazeAlgo.cs         # 迷路の抽象基底クラス
    ├── MazeDist.cs         # 迷路生成・経路探索の実装
    ├── Grid.cs             # グリッド管理
    ├── Constant.cs         # 定数 (NGRID=20, VISION_DISTANCE=4)
    ├── Companion.cs        # AI制御のコンパニオン（2体）
    ├── [敵].cs             # Acid, Bat, Dragon, Dwarf, Hobbit, Ice, Kobold, Orc
    ├── [アイテム].cs       # Gold, Weapon, Armor, Potion, Scroll, Stair, StairUp, Item
    └── StairUp.cs          # 上り階段エンティティ（graph='<'）
```

## アーキテクチャ

- **Entity** がすべての登場物（プレイヤー・敵・アイテム）の基底クラス
- `entitylist: List<Entity>` に全エンティティをフラットに管理
- **Logic** がゲームループ（`tick()`）と全操作を担当。Form から呼ばれる
- **Form1** (RogueLike) は描画とボタンイベントのみ。ロジックは持たない
- マップ表示は `PictureBox` への `Bitmap` 直接描画（**17px/マス**）
- コンソール出力は `TextBoxWriter` でフォーム内 TextBox にリダイレクト
- `MagicEffect` クラス（Logic.cs 内）が魔法エフェクトの一時描画データを保持

## ゲーム仕様

### マップ・視界
- マップサイズ: 20×20
- 視界: 半径4マス（壁による遮蔽判定あり）。**HeroとCompanion両方の視界を合成して表示**
- フロア数: 4階層
- 下り階段（`>`）で次の階へ。上り階段（`<`）で前の階に戻れる
- `<` は各フロアの Hero 入口位置に固定配置される（1階には存在しない）
- `<` で戻ると Hero は元の `>` の位置に、Companion は Hero 近くに再配置される
- フロア状態（maze・entitylist・`>` 座標）は `FloorState` としてスタックに保存される

### 迷路品質チェック
- `init()` / `initNextLevel()` は条件を満たすまで迷路を再生成する（`do...while` ループ）
- 条件: Hero から BFS で到達可能なマスが **80マス以上**、かつ **到達可能な武器が1個以上**
- 1階では到達可能な武器として **Sting** が存在することを必須とする

### 戦闘
- ダメージ = `攻撃者のstrength - 防御者のtoughness`（0以下なら無効）
- 武器装備で strength 加算、鎧装備で toughness 加算
- HP0で死亡 → グラフィックが `%`（死体）に変わる
- 死体の所持品と gold が床に落ちる

### 成長
- 経験値5点でHPmax増加（+1〜3）・**MPmax増加（+1〜3）**、経験値リセット
- 移動のたびに20%でHP自然回復

### パーティ
- Hero（赤）+ Companion 2体（青）の3人パーティ
- Companion は `isPartyMember = true`、`isCompanion = true`
- Hero が Companion のマスへ移動すると位置を入れ替える。Companion は自発的に入れ替えない
- Companion の AI（`Companion.move()`）の優先順位:
  1. **クエストアイテム配達**（名前付き武器を所持）: クエストギバー（Bilbo）のいる階ならそちらへ移動。隣接したら待機
  2. **魔法攻撃**（HP > max/3 かつ MP > 0）: 8方向2マスに敵がいて射線上に味方がいなければ魔法を放つ
  3. **射線確保移動**（HP > max/3 かつ MP > 0）: 隣接セルに移動すれば射線が開く場合は移動（**実際に移動できた場合のみ次の行動をスキップ**）
  4. **近接攻撃フォールバック**（HP > max/3）: 隣接敵を装備武器で攻撃
  5. **逃走**（HP ≤ max/3 かつ視界内に敵）: 敵から遠ざかる方向へ移動。Heroから6マス以内に留まる
  6. **アイテム探索**: 視界内・6マス以内・Heroから8マス以内のアイテムに向かう
  7. **Hero追従**: マンハッタン距離 > 2 で `maze.walk()` で最短経路
- **Companion は Hobbit を一切攻撃しない**（魔法・近接・素手すべて）。`Entity.tryMove()` 内でも `isCompanion && e is Hobbit` の場合は攻撃せず通行不可とする
- フロア移動時、CompanionはHeroの近く（距離3以内）に再配置される
- Companion の配置（`changePosNear`）は `maze.walk()` でHeroへの到達可能性を確認してから確定する。到達不能な孤立マスには配置しない

### Companion の魔法
- MP初期値1、レベルアップで +1〜3 増加
- 毎ターン20%でMP自然回復
- 魔法を放つと MP を1消費
- 8方向2マスに飛ぶ。壁で止まる。味方への誤射を回避
- ランダムダメージ（1〜2）。敵・Heroどちらにも当たる
- 魔法記号: 左右`-` 上下`|` 右上左下`/` 左上右下`\`（シアン色で1秒表示）

### Companion のアイテム挙動
- `%`（死体）: 歩いた時に食べる（HP+1）
- `$`（ゴールド）: 歩いた時に拾う
- `!`（ポーション）: 拾ったターンに即使用。識別済みで有害なら drop
- `?`（スクロール）: 拾ったターンに即使用。識別済みで有害なら drop
- `)`（武器）・`[`（鎧）: 拾って自動装備。今より弱い or 同レベルなら drop
- **名前付き武器（`engraveName` あり）**: 装備・dropせずクエストアイテムとして保持
- 有害判定: Poison/LoseStrength/Amnesia Potion、Scroll of Sleep

### Dwarf
- 文字: `d`、HP=5、strength=3、toughness=1
- **2ターンに1回**行動（迅速性が低い）
- **壁優先移動**: 隣接する壁がある方向からランダムに選択。なければランダム移動
- **壁掘り**: 同じ壁に4回以上押し当てると、4回目以降は毎ターン20%の確率で壁を崩す
  - 壁が崩れた場合、さらに20%の確率で `$`（金貨3〜7枚）が出現する
  - 壁が崩れても視野外なら画面には反映されない（`breakWall()` は `isVisible` を変更しない）
- **パーティと不可侵**: `@`（Hero・Companion）と `h`（Hobbit）は Dwarf を攻撃しない。Dwarf も `@` と `h` を攻撃しない
- **近くで掘ると音**: パーティメンバーとのユークリッド距離が5以内で壁に押し当てると「がんがんがん」と出力
- **配置**: 1階にのみ1体スポーン（`initEnemyAndThings()` でフロア1判定）
- Companion の AI から完全に除外: 魔法攻撃・射線確保・近接攻撃・逃走判定（`getNearestEnemy()`）すべて対象外

### Hobbit
- 全Hobbitに名前あり（Frodo, Samwise, Merry, Pippin, Lobelia, Fatty 等）
- 挨拶時に名前を名乗る
- 2階の Hobbit 1体が **Bilbo**（クエストギバー）: HP=10、攻撃されても怒らない
- Bilbo は毎ターン隣接するパーティメンバー（Hero・Companion）が Sting を持っているか確認する。持っていれば受け取りクエスト完了（Heroの位置によらず実行）

### クエスト: Stingを届けよ
- **発生**: 2階で Bilbo に隣接すると依頼される
- **目標**: 1階にスポーンする名前付きダガー「Sting」（`engraveName = "Sting"`）を Bilbo に届ける
- **完了条件**: Bilbo に隣接した状態で Sting を所持（Hero または Companion どちらでも可）
- **報酬**: Gold +30
- Companion が Sting を拾った場合、自動的に Bilbo のもとへ届けに向かう

### アイテム記号
| 記号 | 種類 |
|------|------|
| `@`  | Hero / Companion |
| `d`  | Dwarf |
| `$`  | Gold |
| `)`  | Weapon |
| `[`  | Armor |
| `!`  | Potion |
| `?`  | Scroll |
| `%`  | 食べ物・死体 |
| `>`  | 下り階段 |
| `<`  | 上り階段 |

### セーブ・ロード
- `BinaryFormatter` で `roguelike.bin` に保存
- `maze`・`floor`・`entitylist`・`floorHistory` をシリアライズ
- ロード後は `entitylist` から `isCompanion` フラグで `companions` リストを再構築し、`ensureTransients()` で非シリアライズフィールドを再初期化、`newvision()` で視界を更新する

## 既知の設計上の注意点

- ゲームオーバー判定は `Logic` ではなく `Form1.show()` 内で行われている（`Form1.cs` にコメントあり）
- `Entity` の乱数 `rnd` はフィールドに直接 `new Random()` しているため、短時間に複数インスタンスを生成すると同じシードになる可能性がある。`Logic.initEnemyAndThings()` で `Thread.Sleep(20)` を挟んでいるのはこの回避策
- `entitylist` を `foreach` 中に変更できないため、死体の持ち物ドロップは `tick()` 内でループ外に分離して処理している
- `tick()` の `foreach` は `entitylist.ToList()` でスナップショットを取っている。Companion の自動装備drop など `move()` 内で `entitylist` を変更する処理があるため
- `Entity` に `isPartyMember`・`isCompanion` フラグあり。敵との `@` 衝突を攻撃にするか入れ替えにするかの判定に使用
- `Companion` の `pendingMagicEffects`・`magicRnd` は `[NonSerialized]`。デシリアライズ後は `ensureTransients()` で再初期化される
- `tryMove()` の `else` 分岐はすべての未知グラフ記号を「敵」として攻撃する。新しいエンティティを追加する際は `>` や `<` のように明示的に素通り処理を追加すること
- `Companion` が `tryMove()` で Hobbit のいるマスに踏み込もうとした場合、攻撃せず通行不可とする処理を `tryMove()` 内に追加済み（`isCompanion && e is Hobbit`）
- 視界は `Logic.addVision()` にまとめられており、`newvision()` から Hero と各 Companion 分を呼ぶ
- 描画時に同一マスに複数エンティティが重なった場合、`Form1.entityPriority()` で優先度を判定し最上位のものだけ表示する（Hero > Companion > 生きている敵 > 死体 > アイテム）
- 射線確保移動（Companion AI）で `manualmove()` が失敗した場合（Hobbit等に阻まれた場合）は `return` せず次の行動に進む。移動成功判定は座標変化で確認する
- `@` と `h` が Dwarf を攻撃しないチェックは `Entity.tryMove()` 内（`e is Dwarf`）で行う。Companion の AI では魔法・近接・逃走の各ループに `if (e is Dwarf) continue;` を追加している
- `MazeAlgo.breakWall()` / `MazeDist.breakWall()` は壁フラグ（`isWall`）のみ変更し、`isVisible` は変更しない。視界への反映は通常の `newvision()` に委ねる
- Dwarf の壁掘りカウント（`digCounts`）は壁座標をキーとする `Dictionary<string, int>`。シリアライズ可能
