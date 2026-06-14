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

        // 穴タイル（シリアライズ対応）
        private HashSet<string> pits = new HashSet<string>();
        // 5x5クリア時にLogicへ通知するキュー（非シリアライズ）
        [NonSerialized]
        private List<int[]> pendingPits;
        // 重複トリガー防止（シリアライズ対応）
        private HashSet<string> triggeredPits = new HashSet<string>();

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

            // 四方が壁（または盤外）に囲まれた孤立マスを壁に変換する（1パスのみ）
            for (int y = 0; y < Constant.NGRID; y++)
                for (int x = 0; x < Constant.NGRID; x++)
                {
                    if (grid[x, y].isWall) continue;
                    bool left  = x == 0                  || grid[x - 1, y].isWall;
                    bool right = x == Constant.NGRID - 1 || grid[x + 1, y].isWall;
                    bool up    = y == 0                  || grid[x, y - 1].isWall;
                    bool down  = y == Constant.NGRID - 1 || grid[x, y + 1].isWall;
                    if (left && right && up && down) grid[x, y].isWall = true;
                }
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

        // Break wall at (x, y). 5x5クリア時にpendingPitsへ通知を積む。
        public override void breakWall(int x, int y)
        {
            grid[x, y].isWall = false;
            check5x5ForPit(x, y);
        }

        // (wx,wy)を含む全5x5ブロックを検査し、全マス非壁ならpendingPitsに追加
        private void check5x5ForPit(int wx, int wy)
        {
            if (pendingPits  == null) pendingPits  = new List<int[]>();
            if (triggeredPits == null) triggeredPits = new HashSet<string>();
            int N = Constant.NGRID;
            for (int bx = Math.Max(0, wx - 4); bx <= Math.Min(wx, N - 5); bx++)
            {
                for (int by = Math.Max(0, wy - 4); by <= Math.Min(wy, N - 5); by++)
                {
                    int cx = bx + 2, cy = by + 2;
                    string key = cx + "," + cy;
                    if (triggeredPits.Contains(key)) continue;
                    bool allClear = true;
                    for (int dx = 0; dx < 5 && allClear; dx++)
                        for (int dy = 0; dy < 5 && allClear; dy++)
                            if (grid[bx + dx, by + dy].isWall) allClear = false;
                    if (allClear)
                    {
                        triggeredPits.Add(key);
                        pendingPits.Add(new int[] { cx, cy });
                    }
                }
            }
        }

        // 穴タイル操作
        public override bool isPit(int x, int y)   => pits.Contains(x + "," + y);
        public override void addPit(int x, int y)  => pits.Add(x + "," + y);

        // 溜まった穴通知を取り出してキューを空にする
        public override List<int[]> takePendingPits()
        {
            if (pendingPits == null) return new List<int[]>();
            var result = pendingPits;
            pendingPits = new List<int[]>();
            return result;
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
