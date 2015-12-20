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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// writeline先を入れ替える
using System.IO;

namespace Maze
{
    public partial class RogueLike : Form
    {
        Logic logic = new Logic();
        
        const int Dots = 11;

        // エントリーポイント
        public RogueLike()
        {
            InitializeComponent();
            Console.SetOut(new TextBoxWriter(textBoxConsole));
            logic.init();
            show();
        }

        private void show(){
            //描画先とするImageオブジェクトを作成する
            Bitmap canvas = new Bitmap(pic.Width, pic.Height);
            //ImageオブジェクトのGraphicsオブジェクトを作成する
            Graphics g = Graphics.FromImage(canvas);

            // 空間□と壁■を描画する
            for (int y = 0; y < Constant.NGRID; y++)
            {
                for (int x = 0; x < Constant.NGRID; x++)
                {
                    if (logic.maze.isVisible(x, y)) // 見えるところだけ描画
                    {

                        if (logic.maze.isWall(x, y))
                        {
                            g.FillRectangle(Brushes.Black, Dots * x, Dots * y, Dots, Dots);
                        }
                        else
                        {
                            g.DrawRectangle(Pens.Black, Dots * x, Dots * y, Dots, Dots);
                        }
                    }
                }
            }

            // キャラクター（生物と物）を描画する
            Font fnt = new Font("MS UI Gothic", Dots-1);
            foreach (Entity e in logic.entitylist)
            {
                if (logic.isEntitySeeable(e))
                {
                    g.DrawString(e.graph.ToString(), fnt, Brushes.Red, Dots * e.xpos, Dots * e.ypos);
                }
            }

            //リソースを解放する
            fnt.Dispose();
            g.Dispose();

            //PictureBox1に表示する
            pic.Image = canvas;

            //
            // ステータスラベルの描画
            //
            labelStatus.Text =  "HP =     " + logic.hero.hit + "/" + logic.hero.hitmax + "\n";
            labelStatus.Text += "Str=     " + logic.hero.getStrength() + " Tough=" + logic.hero.getToughness() + "\n";
            labelStatus.Text += "Exp=     " + logic.hero.experience + "\n";
            labelStatus.Text += "$  =     " + logic.hero.gold + "\n";
            labelStatus.Text += "Floor  = " + logic.floor;

            // REVISIT 2015/09/06 Logic logic 中でゲームオーバー判定したほうが良いのだけど…
            if (logic.hero.hit <= 0)
            {
                MessageBox.Show("やられた");
                // スタティックコンストラクタを再実行したいので、logic.init() ではなく、アプリケーション再実行する
                Application.Restart();
//                logic.init();
//                show();
            }
        }

        //
        // むかしの迷路探索プログラムの名残り。2点をクリックすると経路を表示する。
        //
        private void showroute(int xpos, int ypos, string routestr)
        {
            //描画先とするImageオブジェクトを作成する
            Bitmap canvas = new Bitmap(pic.Width, pic.Height);
            canvas = (System.Drawing.Bitmap)pic.Image; // 現在の画像を読み取る
            //ImageオブジェクトのGraphicsオブジェクトを作成する
            Graphics g = Graphics.FromImage(canvas);
            Pen p = new Pen(Color.Blue, 3); // ペン

            int x = xpos, y = ypos;
            for (int i = 0; i < routestr.Length; i++)
            {
                switch (routestr.Substring(i, 1))
                {
                    case "←":
                        g.DrawLine(p, Dots * x     + (Dots/2), Dots * y + (Dots/2),
                                      Dots * (x-1) + (Dots/2), Dots * y + (Dots/2));
                        x--;
                        break;

                    case "→":
                        g.DrawLine(p, Dots * x     + (Dots/2), Dots * y + (Dots/2),
                                      Dots * (x+1) + (Dots/2), Dots * y + (Dots/2));
                        x++;
                        break;

                    case "↑":
                        g.DrawLine(p, Dots * x + (Dots/2), Dots * y + (Dots/2),
                                      Dots * x + (Dots/2), Dots * (y - 1) + (Dots/2));
                        y--;
                        break;

                    case "↓":
                        g.DrawLine(p, Dots * x + (Dots/2), Dots * y + (Dots/2),
                                      Dots * x + (Dots/2), Dots * (y + 1) + (Dots/2));
                        y++;
                        break;
                }
            }

            //リソースを解放する
            g.Dispose();
            p.Dispose();
            //PictureBox1に表示する
            pic.Image = canvas;
        }

