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
    class Orc : Entity
    {
        Random rnd = new Random();

        public Orc(MazeAlgo maze) : base(maze) {
            name = "Orc";
            graph = graphOrig = 'O';
            hitmax = hit = 4;
            strength = strengthmax = 3;
            toughness = 1;
        }

        public override void move(MazeAlgo maze, List<Entity> entitylist, Entity target)        // 多様性
        {
            if (!isLive()) return;

            string dir;
            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 3)
            {
                dir = maze.walk(xpos, ypos, target.xpos, target.ypos);
                dir = (dir == "") ? "？" : maze.walk(xpos, ypos, target.xpos, target.ypos).Substring(0, 1); // 最短経路の最初の方向を得る
            }
            else
            {
                dir = "←→↑↓"[rnd.Next(4)].ToString();
            }

            switch (dir)
            {
                case "←":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos - 1 && e.ypos == ypos && (e.graph == graph)) return;
                    }
                    break;
                case "→":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos + 1 && e.ypos == ypos && (e.graph == graph)) return;
                    }
                    break;
                case "↑":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos && e.ypos == ypos - 1 && (e.graph == graph)) return;
                    }
                    break;
                case "↓":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos && e.ypos == ypos + 1 && (e.graph == graph)) return;
                    }
                    break;
            }
            base.manualmove(dir, maze, entitylist);
        }
    }
}
