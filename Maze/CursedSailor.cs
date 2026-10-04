/*
Copyright(c) 2015, 2026, piyonkitch<kazuo.horikawa.ko@gmail.com>
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
    // 呪われた乗組員の亡霊。6階の雑魚
    [Serializable]
    class CursedSailor : Entity
    {
        Random rnd = new Random();

        public CursedSailor(MazeAlgo maze) : base(maze)
        {
            name = "Cursed Sailor";
            graph = graphOrig = 'C';
            hit = hitmax = 3;
            strength = strengthmax = 2;
            toughness = 0;
            murmurWait = rnd.Next(1, 6);   // 一斉につぶやかないよう、最初の間をずらす
        }

        // 海峡の怪物と渦、7階の魔女のことを知っている乗組員のつぶやき
        private static readonly string[] Lines = {
            "…スキュラだ…六つの首が、一度に仲間を二人ずつさらっていった…",
            "…あの怪物は硬くて強い。まともに斬り合って勝った者はいない…",
            "…渦には近づくな…カリュブディスは船も人も、何もかも飲み込む…",
            "…飲み込まれた者は、ずっと深い底へ吐き出されるという…",
            "…向こう岸へ渡る道は二つ…怪物の脇か、渦の脇か…",
            "…魔女の島へ行くなら、白い花をつけた黒い根を持っていけ…魔女の杖が効かなくなる…",
        };

        // Hero が近く（4歩以内）にいるとき、数ターンに1回つぶやく（台詞は全員で順番に回す）
        private int murmurWait = 1;
        private static int nextLine;

        private void murmur(Entity target)
        {
            if (--murmurWait > 0) return;
            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) > 4) return;
            string line = Lines[nextLine++ % Lines.Length];
            Console.WriteLine("{0}：「{1}」", "呪われた乗組員", line);
            CombatLog.AddTalk(this, target, line);
            murmurWait = 8 + rnd.Next(6);   // 8〜13ターンおき
        }

        protected override void doMove(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            murmur(target);   // つぶやいても移動・攻撃は今までどおり
            string dir;
            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) <= 3)
            {
                dir = maze.walk(xpos, ypos, target.xpos, target.ypos);
                dir = (dir == "") ? "？" : dir.Substring(0, 1); // 最短経路の最初の方向を得る
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
