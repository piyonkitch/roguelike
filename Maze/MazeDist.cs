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
    class MazeDist : MazeAlgo
    {
        public GridDist[,] grid = new GridDist[Constant.NGRID, Constant.NGRID]; // 迷路のグリッド
        private List<GridDist> candidates;

        // initialize a maze (dungeon) using random number generator.
        public override void initmaze()
        {
            Random rnd = new Random();

            for (int y = 0; y < Constant.NGRID; y++)
            {
                for (int x = 0; x < Constant.NGRID; x++)
                {
                    grid[x, y] = new GridDist();
                    grid[x, y].xpos = x;
                    grid[x, y].ypos = y;
                    grid[x, y].isWall = (rnd.Next(100) < 30);
                    grid[x, y].isVisible = false;
                    grid[x, y].distance = 999;
                }
            }
            grid[0, 0].isWall = false; // スタート地点は壁なしにする
        }

        // returen a direction string to reach "from" to "to".
        private string direction(Grid from, Grid to)
        {
            if (from.xpos - 1 == to.xpos) return "←";
            if (from.xpos + 1 == to.xpos) return "→";
            if (from.ypos - 1 == to.ypos) return "↑";
            if (from.ypos + 1 == to.ypos) return "↓";
            return "?";
        }

        // reverse directions in a string - e.g. For "→↑", "←↓" will be returned.
        private string reverse(string dir)
        {
            if (dir == "←") return "→";
            if (dir == "→") return "←";
            if (dir == "↑") return "↓";
            if (dir == "↓") return "↑";
            return "?";
        }

        // 親と子を指定し、子と親のリンクを張る。
        // また候補リストに子を入れる。
        //
        // ただし、子が壁の場合か、距離が短い場合、なにもしない。
        private void linkchild(GridDist p, GridDist c)
        {
            if (c.isWall) return;
            if (c.distance <= p.distance + 1) return;
            c.distance = p.distance + 1;
            c.parent = p;

            candidates.Remove(c);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].distance > c.distance)
                {
                    candidates.Insert(i, c); // 距離の短い順に、子供を入れる
                    return;
                }
            }
            candidates.Add(c); // 末尾に、子供を入れる
        }

        // Walk (fromx, fromy) to (tox, toy) and returns a direction string.
        // If I cannot reach (tox, toy), then "" will be returned.
        public override string walk(int fromx, int fromy, int tox, int toy)
        {
            for (int y = 0; y < Constant.NGRID; y++)
            {
                for (int x = 0; x < Constant.NGRID; x++)
                {
                    grid[x, y].distance = 999;
                    grid[x, y].parent = null;
                }
            }
            candidates = new List<GridDist>();

            grid[fromx, fromy].parent = grid[fromx, fromy]; // 根は自分を親にする
            grid[fromx, fromy].distance = 0;
            int curx = grid[fromx, fromy].xpos; int cury = grid[fromx, fromy].ypos;
            while (true)
            {
                if (curx > 0)                  linkchild(grid[curx, cury], grid[curx - 1, cury]);
                if (curx < Constant.NGRID - 1) linkchild(grid[curx, cury], grid[curx + 1, cury]);
                if (cury > 0)                  linkchild(grid[curx, cury], grid[curx, cury - 1]);
                if (cury < Constant.NGRID - 1) linkchild(grid[curx, cury], grid[curx, cury + 1]);

                candidates.Remove(grid[curx, cury]);

                if (candidates.Count == 0)
                { // 迷路を探索しつくした
                    return "";
                }

                curx = candidates[0].xpos;
                cury = candidates[0].ypos;
                if (grid[tox, toy] == grid[curx, cury])
                {
                    string routestr = "";
                    for (GridDist tmp = grid[tox, toy]; tmp != grid[fromx, fromy]; tmp = tmp.parent)
                    {
                        if (tmp.parent == null)
                        {
                            Console.WriteLine("{0}", routestr); 
                            return ""; 
                        }
                        routestr = routestr.Insert(0, reverse(direction(tmp, tmp.parent)));
                    }
                    return routestr;
                }
            }
        }

        // Is (x, y) wall?
        public override bool isWall(int x, int y)
        {
            return grid[x, y].isWall;
        }

        // Is (x, y) visible?
        public override bool isVisible(int x, int y)
        {
            return grid[x, y].isVisible;
        }

        // Make (x, y) visible.
        public override bool Visible(int x, int y)
        {
            return grid[x, y].isVisible = true;
        }

        // Make (x, y) invisible.
        public override bool Invisible(int x, int y)
        {
            return grid[x, y].isVisible = false;
        }
    }
}
