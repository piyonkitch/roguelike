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

namespace Maze
{
    [Serializable]
    class Companion : Entity
    {
        // この距離以上離れたらヒーローを追いかける（マンハッタン距離）
        private const int FOLLOW_DISTANCE = 2;
        // この距離を超えたら他の行動をキャンセルして即座に追従する
        private const int MAX_HERO_DISTANCE = 8;

        public int mp { get; set; }
        public int mpmax { get; set; }

        // 別フロアに落下中：true の間はAI・表示・戦闘すべて停止
        public bool isInactive { get; set; } = false;

        // 魔法エフェクト（非シリアライズ：描画用一時データ）
        [NonSerialized]
        public List<MagicEffect> pendingMagicEffects;

        [NonSerialized]
        private Random magicRnd;

        // 宝石ごとに「失敗した祭壇座標」を記憶する。Amnesia で消去される。
        [NonSerialized]
        private Dictionary<Gem, HashSet<string>> failedAltars;

        private struct MagicDir
        {
            public int dx, dy;
            public char sym;
            public MagicDir(int dx, int dy, char sym) { this.dx = dx; this.dy = dy; this.sym = sym; }
        }

        // 8方向: 左右は-, 上下は|, 右上/左下は/, 左上/右下は\
        private static readonly MagicDir[] MAGIC_DIRS = {
            new MagicDir( 1,  0, '-'),  // 右
            new MagicDir(-1,  0, '-'),  // 左
            new MagicDir( 0,  1, '|'),  // 下
            new MagicDir( 0, -1, '|'),  // 上
            new MagicDir( 1, -1, '/'),  // 右上
            new MagicDir(-1,  1, '/'),  // 左下
            new MagicDir( 1,  1, '\\'), // 右下
            new MagicDir(-1, -1, '\\'), // 左上
        };

        public Companion(MazeAlgo maze) : base(maze)
        {
            graph = graphOrig = '@';
            name = "Companion";
            isCompanion = true;
            isPartyMember = true;
            hit = hitmax = 5;
            mp = mpmax = 1;
            strength = strengthmax = 2;
            toughness = 0;
            pendingMagicEffects = new List<MagicEffect>();
            magicRnd = new Random(Guid.NewGuid().GetHashCode());
        }

        internal void ensureTransients()
        {
            if (pendingMagicEffects == null) pendingMagicEffects = new List<MagicEffect>();
            if (magicRnd == null) magicRnd = new Random(Guid.NewGuid().GetHashCode());
            if (failedAltars == null) failedAltars = new Dictionary<Gem, HashSet<string>>();
        }

        // Amnesia Potion で祭壇の失敗記憶を消去する
        public void clearFailedAltars()
        {
            ensureTransients();
            failedAltars.Clear();
            Console.WriteLine("{0} は祭壇のことを忘れてしまった！", name);
        }

        // 持っている大きな宝石と、それを運ぶ先の祭壇（最も近い空き祭壇で失敗記憶なし）を返す
        // 対象がなければ altar は null を返す
        private Altar findAltarTarget(List<Entity> entitylist, out Gem gemToDeliver)
        {
            gemToDeliver = null;
            ensureTransients();
            foreach (Item item in itemlist)
            {
                if (!(item.entity is Gem gem)) continue;
                if (!gem.isLarge || gem.ability == Gem.GemAbility.None) continue;

                Altar nearest = null;
                int nearestDist = int.MaxValue;
                foreach (Entity e in entitylist)
                {
                    if (!(e is Altar altar)) continue;
                    if (altar.embeddedGem != null) continue;
                    string key = altar.xpos + "," + altar.ypos;
                    HashSet<string> failed;
                    if (failedAltars.TryGetValue(gem, out failed) && failed.Contains(key)) continue;
                    int dist = Math.Abs(altar.xpos - xpos) + Math.Abs(altar.ypos - ypos);
                    if (dist < nearestDist) { nearestDist = dist; nearest = altar; }
                }
                if (nearest != null) { gemToDeliver = gem; return nearest; }
            }
            return null;
        }

