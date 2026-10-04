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
    ├── Altar.cs            # 祭壇エンティティ（graph='_'）4階に4つ配置
    ├── BattleView.cs       # 戦闘ビュー（画面左）: CombatLog・タイムライン・エフェクト・背景
    ├── StickFigure.cs      # スティックマン描画基盤（Pose・Anim・Figure・Humanoid・WeaponArt）
    ├── EnemyDesigns.cs     # 全キャラクターの戦闘ビュー用デザイン
    ├── ItemDesigns.cs      # 戦闘ビュー用の物の絵（拾えるもの・祭壇・落とし穴・カリュブディス・階段・岩の壁）
    └── SoundFx.cs          # 効果音（プログラムで波形を合成。音源ファイルなし）
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

### 操作（キーボード）
- 画面のボタンをクリックしても、物理キーでも操作できる。キーは `Form1` が受け取り、各ボタンのクリック処理をそのまま呼ぶ（`KeyPreview = true`。矢印キーと Esc は `ProcessCmdKey()`、文字キーは `OnKeyPress()`）
- 移動: カーソルキー／NumLock を解除したテンキーの 8・2・4・6（カーソルキーと同じキーとして届く）／`k`・`j`・`h`・`l`
- `>`・`<`: 下り階段・上り階段。`i`: 持ち物の一覧を開く・閉じる。`u`・`d`・`w`・`W`: 一覧で選んだ物を使う・落とす・構える・着る。`t`・`T`: 武器を外す・鎧を脱ぐ（文字キーは大文字・小文字を区別）
- **持ち物の一覧が開いている間**は、↑↓（`k`・`j`）で一覧の選択を動かし、Hero は動かさない。`i` か Esc で閉じる。キーで開いたときは先頭の物を選んだ状態にする
- 一覧が開いているときの Enter は選んだ物を使う（`u` と同じ）。それ以外の Enter・Space は何もしない（Windows の標準動作のままだと、最後にクリックしてフォーカスが残っているボタンを押してしまうため `ProcessCmdKey()` で受け取って捨てる）
- デバッグ用の 5F・6F・7F ワープボタンにはキーを割り当てない
- 持ち物の一覧（`listBoxItemlist`）は ＜ などのボタンに重なる位置にあるため、起動時に `BringToFront()` で一番手前に出している（Designer では一覧よりボタンを先に追加しており、先に追加した部品ほど手前に表示される）

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
- 条件: Hero から BFS(幅優先サーチ) で到達可能なマスが **80マス以上**、かつ **到達可能な武器が1個以上**
- 1階では到達可能な武器として **Sting** が存在することを必須とする

### 戦闘
- ダメージ = `攻撃者のstrength - 防御者のtoughness + ランダム(-1〜+1)`（0以下なら無効）
- 武器装備で strength 加算、鎧装備で toughness 加算
- HP0で死亡 → グラフィックが `%`（死体）に変わる
- 死体の所持品と gold が床に落ちる

### Ice Jerry と凍傷
- Ice Jerry（`I`）は動かず、近接攻撃もしない。Hero・Companion に攻撃されると怒る
- 怒っている間、隣にいる生き物（Hero・Companion・ほかの敵）から**毎ターン1体をランダムに選び**、50%で4〜7ターン凍らせる（「○○ は凍りついた！」）。すでに凍っている相手・倒れている者・戦わない相手（`isNonHostile`）は対象外
- **凍傷**: Ice Jerry に凍らされている間、毎ターン15%で HP-1（「○○ は凍傷を負った！」）。HP が0になれば死体になり持ち物を落とす（`Entity.becomeCorpse()`。撃破の処理と共通）。Hero が凍傷で倒れたら `tick()` の凍結のループを止める
- 言葉の使い分け: **状態は「凍結」**（`Entity.frozen`＝残りターン数、`Entity.frozenBy`＝凍らせた者）、**出来事は「凍傷」**（`Entity.applyFrostbite()`、`CombatKind.Frostbite`）。凍傷は `frozenBy` が Ice Jerry のときだけ起きる。Scroll of Sleep の眠りやアンバーの時間停止で止まっているとき（`frozenBy` が空）は起きない。それらで凍結を上書きしたら `frozenBy` を空にする。凍結が解けたら `frozenBy` も空に戻す
- 敵・Companion の凍傷は `Entity.move()` の凍結の処理、Hero の凍傷は `Logic.tick()` の凍結のループで判定する

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
- **Companion は Hobbit を一切攻撃しない**（魔法・近接・素手すべて）。`Entity.tryMove()` 内でも `isCompanion && e is Hobbit` の場合は攻撃せず通行不可とする。魔法は経路上の誰にでも当たる（下記「Companion の魔法」）ので、Hobbit が射線上にいるときは撃たない
- フロア移動時、CompanionはHeroの近く（距離3以内・歩行距離10ステップ以内）に再配置される
- Companion の配置（`changePosNear`）は `maze.walk()` で到達可能性と歩行距離（`maxWalkDist=10`）を確認してから確定する。到達不能または遠すぎる位置には配置しない
- `changePosNear` は壁に加えて穴のマスも選ばない。Logic から Companion を Hero の近くに置くときは必ず `Logic.placeCompanionNearHero()` を使う。これは「穴も生きている敵のいるマス（6階の Scylla 等）も通らずに Hero のもとへ歩いて行けるか」を `isReachableAvoidingPits(..., avoidCreatures: true)` で確かめて位置を選ぶ（`maze.walk()` は穴も敵も通れるものとして扱うため、海峡の向こう側に置かれてしまう）。6階は Companion を配置した後に海峡の壁の帯を作るため、`initEnemyAndThings()` で海峡を作った直後にも同じ条件で置き直す

