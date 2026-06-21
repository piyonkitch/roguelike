/*
Copyright(c) 2026, piyonkitch<kazuo.horikawa.ko@gmail.com>
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

namespace Maze
{
    // 4階に配置される宝石祭壇。通行可能な固定オブジェクト。
    // 対応する季節（season）の大きな原石を「u」コマンドで嵌め込める。
    [Serializable]
    class Altar : Entity
    {
        // この祭壇が受け入れる宝石の季節（プレイヤーには非公開）
        public Gem.GemAbility season { get; private set; }

        // 嵌め込まれた宝石（null = 空）
        public Gem embeddedGem { get; set; }

        public Altar(MazeAlgo maze, int x, int y, Gem.GemAbility season) : base(maze)
        {
            xpos = x;
            ypos = y;
            graph = graphOrig = '_';
            name = "祭壇";
            this.season = season;
            hit = hitmax = 1;
            strength = 0;
            toughness = 0;
        }

        public override void pickup(Entity user)
        {
            // 祭壇は拾えない（tryMove の '_' 分岐により thigsOnGrid には入らないが念のため）
        }
    }
}
