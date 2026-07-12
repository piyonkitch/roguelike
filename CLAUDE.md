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
    ├── [オデュッセイア系敵].cs # Siren, Polyphemus, CursedSailor, Scylla, Circe, Shade（5〜7階）
    ├── Teiresias.cs        # 冥府の予言者NPC（graph='&'）。戦わないクエスト完了役
    ├── MolyRoot.cs         # モーリュの根（graph='%'）。キルケーの豚化を無効化する所持品
    ├── [アイテム].cs       # Gold, Weapon, Armor, Potion, Scroll, Stair, StairUp, Item
    ├── StairUp.cs          # 上り階段エンティティ（graph='<'）
    ├── Gem.cs              # 宝石エンティティ（graph='*'）本物・偽物共通クラス
    └── Altar.cs            # 祭壇エンティティ（graph='_'）4階に4つ配置
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
- 視界: 半径4マス（壁による遮蔽判定あり）。**Heroの視界のみを表示**。Companion自身は常時表示されるが視界は合成しない
- フロア数: 7階層（1〜4階は既存コンテンツ、5〜7階はオデュッセイア由来のクエスト・敵を追加。7階が最終フロアで下り階段はない）
- 下り階段（`>`）で次の階へ。上り階段（`<`）で前の階に戻れる
- `<` は各フロアの Hero 入口位置に固定配置される（1階には存在しない）
- `<` で戻ると Hero は元の `>` の位置に、Companion は Hero 近くに再配置される
- フロア状態（maze・entitylist・`>` 座標）は `FloorState` として `savedFloors: Dictionary<int, FloorState>` に保存される

### 迷路品質チェック
- `init()` / `generateNewFloor()` は条件を満たすまで迷路を再生成する（`do...while` ループ）
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
  0. **Heroから離れすぎ**（マンハッタン距離 > 8）: 他の行動をキャンセルして即座にHeroへ追従（`MAX_HERO_DISTANCE = 8`）
  1. **クエストアイテム配達**（名前付き武器を所持）: クエストギバー（Bilbo）のいる階ならそちらへ移動。隣接したら待機
  2. **魔法攻撃**（HP > max/3 かつ MP > 0）: 8方向2マスに敵がいて射線上に味方がいなければ魔法を放つ
  3. **射線確保移動**（HP > max/3 かつ MP > 0）: 隣接セルに移動すれば射線が開く場合は移動（**実際に移動できた場合のみ次の行動をスキップ**）
  4. **近接攻撃フォールバック**（HP > max/3）: 隣接敵を装備武器で攻撃
  5. **逃走**（HP ≤ max/3 かつ視界内に敵）: 敵から遠ざかる方向へ移動。Heroから6マス以内に留まる
  6. **アイテム探索**: 視界内・6マス以内・Heroから8マス以内のアイテムに向かう
  7. **Hero追従**: `maze.walk()` の実経路長が `FOLLOW_DISTANCE`（2）を超えていれば最短経路で1マス移動。壁を挟むとマンハッタン距離だけでは近く見えて動かなくなるため、経路長で判定する
- **Companion は Hobbit を一切攻撃しない**（魔法・近接・素手すべて）。`Entity.tryMove()` 内でも `isCompanion && e is Hobbit` の場合は攻撃せず通行不可とする
- フロア移動時、CompanionはHeroの近く（距離3以内・歩行距離10ステップ以内）に再配置される
- Companion の配置（`changePosNear`）は `maze.walk()` で到達可能性と歩行距離（`maxWalkDist=10`）を確認してから確定する。到達不能または遠すぎる位置には配置しない

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
- **配置**: `elist`（`initEnemyAndThings()`）の小文字 `d` 列で階ごとの出現数を指定（デフォルトは2階のみ1体）。`clist` の大文字 `D` は Dragon が使用済みのため、Dwarf には小文字 `d` を割り当てている
- Companion の AI から完全に除外: 魔法攻撃・射線確保・近接攻撃・逃走判定（`getNearestEnemy()`）すべて対象外
- **5x5壁クリアで1階上に穴発生**: Dwarf が壁を崩し続け、任意の5x5エリアが壁ゼロになると、その中心座標に対応する1階上の床タイルが穴（`MazeDist.pits`）になる。重複トリガー防止のため `triggeredPits` HashSet で管理。穴は `MazeAlgo.takePendingPits()` → `Logic.processPendingPits()` のパイプラインで `savedFloors[floor-1].maze` に反映される