        int fromx = -1; int fromy = -1; int tox = -1; int toy = -1;
        // picturebox のクリックをひろう
        private void pic_MouseDown(object sender, MouseEventArgs e)
        {
            Console.WriteLine(e.X.ToString() + "," + e.Y.ToString());
            if (fromx == -1 && fromy == -1)
            {
                fromx = e.X / Dots; fromy = e.Y / Dots;
                return;
            }
            if (tox == -1 && toy == -1)
            {
                string routestr;

                tox = e.X / Dots; toy = e.Y / Dots;
                routestr = logic.maze.walk(fromx, fromy, tox, toy);
                Console.WriteLine(routestr);
                showroute(fromx, fromy, routestr);

                fromx = fromy = tox = toy = -1;
                return;
            }
        }

        //
        // 以下、ボタンで駆動される処理
        //

        private void buttonUp_Click(object sender, EventArgs e)
        {
            logic.ctrlUp();
            show();
        }

        private void buttonLeft_Click(object sender, EventArgs e)
        {
            logic.ctrlLeft();
            show();
        }

        private void buttonRight_Click(object sender, EventArgs e)
        {
            logic.ctrlRight();
            show();
        }

        private void buttonDown_Click(object sender, EventArgs e)
        {
            logic.ctrlDown();
            show();
        }

        private void buttonStairDown_Click(object sender, EventArgs e)
        {
            logic.ctrlStairDown();
            show();
        }

        private void buttonUse_Click(object sender, EventArgs e)
        {
            int index = listBoxItemlist.SelectedIndex;
            if (index != -1)
            {
                logic.ctrlUse(index);
                logic.tick();
            }
            listBoxItemlist.ClearSelected();
            listBoxItemlist.Hide();
            show();
        }

        private void buttonDrop_Click(object sender, EventArgs e)
        {
            int index = listBoxItemlist.SelectedIndex;
            if (index != -1)
            {
                logic.ctrlDrop(index);
            }
            listBoxItemlist.ClearSelected();
            listBoxItemlist.Hide();
            show();
        }

        private void buttonWield_Click(object sender, EventArgs e)
        {
            int index = listBoxItemlist.SelectedIndex;
            if (index != -1)
            {
                logic.ctrlWield(index);
                logic.tick();
            }
            listBoxItemlist.ClearSelected();
            listBoxItemlist.Hide();
            show();
        }

        private void buttonWear_Click(object sender, EventArgs e)
        {
            int index = listBoxItemlist.SelectedIndex;
            if (index != -1)
            {
                logic.ctrlWear(index);
                logic.tick();
            }
            listBoxItemlist.ClearSelected();
            listBoxItemlist.Hide();
            show();
        }

        private void buttonTakeOffWeapon_Click(object sender, EventArgs e)
        {
            logic.ctrlTakeOffWeapon();
            show();
        }

        private void buttonTakeOffArmor_Click(object sender, EventArgs e)
        {
            logic.ctrlTakeOffArmor();
            show();
        }

        private void buttonInventory_Click(object sender, EventArgs e)
        {
            if (listBoxItemlist.Visible == false)
            {
                // ポップアップ的にアイテムを選ばせる
                listBoxItemlist.Items.Clear();
                listBoxItemlist.Show();
                foreach (Item i in logic.hero.itemlist)
                {
                    string text;
                    text = i.name +  "(" + i.num + "個)";
                    if (i.entity == logic.hero.weapon) {
                        text += "(武器)";
                    }
                    if (i.entity == logic.hero.armor) {
                        text += "(鎧)";
                    }
                    listBoxItemlist.Items.Add(text);
                }
            }
            else
            {
                listBoxItemlist.Visible = false;            // もう一度 i を押したら、使用せずにリストボックスを閉じる
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            logic.ctrlSave();
            show();
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            logic.ctrlLoad();
            show();
        }

        private void toolStripMenuItemHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
@"↑↓←→  上下左右へ移動
u         アイテムを使う (iで表示&選択してから)
i         アイテムの一覧表示・非表示の切り替え (u, d, w, Wの前に表示と選択)
d         アイテムを落とす
w         武器を構える
W         鎧を着る
t         武器を解除する
T         鎧を脱ぐ", "roguelikeのヘルプ");
        }

    }

    // コンソールをテキストボックスに出力するおまじない
    public class TextBoxWriter : TextWriter
    {
        TextBox _output = null;

        public TextBoxWriter(TextBox output)
        {
            _output = output;
        }

        public override void Write(char value)
        {
            base.Write(value);
            _output.AppendText(value.ToString());
        }

        public override Encoding Encoding
        {
            get { return System.Text.Encoding.UTF8; }
        }
    }
}