### Companion の魔法
- MP初期値1、レベルアップで +1〜3 増加
- 毎ターン20%でMP自然回復
- 魔法を放つと MP を1消費
- 8方向2マスに飛ぶ。壁で止まる
- **経路上の誰にでも当たる**（Nethack と同じ。`castMagic()` は相手を選ばない）。当てられた相手は攻撃されたとみなす（`beat()`。Ice Jerry・Hobbit などが怒る）。ただし戦わない相手（`isNonHostile`＝Teiresias・降参した Circe）は当たっても HP が減らない（「びくともしない」。クエストの案内役を倒してしまわないため）
- **撃つかどうかの判断**（`hasFriendlyFireFrom()`）: 経路上に味方（Hero・Companion）か、敵ではない相手（Hobbit・Dwarf・Teiresias・降参した Circe）がいれば撃たない
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
- **拾っても捨てるだけの物は探しに行かない**（`Companion.wouldDiscard()`）: 今の装備以下の武器・鎧（名前付きのクエストアイテムは除く）、識別済みで有害な Potion・Scroll。探しに行くと「拾う→すぐ捨てる→Hero について1歩離れる→また拾いに戻る」を繰り返してそばから離れなくなるため

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
- Companion の AI から完全に除外: 魔法攻撃・射線確保・近接攻撃・逃走判定（`getNearestEnemy()`）すべて対象外。魔法の経路上にいれば Companion は撃たない（撃った魔法は経路上の Dwarf にも当たる）
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
- 全Hobbitに名前あり（Frodo, Samwise, Merry, Pippin, Lobelia, Fatty 等）。2階に2体、3階に2体
- 話しかける（Hero が隣に来る）と名前を名乗る。**話すのは隣に来た最初のターンだけ**（`wasAdjacent`。隣にいる間は繰り返さない。離れてまた隣に来ると話す）
- Bilbo 以外の Hobbit は、名乗ったあと宝石クエストの昔話を1つずつ語る（`Hobbit.loreIndex`。2階の1人目＝話0、2人目＝話1、3階の1人目＝話2）。内容は「宝石クエスト」の節を参照
- 3階の Hobbit 1体が **Bilbo**（クエストギバー）: HP=10、攻撃されても怒らない。1階の Sting を3階まで運ぶ依頼になる
- Bilbo は毎ターン隣接するパーティメンバー（Hero・Companion）が Sting を持っているか確認する。持っていれば受け取りクエスト完了（Heroの位置によらず実行）

