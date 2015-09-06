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
    class Hobbit : Entity
    {
        bool angry;
        Random rnd = new Random();

        public Hobbit(MazeAlgo maze) : base(maze)
        {
            name = "Hobbit";
            graph = graphOrig = 'h';
            angry = false;
        }

        public override void move(MazeAlgo maze, List<Entity> entitylist, Entity target)        // 多様性
        {
            if (!isLive()) return;

            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 1 && !angry)
            {
                DateTime dt = DateTime.Now;
                string message;

                if (dt.Hour > 7 && dt.Hour < 20)
                {
                    if (dt.Hour < 10) message = "おはよう";
                    else if (dt.Hour < 18) message = "こんにちは";
                    else message = "こんばんは";

                    Console.WriteLine("{0} は、「{1}」と言った", name, message);
                }
                return;
            }

            string dir;
            if (angry && Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 3)
            {
                dir = maze.walk(xpos, ypos, target.xpos, target.ypos);
                dir = (dir == "") ? "？" : maze.walk(xpos, ypos, target.xpos, target.ypos).Substring(0, 1); // 最短経路の最初の方向を得る
            }
            else
            {
                dir = "←→↑↓"[rnd.Next(4)].ToString();
            }
            
            // 仲間(h)は攻撃しない。怒ってなければ人(@)も攻撃しない。
            switch (dir)
            {
                case "←":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos - 1 && e.ypos == ypos && 
                            (e.graph == 'h' || (e.graph == '@' && !angry))) return;
                    }
                    break;
                case "→":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos + 1 && e.ypos == ypos && 
                            (e.graph == 'h' || (e.graph == '@' && !angry))) return;
                    }
                    break;
                case "↑":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos && e.ypos == ypos - 1 && 
                            (e.graph == 'h' || (e.graph == '@' && !angry))) return;
                    }
                    break;
                case "↓":
                    foreach (Entity e in entitylist)
                    {
                        if (e.xpos == xpos && e.ypos == ypos + 1 && 
                            (e.graph == 'h' || (e.graph == '@' && !angry))) return;
                    }
                    break;
            }
            base.manualmove(dir, maze, entitylist);
        }

        public override void beat(Entity attacker)
        {
            if (attacker.graph == '@')
            {
                angry = true;
            }
        }
    }
}
