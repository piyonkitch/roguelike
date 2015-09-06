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
    public abstract class MazeAlgo
    {
        // 迷路の初期化、探索
        abstract public void initmaze();
        abstract public string walk(int oldx, int oldy, int newx, int newy);
        // 壁
        abstract public bool isWall(int x, int y);
        // 見えるかどうか
        abstract public bool isVisible(int x, int y);
        abstract public bool Visible(int x, int y);
        abstract public bool Invisible(int x, int y);
    }
}