### クエスト: Stingを届けよ
- **発生**: 3階で Bilbo に隣接すると依頼される
- **目標**: 1階にスポーンする名前付きダガー「Sting」（`engraveName = "Sting"`）を Bilbo に届ける
- **完了条件**: Bilbo に隣接した状態で Sting を所持（Hero または Companion どちらでも可）
- **報酬**: Gold +30
- Companion が Sting を拾った場合、自動的に Bilbo のもとへ届けに向かう
- Sting を受け取ったあとの Bilbo は、次のターンに「玉座の大きな宝石は2階から4階に散らばっている。似た色の偽物もあるが、本物は不思議な力を持つ」と宝石クエストの手がかりを話す（以後も会うたびに話す）

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
- 識別前は「ピンクの宝石」「青い宝石」「琥珀色の宝石」「水色の宝石」のような見た目名で表示

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
- **祭壇（Altar）**: 4階にのみ4つ配置（graph=`_`）。東南西北の端に近いマスをBFS(幅優先サーチ)で選択。通行可能
  - 空き祭壇は灰色の `_`、宝石嵌め込み済みは宝石色の `_` で表示
  - 一度見たら遠ざかっても表示される（階段と同様）
  - 祭壇ごとに受け入れる宝石の季節がある（東＝春・南＝夏・西＝秋・北＝冬）。直接は教えず、**Hobbit の謎かけで示す**:
    - 話0（2階）: 地下4階の王の間には季節の女神の玉座があり、四方の台座に大きな宝石がはめられていた。今は盗まれて台座だけ。宝石を台座に戻した者には女神の褒美がある（クエストの目的）
    - 話1（2階）: 『日の昇る方には、花咲く季節の石を。日の最も高い方には、深い海の色の石を』（東＝春の薔薇色、南＝夏の青）
    - 話2（3階）: 『日の沈む方には、実りの季節の蜜の色の石を。日の届かぬ方には、凍った水の色の石を』（西＝秋の琥珀色、北＝冬の水色）
    - 祭壇の配置（`Logic` の東南西北と季節の対応）を変えるときは、謎かけ（`Hobbit.Lore`）も合わせて直すこと
- **嵌め込み方法**: Heroが祭壇の上に立ち、インベントリから大きな宝石を選んで `u` を押す。Hero が空いている祭壇の上に乗ると「台座には宝石をはめるくぼみがある。（持ち物の一覧で宝石を選び、u ではめ込む）」と表示する（`Logic.tellAltarHint()`。移動は `Logic.heroStep()` に共通化）。Help にも記載
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
| CursedSailor（呪われた乗組員） | `C` | 3 | 2 | 0 | `elist`の`C`列で階ごとに配置数を指定（デフォルトは6階に4体）。Orc/Kobold型の追跡雑魚。Hero が4歩以内にいると8〜13ターンおきに海峡の手がかりをつぶやく（スキュラの強さ、渦は何でも飲み込む、飲み込まれた者は深い底へ吐き出される、渡る道は二つ、魔女の島へは白い花の黒い根を持っていけ。全員で順番に回す） |
| Scylla | `Y` | 14 | 7 | 2 | 固定1体、移動しない。隣接する最大2体を1ターンで攻撃する |
| Charybdis（渦） | ― | ― | ― | ― | 敵Entityではなく既存の穴システム（`MazeAlgo.addPit()`）を流用した地形ギミック。踏むと即座に7階へ強制落下する |

- **強制チョークポイント**（`Logic.carveStrait6()`）: 6階生成時、他の配置より先にHeroの位置を基準としてマップ中央付近に南北の壁の帯を作り、通行可能な隙間（ゲート）を2箇所だけ残す
  - 一方のゲートにはScyllaを隣接配置、もう一方のゲートにはCharybdis（渦）を配置
  - 下り階段は壁の帯の向こう側（Heroから見て奥側）に強制配置されるため、**ゲートのどちらかを必ず通らないと下り階段・7階へ渡れない**
  - ゲートは帯のマスとその左右のマスを床にした3マスの通り道。生成後に「Heroから両ゲートの手前へ行ける」「両ゲートの向こう側から下り階段へ行ける」を**穴を壁とみなした BFS(幅優先サーチ)**（`isReachableAvoidingPits()`）で確認し、満たさなければ `strait6Valid = false` として `isMazeAcceptable()` でマップを作り直す（`maze.walk()` は穴を床として扱うのでこの判定には使えない）。下り階段の配置は試行回数に上限があり、固まらない
  - 地形編集には `breakWall()` ではなく副作用のない `MazeAlgo.setWall()` を使う（詳細は「既知の設計上の注意点」を参照）
- **クエスト「危険な海峡を渡れ」**: 6階到達で自動発生（`StraitQuest.triggered`）。7階へ渡り切れば（階段経由でもCharybdis経由でも）完了
- **報酬**: 未定（後日検討）

