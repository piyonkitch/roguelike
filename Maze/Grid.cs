using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Maze
{
    [Serializable]
    class Grid
    {
        public int xpos, ypos;          // 座標
        public bool isWall;             // 壁かどうか
        public bool isVisible;          // 見えるかどうか
    }

    [Serializable]
    class GridDist : Grid
    {
        public GridDist parent;             // 親
        public int distance;                // スタート地点からの距離
    }
}
