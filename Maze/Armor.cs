/*
Copyright(c) 2015, piyonkitch<kazuo.horikawa.ko@gmail.com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
 list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
 this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of roguelike nor the names of its
 contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
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