#### 7階：キルケーの島と冥府（最終フロア）

| 敵/NPC/アイテム | graph | HP | str | tough | 行動 |
|---|---|---|---|---|---|
| Circe | `X` | 8 | 2 | 1 | 固定1体、移動しない（Iceと同型）。隣接するパーティメンバーに30%の確率で`polymorphed`（豚化）を付与。モーリュの根を所持している相手・既に豚化中の相手には効果なし。**モーリュの根を持つパーティメンバーが隣に来ると降参する**（原典どおり。`Circe.yielded`、セーブ対象）: 豚にした者を元に戻し、「冥府の奥の盲目の予言者テイレシアスに会え」と案内し、以後は誰も豚にせず、誰からも攻撃されない（`isNonHostile`） |
| MolyRoot（モーリュの根） | `%` | 1 | ― | ― | 敵ではなくアイテム。見た目は食料と区別がつかないが、拾っても食べられることはなく、所持しているだけでキルケーの豚化を無効化する。拾うと「食べない方がよさそうだ」という警告が出る |
| Shade（冥府の霊） | `G` | 1 | 1 | 0 | Bat型のランダム徘徊。複数体配置。`Entity.avoidsAttack()`により物理攻撃を50%の確率ですり抜ける。乗組員と同じ仕組みで、近くでテイレシアスのことや原典の仲間エルペーノールのことをささやく |
| Teiresias | `&` | 10 | 0 | 0 | 固定1体、移動せず戦わない冥府の予言者NPC。Hero・Companion問わず誰からも攻撃できない（`Entity.isNonHostile` が true。`Entity.tryMove()` の攻撃分岐と Companion の AI がこれを見る。魔法は当たるが HP は減らない）。クエスト完了役を誤って倒してソフトロックする事故を防ぐための措置 |

- 7階には下り階段がない（最終フロア）。小さな宝石も1〜4階の宝石クエスト専用のため7階では出現しない
- **クエスト「キルケーの呪いを越えて冥府へ」**: 7階到達で自動発生（`UnderworldQuest.triggered`）。到着時に「ここがキルケーの島…」に続けて、白い花をつけた黒い根の草（モーリュの根）がお守りになることを語る。Hero・Companionのいずれかがテイレシアスに隣接すると完了。報酬はGold+100
- 物語の流れ: 6階の乗組員がモーリュの根を教える → 7階でモーリュの根を拾う → キルケーが降参してテイレシアスへ案内する → Shade もテイレシアスのことをささやく → テイレシアスに会う
- graph `&` は「唯一の高位存在」向けの記号としてTeiresiasが使用している（Nethack由来）。将来「悪しき神」「魔王」のような敵を追加する場合も同じ記号を共用する想定

