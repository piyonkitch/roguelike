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

using System.Runtime.Serialization;
using System.Xml;

namespace Maze
{
    [Serializable]
    class Hobbit : Entity
    {
        bool angry;
        Random rnd = new Random();

        public bool isQuestGiver { get; set; }
        public bool questCompleted { get; set; }
        private bool questRequested;

        // 宝石クエストの昔話。Bilbo 以外の Hobbit が1人1つずつ語る（loreIndex で選ぶ。数が多いときは繰り返す）。
        // 色と方角の対応は謎かけ: 日の昇る方＝東・花咲く季節＝春の薔薇色、日の最も高い方＝南・深い海の色＝夏の青、
        // 日の沈む方＝西・実りの季節の蜜の色＝秋の琥珀色、日の届かぬ方＝北・凍った水の色＝冬の水色（Logic の祭壇の配置と同じ）
        public int loreIndex { get; set; }
        private static readonly string[] Lore = {
            "昔話を知っているかい？地下4階の王の間には季節の女神の玉座があって、四方の台座に大きな宝石が一つずつはめられていたんだ。今は盗まれて台座しか残っていないが、宝石を台座に戻した者には女神の褒美があるそうだ",
            "祖母の歌ではこうさ。『日の昇る方には、花咲く季節の石を。日の最も高い方には、深い海の色の石を』",
            "歌の続きはこうだよ。『日の沈む方には、実りの季節の蜜の色の石を。日の届かぬ方には、凍った水の色の石を』",
        };
        private const string BilboGemTalk = "宝石の話を知っているかい？玉座の大きな宝石は、2階から4階に散らばっているらしい。似た色の偽物も出回っているが、本物は不思議な力を持っているそうだ";

        // 前のターンに Hero の隣にいたか（隣に来た最初のターンだけ話し、隣にいる間は繰り返さないため）
        private bool wasAdjacent;
        private const int QUEST_GOLD = 30;
        private const string QUEST_ITEM_NAME = "Sting";

        public Hobbit(MazeAlgo maze) : base(maze)
        {
            name = "Hobbit";
            graph = graphOrig = 'h';
            angry = false;
        }

        // 隣接パーティメンバーがStingを持っているか確認し、持っていれば Item を返す
        private Item findStingOn(Entity pm)
        {
            foreach (Item i in pm.itemlist)
            {
                if (i.entity is Weapon w && w.engraveName == QUEST_ITEM_NAME) return i;
            }
            return null;
        }

        // クエスト完了処理
        private void completeQuest(Item stingItem, Entity carrier, Entity hero)
        {
            carrier.itemlist.Remove(stingItem);
            if (carrier.weapon == stingItem.entity) carrier.weapon = null;
            hero.gold += QUEST_GOLD;
            questCompleted = true;
            wasAdjacent = false;   // お礼の次のターンに、続けて宝石のありかを話す
            Console.WriteLine("{0}：「{1}を持ってきてくれたのか！ありがとう！約束の金貨{2}枚だ」", name, QUEST_ITEM_NAME, QUEST_GOLD);
            CombatLog.AddGive(carrier, this, stingItem.entity, QUEST_GOLD);
        }

        protected override void doMove(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            // クエストギバーのSting受け取りチェック（Heroの位置によらず毎ターン実行）
            if (isQuestGiver && !questCompleted)
            {
                // Hero が隣接してSting を持っているか
                if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 1)
                {
                    Item stingItem = findStingOn(target);
                    if (stingItem != null) { completeQuest(stingItem, target, target); return; }
                }
                // 隣接Companion が Sting を持っているか
                foreach (Entity e in entitylist)
                {
                    if (!e.isCompanion) continue;
                    if (Math.Abs(e.xpos - xpos) + Math.Abs(e.ypos - ypos) > 1) continue;
                    Item stingItem = findStingOn(e);
                    if (stingItem != null) { completeQuest(stingItem, e, target); return; }
                }
            }

            bool adjacent = Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 1;
            bool justArrived = adjacent && !wasAdjacent;   // Hero が隣に来た最初のターン
            wasAdjacent = adjacent;

            if (adjacent && !angry)
            {
                // 話すのは Hero が隣に来た最初のターンだけ（隣にいる間は黙ってその場にいる）
                if (!justArrived) return;

                // Bilbo: Sting を受け取ったあとは、宝石のありかを教える
                if (isQuestGiver && questCompleted)
                {
                    Console.WriteLine("{0}：「{1}」", name, BilboGemTalk);
                    CombatLog.AddTalk(this, target, BilboGemTalk);
                    return;
                }

                // クエストギバー：依頼メッセージ（Sting未入手の場合）
                if (isQuestGiver && !questCompleted)
                {
                    if (!questRequested)
                    {
                        questRequested = true;
                        Console.WriteLine("{0}：「こんにちは！私は{0}です。「{1}」というダガーを探しているんだ。見つけたら持ってきてくれないか？金貨{2}枚でどうだ？」", name, QUEST_ITEM_NAME, QUEST_GOLD);
                        CombatLog.AddTalk(this, target, string.Format("私は{0}。「{1}」というダガーを探しているんだ。持ってきてくれたら金貨{2}枚！", name, QUEST_ITEM_NAME, QUEST_GOLD));
                    }
                    else
                    {
                        Console.WriteLine("{0}：「まだ「{1}」を見つけていないのかい？」", name, QUEST_ITEM_NAME);
                        CombatLog.AddTalk(this, target, string.Format("まだ「{0}」を見つけていないのかい？", QUEST_ITEM_NAME));
                    }
                    return;
                }

                // 名乗ってから、宝石クエストの昔話を1つ語る
                string lore = string.Format("私は{0}。{1}", name, Lore[loreIndex % Lore.Length]);
                Console.WriteLine("{0}：「{1}」", name, lore);
                CombatLog.AddTalk(this, target, lore);
                return;
            }

            string dir;
            if (angry && Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 3)
            {
                dir = maze.walk(xpos, ypos, target.xpos, target.ypos);
                dir = (dir == "") ? "？" : maze.walk(xpos, ypos, target.xpos, target.ypos).Substring(0, 1); // 最短経路の最初の方向を得る
            }
            else
            {
                dir = "←→↑↓"[rnd.Next(4)].ToString();
            }
            
            // 仲間(h)は攻撃しない。怒ってなければ人(@)も攻撃しない。
            switch (dir)
            {
                case "←":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos - 1 && e.ypos == ypos && 
                            (e.graph == 'h' || (e.graph == '@' && !angry))) return;
                    }
                    break;
                case "→":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos + 1 && e.ypos == ypos && 
                            (e.graph == 'h' || (e.graph == '@' && !angry))) return;
                    }
                    break;
                case "↑":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos && e.ypos == ypos - 1 && 
                            (e.graph == 'h' || (e.graph == '@' && !angry))) return;
                    }
                    break;
                case "↓":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos && e.ypos == ypos + 1 && 
                            (e.graph == 'h' || (e.graph == '@' && !angry))) return;
                    }
                    break;
            }
            base.manualmove(dir, maze, entitylist);
        }

        public override void beat(Entity attacker)
        {
            if (isQuestGiver) return;   // クエストギバーは怒らない
            if (attacker.graph == '@')
            {
                angry = true;
            }
        }
    }
}
