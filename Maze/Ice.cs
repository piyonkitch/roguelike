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
    class Ice : Entity
    {
        bool angry;
        Random rnd = new Random();

        public Ice(MazeAlgo maze) : base(maze)
        {
            name = "Ice Jerry";
            graph = graphOrig = 'I';
            hit = hitmax = 5;
            angry = false;
        }

        public override void move(MazeAlgo maze, List<Entity> entitylist, Entity target)        // 多様性
        {
            if (!isLive()) return;
            if (!angry) return;

            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) > 1)
            {
                return;               // 遠いので何もしない
            }

            // 隣にいるので、凍らせようとする
            if (rnd.Next(100) < 50)
            {
                target.frozen += rnd.Next(4) + 4;
                Console.WriteLine("氷った");
            }
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