        // クエストアイテム（engraveName付き武器）を持っているか確認
        private Item findQuestItem()
        {
            foreach (Item i in itemlist)
            {
                if (i.entity is Weapon w && w.engraveName != null) return i;
            }
            return null;
        }

        protected override void doMove(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            if (isInactive) return;   // 別フロアに落下中は行動しない
            ensureTransients();

            // MP自然回復（20%）
            if (magicRnd.Next(100) < 20 && mp < mpmax) mp++;

            // Heroから離れすぎている場合は即座に追従
            int heroManhattan = Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos);
            if (heroManhattan > MAX_HERO_DISTANCE)
            {
                string routeToHero = maze.walk(xpos, ypos, target.xpos, target.ypos);
                if (routeToHero != "") base.manualmove(routeToHero.Substring(0, 1), maze, entitylist);
                autoEquip(entitylist);
                return;
            }

            // クエストアイテム配達: Stingを持っていたらBilboへ届ける
            Item questItem = findQuestItem();
            if (questItem != null)
            {
                Hobbit questGiver = null;
                foreach (Entity e in entitylist)
                {
                    if (e is Hobbit h && h.isQuestGiver && !h.questCompleted && h.hit > 0)
                    {
                        questGiver = h;
                        break;
                    }
                }
                if (questGiver != null)
                {
                    int distToGiver = Math.Abs(questGiver.xpos - xpos) + Math.Abs(questGiver.ypos - ypos);
                    if (distToGiver > 1)
                    {
                        string routeToGiver = maze.walk(xpos, ypos, questGiver.xpos, questGiver.ypos);
                        if (routeToGiver != "")
                        {
                            base.manualmove(routeToGiver.Substring(0, 1), maze, entitylist);
                            autoEquip(entitylist);
                            return;
                        }
                    }
                    else
                    {
                        // 隣接: Hobbitのmove()がStingを受け取る。ここでは待機
                        autoEquip(entitylist);
                        return;
                    }
                }
            }

            // 祭壇への宝石配達（大きな原石を持っていて、entitylistに空き祭壇がある場合）
            Gem gemToDeliver;
            Altar altarTarget = findAltarTarget(entitylist, out gemToDeliver);
            if (altarTarget != null)
            {
                if (xpos == altarTarget.xpos && ypos == altarTarget.ypos)
                {
                    // 祭壇の上にいる: 嵌め込みを試みる
                    string altarKey = altarTarget.xpos + "," + altarTarget.ypos;
                    if (altarTarget.season == gemToDeliver.ability && altarTarget.embeddedGem == null)
                    {
                        altarTarget.embeddedGem = gemToDeliver;
                        for (int i = itemlist.Count - 1; i >= 0; i--)
                            if (itemlist[i].entity == gemToDeliver) { itemlist.RemoveAt(i); break; }
                        gemToDeliver.revealByEffect();
                        Console.WriteLine("{0} は {1} を祭壇に嵌め込んだ！", name, gemToDeliver.name);
                    }
                    else
                    {
                        // 季節が合わない: 失敗記憶に記録
                        if (!failedAltars.ContainsKey(gemToDeliver))
                            failedAltars[gemToDeliver] = new HashSet<string>();
                        failedAltars[gemToDeliver].Add(altarKey);
                    }
                    autoEquip(entitylist);
                    return;
                }
                else
                {
                    string routeToAltar = maze.walk(xpos, ypos, altarTarget.xpos, altarTarget.ypos);
                    if (routeToAltar != "")
                    {
                        base.manualmove(routeToAltar.Substring(0, 1), maze, entitylist);
                        autoEquip(entitylist);
                        return;
                    }
                }
            }

