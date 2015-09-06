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
    class Weapon : Entity
    {
        Random rnd = new Random();
        private string origName;
        public override string name
        {
            get { return ((isRust) ? "錆びた" : "") + origName; }
            set { origName = value; }
        }
        public int sharpness { get; set; }
        private bool rustable; // 錆びやすい
        public bool isRust;    // 錆びた

        public Weapon(MazeAlgo maze, int floor)
            : base(maze)
        {
            graph = graphOrig = ')';

            switch (floor) 
            {
                case 1: 
                    name = "Dagger";
                    sharpness = 1;
                    rustable = true;
                    break;
                case 2:
                    name = "Mace";
                    sharpness = 2;
                    rustable = false;
                    break;
                case 3:
                    name = "Short Sword";
                    sharpness = 3;
                    rustable = true;
                    break;
                case 4:
                    name = "Long Sword";
                    rustable = true;
                    sharpness = 4;
                    break;
                default:
                    name = "Vopal Weapon";
                    rustable = false;
                    sharpness = 5;
                    break;
            }
        }

        public override bool isWieldable()
        {
            return true;
        }

        public override void wield(Entity user)
        {
            user.weapon = this;
        }

        public override void rust()
        {
            if (rustable)
            {
                isRust = true;
                sharpness = (sharpness > 0) ? sharpness - 1 : 0;
                Console.WriteLine("武器が錆びた");
            }
        }

        public override void enchant()
        {
            sharpness++;
        }

        public override void protect()
        {
            isRust = rustable = false;
        }

        public override void pickup(Entity user)
        {
            user.itemlist.Add(new Item(this));          // weapon はまとめない
            Console.WriteLine("{0} は {1} を拾った", user.name, this.name);
        }
    }
}