### 戦闘ビュー（画面左 360x340）
- `Form1` のコンストラクタで `battlePic` を画面左に追加し、Designer 上の既存コントロールは実行時に X を +372 ずらしている（Designer ファイル自体は変更していない）
- 戦闘の記録: `CombatLog.Add(attacker, defender, CombatKind, damage)`（静的・セーブ対象外）。`Entity.tryMove()` の攻撃分岐、`Companion.castMagic()`、Dragon（炎）、Ice（凍結）、Siren（魅了）、Circe（豚化）、`Logic.updateUnderworldQuest()`（テイレシアスとの対面）から呼ぶ
- `Form1.afterAction()` が `CombatLog.Drain()` → `BattleView.Play()` を呼ぶ。`show()` より先に呼ぶのはゲームオーバーのダイアログ中も倒れる演出を再生するため
- `Drain()` は各 defender の最後のイベントに、その時点で `hit <= 0` なら `killed` を付ける
- 再生: Timer 33ms で描画。1イベント 400ms（同じ相手との複数イベントは 320ms ずつ）、撃破は +300ms の倒れ込み。**入力はブロックせず、次の `Play()` で即座に場面が切り替わる**
- 場面は「敵（非パーティ側）1体 vs パーティ」を1段として最大3段。Hero が関わる段を優先。戦闘がないときはパーティと視界内で最も近い生き物の待機姿
- Hero の視界外（攻撃者・防御者とも `isEntitySeeable()` が false で Hero も無関係）の戦闘は表示しない
- 状態表示: 凍結=氷のブロック、魅了=頭上のハート、豚化=豚の頭。Hero の frozen/charmed は `tick()` のループ内で解けてしまうため、イベント由来でも表示する
- **新しい敵を追加したら `EnemyDesigns.Create()` にデザインを追加すること**（未登録は `GenericFig`＝頭に graph 文字の灰色スティックマンで表示される）。新しい攻撃手段を追加したら `CombatKind` を追加し、`BattleView.DrawEffect()`・`TextFor()` に演出を足す
- 描画座標系: 足元中心が原点・右向き・Hero の身長 100。左向きは `ScaleTransform(-1, 1)` で反転するので、文字は `Figure.Text()`（反転補正あり）で描く
- **戦闘以外の場面**も同じ仕組みで再生する（`CombatLog.IsCombat()` が false の種類）。記録する場所:
  - `Pickup`（拾う・死体を食べる）: `Entity.tryMove()` の拾うループ。パーティメンバーか、見えている者のみ
  - `Use`（Potion を飲む・Scroll を読む）: `Item.use()` が `AddUse()`、各効果の関数（`Potion.useHealing` 等・`Scroll.useIdentify` 等）が `SetUseEffect(user, 効果, 効いたか)` を書き込む。**新しい Potion・Scroll を追加したら `UseEffect` と `BattleView.DrawUse()` にも演出を足すこと**
  - `Fall`（穴・カリュブディスに落ちる）: `Logic.heroFall()`・`companionFall()`・`entityFall()`。Hero が落ちた場面は落ちる前の階の背景で描く
  - `Dig`（Dwarf の壁掘り）: `Dwarf` の「がんがんがん」・壁を砕く・金貨が出る（パーティが近いときのみ）
  - `Talk`・`Give`（Hobbit の昔話・Bilbo の依頼と宝石の話、Sting を渡す、乗組員・Shade のつぶやき、キルケーの降参）: `Hobbit`・`CursedSailor`・`Shade`・`Circe`。手を振る動きは Hobbit だけ
  - `Embed`・`GemsComplete`（祭壇への嵌め込み、4つ揃った）: `Logic.tryEmbedGem()`・`Companion.doMove()`・`Logic.updateGemQuest()`
- **待機画面**: パーティ、向かい側に「一番近い生き物」か「一番近い見えている穴（6階はカリュブディス）」、Hero が階段の上か隣なら背景に階段、手前の地面に見えている拾えるもの・祭壇を近い順に最大4個（名前付き）
- **階名タイトル**: 表示中の階が変わったら（階段・落下・ワープ・ロード）画面中央に階名を約1.6秒出す（ロジック側の変更は不要）
- **物の絵**（`ItemDesigns.Create()`）: 見た目で区別できる情報は絵でも区別する。Potion は未識別名の色（`Potion.appearance`）で液体を塗り、Scroll は羊皮紙にラベル（`Scroll.label`）を書く。宝石は `DisplayColor` と大小、武器・防具は錆びを赤茶色、Sting は青白い縁取り、死体はその生き物の倒れた姿、モーリュの根は本来の姿（黒い根に乳白色の花）。**新しいアイテムを追加したら `ItemDesigns.Create()` にも絵を足すこと**（未登録は戦闘ビューに出ない）
- 武器の絵は `WeaponArt.Draw()` に一本化しており、手に持つ武器と地面の武器で共用する（手に持つ武器の錆びも見える）
- **背景のアニメーション**: 背景は階ごとに1枚の画像にキャッシュしているので、動かしたいものはキャッシュに含めず `BattleView.DrawAnimatedBackground()` で毎フレーム描く。今は6階の海の波（ゆっくり横に流れ、手前ほど速く、上下にわずかに揺れる）のみ。ほかの階の動きもここに足す
- 手前の地面の物の名前は 9pt（左上の階名タイトルと同じ）。`LayoutFrontLabels()` が名前の幅を測り、隣と重ならないよう上下2段に振り分け、必要なら右へずらす