            if (hit * 3 > hitmax)
            {
                // 魔法攻撃を試みる（MP > 0 のときのみ）
                if (mp > 0)
                {
                    foreach (Entity e in entitylist)
                    {
                        if (e.isPartyMember) continue;
                        if (!char.IsLetter(e.graph)) continue;
                        if (e.hit <= 0) continue;
                        if (e is Hobbit) continue;  // Hobbitは攻撃しない
                        if (e is Dwarf) continue;   // Dwarfは攻撃しない

                        foreach (MagicDir dir in MAGIC_DIRS)
                        {
                            if (!isEnemyInMagicRangeFrom(e, xpos, ypos, dir.dx, dir.dy, maze)) continue;
                            if (hasFriendlyFireFrom(xpos, ypos, dir.dx, dir.dy, maze, entitylist)) continue;

                            castMagic(dir.dx, dir.dy, dir.sym, maze, entitylist);
                            autoEquip(entitylist);
                            return;
                        }
                    }
                }

                // 魔法のために移動して射線を確保する（MP > 0 のときのみ）
                if (mp > 0)
                foreach (Entity e in entitylist)
                {
                    if (e.isPartyMember) continue;
                    if (!char.IsLetter(e.graph)) continue;
                    if (e.hit <= 0) continue;
                    if (e is Hobbit) continue;  // Hobbitは攻撃しない
                    if (e is Dwarf) continue;   // Dwarfは攻撃しない

                    string[] moves4 = { "←", "→", "↑", "↓" };
                    foreach (string mv in moves4)
                    {
                        int nx = xpos + (mv == "→" ? 1 : mv == "←" ? -1 : 0);
                        int ny = ypos + (mv == "↓" ? 1 : mv == "↑" ? -1 : 0);
                        if (nx < 0 || nx >= Constant.NGRID || ny < 0 || ny >= Constant.NGRID) continue;
                        if (maze.isWall(nx, ny)) continue;

                        foreach (MagicDir dir in MAGIC_DIRS)
                        {
                            if (!isEnemyInMagicRangeFrom(e, nx, ny, dir.dx, dir.dy, maze)) continue;
                            if (hasFriendlyFireFrom(nx, ny, dir.dx, dir.dy, maze, entitylist)) continue;

                            // この方向に移動すれば射線が開く（実際に移動できた場合のみ return）
                            int prevX = xpos, prevY = ypos;
                            base.manualmove(mv, maze, entitylist);
                            if (xpos != prevX || ypos != prevY)
                            {
                                autoEquip(entitylist);
                                return;
                            }
                        }
                    }
                }

                // 近接攻撃フォールバック: 装備武器で隣接する敵を攻撃
                foreach (Entity e in entitylist)
                {
                    if (e.isPartyMember) continue;
                    if (!char.IsLetter(e.graph)) continue;
                    if (e is Hobbit) continue;  // Hobbitは攻撃しない
                    if (e is Dwarf) continue;   // Dwarfは攻撃しない
                    bool adjacent = (Math.Abs(e.xpos - xpos) == 1 && e.ypos == ypos) ||
                                    (e.xpos == xpos && Math.Abs(e.ypos - ypos) == 1);
                    if (!adjacent) continue;

                    string dir = "";
                    if      (e.xpos == xpos - 1) dir = "←";
                    else if (e.xpos == xpos + 1) dir = "→";
                    else if (e.ypos == ypos - 1) dir = "↑";
                    else if (e.ypos == ypos + 1) dir = "↓";

                    base.manualmove(dir, maze, entitylist);
                    autoEquip(entitylist);
                    return;
                }
            }
            else
            {
                // HP が低い → 近くの敵から逃走（Hero から離れすぎない）
                Entity nearest = getNearestEnemy(entitylist);
                if (nearest != null)
                {
                    string fleeMove = getBestFleeMove(nearest, target, maze);
                    if (fleeMove != "")
                    {
                        base.manualmove(fleeMove, maze, entitylist);
                        autoEquip(entitylist);
                        return;
                    }
                }
            }

