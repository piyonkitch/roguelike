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
    // 海峡の片側に固定配置される多頭の怪物。移動せず、隣接する敵を複数回攻撃する
    [Serializable]
    class Scylla : Entity
    {
        public Scylla(MazeAlgo maze) : base(maze)
        {
            name = "Scylla";
            graph = graphOrig = 'Y';
            hit = hitmax = 14;
            strength = strengthmax = 5;
            toughness = 2;
        }

        protected override void doMove(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            // その場から動かず、隣接する頭の数（最大2）だけパーティメンバーを攻撃する
            string[] dirs = { "←", "→", "↑", "↓" };
            int[] dxs = { -1, 1, 0, 0 };
            int[] dys = { 0, 0, -1, 1 };
            int attacks = 0;

            for (int i = 0; i < 4 && attacks < 2; i++)
            {
                int nx = xpos + dxs[i], ny = ypos + dys[i];
                foreach (Entity e in entitylist)
                {
                    if (e.isPartyMember && e.hit > 0 && e.xpos == nx && e.ypos == ny)
                    {
                        manualmove(dirs[i], maze, entitylist);
                        attacks++;
                        break;
                    }
                }
            }
        }
    }
}
