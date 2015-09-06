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
    class Hero : Entity
    {
        public Hero(MazeAlgo maze)
            : base(maze)
        {
            graph = graphOrig = '@';
            name = "Hero";
        }
    }
}
