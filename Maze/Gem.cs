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
using System.Drawing;

namespace Maze
{
    // 宝石エンティティ。本物（能力あり）と偽物（見た目が似て能力なし）を同クラスで表現。
    // 識別前は apparentName を表示。Scroll of Identify か効果発動で realName が判明する。
    [Serializable]
    class Gem : Entity
    {
        public enum GemAbility { None, Heal, Barrier, TimeStop, CritBoost }

        private string realName;
        private string apparentName;
        public GemAbility ability { get; private set; }
        // power: 1=小さな原石、2=大きな原石（クエスト対象）
        public int power { get; private set; }
        public bool isLarge => power >= 2;
        private bool _identified;
        private int colorArgb;

        public Color DisplayColor => Color.FromArgb(colorArgb);

        public override string name
        {
            get => _identified ? realName : apparentName;
            set => realName = value;
        }

        private Gem(MazeAlgo maze, GemAbility ability, int power,
                    string realName, string apparentName, Color color) : base(maze)
        {
            graph = graphOrig = '*';
            this.ability = ability;
            this.power = power;
            this.realName = realName;
            this.apparentName = apparentName;
            this.colorArgb = color.ToArgb();
            this._identified = false;
            hit = hitmax = 1;
        }

        public override bool isIdentified() => _identified;

        public override void identify(Entity user)
        {
            if (_identified) return;
            _identified = true;
            Console.WriteLine("{0} は「{1}」だとわかった！", apparentName, realName);
        }

        // 宝石の効果が発動したとき（効果でアイデンタファイ）
        public void revealByEffect()
        {
            if (!_identified)
            {
                _identified = true;
                Console.WriteLine("→ {0} の正体は「{1}」だ！", apparentName, realName);
            }
        }

        public override void pickup(Entity user)
        {
            user.itemlist.Add(new Item(this));  // 宝石は同名でもスタックしない（各個体が別の宝石）
            Console.WriteLine("{0} は {1} を拾った", user.name, this.name);
        }

        // ─────────────────────────────────────────
        // 宝石生成ファクトリ
        //   春: ローズクォーツ（回復）/ ロードナイト（偽物）
        //   夏: サファイア（結界）  / アイオライト（偽物）
        //   秋: アンバー（時間停止）/ シトリン（偽物）
        //   冬: アクアマリン（クリティカル）/ ブルートパーズ（偽物）
        // ─────────────────────────────────────────

        public static Gem CreateLargeRoseQuartz(MazeAlgo maze) =>
            new Gem(maze, GemAbility.Heal, 2,
                "大ローズクォーツ", "大きなピンクの星石",
                Color.FromArgb(255, 183, 197));

        public static Gem CreateSmallRoseQuartz(MazeAlgo maze) =>
            new Gem(maze, GemAbility.Heal, 1,
                "ローズクォーツ", "ピンクの星石",
                Color.FromArgb(255, 183, 197));

        // 偽物：薄ピンクより赤みが強い
        public static Gem CreateRhodonite(MazeAlgo maze) =>
            new Gem(maze, GemAbility.None, 0,
                "ロードナイト", "ピンクの星石",
                Color.FromArgb(188, 100, 120));

        public static Gem CreateLargeSapphire(MazeAlgo maze) =>
            new Gem(maze, GemAbility.Barrier, 2,
                "大サファイア", "大きな青い星石",
                Color.FromArgb(15, 82, 186));

        public static Gem CreateSmallSapphire(MazeAlgo maze) =>
            new Gem(maze, GemAbility.Barrier, 1,
                "サファイア", "青い星石",
                Color.FromArgb(15, 82, 186));

        // 偽物：青紫寄り
        public static Gem CreateIolite(MazeAlgo maze) =>
            new Gem(maze, GemAbility.None, 0,
                "アイオライト", "青い星石",
                Color.FromArgb(60, 70, 170));

        public static Gem CreateLargeAmber(MazeAlgo maze) =>
            new Gem(maze, GemAbility.TimeStop, 2,
                "大アンバー", "大きな琥珀色の星石",
                Color.FromArgb(255, 191, 0));

        public static Gem CreateSmallAmber(MazeAlgo maze) =>
            new Gem(maze, GemAbility.TimeStop, 1,
                "アンバー", "琥珀色の星石",
                Color.FromArgb(255, 191, 0));

        // 偽物：より黄金色
        public static Gem CreateCitrine(MazeAlgo maze) =>
            new Gem(maze, GemAbility.None, 0,
                "シトリン", "琥珀色の星石",
                Color.FromArgb(240, 215, 45));

        public static Gem CreateLargeAquamarine(MazeAlgo maze) =>
            new Gem(maze, GemAbility.CritBoost, 2,
                "大アクアマリン", "大きな水色の星石",
                Color.FromArgb(127, 255, 212));

        public static Gem CreateSmallAquamarine(MazeAlgo maze) =>
            new Gem(maze, GemAbility.CritBoost, 1,
                "アクアマリン", "水色の星石",
                Color.FromArgb(127, 255, 212));

        // 偽物：空色（やや青寄り）
        public static Gem CreateBlueTopaz(MazeAlgo maze) =>
            new Gem(maze, GemAbility.None, 0,
                "ブルートパーズ", "水色の星石",
                Color.FromArgb(95, 195, 240));
    }
}
