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

namespace Maze
{
    [Serializable]
    class Companion : Entity
    {
        // この距離以上離れたらヒーローを追いかける（マンハッタン距離）
        private const int FOLLOW_DISTANCE = 2;

        public Companion(MazeAlgo maze) : base(maze)
        {
            graph = graphOrig = '@';
            name = "Companion";
            isCompanion = true;
            isPartyMember = true;
            hit = hitmax = 5;
            strength = strengthmax = 2;
            toughness = 0;
        }

        public override void move(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            if (!isLive()) return;

            // HP が最大の 1/3 より多ければ、隣接する敵を攻撃する
            if (hit * 3 > hitmax)
            {
                foreach (Entity e in entitylist)
                {
                    if (e.isPartyMember) continue;          // パーティメンバーは攻撃しない
                    if (!char.IsLetter(e.graph)) continue;  // アイテム・死体は除く
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

            // 隣接する敵なし、または弱っている → Hero 追従
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

        private void autoEquip(List<Entity> entitylist)
        {
            // 武器の自動装備・弱い武器のdrop
            for (int i = itemlist.Count - 1; i >= 0; i--)
            {
                Entity e = itemlist[i].entity;
                if (!e.isWieldable()) continue;
                if (e == weapon) continue;                      // 装備中はスキップ

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
