/*
Copyright(c) 2015, 2026, piyonkitch<kazuo.horikawa.ko@gmail.com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
 list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
 this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of roguelike nor the names of its
 contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Maze
{
    class MagicEffect
    {
        public int fromX, fromY, dx, dy;
        public char symbol;
        public DateTime expiry;
    }

    // フロア状態（上の階に戻る / 穴で落ちた後に戻る ために保存）
    [Serializable]
    class FloorState
    {
        public MazeAlgo maze;
        public List<Entity> entitylist;
        public int stairX, stairY;  // Hero がこのフロアを離れた座標（戻り先）
    }

    // 宝石クエスト進捗：4階の祭壇に大きな原石4種を嵌め込む
    [Serializable]
    class GemQuest
    {
        public bool roseQuartzEmbedded;
        public bool sapphireEmbedded;
        public bool amberEmbedded;
        public bool aquamarineEmbedded;
        public bool IsCompleted =>
            roseQuartzEmbedded && sapphireEmbedded && amberEmbedded && aquamarineEmbedded;
    }

    // ポリュペモス討伐クエスト進捗：5階のポリュペモスを倒す
    [Serializable]
    class PolyphemusQuest
    {
        public bool triggered;
        public bool completed;
    }

    // 海峡越えクエスト進捗：6階でスキュラかカリュブディスの危険を乗り越えて7階へ渡る
    [Serializable]
    class StraitQuest
    {
        public bool triggered;
        public bool completed;
    }

    // 冥府探訪クエスト進捗：7階でキルケーの豚化を越え、テイレシアスと出会う
    [Serializable]
    class UnderworldQuest
    {
        public bool triggered;
        public bool completed;
    }

    // セーブデータ一式。1回の Serialize でまとめて書き出すことで、現フロアと savedFloors の
    // entitylist に共通で入っている Hero・Companion の参照が共有されたまま保存される
    // （別々に Serialize すると、ロード後に他フロアのリストの Hero・Companion が別物のコピーになる）
    [Serializable]
    class SaveData
    {
        public MazeAlgo maze;
        public int floor;
        public Entity hero;
        public List<Entity> companions;     // 別フロアに落下中の Companion は entitylist にいないため別に保存する
        public List<Entity> entitylist;
        public Dictionary<int, FloorState> savedFloors;
        public GemQuest gemQuest;
        public int turnCounter;
        public PolyphemusQuest polyphemusQuest;
        public StraitQuest straitQuest;
        public UnderworldQuest underworldQuest;
    }

    class Logic
    {
        public List<MagicEffect> magicEffects = new List<MagicEffect>();
        public MazeAlgo maze { get; set; }
        public Entity hero { get; set;  }
        public List<Entity> companions { get; set; }
        public int floor { get; set;  }
        public List<Entity> entitylist { get; set; }

        // 全フロア状態をフロア番号をキーに保持（Stack→Dictionaryに変更）
        // これにより「一度戻った階」への再移動でも状態が保持される
        private Dictionary<int, FloorState> savedFloors = new Dictionary<int, FloorState>();

        public GemQuest gemQuest = new GemQuest();
        public PolyphemusQuest polyphemusQuest = new PolyphemusQuest();
        public StraitQuest straitQuest = new StraitQuest();
        public UnderworldQuest underworldQuest = new UnderworldQuest();
        private int turnCounter;

        // Hero から到達可能なマス数を BFS(幅優先サーチ) で数える
        private int countReachableCells(int startX, int startY)
        {
            bool[,] visited = new bool[Constant.NGRID, Constant.NGRID];
            var queue = new Queue<int[]>();
            queue.Enqueue(new int[] { startX, startY });
            visited[startX, startY] = true;
            int count = 0;
            int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
            while (queue.Count > 0)
            {
                int[] pos = queue.Dequeue();
                count++;
                foreach (int[] d in dirs)
                {
                    int nx = pos[0] + d[0], ny = pos[1] + d[1];
                    if (nx >= 0 && nx < Constant.NGRID && ny >= 0 && ny < Constant.NGRID
                        && !visited[nx, ny] && !maze.isWall(nx, ny))
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new int[] { nx, ny });
                    }
                }
            }
            return count;
        }

        // 迷路の品質チェック: 広さ十分 かつ 必要な武器・大きな原石が到達可能
        private bool isMazeAcceptable()
        {
            if (countReachableCells(hero.xpos, hero.ypos) < 80) return false;
            // 6階は海峡の両ゲートが通れ、奥側に下り階段が置けていること
            if (floor == 6 && !strait6Valid) return false;
            // 大きな原石は Hero から到達可能でなければならない
            foreach (Entity e in entitylist)
            {
                if (e is Gem g && g.isLarge)
                {
                    if (maze.walk(hero.xpos, hero.ypos, e.xpos, e.ypos) == "") return false;
                }
            }
            // Polyphemus はクエスト撃破対象のため Hero から到達可能でなければならない
            foreach (Entity e in entitylist)
            {
                if (e is Polyphemus)
                {
                    if (maze.walk(hero.xpos, hero.ypos, e.xpos, e.ypos) == "") return false;
                }
            }
            foreach (Entity e in entitylist)
            {
                if (e.graph != ')') continue;
                if (maze.walk(hero.xpos, hero.ypos, e.xpos, e.ypos) == "") continue;
                // 1階はStingが到達可能であることが必須
                if (floor == 1)
                {
                    if (e is Weapon w && w.engraveName == "Sting") return true;
                }
                else
                {
                    return true;  // 2階以降は通常武器で可
                }
            }
            return false;
        }

        // Form の RogueLike() から呼ばれる
        public void init()
        {
            floor = 1;
            savedFloors = new Dictionary<int, FloorState>();
            gemQuest = new GemQuest();
            polyphemusQuest = new PolyphemusQuest();
            straitQuest = new StraitQuest();
            underworldQuest = new UnderworldQuest();
            turnCounter = 0;

            do
            {
                maze = new MazeDist();
                maze.initmaze();
                entitylist = new List<Entity>();

                hero = new Hero(maze);
                entitylist.Add(hero);
                System.Threading.Thread.Sleep(20);

                companions = new List<Entity>();
                companions.Add(new Companion(maze));
                placeCompanionNearHero(companions[0]);
                System.Threading.Thread.Sleep(20);
                companions.Add(new Companion(maze));
                placeCompanionNearHero(companions[1]);
                System.Threading.Thread.Sleep(20);
                foreach (Entity c in companions) entitylist.Add(c);

                initEnemyAndThings();
            } while (!isMazeAcceptable());

            newvision();
        }

        // ─────────────────────────────────────────
        // フロア遷移ヘルパー
        // ─────────────────────────────────────────

        // 現フロアの状態を savedFloors[floor] に保存
        private void saveCurrentFloor(int heroLeaveX, int heroLeaveY)
        {
            savedFloors[floor] = new FloorState
            {
                maze       = this.maze,
                entitylist = this.entitylist,
                stairX     = heroLeaveX,
                stairY     = heroLeaveY
            };
        }

        // targetFloor を savedFloors から復元し、Hero を (heroX, heroY) に配置
        private void restoreFloor(int targetFloor, int heroX, int heroY)
        {
            FloorState saved = savedFloors[targetFloor];
            floor      = targetFloor;
            maze       = saved.maze;
            entitylist = saved.entitylist;
            hero.xpos  = heroX;
            hero.ypos  = heroY;
            ensurePartyInEntitylist();
            reactivateCompanionsOnCurrentFloor();
            foreach (Entity c in companions)
            {
                if (c is Companion comp && comp.isInactive) continue; // 別フロアは触らない
                placeCompanionNearHero(c);
            }
            newvision();
        }

        // 新規フロアを生成して切り替える（floor はすでにインクリメント済みであること）
        private void generateNewFloor(int heroX = -1, int heroY = -1)
        {
            do
            {
                maze = new MazeDist();
                maze.initmaze();
                entitylist = new List<Entity>();

                entitylist.Add(hero);
                // 指定座標が有効なら使用、そうでなければランダム配置
                if (heroX >= 0 && heroY >= 0 && !maze.isWall(heroX, heroY))
                {
                    hero.xpos = heroX;
                    hero.ypos = heroY;
                }
                else
                {
                    hero.changePos(maze);
                }

                // 上り階段を Hero の入口位置に配置（1階には上り階段なし）
                if (floor > 1)
                    entitylist.Add(new StairUp(maze, hero.xpos, hero.ypos));
                System.Threading.Thread.Sleep(20);

                foreach (Entity c in companions)
                {
                    if (c is Companion comp && comp.isInactive) continue; // 別フロアは追加しない
                    entitylist.Add(c);
                    placeCompanionNearHero(c);
                }

                initEnemyAndThings();
            } while (!isMazeAcceptable());

            reactivateCompanionsOnCurrentFloor();
            newvision();
        }

        // 現フロアの entitylist に含まれる非アクティブ Companion を復活させる。
        // entitylist 未登録（未訪問フロアへ落下）の Companion も Hero 近くに追加して復活させる。
        private void reactivateCompanionsOnCurrentFloor()
        {
            foreach (Entity c in entitylist)
            {
                if (c is Companion comp && comp.isInactive)
                    comp.isInactive = false;
            }

            // entitylist に入っていない非アクティブ Companion を拾い直す
            foreach (Entity c in companions)
            {
                if (!(c is Companion fallen) || !fallen.isInactive) continue;
                if (entitylist.Contains(fallen)) continue;
                fallen.isInactive = false;
                placeCompanionNearHero(fallen);
                entitylist.Add(fallen);
            }
        }

        // ─────────────────────────────────────────
        // 穴落下処理
        // ─────────────────────────────────────────

        // Dwarfが5x5壁クリアした通知を受け取り、一つ上の階（floor-1）に穴を追加する
        private void processPendingPits()
        {
            List<int[]> pending = maze.takePendingPits();
            if (pending.Count == 0) return;

            int upperFloor = floor - 1;
            if (!savedFloors.ContainsKey(upperFloor)) return;

            foreach (int[] pit in pending)
            {
                int cx = pit[0], cy = pit[1];
                savedFloors[upperFloor].maze.addPit(cx, cy);
                Console.WriteLine("{0}階の崩落が{1}階に穴を開けた！ ({2},{3})", floor, upperFloor, cx, cy);
            }
        }

        // Hero が穴マスにいる場合、1階下へ落下させる
        public void heroFall()
        {
            int pitX = hero.xpos, pitY = hero.ypos;
            CombatLog.AddFall(hero, floor, floor == 6);   // 6階の穴はカリュブディスの渦

            // 現フロアを保存（> 階段の座標を戻り先とする）
            int returnX = pitX, returnY = pitY;
            foreach (Entity e in entitylist)
            {
                if (e.graph == '>') { returnX = e.xpos; returnY = e.ypos; break; }
            }
            saveCurrentFloor(returnX, returnY);

            floor++;
            Console.WriteLine("ズドーン！ Hero は穴に落ちた！ ({0},{1}) → 地下{2}階", pitX, pitY, floor);

            if (savedFloors.ContainsKey(floor))
            {
                // 既訪問フロアを復元
                FloorState saved = savedFloors[floor];
                maze       = saved.maze;
                entitylist = saved.entitylist;
                hero.xpos  = pitX;
                hero.ypos  = pitY;
                ensurePartyInEntitylist();
                reactivateCompanionsOnCurrentFloor();
                foreach (Entity c in companions)
                {
                    if (c is Companion comp && comp.isInactive) continue; // 別フロアは触らない
                    placeCompanionNearHero(c);
                }
                newvision();
            }
            else
            {
                // 未訪問フロア：新規生成（穴座標に着地）
                generateNewFloor(pitX, pitY);
            }
        }

        // Companion が穴マスに落ちた場合の処理
        private void companionFall(Companion comp)
        {
            int pitX = comp.xpos, pitY = comp.ypos;
            Console.WriteLine("{0} は穴に落ちた！ 地下{1}階へ…", comp.name, floor + 1);
            CombatLog.AddFall(comp, floor, floor == 6);

            // 現フロアの entitylist から除外
            entitylist.Remove(comp);
            comp.isInactive = true;
            comp.xpos = pitX;
            comp.ypos = pitY;

            // 1階下の entitylist にいる（Companionは全フロアのentitylistに共通参照で存在）
            // 既訪問フロアなら同一オブジェクトが savedFloors[floor+1].entitylist にも入っている
            // ただし初回生成時に追加されていない場合（Hero が穴落下で先行したとき）のみ追加
            int nextFloor = floor + 1;
            if (savedFloors.ContainsKey(nextFloor))
            {
                var nextList = savedFloors[nextFloor].entitylist;
                if (!nextList.Contains(comp))
                    nextList.Add(comp);
            }
        }

        // 敵など一般エンティティが穴に落ちた場合の処理
        private void entityFall(Entity e)
        {
            int pitX = e.xpos, pitY = e.ypos;
            if (char.IsLetter(e.graph) && e.hit > 0 && isEntitySeeable(e)) CombatLog.AddFall(e, floor, floor == 6);
            entitylist.Remove(e);
            e.xpos = pitX;
            e.ypos = pitY;

            int nextFloor = floor + 1;
            if (savedFloors.ContainsKey(nextFloor))
            {
                var nextList = savedFloors[nextFloor].entitylist;
                if (!nextList.Contains(e))
                    nextList.Add(e);
            }
        }

        // tick() 内でHero以外の穴落下を一括チェック
        private void checkPitFalls()
        {
            foreach (Entity e in entitylist.ToList())
            {
                if (e == hero) continue;
                if (!maze.isPit(e.xpos, e.ypos)) continue;

                if (e is Companion comp)
                    companionFall(comp);
                else
                    entityFall(e);
            }
        }

        // ─────────────────────────────────────────
        // 既存メソッド群
        // ─────────────────────────────────────────

        // Hobbit の名前プール（Bilbo はクエストギバーとして別途生成）
        private static readonly string[] HOBBIT_NAMES = { "Frodo", "Samwise", "Merry", "Pippin", "Lobelia", "Fatty" };
        private int hobbitNameIdx = 0;

        private string nextHobbitName()
        {
            return HOBBIT_NAMES[hobbitNameIdx++ % HOBBIT_NAMES.Length];
        }

        private void initEnemyAndThings()
        {
            // 6階は中央に壁の帯を作り、Scylla側かCharybdis側のどちらかを必ず通らないと
            // 反対側（下り階段）へ渡れないようにする。他の配置より先に地形を確定させる
            if (floor == 6)
            {
                carveStrait6();
                // 海峡が不良なら作り直しになるので、残りの配置（Thread.Sleep を伴う）を省いて早めに抜ける
                if (!strait6Valid) return;

                // Companion は海峡を作る前に配置されているため、壁の帯に埋まったり、渦の上や向こう側に
                // 置かれていることがある。渦も Scylla も通らずに Hero のもとへ行ける位置に置き直す
                foreach (Entity c in companions)
                {
                    if (c is Companion comp && comp.isInactive) continue;
                    if (!isReachableAvoidingPits(c.xpos, c.ypos, hero.xpos, hero.ypos, true))
                        placeCompanionNearHero(c);
                }
            }

            string   clist =     "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz$[)!?>";
            string[] elist = {
                                 "0300000000400000000000000000000000000000000000000000522941",
                                 "0000000220400020000000000000010000000000000000000000522341",
                                 "0000000240000060000000000000000000000000000000000000522341",
                                 "0006000000000020000000000000000000000000000000000000522341",
                                 "0000000000300031003000000000000000000000000000000000522341",
                                 "0040000000000000000000000000000000000000000000000000522340",
                                 "0000000000000000000000000000000000000000000000000000522340",
                             };
            // AcidBlob
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("A"), 1)); i++)
            {
                entitylist.Add(new Acid(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Bat
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("B"), 1)); i++)
            {
                entitylist.Add(new Bat(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Dragon
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("D"), 1)); i++)
            {
                entitylist.Add(new Dragon(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Hobbit（全員に名前をつける。3階の最後の1体はBilbo＝クエストギバー。1階の Sting を3階まで運ぶ依頼になる）
            // Bilbo 以外は宝石クエストの昔話を1つずつ語る（2階の1人目＝話0、2人目＝話1、3階の1人目＝話2）
            int hobbitCount = int.Parse(elist[floor - 1].Substring(clist.IndexOf("H"), 1));
            for (int i = 0; i < hobbitCount; i++)
            {
                Hobbit h = new Hobbit(maze);
                System.Threading.Thread.Sleep(20);
                h.loreIndex = Math.Max(0, (floor - 2) * 2 + i);
                if (floor == 3 && i == hobbitCount - 1)
                {
                    h.name = "Bilbo";
                    h.isQuestGiver = true;
                    h.hitmax = h.hit = 10;  // クエストギバーはタフ
                }
                else
                {
                    h.name = nextHobbitName();
                }
                entitylist.Add(h);
            }
            // Ice
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("I"), 1)); i++)
            {
                entitylist.Add(new Ice(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Kobold
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("K"), 1)); i++)
            {
                entitylist.Add(new Kobold(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Orc
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("O"), 1)); i++)
            {
                entitylist.Add(new Orc(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Gold
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("$"), 1)); i++)
            {
                entitylist.Add(new Gold(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Potion
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("!"), 1)); i++)
            {
                entitylist.Add(new Potion(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Scroll
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("?"), 1)); i++)
            {
                entitylist.Add(new Scroll(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Weapon
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf(")"), 1)); i++)
            {
                entitylist.Add(new Weapon(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // クエストアイテム: 1階にのみ「Sting」を配置
            if (floor == 1)
            {
                Weapon sting = new Weapon(maze, 1);
                sting.engraveName = "Sting";
                entitylist.Add(sting);
                System.Threading.Thread.Sleep(20);
            }

            // Dwarf（elist の d 列。大文字 D は Dragon が使用済みのため小文字 d を割り当てている）
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("d"), 1)); i++)
            {
                entitylist.Add(new Dwarf(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Siren
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("S"), 1)); i++)
            {
                entitylist.Add(new Siren(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Polyphemus
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("P"), 1)); i++)
            {
                entitylist.Add(new Polyphemus(maze));
                System.Threading.Thread.Sleep(20);
            }
            // 呪われた乗組員（Scylla・Charybdisは6階生成時に carveStrait6() で別途配置される）
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("C"), 1)); i++)
            {
                entitylist.Add(new CursedSailor(maze));
                System.Threading.Thread.Sleep(20);
            }

            // Circe・モーリュの根・冥府の霊・テイレシアス: 7階にのみ配置
            if (floor == 7)
            {
                entitylist.Add(new Circe(maze));
                System.Threading.Thread.Sleep(20);
                entitylist.Add(new MolyRoot(maze));
                System.Threading.Thread.Sleep(20);
                for (int i = 0; i < 3; i++)
                {
                    entitylist.Add(new Shade(maze));
                    System.Threading.Thread.Sleep(20);
                }
                entitylist.Add(new Teiresias(maze));
                System.Threading.Thread.Sleep(20);
            }

            // Armor
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("["), 1)); i++)
            {
                entitylist.Add(new Armor(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Stair
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf(">"), 1)); i++)
            {
                do
                {
                    entitylist.Add(new Stair(maze));
                    System.Threading.Thread.Sleep(20);
                } while (maze.walk(hero.xpos, hero.ypos, entitylist.Last().xpos, entitylist.Last().ypos) == "");
            }

            // 宝石: フロア2-4に大きな原石（フロア1には登場しない）、小さな宝石2個
            // 4階のみ大アンバーと大アクアマリンの2個が登場する
            switch (floor)
            {
                case 2: entitylist.Add(Gem.CreateLargeRoseQuartz(maze)); System.Threading.Thread.Sleep(20); break;
                case 3: entitylist.Add(Gem.CreateLargeSapphire(maze));   System.Threading.Thread.Sleep(20); break;
                case 4:
                    entitylist.Add(Gem.CreateLargeAmber(maze));       System.Threading.Thread.Sleep(20);
                    entitylist.Add(Gem.CreateLargeAquamarine(maze));  System.Threading.Thread.Sleep(20);
                    break;
            }
            // 小さな宝石は宝石クエストの対象である1-4階にのみ配置する
            if (floor <= 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    entitylist.Add(createRandomSmallGem(maze));
                    System.Threading.Thread.Sleep(20);
                }
            }

            // 4階に祭壇を4つ配置（東南西北、季節との対応はプレイヤーには秘密）
            if (floor == 4) placeAltars();
        }

        // 4階の四方向の端に祭壇を配置する
        private void placeAltars()
        {
            // BFS(幅優先サーチ) で Hero から到達可能な全マスを収集
            bool[,] visited = new bool[Constant.NGRID, Constant.NGRID];
            List<int[]> bfsQueue = new List<int[]> { new int[] { hero.xpos, hero.ypos } };
            visited[hero.xpos, hero.ypos] = true;
            List<int[]> reachable = new List<int[]>();
            int head = 0;
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while (head < bfsQueue.Count)
            {
                int[] cur = bfsQueue[head++];
                reachable.Add(cur);
                for (int i = 0; i < 4; i++)
                {
                    int nx = cur[0] + dx[i], ny = cur[1] + dy[i];
                    if (nx < 0 || nx >= Constant.NGRID || ny < 0 || ny >= Constant.NGRID) continue;
                    if (maze.isWall(nx, ny) || visited[nx, ny]) continue;
                    visited[nx, ny] = true;
                    bfsQueue.Add(new int[] { nx, ny });
                }
            }

            // 既存エンティティの座標を除外
            HashSet<string> occupied = new HashSet<string>(entitylist.Select(e => e.xpos + "," + e.ypos));
            List<int[]> altarCandidates = reachable.Where(c => !occupied.Contains(c[0] + "," + c[1])).ToList();
            if (altarCandidates.Count < 4) return;

            int mid = Constant.NGRID / 2;
            int[] east  = altarCandidates.OrderByDescending(c => c[0]).ThenBy(c => Math.Abs(c[1] - mid)).First();
            altarCandidates.Remove(east);
            int[] south = altarCandidates.OrderByDescending(c => c[1]).ThenBy(c => Math.Abs(c[0] - mid)).First();
            altarCandidates.Remove(south);
            int[] west  = altarCandidates.OrderBy(c => c[0]).ThenBy(c => Math.Abs(c[1] - mid)).First();
            altarCandidates.Remove(west);
            int[] north = altarCandidates.OrderBy(c => c[1]).ThenBy(c => Math.Abs(c[0] - mid)).First();

            // 方角と季節の対応（プレイヤーには秘密）: 東=春, 南=夏, 西=秋, 北=冬
            entitylist.Add(new Altar(maze, east[0],  east[1],  Gem.GemAbility.Heal));
            entitylist.Add(new Altar(maze, south[0], south[1], Gem.GemAbility.Barrier));
            entitylist.Add(new Altar(maze, west[0],  west[1],  Gem.GemAbility.TimeStop));
            entitylist.Add(new Altar(maze, north[0], north[1], Gem.GemAbility.CritBoost));
        }

        // 6階中央に南北に貫く壁の帯を作り、2箇所だけ通路（ゲート）を開ける。
        // 一方にはScyllaを配置し、もう一方にはCharybdis（渦=既存の穴システム）を仕込む。
        // 下り階段はHeroから見て奥側（壁の帯の向こう）に強制配置するため、
        // どちらかのゲートを通らなければ先に進めない。
        // carveStrait6() の結果が「両方のゲートが通れる海峡」になっているか（isMazeAcceptable() で参照）
        private bool strait6Valid;

        private void carveStrait6()
        {
            strait6Valid = false;
            int column = (hero.xpos < Constant.NGRID / 2) ? Constant.NGRID / 2 : Constant.NGRID / 2 - 1;
            for (int y = 0; y < Constant.NGRID; y++)
                maze.setWall(column, y, true);

            Random rnd = new Random();
            System.Threading.Thread.Sleep(20);
            int scyllaGateY = rnd.Next(1, Constant.NGRID - 1);
            int charybdisGateY;
            do { charybdisGateY = rnd.Next(1, Constant.NGRID - 1); } while (Math.Abs(charybdisGateY - scyllaGateY) < 4);

            // ゲートは帯のマスと、その左右のマスも床にして3マスの通り道にする
            // （左右が元の迷路の壁のままだと、手前側から入れない「飾りのゲート」になってしまう）
            foreach (int gy in new[] { scyllaGateY, charybdisGateY })
                for (int dx = -1; dx <= 1; dx++)
                    maze.setWall(column + dx, gy, false);

            int farSide  = (hero.xpos < column) ? 1 : -1; // Heroから見て下り階段側の方向
            int nearX    = column - farSide;              // ゲートの手前側（Hero側）の列
            int farX     = column + farSide;              // ゲートの向こう側の列
            int scyllaX  = farX;

            Scylla scylla = new Scylla(maze);
            scylla.xpos = scyllaX;
            scylla.ypos = scyllaGateY;
            entitylist.Add(scylla);

            maze.addPit(column, charybdisGateY); // 踏むと即座に7階へ落下する

            // Hero から両方のゲートの手前まで、穴を踏まずに行けること
            if (!isReachableAvoidingPits(hero.xpos, hero.ypos, nearX, scyllaGateY)) return;
            if (!isReachableAvoidingPits(hero.xpos, hero.ypos, nearX, charybdisGateY)) return;

            // 下り階段を奥側に強制配置する。どちらのゲートの向こう側からも（穴を踏まずに）行ける場所を選ぶ。
            // 見つからなければ配置せずに終え、isMazeAcceptable() で作り直させる（無限ループで固まるのを防ぐ）
            for (int attempt = 0; attempt < 400; attempt++)
            {
                int sx = (farSide > 0) ? rnd.Next(column + 1, Constant.NGRID) : rnd.Next(0, column);
                int sy = rnd.Next(Constant.NGRID);
                if (maze.isWall(sx, sy) || maze.isPit(sx, sy)) continue;
                if (!isReachableAvoidingPits(farX, scyllaGateY, sx, sy)) continue;
                if (!isReachableAvoidingPits(farX, charybdisGateY, sx, sy)) continue;

                Stair stair = new Stair(maze);
                stair.xpos = sx;
                stair.ypos = sy;
                entitylist.Add(stair);
                strait6Valid = true;
                return;
            }
        }

        // Companion を Hero の近くに置く。穴も敵のいるマス（6階の Scylla 等）も通らずに
        // Hero のもとへ歩いて行ける位置を選ぶ（見つからなければ最後の候補のまま）
        private void placeCompanionNearHero(Entity c)
        {
            for (int tries = 0; tries < 50; tries++)
            {
                c.changePosNear(maze, hero.xpos, hero.ypos, 3);
                if (isReachableAvoidingPits(c.xpos, c.ypos, hero.xpos, hero.ypos, true)) return;
            }
        }

        // 壁と穴を通らずに (fromX,fromY) から (toX,toY) へ行けるかを BFS(幅優先サーチ) で調べる
        // （maze.walk() は穴を床として扱うため、海峡のゲート判定には使えない）
        // avoidCreatures が true なら、生きている敵（パーティ以外の生き物）のいるマスも通れないものとする
        private bool isReachableAvoidingPits(int fromX, int fromY, int toX, int toY, bool avoidCreatures = false)
        {
            if (maze.isWall(fromX, fromY) || maze.isPit(fromX, fromY)) return false;
            bool[,] visited = new bool[Constant.NGRID, Constant.NGRID];
            if (avoidCreatures && entitylist != null)
                foreach (Entity e in entitylist)
                    if (!e.isPartyMember && e.hit > 0 && char.IsLetter(e.graph) && !(e.xpos == fromX && e.ypos == fromY))
                        visited[e.xpos, e.ypos] = true;
            var queue = new Queue<int[]>();
            queue.Enqueue(new[] { fromX, fromY });
            visited[fromX, fromY] = true;
            int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
            while (queue.Count > 0)
            {
                int[] p = queue.Dequeue();
                if (p[0] == toX && p[1] == toY) return true;
                foreach (int[] d in dirs)
                {
                    int nx = p[0] + d[0], ny = p[1] + d[1];
                    if (nx < 0 || nx >= Constant.NGRID || ny < 0 || ny >= Constant.NGRID) continue;
                    if (visited[nx, ny] || maze.isWall(nx, ny) || maze.isPit(nx, ny)) continue;
                    visited[nx, ny] = true;
                    queue.Enqueue(new[] { nx, ny });
                }
            }
            return false;
        }

        private Gem createRandomSmallGem(MazeAlgo maze)
        {
            Random rnd = new Random();
            System.Threading.Thread.Sleep(20);
            switch (rnd.Next(8))
            {
                case 0: return Gem.CreateSmallRoseQuartz(maze);
                case 1: return Gem.CreateRhodonite(maze);
                case 2: return Gem.CreateSmallSapphire(maze);
                case 3: return Gem.CreateIolite(maze);
                case 4: return Gem.CreateSmallAmber(maze);
                case 5: return Gem.CreateCitrine(maze);
                case 6: return Gem.CreateSmallAquamarine(maze);
                default: return Gem.CreateBlueTopaz(maze);
            }
        }

        public bool isSeeThru(int fromx, int fromy, int tox, int toy) {
            // 隣は、からなず見える
            if (Math.Abs(tox - fromx) <= 1 && Math.Abs(toy - fromy) <= 1)
            {
                return true;
            }

            if (fromx < tox && Math.Abs(tox - fromx) > Math.Abs(toy - fromy) /*Xの差のほうが大きい*/)
            { // 右側の視界
                for (int tmpx = fromx; tmpx < tox; tmpx++)
                {
                    int tmpy = (int)((double)(tmpx - fromx) * /*傾きΔy/Δx*/(double)(toy - fromy) / (tox - fromx) + fromy + .5);
                    if (maze.isWall(tmpx, tmpy)) return false; // 壁ならば視界探索終わり
                }
                return true;
            }
            else if (tox < fromx && Math.Abs(tox - fromx) > Math.Abs(toy - fromy)/*Xの差のほうが大きい*/)
            { // 左側の視界
                for (int tmpx = fromx; tox < tmpx; tmpx--)
                {
                    int tmpy = (int)((double)(tmpx - fromx) * /*傾きΔy/Δx*/(double)(toy - fromy) / (tox - fromx) + fromy + .5);
                    if (maze.isWall(tmpx, tmpy)) return false; // 壁ならば視界探索終わり
                }
                return true;
            }
            else if (fromy < toy)
            {
                // 下側の視界で、Yの差のほうが大きい
                for (int tmpy = fromy; tmpy < toy; tmpy++)
                {
                    int tmpx = (int)((double)(tmpy - fromy) * /*傾きΔx/Δy*/(double)(tox - fromx) / (toy - fromy) + fromx + .5);
                    if (maze.isWall(tmpx, tmpy)) return false; // 壁ならば視界探索終わり
                }
                return true;
            }
            else
            {
                // 上側の視界で、Yの差のほうが大きい
                for (int tmpy = fromy; toy < tmpy; tmpy--)
                {
                    int tmpx = (int)((double)(tmpy - fromy) * /*傾きΔx/Δy*/(double)(tox - fromx) / (toy - fromy) + fromx + .5);
                    if (maze.isWall(tmpx, tmpy)) return false; // 壁ならば視界探索終わり
                }
                return true;
            }
        }

        private void newvision() // hero の周りを見えるようにする
        {
            if (hero.amnesia)       // 忘れ薬
            {
                for (int y = 0; y < Constant.NGRID; y++)
                {
                    for (int x = 0; x < Constant.NGRID; x++)
                    {
                        if (maze.isVisible(x, y)) // 見えるところだけ描画
                            maze.Invisible(x, y);
                    }
                }
                hero.amnesia = false;
            }

            // Hero の視界のみ（Companion の視界は画面に反映しない）
            addVision(hero.xpos, hero.ypos);
        }

        private void addVision(int cx, int cy)
        {
            for (int visiondist = 1; visiondist <= Constant.VISION_DISTANCE; visiondist++)
            {
                for (int y = Math.Max(cy - visiondist, 0); y <= Math.Min(cy + visiondist, Constant.NGRID - 1); y++)
                {
                    for (int x = Math.Max(cx - visiondist, 0); x <= Math.Min(cx + visiondist, Constant.NGRID - 1); x++)
                    {
                        if (isSeeThru(cx, cy, x, y)) maze.Visible(x, y);
                    }
                }
            }
        }

        private void visionAllGrid() // 全迷路を見えるようにする
        {
            for (int y = 0; y < Constant.NGRID; y++)
            {
                for (int x = 0; x < Constant.NGRID; x++)
                {
                    maze.Visible(x, y);
                }
            }
        }

        public bool isEntitySeeable(Entity e)
        {
            // Companion 自身は常時表示（視界には依存しない）
            if (e.isCompanion && e.hit > 0 && !(e is Companion ci && ci.isInactive))
                return true;

            // それ以外は Hero の視界のみで判定
            return isSeeThru(hero.xpos, hero.ypos, e.xpos, e.ypos) &&
                   Math.Sqrt(Math.Pow(e.xpos - hero.xpos, 2) + Math.Pow(e.ypos - hero.ypos, 2)) <= Constant.VISION_DISTANCE;
        }

        // Hero が凍結・魅了で動けない間に、1回の tick() で進めるワールドのターン数の上限。
        // 上限に達したらいったん画面に戻り、次の操作で続きを進める（凍結の重ねがけ等でゲームが固まるのを防ぐ）
        private const int MaxWorldTurnsPerTick = 50;

        public void tick()
        {
            bool wasFrozen;
            int worldTurns = 0;
            do
            {                    // hero.frozen が > 0、または hero.charmed が > 0 なら繰り返す
                if (hero.polymorphed > 0) hero.polymorphed--;

                foreach (Entity e in entitylist.ToList())  // ToList() でスナップショットを作りループ中の変更を許容
                {
                    e.move(maze, entitylist, hero);
                }

                // ' ' になってしまった Entity を削る (他の Entity と重なった時にうまくいかないので)
                for (int i = entitylist.Count - 1; i >= 0; i--)
                {
                    Entity eDel = entitylist[i];
                    if (eDel.graph == ' ')
                    {
                        entitylist.Remove(eDel);
                    }
                }

                // 死体の持ち物を entitylist へ戻す
                List<Entity> dropThings = new List<Entity>();
                foreach (Entity e in entitylist)        // 持ち物を dropThings に列挙する
                {
                    if (e.graph == '%')
                    {
                        foreach (Item i in e.itemlist)
                        {
                            dropThings.Add(i.entity);
                        }
                        e.itemlist.Clear();
                        if (e.gold > 0) dropThings.Add(new Gold(maze, floor, e.gold, e.xpos, e.ypos)); // gold があれば '$' を作る
                    }
                }
                foreach (Entity e in dropThings)        // foreach(entitylist) の外で、entitylist に dropThigs を追加する
                {
                    entitylist.Add(e);
                }

                // Hero以外の穴落下チェック
                checkPitFalls();

                // 2階Dwarfが5x5壁クリアした場合、1階に穴を追加する
                processPendingPits();

                // ローズクォーツ回復・クエスト更新
                turnCounter++;
                applyGemHealing();
                updateGemQuest();
                updatePolyphemusQuest();
                updateStraitQuest();
                updateUnderworldQuest();

                // 視界を更新
                newvision();

                // Companion の魔法エフェクトを収集する
                foreach (Entity e in entitylist)
                {
                    if (!e.isCompanion) continue;
                    Companion comp = e as Companion;
                    if (comp?.pendingMagicEffects != null && comp.pendingMagicEffects.Count > 0)
                    {
                        magicEffects.AddRange(comp.pendingMagicEffects);
                        comp.pendingMagicEffects.Clear();
                    }
                }
                // 期限切れエフェクトを削除
                magicEffects.RemoveAll(ef => ef.expiry <= DateTime.Now);

                wasFrozen = hero.frozen > 0;
                if (wasFrozen)
                {
                    hero.applyFrostbite();      // Ice Jerry に凍らされている間は凍傷を負うことがある
                    hero.frozen--;
                    if (hero.frozen <= 0) hero.frozenBy = null;
                }
                // Hero が倒れたら（凍傷など）、続きのターンは進めない
            } while (hero.hit > 0 && ++worldTurns < MaxWorldTurnsPerTick && (wasFrozen || applyHeroCharm()));
        }

        // 魅了状態のHeroを魅了元へ強制的に1マス近づける。まだ魅了が残っていれば true を返す（tick()のループ継続条件）
        private bool applyHeroCharm()
        {
            if (hero.charmed <= 0) return false;
            if (hero.charmSource != null && hero.charmSource.hit > 0)
            {
                string path = maze.walk(hero.xpos, hero.ypos, hero.charmSource.xpos, hero.charmSource.ypos);
                if (path != "") hero.manualmove(path.Substring(0, 1), maze, entitylist);
            }
            hero.charmed--;
            return hero.charmed > 0;
        }

        // ローズクォーツのパッシブ回復を全エンティティに適用する（同季節は最強1個のみ有効）
        private void applyGemHealing()
        {
            foreach (Entity pm in entitylist)
            {
                if (pm.hit <= 0 || pm.hit >= pm.hitmax) continue;
                if (pm is Companion comp && comp.isInactive) continue;
                Gem bestHeal = null;
                foreach (Item item in pm.itemlist)
                    if (item.entity is Gem g && g.ability == Gem.GemAbility.Heal)
                        if (bestHeal == null || g.power > bestHeal.power) bestHeal = g;
                if (bestHeal == null) continue;
                int interval = bestHeal.power >= 2 ? 3 : 5;
                if (turnCounter % interval == 0)
                {
                    pm.hit++;
                    Console.WriteLine("{0} の体が温まった（{1}の加護）", pm.name, bestHeal.name);
                    bestHeal.revealByEffect();
                }
            }
        }

        // 4階の祭壇の嵌め込み状態を確認してクエスト進捗を更新する
        private void updateGemQuest()
        {
            if (gemQuest.IsCompleted) return;

            // 祭壇はフロア4にのみ存在するため、フロア4にいるときのみ状態を更新する
            if (floor != 4) return;

            bool wasCompleted = gemQuest.IsCompleted;
            bool rq = false, sa = false, am = false, aq = false;
            foreach (Entity e in entitylist)
            {
                if (!(e is Altar altar) || altar.embeddedGem == null) continue;
                switch (altar.season)
                {
                    case Gem.GemAbility.Heal:      rq = true; break;
                    case Gem.GemAbility.Barrier:   sa = true; break;
                    case Gem.GemAbility.TimeStop:  am = true; break;
                    case Gem.GemAbility.CritBoost: aq = true; break;
                }
            }
            gemQuest.roseQuartzEmbedded  = rq;
            gemQuest.sapphireEmbedded    = sa;
            gemQuest.amberEmbedded       = am;
            gemQuest.aquamarineEmbedded  = aq;

            if (!wasCompleted && gemQuest.IsCompleted)
            {
                Console.WriteLine("★★ 伝説の宝石４種を全て祭壇に嵌め込んだ！ 大クエスト完了！ ★★");
                CombatLog.AddGemsComplete(hero);
                hero.gold += 200;
            }
        }

        // 5階のポリュペモス討伐クエストの進捗を確認する
        private void updatePolyphemusQuest()
        {
            if (polyphemusQuest.completed) return;
            if (floor != 5) return;

            Polyphemus poly = entitylist.OfType<Polyphemus>().FirstOrDefault();
            if (poly == null) return;

            if (!polyphemusQuest.triggered && isEntitySeeable(poly))
            {
                polyphemusQuest.triggered = true;
                Console.WriteLine("一つ目の巨人ポリュペモスが目の前に現れた！");
            }

            if (polyphemusQuest.triggered && poly.hit <= 0)
            {
                polyphemusQuest.completed = true;
                Console.WriteLine("★ ポリュペモスを打ち倒した！ クエスト達成！ ★");
                // 報酬（あると便利な特別アイテム）は後続フロアの内容と合わせて後日決定する
            }
        }

        // 6階の海峡越えクエストの進捗を確認する（Scylla側の突破・Charybdisでの強制落下のどちらでも達成扱い）
        private void updateStraitQuest()
        {
            if (straitQuest.completed) return;

            if (floor == 6)
            {
                if (!straitQuest.triggered)
                {
                    straitQuest.triggered = true;
                    Console.WriteLine("危険な海峡だ…！ スキュラかカリュブディスか、慎重に選ばねば。");
                }
                return;
            }

            if (floor == 7 && straitQuest.triggered)
            {
                straitQuest.completed = true;
                Console.WriteLine("★ 危険な海峡を渡り切った！ クエスト達成！ ★");
                // 報酬は未定（後日検討）
            }
        }

        // 7階の冥府探訪クエストの進捗を確認する（Hero・Companionどちらがテイレシアスに隣接しても達成）
        private void updateUnderworldQuest()
        {
            if (underworldQuest.completed) return;
            if (floor != 7) return;

            if (!underworldQuest.triggered)
            {
                underworldQuest.triggered = true;
                Console.WriteLine("ここがキルケーの島…この先に冥府へ続く道があるという。");
                Console.WriteLine("島のどこかに、白い花をつけた黒い根の草が生えているらしい。魔女の魔法から身を守るお守りになるという…");
            }

            Teiresias sage = entitylist.OfType<Teiresias>().FirstOrDefault();
            if (sage == null || sage.hit <= 0) return;

            bool heroAdjacent = Math.Abs(hero.xpos - sage.xpos) + Math.Abs(hero.ypos - sage.ypos) <= 1;
            bool companionAdjacent = companions.Any(c => c.hit > 0 &&
                Math.Abs(c.xpos - sage.xpos) + Math.Abs(c.ypos - sage.ypos) <= 1);

            if (heroAdjacent || companionAdjacent)
            {
                underworldQuest.completed = true;
                Console.WriteLine("★ 盲目の予言者テイレシアスと出会った！ クエスト達成！ ★");
                Entity visitor = heroAdjacent ? hero : companions.First(c => c.hit > 0 &&
                    Math.Abs(c.xpos - sage.xpos) + Math.Abs(c.ypos - sage.ypos) <= 1);
                CombatLog.Add(sage, visitor, CombatKind.Meet, 0);
                hero.gold += 100;
            }
        }

        //
        // ユーザ操作から呼ばれる処理
        //

        // 移動後に Hero が穴マスにいれば落下させ true を返す
        private bool checkAndHandleHeroFall()
        {
            if (!maze.isPit(hero.xpos, hero.ypos)) return false;
            heroFall();
            return true;
        }

        // Hero を1マス動かしてワールドを1ターン進める（穴に落ちたらそちらの処理へ）
        private void heroStep(string dir)
        {
            int ox = hero.xpos, oy = hero.ypos;
            if (hero.charmed <= 0) hero.manualmove(dir, maze, entitylist);
            if (checkAndHandleHeroFall()) return;
            if (hero.xpos != ox || hero.ypos != oy) tellAltarHint();
            tick();
        }

        // 空いている祭壇の上に乗ったら、宝石のはめ込み方を教える
        private void tellAltarHint()
        {
            Altar altar = entitylist.OfType<Altar>().FirstOrDefault(a => a.xpos == hero.xpos && a.ypos == hero.ypos);
            if (altar == null || altar.embeddedGem != null) return;
            Console.WriteLine("台座には宝石をはめるくぼみがある。（持ち物の一覧で宝石を選び、u ではめ込む）");
        }

        public void ctrlUp()
        {
            heroStep("↑");
        }

        public void ctrlLeft()
        {
            heroStep("←");
        }

        public void ctrlRight()
        {
            heroStep("→");
        }

        public void ctrlDown()
        {
            heroStep("↓");
        }

        public void ctrlStairDown()
        {
            // Hero が > の上にいるか確認
            bool onStair = false;
            foreach (Entity e in entitylist)
            {
                if (e.xpos == hero.xpos && e.ypos == hero.ypos && e.graph == '>')
                { onStair = true; break; }
            }
            if (!onStair) return;

            // 現フロアを保存（> 座標を戻り先に）
            saveCurrentFloor(hero.xpos, hero.ypos);
            floor++;

            if (savedFloors.ContainsKey(floor))
            {
                // 既訪問フロアを復元（< 階段の位置に着地）
                FloorState saved = savedFloors[floor];
                maze       = saved.maze;
                entitylist = saved.entitylist;
                int hx = hero.xpos, hy = hero.ypos; // fallback
                foreach (Entity e in entitylist)
                    if (e.graph == '<') { hx = e.xpos; hy = e.ypos; break; }
                hero.xpos = hx;
                hero.ypos = hy;
                ensurePartyInEntitylist();
                reactivateCompanionsOnCurrentFloor();
                foreach (Entity c in companions)
                {
                    if (c is Companion comp && comp.isInactive) continue; // 別フロアは触らない
                    placeCompanionNearHero(c);
                }
                newvision();
            }
            else
            {
                // 未訪問フロア：新規生成
                generateNewFloor();
            }
        }

        public void ctrlStairUp()
        {
            // Hero が < の上にいるか確認
            bool onStairUp = false;
            foreach (Entity e in entitylist)
            {
                if (e.xpos == hero.xpos && e.ypos == hero.ypos && e.graph == '<')
                { onStairUp = true; break; }
            }
            if (!onStairUp) return;
            if (floor <= 1) return;

            // 現フロアを保存
            saveCurrentFloor(hero.xpos, hero.ypos);
            floor--;

            // 一つ上の階を復元（> 階段の位置に着地）
            FloorState prev = savedFloors[floor];
            maze       = prev.maze;
            entitylist = prev.entitylist;
            hero.xpos  = prev.stairX;
            hero.ypos  = prev.stairY;

            ensurePartyInEntitylist();
            reactivateCompanionsOnCurrentFloor();
            foreach (Entity c in companions)
            {
                if (c is Companion comp && comp.isInactive) continue; // 別フロアは触らない
                placeCompanionNearHero(c);
            }
            newvision();
        }

        // DEBUG専用: オデュッセイアフロアの動作確認用（不要になったら削除可）
        // 現在地を保存し、指定フロアへ直接ワープする
        public void ctrlDebugWarp(int targetFloor)
        {
            if (targetFloor == floor) return;

            saveCurrentFloor(hero.xpos, hero.ypos);

            if (savedFloors.ContainsKey(targetFloor))
            {
                restoreFloor(targetFloor, hero.xpos, hero.ypos);
            }
            else
            {
                floor = targetFloor;
                generateNewFloor();
            }
        }

        public void ctrlUse(int index)
        {
            Item item = hero.itemlist[index];

            // 宝石を選んでいて、Heroが祭壇の上にいる場合は嵌め込みを試みる
            if (item.entity is Gem gem)
            {
                Altar altar = entitylist.OfType<Altar>().FirstOrDefault(
                    a => a.xpos == hero.xpos && a.ypos == hero.ypos);
                if (altar != null)
                {
                    tryEmbedGem(gem, altar, hero, index);
                    return;
                }
            }

            item.use(hero);
            if (item.num == 0) hero.itemlist.RemoveAt(index);
        }

        // 宝石を祭壇に嵌め込む（Hero用）
        private void tryEmbedGem(Gem gem, Altar altar, Entity user, int itemIndex)
        {
            if (altar.embeddedGem != null)
            {
                Console.WriteLine("この祭壇にはすでに宝石が嵌まっている");
                CombatLog.AddEmbed(user, gem, altar, false);
                return;
            }
            if (!gem.isLarge || gem.ability == Gem.GemAbility.None || gem.ability != altar.season)
            {
                Console.WriteLine("…何も起きなかった");
                CombatLog.AddEmbed(user, gem, altar, false);
                return;
            }
            altar.embeddedGem = gem;
            user.itemlist.RemoveAt(itemIndex);
            gem.revealByEffect();
            Console.WriteLine("{0} を祭壇に嵌め込んだ！", gem.name);
            CombatLog.AddEmbed(user, gem, altar, true);
        }

        public void ctrlDrop(int index)
        {
            if (hero.itemlist[index].drop(hero) == false) return; // 数を1個減らす

            // Potion などでは、entity は1個にまとまってしまっている。
            // entity をコピーしてから、entitylist に置く必要がある。
            Entity e = hero.itemlist[index].entity.Clone();
            e.xpos = hero.xpos;
            e.ypos = hero.ypos;
            e.graph = e.graphOrig;
            entitylist.Add(e);

            if (hero.itemlist[index].num == 0) hero.itemlist.RemoveAt(index);
        }

        public void ctrlWield(int index)
        {
            hero.itemlist[index].wield(hero);

            if (hero.itemlist[index].num == 0)
            {
                hero.itemlist.RemoveAt(index);
            }
        }

        public void ctrlTakeOffWeapon()
        {
            hero.takeOffWeapon();
        }

        public void ctrlWear(int index)
        {
            hero.itemlist[index].wear(hero);

            if (hero.itemlist[index].num == 0)
            {
                hero.itemlist.RemoveAt(index);
            }
        }

        public void ctrlTakeOffArmor()
        {
            hero.takeOffArmor();
        }

        public void ctrlSave()
        {
            try
            {
                using (Stream stream = File.Create("roguelike.bin"))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(stream, new SaveData
                    {
                        maze            = maze,
                        floor           = floor,
                        hero            = hero,
                        companions      = companions,
                        entitylist      = entitylist,
                        savedFloors     = savedFloors,
                        gemQuest        = gemQuest,
                        turnCounter     = turnCounter,
                        polyphemusQuest = polyphemusQuest,
                        straitQuest     = straitQuest,
                        underworldQuest = underworldQuest,
                    });
                }
            }
            catch (System.IO.IOException ex)
            {
                Console.WriteLine("ファイルを開けませんでした");
                Console.WriteLine(ex.Message);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Console.WriteLine("ファイルの書き込み権限がありません");
                Console.WriteLine(ex.Message);
            }
        }

        public void ctrlLoad()
        {
            try{
                using (Stream stream = File.OpenRead("roguelike.bin"))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    object first = formatter.Deserialize(stream);
                    if (first is SaveData sd)
                    {
                        maze            = sd.maze;
                        floor           = sd.floor;
                        hero            = sd.hero;
                        companions      = sd.companions;
                        entitylist      = sd.entitylist;
                        savedFloors     = sd.savedFloors;
                        gemQuest        = sd.gemQuest;
                        turnCounter     = sd.turnCounter;
                        polyphemusQuest = sd.polyphemusQuest;
                        straitQuest     = sd.straitQuest;
                        underworldQuest = sd.underworldQuest;
                    }
                    else
                    {
                        loadOldFormat((MazeDist)first, formatter, stream);
                    }

                    // 他フロアのリストに残った Hero・Companion のコピー（旧形式のセーブで発生）を取り除く
                    removeStalePartyCopies();

                    // Companion の非シリアライズフィールドを再初期化
                    foreach (Entity c in companions)
                    {
                        if (c is Companion comp) comp.ensureTransients();
                    }

                    // 魔法エフェクトをリセット
                    magicEffects.Clear();

                    // 視界を現在のHero・Companion位置で更新
                    newvision();
                }
            }
            catch (System.IO.IOException ex)
            {
                Console.WriteLine("ファイルを開けませんでした");
                Console.WriteLine(ex.Message);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Console.WriteLine("ファイルの読み込み権限がありません");
                Console.WriteLine(ex.Message);
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                Console.WriteLine("セーブデータの形式が古いため読み込めません（新規ゲームを開始してください）");
                Console.WriteLine(ex.Message);
            }
        }

        // 旧形式のセーブ（各データを別々に Serialize していた）の残りを読み込む
        private void loadOldFormat(MazeDist loadedMaze, BinaryFormatter formatter, Stream stream)
        {
            maze = loadedMaze;
            floor = (int)formatter.Deserialize(stream);
            entitylist = (List<Entity>)formatter.Deserialize(stream);
            hero = entitylist[0];
            savedFloors = (Dictionary<int, FloorState>)formatter.Deserialize(stream);

            // 宝石クエスト・ターンカウンター（旧セーブには存在しない場合あり）
            try
            {
                gemQuest         = (GemQuest)formatter.Deserialize(stream);
                turnCounter      = (int)formatter.Deserialize(stream);
                polyphemusQuest  = (PolyphemusQuest)formatter.Deserialize(stream);
                straitQuest      = (StraitQuest)formatter.Deserialize(stream);
                underworldQuest  = (UnderworldQuest)formatter.Deserialize(stream);
            }
            catch
            {
                gemQuest        = new GemQuest();
                turnCounter     = 0;
                polyphemusQuest = new PolyphemusQuest();
                straitQuest     = new StraitQuest();
                underworldQuest = new UnderworldQuest();
            }

            // companions リストを entitylist から再構築。
            // 別フロアに落下中の Companion は現フロアにいないので、他フロアのリストから拾う
            companions = entitylist.Where(e => e.isCompanion).ToList();
            foreach (FloorState fs in savedFloors.Values)
                foreach (Entity e in fs.entitylist)
                    if (e is Companion fallen && fallen.isInactive && !companions.Contains(fallen) && companions.Count < 2)
                        companions.Add(fallen);
        }

        // 他フロアのリストに入っている「本物ではない」Hero・Companion（旧形式のセーブで生じたコピー）を取り除く
        private void removeStalePartyCopies()
        {
            foreach (FloorState fs in savedFloors.Values)
                fs.entitylist.RemoveAll(e => e.isPartyMember && e != hero && !companions.Contains(e));
        }

        // 保存済みフロアへ戻ったとき、本物の Hero と行動中の Companion が entitylist にいることを保証する
        private void ensurePartyInEntitylist()
        {
            if (!entitylist.Contains(hero)) entitylist.Insert(0, hero);
            foreach (Entity c in companions)
            {
                if (c is Companion comp && comp.isInactive) continue;
                if (!entitylist.Contains(c)) entitylist.Add(c);
            }
        }

    }
}
