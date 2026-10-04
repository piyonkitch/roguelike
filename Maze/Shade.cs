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
    // 冥府の霊。Bat並みに脆いが、物理攻撃を50%の確率ですり抜ける
    [Serializable]
    class Shade : Entity
    {
        Random rnd = new Random();

        public Shade(MazeAlgo maze) : base(maze)
        {
            name = "Shade";
            graph = graphOrig = 'G';
            hit = hitmax = 1;
            strength = strengthmax = 1;
            toughness = 0;
            murmurWait = rnd.Next(1, 6);   // 一斉にささやかないよう、最初の間をずらす
        }

        // 冥府の霊のささやき。テイレシアスのこと、原典の仲間エルペーノールのこと
        private static readonly string[] Lines = {
            "…冥府の奥に…目の見えぬ予言者がいる…",
            "…テイレシアス…故郷への帰り道を知る、ただ一人の者…",
            "…生きた者がここへ来るとは…予言者に会いに来たのか…",
            "…わたしはエルペーノール…魔女の館の屋根から落ちて死んだ…どうか弔ってくれ…",
        };

        // Hero が近く（4歩以内）にいるとき、数ターンに1回つぶやく（台詞は全員で順番に回す）
        private int murmurWait = 1;
        private static int nextLine;

        private void murmur(Entity target)
        {
            if (--murmurWait > 0) return;
            if (Math.Abs(target.xpos - xpos) + Math.Abs(target.ypos - ypos) > 4) return;
            string line = Lines[nextLine++ % Lines.Length];
            Console.WriteLine("{0}：「{1}」", "冥府の霊", line);
            CombatLog.AddTalk(this, target, line);
            murmurWait = 8 + rnd.Next(6);   // 8〜13ターンおき
        }

        public override bool avoidsAttack(Entity attacker)
        {
            return rnd.Next(100) < 50;
        }

        protected override void doMove(MazeAlgo maze, List<Entity> entitylist, Entity target)
        {
            murmur(target);   // ささやいても動きは今までどおり
            string dir = "←→↑↓"[rnd.Next(4)].ToString();
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
