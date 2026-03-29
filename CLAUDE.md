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
    ├── [敵].cs             # Acid, Bat, Dragon, Hobbit, Ice, Kobold, Orc
    └── [アイテム].cs       # Gold, Weapon, Armor, Potion, Scroll, Stair, Item
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
- フロア数: 4階層、下り階段（`>`）で次の階へ

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
  1. **魔法攻撃**（HP > max/3 かつ MP > 0）: 8方向2マスに敵がいて射線上に味方がいなければ魔法を放つ
  2. **射線確保移動**（HP > max/3 かつ MP > 0）: 隣接セルに移動すれば射線が開く場合は移動
  3. **近接攻撃フォールバック**（HP > max/3）: 隣接敵を装備武器で攻撃
  4. **逃走**（HP ≤ max/3 かつ視界内に敵）: 敵から遠ざかる方向へ移動。Heroから6マス以内に留まる
  5. **アイテム探索**: 視界内・6マス以内・Heroから8マス以内のアイテムに向かう
  6. **Hero追従**: マンハッタン距離 > 2 で `maze.walk()` で最短経路
- フロア移動時、CompanionはHeroの近く（距離3以内）に再配置される
- Companion の配置（`changePosNear`）は `maze.walk()` でHeroへの到達可能性を確認してから確定する。到達不能な孤立マスには配置しない

### Companion の魔法
- MP初期値1、レベルアップで +1〜3 増加
- 毎ターン20%でMP自然回復
- 魔法を放つと MP を1消費
- 8方向2マスに飛ぶ。壁で止まる。味方への誤射を回避
- ランダムダメージ（1〜4）。敵・Heroどちらにも当たる
- 魔法記号: 左右`-` 上下`|` 右上左下`/` 左上右下`\`（シアン色で1秒表示）

### Companion のアイテム挙動
- `%`（死体）: 歩いた時に食べる（HP+1）
- `$`（ゴールド）: 歩いた時に拾う
- `!`（ポーション）: 拾ったターンに即使用。識別済みで有害なら drop
- `?`（スクロール）: 拾ったターンに即使用。識別済みで有害なら drop
- `)`（武器）・`[`（鎧）: 拾って自動装備。今より弱い or 同レベルなら drop
- 有害判定: Poison/LoseStrength/Amnesia Potion、Scroll of Sleep

### アイテム記号
| 記号 | 種類 |
|------|------|
| `@`  | Hero / Companion |
| `$`  | Gold |
| `)`  | Weapon |
| `[`  | Armor |
| `!`  | Potion |
| `?`  | Scroll |
| `%`  | 食べ物・死体 |
| `>`  | 下り階段 |

### セーブ・ロード
- `BinaryFormatter` で `roguelike.bin` に保存
- `maze`・`floor`・`entitylist` をシリアライズ

## 既知の設計上の注意点

- ゲームオーバー判定は `Logic` ではなく `Form1.show()` 内で行われている（`Form1.cs` にコメントあり）
- `Entity` の乱数 `rnd` はフィールドに直接 `new Random()` しているため、短時間に複数インスタンスを生成すると同じシードになる可能性がある。`Logic.initEnemyAndThings()` で `Thread.Sleep(20)` を挟んでいるのはこの回避策
- `entitylist` を `foreach` 中に変更できないため、死体の持ち物ドロップは `tick()` 内でループ外に分離して処理している
- `tick()` の `foreach` は `entitylist.ToList()` でスナップショットを取っている。Companion の自動装備drop など `move()` 内で `entitylist` を変更する処理があるため
- `Entity` に `isPartyMember`・`isCompanion` フラグあり。敵との `@` 衝突を攻撃にするか入れ替えにするかの判定に使用
- `Companion` の `pendingMagicEffects`・`magicRnd` は `[NonSerialized]`。デシリアライズ後は `ensureTransients()` で再初期化される
- セーブ・ロード後は `logic.companions` リストが再構築されない既知の問題あり（`entitylist` には含まれる）
- 視界は `Logic.addVision()` にまとめられており、`newvision()` から Hero と各 Companion 分を呼ぶ
- 描画時に同一マスに複数エンティティが重なった場合、`Form1.entityPriority()` で優先度を判定し最上位のものだけ表示する（Hero > Companion > 生きている敵 > 死体 > アイテム）
