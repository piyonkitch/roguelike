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

        // 隣り合う Grid を受け付け、fromからtoへは、どの方向に歩けばよいかを返す
        private string direction(Grid from, Grid to)
        {
            if (from.xpos - 1 == to.xpos) return "←";
            if (from.xpos + 1 == to.xpos) return "→";
            if (from.ypos - 1 == to.ypos) return "↑";
            if (from.ypos + 1 == to.ypos) return "↓";
            return "?";
        }

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
                        {  // XXX なぜか tmp.parent が null のことがあった。監視する 
                            Console.WriteLine("{0}", routestr); 
                            return ""; 
                        }
                        routestr = routestr.Insert(0, reverse(direction(tmp, tmp.parent)));
                    }
                    return routestr;
                }
            }
        }

        // 壁
        public override bool isWall(int x, int y)
        {
            return grid[x, y].isWall;
        }

        // 見えるかどうか
        public override bool isVisible(int x, int y)
        {
            return grid[x, y].isVisible;
        }
        public override bool Visible(int x, int y)
        {
            return grid[x, y].isVisible = true;
        }
        public override bool Invisible(int x, int y)
        {
            return grid[x, y].isVisible = false;
        }
    }
}
