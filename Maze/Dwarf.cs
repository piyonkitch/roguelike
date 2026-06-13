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
    class Dwarf : Entity
    {
        Random rnd = new Random();

        // 移動速度制御（2ターンに1回行動）
        private bool skipTurn = false;

        // 壁掘りカウント: キー=(x,y)、値=その壁への累計押し当て回数
        private Dictionary<string, int> digCounts = new Dictionary<string, int>();

        public Dwarf(MazeAlgo maze) : base(maze)
        {
            name = "Dwarf";
            graph = graphOrig = 'd';
            hit = hitmax = 5;
            strength = strengthmax = 3;
            toughness = 1;
        }

        // 隣接する壁方向のリストを返す
        private List<string> adjacentWallDirs(MazeAlgo maze)
        {
            var dirs = new List<string>();
            if (xpos > 0                  && maze.isWall(xpos - 1, ypos)) dirs.Add("←");
            if (xpos < Constant.NGRID - 1 && maze.isWall(xpos + 1, ypos)) dirs.Add("→");
            if (ypos > 0                  && maze.isWall(xpos, ypos - 1)) dirs.Add("↑");
            if (ypos < Constant.NGRID - 1 && maze.isWall(xpos, ypos + 1)) dirs.Add("↓");
            return dirs;
        }

        // Hero または Companion が距離 dist マス以内にいるか
        private bool partyNearby(List<Entity> entitylist, int dist)
        {
            foreach (Entity e in entitylist)
            {
                if (!e.isPartyMember) continue;
                if (Math.Sqrt(Math.Pow(e.xpos - xpos, 2) + Math.Pow(e.ypos - ypos, 2)) <= dist) return true;
            }
            return false;
        }

        protected override void doMove(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            // 2ターンに1回だけ行動
            skipTurn = !skipTurn;
            if (skipTurn) return;

            // 隣接壁があればそちらを優先、なければランダム
            var wallDirs = adjacentWallDirs(maze);
            string dir = wallDirs.Count > 0
                ? wallDirs[rnd.Next(wallDirs.Count)]
                : "←→↑↓"[rnd.Next(4)].ToString();

            // 移動先の座標を計算
            int nx = xpos, ny = ypos;
            switch (dir)
            {
                case "←": nx = xpos - 1; break;
                case "→": nx = xpos + 1; break;
                case "↑": ny = ypos - 1; break;
                case "↓": ny = ypos + 1; break;
            }

            // 範囲外チェック
            if (nx < 0 || nx >= Constant.NGRID || ny < 0 || ny >= Constant.NGRID) return;

            // 壁への押し当て処理
            if (maze.isWall(nx, ny))
            {
                // パーティが近ければ音を出す
                if (partyNearby(entitylist, 5))
                    Console.WriteLine("がんがんがん");

                string key = nx + "," + ny;
                if (!digCounts.ContainsKey(key)) digCounts[key] = 0;
                digCounts[key]++;

                if (digCounts[key] >= 4 && rnd.Next(100) < 20)
                {
                    // 壁を崩す
                    maze.breakWall(nx, ny);
                    digCounts.Remove(key);
                    Console.WriteLine("Dwarf は壁を砕いた！");

                    if (rnd.Next(100) < 20)
                    {
                        entitylist.Add(new Gold(maze, 0, rnd.Next(5) + 3, nx, ny));
                        Console.WriteLine("壁から金貨が現れた！");
                    }
                }
                return;
            }

            // 壁でない場合：@ と h は攻撃しない（通過もしない）
            foreach (Entity e in entitylist)
            {
                if (e.xpos == nx && e.ypos == ny && (e.graph == '@' || e.graph == 'h'))
                    return;
            }

            // 仲間のドワーフも攻撃しない
            foreach (Entity e in entitylist)
            {
                if (e.xpos == nx && e.ypos == ny && e is Dwarf)
                    return;
            }

            base.manualmove(dir, maze, entitylist);
        }
    }
}
