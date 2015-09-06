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
    class Armor : Entity
    {
        Random rnd = new Random();
        public int hardness { get; set; }
        private bool rustable; // 錆びやすい
        public bool isRust;    // 錆びた

        public Armor(MazeAlgo maze, int floor)
            : base(maze)
        {
            graph = graphOrig = '[';
            switch (floor) 
            {
                case 1: 
                    name = "Leather Armor";
                    hardness = 1;
                    rustable = false;
                    break;
                case 2: 
                    name = "Ring Mail";
                    hardness = 2;
                    rustable = true;
                    break;
                case 3:
                    name = "Scale Mail";
                    hardness = 3;
                    rustable = true;
                    break;
                case 4:
                    name = "Chain Mail";
                    hardness = 4;
                    rustable = true;
                    break;
                case 5:
                    name = "Banded Mail";
                    hardness = 5;
                    rustable = false;
                    break;
                default:
                    name = "Plate Mail";
                    hardness = 6;
                    break;
            }
        }

        public override bool isWearable()
        {
            return true;
        }

        public override void wear(Entity user)
        {
            user.armor = this;
        }

        public override void rust()
        {
            if (rustable)
            {
                isRust = true;
                hardness = (hardness > 0) ? hardness - 1 : 0;
                Console.WriteLine("鎧が錆びた");
            }
        }

        public override void enchant()
        {
            hardness++;
        }

        public override void protect()
        {
            isRust = rustable = false;
        }

        public override void pickup(Entity user)
        {
            user.itemlist.Add(new Item(this)); // armor はまとめない
            Console.WriteLine("{0} は {1} を拾った", user.name, this.name);
        }
    }
}