### 穴タイルと落下
- 穴は `MazeDist.pits` (HashSet<string>) で管理。`isPit(x,y)` / `addPit(x,y)` で操作
- 表示: `Form1.show()` で `isPit` を先に判定し DarkSlateGray で塗りつぶす
- **Hero が穴マスに進む**: `ctrlUp/Down/Left/Right` の `manualmove()` 後に `checkAndHandleHeroFall()` を呼ぶ。穴なら `heroFall()` を実行して tick() をスキップ
- **Companion が穴マスに進む**: `tick()` 内の `checkPitFalls()` で検出。`companionFall()` を呼び、現フロアの entitylist から除いて `isInactive = true` にする
- **その他のエンティティ**: 同様に `entityFall()` で現フロア除外・下フロアへ追加
- `heroFall()`: 現フロア状態を `savedFloors[floor]` に保存 → `floor++` → 既訪問なら復元、未訪問なら `generateNewFloor()` で新規生成。Hero は穴の XY 座標に着地
- `companionFall()`: entitylist.Remove → isInactive=true → `savedFloors[floor+1].entitylist` に未登録なら追加（同一参照が既にある場合はスキップ）
- 非アクティブ Companion: `isInactive == true` の間は `Companion.move()` でスキップ。`newvision()`・`isEntitySeeable()` でも除外。ステータス欄に「(別フロア)」と表示
- **Hero が同じフロアに来ると復活**: `reactivateCompanionsOnCurrentFloor()` がフロア切り替え時に entitylist を走査し `isInactive = false` にする

### フロア状態管理（savedFloors）
- `floorHistory: Stack<FloorState>` を廃止し `savedFloors: Dictionary<int, FloorState>` に変更
- 全フロアの状態を番号をキーに保持するため、「一度戻った階」への再移動でも状態が保持される
- セーブ/ロード: `formatter.Serialize(stream, savedFloors)` で永続化。旧フォーマットのセーブは SerializationException をキャッチして案内メッセージを出す

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
| `%`  | 食べ物・死体（7階のモーリュの根も同じ記号で偽装している） |
| `>`  | 下り階段 |
| `<`  | 上り階段 |
| `*`  | 宝石（Gem）本物・偽物共通。色で種類を推測する |
| `_`  | 祭壇（Altar）4階のみ。空=灰色、嵌め込み済=宝石色 |
| `S`  | Siren（5階） |
| `P`  | Polyphemus（5階） |
| `C`  | CursedSailor（6階） |
| `Y`  | Scylla（6階） |
| `X`  | Circe（7階） |
| `G`  | Shade（7階） |
| `&`  | Teiresias（7階）。「唯一の高位存在」向けの記号として今後も共用予定 |
| (穴) | DarkSlateGray塗りつぶし（文字なし）。6階のカリュブディスもこの仕組みを流用 |

### 宝石システム（Gem）

- 表示文字: `*`。色で本物・偽物の種類を推測する
- 本物の宝石は **効果が発動したとき** または **Scroll of Identify** によって名前が判明する
- 識別前は「ピンクの星石」「青い星石」「琥珀色の星石」「水色の星石」のような見た目名で表示

| 本物 | 色 | 能力 | 偽物 | 偽物色 |
|------|----|------|------|--------|
| ローズクォーツ（春） | ローズピンク | **回復**：5ターンごとHP+1（大:3ターン） | ロードナイト | 赤みピンク |
| サファイア（夏） | ロイヤルブルー | **結界**：受けるダメージ-1（大:-2） | アイオライト | 青紫 |
| アンバー（秋） | 琥珀色 | **時間停止**：攻撃時30%で敵1ターン凍結（大:2ターン） | シトリン | 黄金色 |
| アクアマリン（冬） | アクアマリン | **クリティカル**：攻撃時15%で1.3倍ダメージ・端数切捨て（大:25%で1.5倍） | ブルートパーズ | 空色 |

- `power` フィールド: 1=小さな原石（通常効果）, 2=大きな原石（強化効果・クエスト対象）
- 宝石は Bat 以外のすべてのキャラクター（Hero・Companion・Hobbit・Orc・Kobold 等）が拾える（Bat は `levitation=true` のため不可）
- Companion は宝石を `findNearestItem()` で検索・自動収集する
- 同一セルに複数の宝石がある場合は歩くだけですべて拾う（スタックしない）
- **同じ季節は最強1個のみ有効**（大+小を持っても大単独と同じ効果。結界は合算ではなく最大値）
- ローズクォーツの回復効果は敵を含む**全エンティティ**に適用される

