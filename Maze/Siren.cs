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
    class Siren : Entity
    {
        Random rnd = new Random();
        private const int SONG_RANGE = 5;

        public Siren(MazeAlgo maze) : base(maze)
        {
            name = "Siren";
            graph = graphOrig = 'S';
            hit = hitmax = 4;
            strength = strengthmax = 1;
            toughness = 0;
        }

        protected override void doMove(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            // 待ち伏せ型: 隣接するパーティメンバーがいれば近接攻撃を試みる
            string[] dirs = { "←", "→", "↑", "↓" };
            int[] dxs = { -1, 1, 0, 0 };
            int[] dys = { 0, 0, -1, 1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = xpos + dxs[i], ny = ypos + dys[i];
                foreach (Entity e in entitylist)
                {
                    if (e.isPartyMember && e.hit > 0 && e.xpos == nx && e.ypos == ny)
                    {
                        manualmove(dirs[i], maze, entitylist);
                        return;
                    }
                }
            }

            // 魅了の歌: 射程内・射線が通るパーティメンバーを1体、40%の確率で魅了する
            foreach (Entity e in entitylist)
            {
                if (!e.isPartyMember || e.hit <= 0 || e.charmed > 0) continue;
                if (Math.Abs(e.xpos - xpos) + Math.Abs(e.ypos - ypos) > SONG_RANGE) continue;
                if (!isSeeThru(maze, xpos, ypos, e.xpos, e.ypos)) continue;

                if (rnd.Next(100) < 40)
                {
                    e.charmed = rnd.Next(3) + 3; // 3〜5ターン
                    e.charmSource = this;
                    Console.WriteLine("セイレーンの歌が {0} を魅了した！", e.name);
                }
                return; // 1ターンに1体だけ狙う
            }
        }

        // Logic.isSeeThru と同じアルゴリズム（Entity単体からmazeのみで判定できるよう複製）
        private static bool isSeeThru(MazeAlgo maze, int fromx, int fromy, int tox, int toy)
        {
            if (Math.Abs(tox - fromx) <= 1 && Math.Abs(toy - fromy) <= 1) return true;

            if (fromx < tox && Math.Abs(tox - fromx) > Math.Abs(toy - fromy))
            {
                for (int tmpx = fromx; tmpx < tox; tmpx++)
                {
                    int tmpy = (int)((double)(tmpx - fromx) * (double)(toy - fromy) / (tox - fromx) + fromy + .5);
                    if (maze.isWall(tmpx, tmpy)) return false;
                }
                return true;
            }
            else if (tox < fromx && Math.Abs(tox - fromx) > Math.Abs(toy - fromy))
            {
                for (int tmpx = fromx; tox < tmpx; tmpx--)
                {
                    int tmpy = (int)((double)(tmpx - fromx) * (double)(toy - fromy) / (tox - fromx) + fromy + .5);
                    if (maze.isWall(tmpx, tmpy)) return false;
                }
                return true;
            }
            else if (fromy < toy)
            {
                for (int tmpy = fromy; tmpy < toy; tmpy++)
                {
                    int tmpx = (int)((double)(tmpy - fromy) * (double)(tox - fromx) / (toy - fromy) + fromx + .5);
                    if (maze.isWall(tmpx, tmpy)) return false;
                }
                return true;
            }
            else
            {
                for (int tmpy = fromy; toy < tmpy; tmpy--)
                {
                    int tmpx = (int)((double)(tmpy - fromy) * (double)(tox - fromx) / (toy - fromy) + fromx + .5);
                    if (maze.isWall(tmpx, tmpy)) return false;
                }
                return true;
            }
        }
    }
}