### 効果音（SoundFx.cs）
- 音源ファイルは使わず、すべてプログラムで波形を合成する（22050Hz・16bit・モノラル。音ごとに1回だけ作ってキャッシュ）。**常に ON**（切り替えメニューはない。ユーザー指定）
- 鳴らす音: 武器ごとの攻撃（振る音＋当たった音）、攻撃の結果（クリティカル・はじかれた・結界・すり抜け）、武器を持たない敵の攻撃（噛みつき・炎・酸・凍結・魅了の歌・豚化・巨人の一撃・Shade）、Companion の魔法、金貨・ポーション・巻物を拾う音。それ以外の場面は無音
- `BattleView.Play()` が場面を組み立てたあと `CollectSounds()` で（音, 時刻）を集め、`SoundFx.PlayScene()` が1ターン分を1本に混ぜて（加算＋tanh で音割れ防止）`SoundPlayer` で非同期再生する。時刻はアニメーションに合わせる（振る音＝開始から30%、当たった音＝50%、拾う音＝55%）。新しい場面の音が来たら前の音は止まる。待機画面では鳴らさない
- 攻撃の音は `BattleView.AttackSounds()` で決める: 武器を持つ人型は `Humanoid.WeaponForSound`（武器の種類）ごと、持たない敵は種類ごと。**新しい武器・敵を追加したら `AttackSounds()` と `Sfx` にも音を足すこと**
- `SoundFx.silentForTest` は画面を使わないテスト用プログラムでスピーカーから鳴らさないためのもので、ゲームでは常に false

### セーブ・ロード
- `BinaryFormatter` で `roguelike.bin` に保存
- 保存するもの一式を `SaveData`（Logic.cs）にまとめ、**1回の `Serialize` で書き出す**。Hero・Companion は現フロアと `savedFloors` の各 `entitylist` に同じ参照で入っているため、別々に `Serialize` するとロード後に他フロアのリストの Hero・Companion が別物のコピーになる（本物の Hero がマップに出ず、敵からも攻撃されなくなる不具合の原因だった）
- `SaveData` には `hero` と `companions` も入れる（別フロアに落下中の Companion は `entitylist` にいないため）
- 旧形式（各データを別々に `Serialize`）のセーブも読める（`loadOldFormat()`）。ロード後に `removeStalePartyCopies()` で他フロアのリストから本物でない Hero・Companion を取り除く
- 保存済みフロアへ戻る処理（階段・落下・ワープ）は `ensurePartyInEntitylist()` で、本物の Hero と行動中の Companion が `entitylist` にいることを保証する
- ロード後は `ensureTransients()` で非シリアライズフィールドを再初期化し、`newvision()` で視界を更新する

## 既知の設計上の注意点

