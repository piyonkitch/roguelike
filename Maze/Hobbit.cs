/*
Copyright(c) 2015, piyonkitch<kazuo.horikawa.ko@gmail.com>
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
            Console.WriteLine("{0}：「{1}を持ってきてくれたのか！ありがとう！約束の金貨{2}枚だ」", name, QUEST_ITEM_NAME, QUEST_GOLD);
        }

        public override void move(MazeAlgo maze, List<Entity> entitylist, Entity target)        // 多様性
        {
            if (!isLive()) return;

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

            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 1 && !angry)
            {
                // クエストギバー：依頼メッセージ（Sting未入手の場合）
                if (isQuestGiver && !questCompleted)
                {
                    if (!questRequested)
                    {
                        questRequested = true;
                        Console.WriteLine("{0}：「こんにちは！私は{0}です。「{1}」というダガーを探しているんだ。見つけたら持ってきてくれないか？金貨{2}枚でどうだ？」", name, QUEST_ITEM_NAME, QUEST_GOLD);
                    }
                    else
                    {
                        Console.WriteLine("{0}：「まだ「{1}」を見つけていないのかい？」", name, QUEST_ITEM_NAME);
                    }
                    return;
                }

                // 通常の挨拶（名前あり）
                DateTime dt = DateTime.Now;
                if (dt.Hour > 7 && dt.Hour < 20)
                {
                    string message;
                    if (dt.Hour < 10) message = "おはよう";
                    else if (dt.Hour < 18) message = "こんにちは";
                    else message = "こんばんは";
                    Console.WriteLine("{0}：「{1}！私は{0}です」", name, message);
                }
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