            // 近くにアイテムがあれば拾いに行く（Hero から離れすぎない範囲で）
            Entity nearItem = findNearestItem(entitylist, target, maze);
            if (nearItem != null)
            {
                string itemRoute = maze.walk(xpos, ypos, nearItem.xpos, nearItem.ypos);
                if (itemRoute != "")
                {
                    base.manualmove(itemRoute.Substring(0, 1), maze, entitylist);
                    autoEquip(entitylist);
                    return;
                }
            }

            // Hero 追従
            int dist = Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos);
            if (dist <= FOLLOW_DISTANCE)
            {
                autoEquip(entitylist);
                return;
            }

            string route = maze.walk(xpos, ypos, target.xpos, target.ypos);
            if (route == "") return;

            base.manualmove(route.Substring(0, 1), maze, entitylist);
            autoEquip(entitylist);
        }

        // 近くの拾えるアイテムを探す（Heroから離れすぎない範囲）
        private const int MAX_ITEM_SEEK_DIST = 8;   // Heroからこの距離まで離れて良い
        private const int ITEM_SEARCH_RANGE  = 6;   // Companionからこの距離以内のアイテムを探す

        private Entity findNearestItem(List<Entity> entitylist, Entity hero, MazeAlgo maze)
        {
            Entity nearest = null;
            int bestDist = int.MaxValue;
            foreach (Entity e in entitylist)
            {
                // 床に落ちているアイテムのみ対象（宝石'*'も含む）
                if (e.graph != '$' && e.graph != '%' &&
                    e.graph != '!' && e.graph != '?' &&
                    e.graph != ')' && e.graph != '[' &&
                    e.graph != '*') continue;
                // % は HP が満タンなら不要
                if (e.graph == '%' && hit >= hitmax) continue;
                int distToItem = Math.Abs(e.xpos - xpos) + Math.Abs(e.ypos - ypos);
                if (distToItem == 0) continue;                  // すでに同じマスにいる
                if (distToItem > ITEM_SEARCH_RANGE) continue;  // 遠すぎる
                if (!maze.isVisible(e.xpos, e.ypos)) continue; // 見えていない
                // アイテム地点がHeroから離れすぎるなら除外
                int heroDistAtItem = Math.Abs(e.xpos - hero.xpos) + Math.Abs(e.ypos - hero.ypos);
                if (heroDistAtItem > MAX_ITEM_SEEK_DIST) continue;
                if (distToItem < bestDist) { bestDist = distToItem; nearest = e; }
            }
            return nearest;
        }

        // 視界内で最も近い敵を返す
        private Entity getNearestEnemy(List<Entity> entitylist)
        {
            Entity nearest = null;
            int minDist = int.MaxValue;
            foreach (Entity e in entitylist)
            {
                if (e.isPartyMember) continue;
                if (!char.IsLetter(e.graph)) continue;
                if (e.hit <= 0) continue;
                if (e is Hobbit) continue;  // Hobbitは敵扱いしない
                if (e is Dwarf) continue;   // Dwarfは敵扱いしない
                int d = Math.Abs(e.xpos - xpos) + Math.Abs(e.ypos - ypos);
                if (d <= Constant.VISION_DISTANCE && d < minDist) { minDist = d; nearest = e; }
            }
            return nearest;
        }

        // 敵から遠ざかり、かつHeroから離れすぎない方向を返す（なければ""）
        private string getBestFleeMove(Entity enemy, Entity hero, MazeAlgo maze)
        {
            const int MAX_DIST_FROM_HERO = 6;
            string bestMove = "";
            int bestScore = int.MinValue;
            string[] moves = { "←", "→", "↑", "↓" };
            foreach (string mv in moves)
            {
                int nx = xpos + (mv == "→" ? 1 : mv == "←" ? -1 : 0);
                int ny = ypos + (mv == "↓" ? 1 : mv == "↑" ? -1 : 0);
                if (nx < 0 || nx >= Constant.NGRID || ny < 0 || ny >= Constant.NGRID) continue;
                if (maze.isWall(nx, ny)) continue;
                // Heroから遠くなりすぎる方向は除外
                int heroDistAfter = Math.Abs(nx - hero.xpos) + Math.Abs(ny - hero.ypos);
                if (heroDistAfter > MAX_DIST_FROM_HERO) continue;
                // 敵から遠ざかるほど高スコア
                int enemyDistAfter = Math.Abs(nx - enemy.xpos) + Math.Abs(ny - enemy.ypos);
                if (enemyDistAfter > bestScore) { bestScore = enemyDistAfter; bestMove = mv; }
            }
            return bestMove;
        }

        // (fromX, fromY) から (dx,dy) 方向 1〜2マスにenemyがいるか（壁で止まる）
        private bool isEnemyInMagicRangeFrom(Entity enemy, int fromX, int fromY, int dx, int dy, MazeAlgo maze)
        {
            for (int step = 1; step <= 2; step++)
            {
                int tx = fromX + step * dx;
                int ty = fromY + step * dy;
                if (tx < 0 || tx >= Constant.NGRID || ty < 0 || ty >= Constant.NGRID) return false;
                if (maze.isWall(tx, ty)) return false;
                if (enemy.xpos == tx && enemy.ypos == ty) return true;
            }
            return false;
        }

        // (fromX, fromY) から (dx,dy) 方向の射線上にパーティメンバーがいるか（壁で止まる）
        private bool hasFriendlyFireFrom(int fromX, int fromY, int dx, int dy, MazeAlgo maze, List<Entity> entitylist)
        {
            for (int step = 1; step <= 2; step++)
            {
                int tx = fromX + step * dx;
                int ty = fromY + step * dy;
                if (tx < 0 || tx >= Constant.NGRID || ty < 0 || ty >= Constant.NGRID) return false;
                if (maze.isWall(tx, ty)) return false;
                foreach (Entity f in entitylist)
                {
                    if (!f.isPartyMember) continue;
                    if (f == this) continue;
                    if (f.xpos == tx && f.ypos == ty) return true;
                }
            }
            return false;
        }

        private void castMagic(int dx, int dy, char sym, MazeAlgo maze, List<Entity> entitylist)
        {
            mp--;   // MP消費
            Console.WriteLine("{0} は魔法を放った！ (MP {1}/{2})", name, mp, mpmax);

            for (int step = 1; step <= 2; step++)
            {
                int tx = xpos + step * dx;
                int ty = ypos + step * dy;
                if (tx < 0 || tx >= Constant.NGRID || ty < 0 || ty >= Constant.NGRID) break;
                if (maze.isWall(tx, ty)) break;

                foreach (Entity e in entitylist.ToList())
                {
                    if (e == this) continue;
                    if (e.xpos != tx || e.ypos != ty) continue;
                    if (!char.IsLetter(e.graph)) continue;
                    if (e is Dwarf) continue;   // Dwarfは攻撃しない

                    int damage = magicRnd.Next(1, 3); // 1〜2ダメージ
                    e.hit -= damage;
                    Console.WriteLine("{0} は {1} に魔法で {2} のダメージを与えた", name, e.name, damage);

                    if (e.hit <= 0)
                    {
                        e.graph = '%';
                        Console.WriteLine("{0} は {1} を魔法で倒した", name, e.name);
                        foreach (Item i in e.itemlist)
                        {
                            i.entity.xpos = e.xpos;
                            i.entity.ypos = e.ypos;
                            i.entity.graph = i.entity.graphOrig;
                        }
                        experience++;
                        if (experience >= 5)
                        {
                            hitmax += magicRnd.Next(3) + 1;
                            mpmax += magicRnd.Next(3) + 1;
                            experience = 0;
                            Console.WriteLine("{0} はレベルアップした！ MPmax={1}", name, mpmax);
                        }
                    }
                }
            }

            pendingMagicEffects.Add(new MagicEffect
            {
                fromX  = xpos,
                fromY  = ypos,
                dx     = dx,
                dy     = dy,
                symbol = sym,
                expiry = DateTime.Now.AddSeconds(1)
            });
        }

        private void autoEquip(List<Entity> entitylist)
        {
            // 武器の自動装備・弱い武器のdrop
            for (int i = itemlist.Count - 1; i >= 0; i--)
            {
                Entity e = itemlist[i].entity;
                if (!e.isWieldable()) continue;
                if (e == weapon) continue;                      // 装備中はスキップ
                if (e is Weapon qw && qw.engraveName != null) continue;  // クエストアイテムは装備しない

                Weapon newWeapon = (Weapon)e;
                if (weapon == null || newWeapon.sharpness > ((Weapon)weapon).sharpness)
                {
                    // 古い武器をunequip & drop
                    if (weapon != null)
                    {
                        for (int j = itemlist.Count - 1; j >= 0; j--)
                        {
                            if (itemlist[j].entity == weapon)
                            {
                                dropToFloor(weapon, entitylist);
                                itemlist.RemoveAt(j);
                                break;
                            }
                        }
                        weapon = null;
                    }
                    newWeapon.wield(this);
                    Console.WriteLine("{0} は {1} を装備した", name, newWeapon.name);
                }
                else
                {
                    // 弱い武器はdrop
                    dropToFloor(e, entitylist);
                    itemlist.RemoveAt(i);
                    Console.WriteLine("{0} は弱い {1} を捨てた", name, e.name);
                }
            }

            // 鎧の自動装備・弱い鎧のdrop
            for (int i = itemlist.Count - 1; i >= 0; i--)
            {
                Entity e = itemlist[i].entity;
                if (!e.isWearable()) continue;
                if (e == armor) continue;                       // 装備中はスキップ

                Armor newArmor = (Armor)e;
                if (armor == null || newArmor.hardness > ((Armor)armor).hardness)
                {
                    // 古い鎧をunequip & drop
                    if (armor != null)
                    {
                        for (int j = itemlist.Count - 1; j >= 0; j--)
                        {
                            if (itemlist[j].entity == armor)
                            {
                                dropToFloor(armor, entitylist);
                                itemlist.RemoveAt(j);
                                break;
                            }
                        }
                        armor = null;
                    }
                    newArmor.wear(this);
                    Console.WriteLine("{0} は {1} を着用した", name, newArmor.name);
                }
                else
                {
                    // 弱い鎧はdrop
                    dropToFloor(e, entitylist);
                    itemlist.RemoveAt(i);
                    Console.WriteLine("{0} は弱い {1} を捨てた", name, e.name);
                }
            }

            // ポーション・スクロール: 識別済みで害があればdrop、それ以外は使用
            for (int i = itemlist.Count - 1; i >= 0; i--)
            {
                if (!itemlist[i].entity.isUsable()) continue;
                string itemName = itemlist[i].name;
                if (itemlist[i].entity.isHarmful())
                {
                    dropToFloor(itemlist[i].entity, entitylist);
                    itemlist.RemoveAt(i);
                    Console.WriteLine("{0} は害のある {1} を捨てた", name, itemName);
                }
                else
                {
                    itemlist[i].use(this);
                    if (itemlist[i].num == 0) itemlist.RemoveAt(i);
                }
            }
        }

        private void dropToFloor(Entity item, List<Entity> entitylist)
        {
            Entity dropped = item.Clone();
            dropped.xpos = xpos;
            dropped.ypos = ypos;
            dropped.graph = dropped.graphOrig;
            entitylist.Add(dropped);
        }
    }
}
