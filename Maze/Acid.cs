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
    class Acid : Entity
    {
        bool angry;
        Random rnd = new Random();

        public Acid(MazeAlgo maze) : base(maze)
        {
            name = "Acid Blob";
            graph = graphOrig = 'A';
            hitmax = hit = 3;
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

            if (target.armor != null)
            {
                target.armor.rust();
            }
        }

        public override void beat(Entity attacker)
        {
            if (attacker.graph == '@')
            {
                angry = true;
            }
            if (attacker.weapon != null)
            {
                attacker.weapon.rust();
            }
        }
    }
}