### クエスト：伝説の宝石を集めよ（祭壇に捧げよ）

- **目標**: 大きな原石4種（大ローズクォーツ・大サファイア・大アンバー・大アクアマリン）を4階の祭壇4つに嵌め込む
- **大きな原石の配置**: 1階=なし、2階=大ローズクォーツ、3階=大サファイア、4階=大アンバー+大アクアマリン（4階のみ2個）
- **小さな原石**: 各フロアに2個のランダムな石（本物・偽物混在）が散在
- **祭壇（Altar）**: 4階にのみ4つ配置（graph=`_`）。東南西北の端に近いマスをBFSで選択。通行可能
  - 空き祭壇は灰色の `_`、宝石嵌め込み済みは宝石色の `_` で表示
  - 一度見たら遠ざかっても表示される（階段と同様）
  - 祭壇ごとに受け入れる宝石の季節があるが、**プレイヤーには非公開**
- **嵌め込み方法**: Heroが祭壇の上に立ち、インベントリから大きな宝石を選んで `u` を押す
  - 季節が一致すれば嵌め込み成功 → 宝石が識別され、宝石の効果は消える
  - 一致しなければ「何も起きなかった」（拾い直し可能）
- **完了条件**: 4つの祭壇すべてに正しい宝石が嵌め込まれた状態でフロア4に滞在中
- **報酬**: Gold +200と勝利メッセージ
- **進捗表示**: ステータスラベルに `★/☆` で各祭壇の嵌め込み状況を表示（フロア4滞在中のみ更新）
- **クエスト管理**: `GemQuest` クラス（Logic.cs内）。フィールドは `roseQuartzEmbedded` 等。`gemQuest` はセーブ/ロード対応

**Companion の祭壇配達AI**（`Companion.doMove()` の優先順位3.5として挿入）:
- 大きな原石を持っていてフロアに空き祭壇がある場合、最も近い空き祭壇へ向かう
- 祭壇の上で自動的に嵌め込みを試みる。失敗（季節不一致）した祭壇を `failedAltars: Dictionary<Gem, HashSet<string>>` に記憶し、次に近い未試行の空き祭壇を探す
- **Amnesia Potionを飲むと `failedAltars` がクリアされ、すべての祭壇を再試行する**
- `failedAltars` は `[NonSerialized]`（セーブ/ロード後にリセット）

### 5〜7階：オデュッセイア由来のクエスト・敵

『オデュッセイア』の挿話をモチーフにした追加フロア。4階に下り階段（`>`）を1つ追加して接続している。

#### 5階：一つ目の巨人ポリュペモス

| 敵 | graph | HP | str | tough | 行動 |
|---|---|---|---|---|---|
| Siren | `S` | 4 | 1 | 0 | `elist`の`S`列で階ごとに配置数を指定（デフォルトは5階に3体）。待ち伏せ型で自分からは追跡しない。隣接するパーティメンバーには近接攻撃、そうでなければ射程5マス以内・射線が通る相手を40%の確率で`charmed`にする |
| Polyphemus | `P` | 16 | 6 | 3 | `elist`の`P`列で階ごとに配置数を指定（デフォルトは5階に1体）。Orc/Kobold型の追跡ロジック（距離6以内なら`maze.walk()`で追跡、それ以外はランダム移動） |

- **クエスト「一つ目の巨人を欺け」**: 5階でPolyphemusを視認すると自動発生（`PolyphemusQuest.triggered`）。撃破で完了（討伐のみ、原典のような目つぶし搦め手は未実装）
- **報酬**: 未定。「後続フロアで使うあると便利な特別アイテム」にする方針だけ決まっており、具体的なアイテムは未実装（`Logic.updatePolyphemusQuest()` にコメントあり）

#### 6階：スキュラとカリュブディスの海峡

| 敵/ギミック | graph | HP | str | tough | 行動 |
|---|---|---|---|---|---|
| CursedSailor（呪われた乗組員） | `C` | 3 | 2 | 0 | `elist`の`C`列で階ごとに配置数を指定（デフォルトは6階に4体）。Orc/Kobold型の追跡雑魚 |
| Scylla | `Y` | 14 | 5 | 2 | 固定1体、移動しない。隣接する最大2体を1ターンで攻撃する |
| Charybdis（渦） | ― | ― | ― | ― | 敵Entityではなく既存の穴システム（`MazeAlgo.addPit()`）を流用した地形ギミック。踏むと即座に7階へ強制落下する |

