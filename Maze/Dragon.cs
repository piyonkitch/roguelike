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
    class Dragon : Entity
    {
        Random rnd = new Random();

        public Dragon(MazeAlgo maze) : base(maze) {
            graph = graphOrig = 'D';
            name = "Dragon";
            hit = hitmax = 10;
            strength = strengthmax = 4;
            toughness = 4;
        }

        public override void move(MazeAlgo maze, List<Entity> entitylist, Entity target)        // 多様性
        {

            String dir;

            if (!isLive()) return;

            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 2)
            {
                Console.WriteLine("{0} は {1} に炎を吐いた", name, target.name);
                if (rnd.Next(100) < 50)             // 30%の確率でヒット
                {
                    target.hit = target.hit - (rnd.Next(2) + 2);
                }
                return;
            }
            else if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 3)
            {
                dir = maze.walk(xpos, ypos, target.xpos, target.ypos);
                dir = (dir == "") ? "？" : maze.walk(xpos, ypos, target.xpos, target.ypos).Substring(0, 1); // 最短経路の最初の方向を得る
            }
            else
            {
                dir = "←→↑↓"[rnd.Next(4)].ToString();
            }

            // 仲間は攻撃しない。
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
