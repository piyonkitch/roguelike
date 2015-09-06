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
    class Stair : Entity
    {
        Random rnd = new Random();

        public Stair(MazeAlgo maze)
            : base(maze)
        {
            graph = graphOrig = '>';
            name = "Stair";
        }
    }
}