- **強制チョークポイント**（`Logic.carveStrait6()`）: 6階生成時、他の配置より先にHeroの位置を基準としてマップ中央付近に南北の壁の帯を作り、通行可能な隙間（ゲート）を2箇所だけ残す
  - 一方のゲートにはScyllaを隣接配置、もう一方のゲートにはCharybdis（渦）を配置
  - 下り階段は壁の帯の向こう側（Heroから見て奥側）に強制配置されるため、**ゲートのどちらかを必ず通らないと下り階段・7階へ渡れない**
  - 地形編集には `breakWall()` ではなく副作用のない `MazeAlgo.setWall()` を使う（詳細は「既知の設計上の注意点」を参照）
- **クエスト「危険な海峡を渡れ」**: 6階到達で自動発生（`StraitQuest.triggered`）。7階へ渡り切れば（階段経由でもCharybdis経由でも）完了
- **報酬**: 未定（後日検討）

#### 7階：キルケーの島と冥府（最終フロア）

| 敵/NPC/アイテム | graph | HP | str | tough | 行動 |
|---|---|---|---|---|---|
| Circe | `X` | 8 | 2 | 1 | 固定1体、移動しない（Iceと同型）。隣接するパーティメンバーに30%の確率で`polymorphed`（豚化）を付与。モーリュの根を所持している相手・既に豚化中の相手には効果なし |
| MolyRoot（モーリュの根） | `%` | 1 | ― | ― | 敵ではなくアイテム。見た目は食料と区別がつかないが、拾っても食べられることはなく、所持しているだけでキルケーの豚化を無効化する。拾うと「食べない方がよさそうだ」という警告が出る |
| Shade（冥府の霊） | `G` | 1 | 1 | 0 | Bat型のランダム徘徊。複数体配置。`Entity.avoidsAttack()`により物理攻撃を50%の確率ですり抜ける |
| Teiresias | `&` | 10 | 0 | 0 | 固定1体、移動せず戦わない冥府の予言者NPC。Companionからは`Hobbit`・`Dwarf`と同様に非攻撃対象として除外される |

- 7階には下り階段がない（最終フロア）。小さな星石も1〜4階の宝石クエスト専用のため7階では出現しない
- **クエスト「キルケーの呪いを越えて冥府へ」**: 7階到達で自動発生（`UnderworldQuest.triggered`）。Hero・Companionのいずれかがテイレシアスに隣接すると完了。報酬はGold+100
- graph `&` は「唯一の高位存在」向けの記号としてTeiresiasが使用している（Nethack由来）。将来「悪しき神」「魔王」のような敵を追加する場合も同じ記号を共用する想定