- ゲームオーバー判定は `Logic` ではなく `Form1.show()` 内で行われている（`Form1.cs` にコメントあり）
- `Entity` の乱数 `rnd` はフィールドに直接 `new Random()` しているため、短時間に複数インスタンスを生成すると同じシードになる可能性がある。`Logic.initEnemyAndThings()` で `Thread.Sleep(20)` を挟んでいるのはこの回避策
- `entitylist` を `foreach` 中に変更できないため、死体の持ち物ドロップは `tick()` 内でループ外に分離して処理している
- `tick()` の `foreach` は `entitylist.ToList()` でスナップショットを取っている。Companion の自動装備drop など `move()` 内で `entitylist` を変更する処理があるため
- `Entity` に `isPartyMember`・`isCompanion` フラグあり。敵との `@` 衝突を攻撃にするか入れ替えにするかの判定に使用
- `Companion` の `pendingMagicEffects`・`magicRnd` は `[NonSerialized]`。デシリアライズ後は `ensureTransients()` で再初期化される
- `tryMove()` の `else` 分岐はすべての未知グラフ記号を「敵」として攻撃する。新しいエンティティを追加する際は `>` や `<` のように明示的に素通り処理を追加すること
- 攻撃されてはいけない NPC（クエストの案内役など）は `Entity.isNonHostile` を true にする（Teiresias、降参した Circe）。`tryMove()` の攻撃分岐、Companion の敵選び・魔法を撃つかどうかの判断、魔法が当たったときのダメージ（無効）がこれを見る。`e is Teiresias` のような個別の型判定は増やさないこと
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
- **Hero が動けない間のワールドのターン**: `Logic.tick()` は Hero が凍結・魅了で動けない間、解けるまでワールドのターンを繰り返すが、1回の呼び出しで進めるのは最大 `MaxWorldTurnsPerTick`（50）ターンまで（超えたらいったん画面に戻り、次の操作で続きを進める）。凍結を**加算**する処理があると、解けるより速く凍結が増えてループが終わらずゲームが固まる（Ice Jerry の `frozen += 4〜7` で実際に起きた）。凍結させる処理を足すときは、すでに凍っている相手には効かないようにすること（Ice Jerry は `frozen > 0` の相手を選ばない）
- **`charmed`（魅了）**: `Entity.charmed`（残りターン数）と `Entity.charmSource`（魅了元）。`Entity.move()` テンプレートが `frozen` の次にチェックし、`charmed > 0` の間は `doMove()` を呼ばず `charmSource` へ向かって強制的に1マス移動する（`maze.walk()` で経路を求め `manualmove()` を呼ぶ）。Hero は `move()` をバイパスするため、`Logic.tick()` の `applyHeroCharm()` が同じロジックを担当し、`while (hero.frozen-- > 0 || applyHeroCharm())` で、Heroが魅了により行動不能な間もワールドの1ターン分の処理（`tick()` 本体）を魅了が解けるまで繰り返す。プレイヤー操作側も `ctrlUp/Down/Left/Right` で `hero.charmed <= 0` のときのみ `manualmove()` を呼ぶようガードしている（魅了中は自分で操作できない）。**魅了中は攻撃できない**: `Entity.tryMove()` の攻撃分岐で `this.charmed > 0` なら `return false`（魅了元の隣で立ち止まるだけ。経路上の他の敵も攻撃しない）。このため `Entity.move()` と `applyHeroCharm()` はどちらも「移動してから `charmed--`」の順にしている
- **`polymorphed`（豚化）**: `Entity.polymorphed`（残りターン数）。`frozen`・`charmed` と異なり移動そのものは妨げず、`Entity.move()` テンプレートでデクリメントした後 `doMove()` は通常どおり呼ぶ。攻撃・魔法の封じ込めは各所で個別にガードする方式: 近接攻撃は `Entity.tryMove()` の攻撃分岐で `this.polymorphed > 0` なら `return false`（通れないが攻撃もしない）、Companion の魔法発動・射線確保移動は `Companion.doMove()` 内で `polymorphed <= 0` を条件に追加している。Hero の `polymorphed` は `move()` をバイパスするため `Logic.tick()` の冒頭で毎ターンデクリメントする
- **新規 `.cs` ファイルは `RogueLike.csproj` への追加が必須**: このプロジェクトはSDKスタイルではない旧形式のcsprojで、`<Compile Include="...">` に列挙されていないファイルはビルド対象に含まれない（コンパイルエラーにもならず「型が見つかりません」という紛らわしいエラーになる）。新規クラスファイルを追加したら必ず `RogueLike.csproj` の `<ItemGroup>` にも `<Compile Include="XXX.cs" />` を追記すること
- **`MazeAlgo.setWall()` と `breakWall()` は別物**: `breakWall()` はDwarfの壁掘り専用で、呼ぶと `check5x5ForPit()` が走り、5x5エリアが全クリアになった際に「1階上に穴を開ける」という副作用（`pendingPits`）を発生させる。フロア生成時にマップ形状を意図的に編集したい場合（例: 6階の海峡=`Logic.carveStrait6()`で中央に壁の帯を作りゲートを開ける処理）はこの副作用のない `setWall(x, y, isWall)` を使うこと。生成時の地形編集に `breakWall()` を誤用すると、無関係なフロアに意図しない穴が発生する
- **`clist`/`elist` によるフロア別配置数の指定**: `Logic.initEnemyAndThings()` の `clist` はA-Z・a-zの全52文字＋記号6種（58文字）を定義済みで、`elist` は各フロアにつき58桁の数字列（`clist` と同じ位置の文字に対応する出現数）。新しい敵を任意のフロアに配置可能にしたい場合は、`clist` 内の未使用文字（`initEnemyAndThings()` 内で `clist.IndexOf()` により実際に参照されている文字と重複しないもの）を選び、`elist` の該当桁を設定してパースループを追加する。**`clist` の文字は各敵の表示グラフ（`graph`）とは無関係な内部インデックス**であり、大文字・小文字も区別されるので、表示グラフと同じ文字を使いたい場合は大文字・小文字どちらかが既存の敵と衝突していないか確認すること（例: Dwarf の表示グラフは小文字 `d` で、大文字 `D` は既に Dragon が使用しているが、小文字 `d` は空いていたためそのまま `clist` 側の索引にも使っている）
