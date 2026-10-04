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
    class Ice : Entity
    {
        bool angry;
        Random rnd = new Random();

        public Ice(MazeAlgo maze) : base(maze)
        {
            name = "Ice Jerry";
            graph = graphOrig = 'I';
            hit = hitmax = 5;
            angry = false;
        }

        protected override void doMove(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            if (!angry) return;

            // 隣にいる生き物（Hero・Companion・ほかの敵）から、毎ターン1体をランダムに選んで凍らせようとする。
            // すでに凍っている相手は凍らせない（重ねがけすると凍結が解けず、Hero のターンが永遠に回らなくなる）
            List<Entity> near = entitylist.Where(e => e != this && e.hit > 0 &&
                (e.isPartyMember || char.IsLetter(e.graph)) && !e.isNonHostile && e.frozen <= 0 &&
                Math.Abs(e.xpos - xpos) + Math.Abs(e.ypos - ypos) == 1).ToList();
            if (near.Count == 0) return;
            Entity victim = near[rnd.Next(near.Count)];

            if (rnd.Next(100) < 50)
            {
                victim.frozen = rnd.Next(4) + 4;    // 4〜7ターン
                victim.frozenBy = this;             // 凍っている間、凍傷を負うことがある
                Console.WriteLine("{0} は凍りついた！", victim.name);
                CombatLog.Add(this, victim, CombatKind.Freeze, 0);
            }
        }

        public override void beat(Entity attacker)
        {
            if (attacker.graph == '@')
            {
                angry = true;
            }
        }
    }
}
