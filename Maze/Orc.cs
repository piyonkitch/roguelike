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
    class Orc : Entity
    {
        Random rnd = new Random();

        public Orc(MazeAlgo maze) : base(maze) {
            name = "Orc";
            graph = graphOrig = 'O';
            hitmax = hit = 4;
            strength = strengthmax = 3;
            toughness = 1;
        }

        public override void move(MazeAlgo maze, List<Entity> entitylist, Entity target)        // 多様性
        {
            if (!isLive()) return;

            string dir;
            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 3)
            {
                dir = maze.walk(xpos, ypos, target.xpos, target.ypos);
                dir = (dir == "") ? "？" : maze.walk(xpos, ypos, target.xpos, target.ypos).Substring(0, 1); // 最短経路の最初の方向を得る
            }
            else
            {
                dir = "←→↑↓"[rnd.Next(4)].ToString();
            }

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
        }
    }
}
