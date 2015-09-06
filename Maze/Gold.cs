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
    class Gold : Entity
    {
        Random rnd = new Random();

        public Gold(MazeAlgo maze, int floor)
            : base(maze)
        {
            graph = graphOrig = '$';
            name = "Gold";
            hit = rnd.Next(5) + 5;
        }

        public Gold(MazeAlgo maze, int floor, int price, int x, int y)
            : base(maze)
        {
            graph = graphOrig = '$';
            name = "Gold";
            hit = price;
            xpos = x;
            ypos = y;
        }

        public override void pickup(Entity user)     // user が拾う
        {
            user.gold += hit;
            Console.WriteLine("{0} は {1} を拾った", user.name, name);
        }
    }
}