### セーブ・ロード
- `BinaryFormatter` で `roguelike.bin` に保存
- `maze`・`floor`・`entitylist`・`savedFloors` をシリアライズ
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
- 視界は `Logic.addVision()` にまとめられており、`newvision()` から **Heroのみ** 呼ぶ。Companion の視界は合成しない。`isEntitySeeable()` でも Hero の視界のみ判定し、Companion 自身は `isInactive` でなければ常時 `true` を返す
- 描画時に同一マスに複数エンティティが重なった場合、`Form1.entityPriority()` で優先度を判定し最上位のものだけ表示する（Hero > Companion > 生きている敵 > 死体 > アイテム）
- 射線確保移動（Companion AI）で `manualmove()` が失敗した場合（Hobbit等に阻まれた場合）は `return` せず次の行動に進む。移動成功判定は座標変化で確認する
- `@` と `h` が Dwarf を攻撃しないチェックは `Entity.tryMove()` 内（`e is Dwarf`）で行う。Companion の AI では魔法・近接・逃走の各ループに `if (e is Dwarf) continue;` を追加している
- `MazeAlgo.breakWall()` / `MazeDist.breakWall()` は壁フラグ（`isWall`）のみ変更し、`isVisible` は変更しない。視界への反映は通常の `newvision()` に委ねる
- Dwarf の壁掘りカウント（`digCounts`）は壁座標をキーとする `Dictionary<string, int>`。シリアライズ可能
- `Entity.suppressConsole`（`[NonSerialized] internal bool`）: パーティ外かつ非可視エンティティがアイテムを拾う際に `tryMove()` 内でセットし、各 `pickup()` でメッセージを抑制する。pickup 後に `false` にリセットする
- `MazeDist.initmaze()` の末尾で、四方が壁（または盤外）に囲まれた孤立床マスを壁に変換する（1パスのみ・連鎖しない）
- `Altar` は `tryMove()` で `_` を明示的に素通り処理（階段と同様）。`entityPriority()` でもアイテムと同扱い（優先度0）にして、生物より下に描画される
- **`frozen` のデクリメント責任**: `Entity.move()` がテンプレートメソッドとして一元管理する（`if (!isLive()) return; if (frozen > 0) { frozen--; return; } doMove(...)`）。個々のエンティティ（Companion・敵）は `move()` を直接オーバーライドせず、`doMove()` だけを実装すればよい。例外は `Hero` で、`frozen` は `Logic.tick()` の `while (hero.frozen-- > 0)` が管理するため `move()` 自体を空実装でオーバーライドしている。新規エンティティ追加時は `move()` ではなく `doMove()` を実装すること（`move()` を誤って直接オーバーライドするとこの一元管理から外れ、永久凍結バグを再発させる）
- **`charmed`（魅了）**: `Entity.charmed`（残りターン数）と `Entity.charmSource`（魅了元）。`Entity.move()` テンプレートが `frozen` の次にチェックし、`charmed > 0` の間は `doMove()` を呼ばず `charmSource` へ向かって強制的に1マス移動する（`maze.walk()` で経路を求め `manualmove()` を呼ぶ）。Hero は `move()` をバイパスするため、`Logic.tick()` の `applyHeroCharm()` が同じロジックを担当し、`while (hero.frozen-- > 0 || applyHeroCharm())` で、Heroが魅了により行動不能な間もワールドの1ターン分の処理（`tick()` 本体）を魅了が解けるまで繰り返す。プレイヤー操作側も `ctrlUp/Down/Left/Right` で `hero.charmed <= 0` のときのみ `manualmove()` を呼ぶようガードしている（魅了中は自分で操作できない）
- **`polymorphed`（豚化）**: `Entity.polymorphed`（残りターン数）。`frozen`・`charmed` と異なり移動そのものは妨げず、`Entity.move()` テンプレートでデクリメントした後 `doMove()` は通常どおり呼ぶ。攻撃・魔法の封じ込めは各所で個別にガードする方式: 近接攻撃は `Entity.tryMove()` の攻撃分岐で `this.polymorphed > 0` なら `return false`（通れないが攻撃もしない）、Companion の魔法発動・射線確保移動は `Companion.doMove()` 内で `polymorphed <= 0` を条件に追加している。Hero の `polymorphed` は `move()` をバイパスするため `Logic.tick()` の冒頭で毎ターンデクリメントする
- **新規 `.cs` ファイルは `RogueLike.csproj` への追加が必須**: このプロジェクトはSDKスタイルではない旧形式のcsprojで、`<Compile Include="...">` に列挙されていないファイルはビルド対象に含まれない（コンパイルエラーにもならず「型が見つかりません」という紛らわしいエラーになる）。新規クラスファイルを追加したら必ず `RogueLike.csproj` の `<ItemGroup>` にも `<Compile Include="XXX.cs" />` を追記すること
- **`MazeAlgo.setWall()` と `breakWall()` は別物**: `breakWall()` はDwarfの壁掘り専用で、呼ぶと `check5x5ForPit()` が走り、5x5エリアが全クリアになった際に「1階上に穴を開ける」という副作用（`pendingPits`）を発生させる。フロア生成時にマップ形状を意図的に編集したい場合（例: 6階の海峡=`Logic.carveStrait6()`で中央に壁の帯を作りゲートを開ける処理）はこの副作用のない `setWall(x, y, isWall)` を使うこと。生成時の地形編集に `breakWall()` を誤用すると、無関係なフロアに意図しない穴が発生する
- **`clist`/`elist` によるフロア別配置数の指定**: `Logic.initEnemyAndThings()` の `clist` はA-Z・a-zの全52文字＋記号6種（58文字）を定義済みで、`elist` は各フロアにつき58桁の数字列（`clist` と同じ位置の文字に対応する出現数）。新しい敵を任意のフロアに配置可能にしたい場合は、`clist` 内の未使用文字（`initEnemyAndThings()` 内で `clist.IndexOf()` により実際に参照されている文字と重複しないもの）を選び、`elist` の該当桁を設定してパースループを追加する。**`clist` の文字は各敵の表示グラフ（`graph`）とは無関係な内部インデックス**であり、大文字・小文字も区別されるので、表示グラフと同じ文字を使いたい場合は大文字・小文字どちらかが既存の敵と衝突していないか確認すること（例: Dwarf の表示グラフは小文字 `d` で、大文字 `D` は既に Dragon が使用しているが、小文字 `d` は空いていたためそのまま `clist` 側の索引にも使っている）
