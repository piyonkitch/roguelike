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
    class Bat : Entity
    {
        Random rnd = new Random();

        public Bat(MazeAlgo maze)
            : base(maze)
        {
            name = "Bat";
            graph = graphOrig = 'B';
            hitmax = hit = 1;
            levitation = true;          // 飛んでいるので、物は拾えない
        }

        public override void move(MazeAlgo maze, List<Entity> entitylist, Entity target)        // 多様性
        {
            if (!isLive()) return;

            string dir;
            dir = "←→↑↓"[rnd.Next(4)].ToString();
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

            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 4)
            {
                Console.WriteLine("バサバサ");
            }
        }
    }
}
