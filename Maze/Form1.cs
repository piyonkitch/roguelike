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

        const int Dots = 20;    // 1マスのピクセル数

        // 日本語を出す部品（ボタン・メニュー・持ち物の一覧・ステータス欄・メッセージ欄・知らせ）のフォント
        static readonly Font UiFont = new Font("Meiryo UI", 9);

        private System.Windows.Forms.Timer magicTimer;

        // 戦闘ビュー（画面左）
        const int BattleWidth = 360;
        private PictureBox battlePic;
        private BattleView battleView;

        // エントリーポイント
        public RogueLike()
        {
            InitializeComponent();
            Console.SetOut(new TextBoxWriter(textBoxConsole));
            logic.init();

#if DEBUG
            // デバッグ用: 5〜7階へ直接行くワープ（メニューの Debug。Debug ビルドのときだけ出る。Release ビルドには入らない）
            ToolStripMenuItem debugMenu = new ToolStripMenuItem("Debug");
            foreach (int f in new[] { 5, 6, 7 })
            {
                int targetFloor = f;
                debugMenu.DropDownItems.Add(targetFloor + "F へワープ", null, (s, ev) =>
                {
                    logic.ctrlDebugWarp(targetFloor);
                    afterAction();
                });
            }
            menuStrip1.Items.Add(debugMenu);
#endif

            // 地図を 1マス Dots ピクセルの大きさにする（Designer では 17ピクセル用の 340x340）。
            // 一番右の列・一番下の行の枠線（Dots*20 ピクセル目）も描けるよう、1ピクセル大きくする。
            // 広がった分だけ、地図より右の部品を右へ、ウィンドウを下へ広げ、メッセージ欄を下へ伸ばす
            int mapSize = Dots * Constant.NGRID + 1;
            int grow = mapSize - pic.Width;
            int growY = mapSize - pic.Height;
            int mapRight = pic.Right;
            foreach (Control c in this.Controls)
                if (!(c is MenuStrip) && c != pic && c.Left >= mapRight) c.Left += grow;
            pic.Size = new Size(mapSize, mapSize);
            textBoxConsole.Height += growY;
            this.ClientSize = new Size(this.ClientSize.Width, this.ClientSize.Height + growY);

            // 操作ボタン（矢印・＜＞・u i d w W t T）は、← と ＜ の左端がステータス欄の左端にそろうよう左へ寄せる
            Button[] keypad = { buttonUp, buttonDown, buttonLeft, buttonRight, buttonStairUp, buttonStairDown,
                                buttonUse, buttonInventry, buttonDrop, buttonWield, buttonWear, buttonTakeOffWeapon, buttonTakeOffArmor };
            int keypadShift = labelStatus.Left - buttonLeft.Left;
            foreach (Button b in keypad) b.Left += keypadShift;

            // ボタン・メニュー・持ち物の一覧・ステータス欄・メッセージ欄は Meiryo UI（日本語が読みやすい）。
            // フォーム自体の Font を変えると AutoScaleMode.Font で部品の大きさまで変わるので、部品ごとに設定する
            foreach (Control c in this.Controls)
                if (c != pic) c.Font = UiFont;
            SetMenuFont(menuStrip1.Items);
            // ステータス欄は1行が高くなるので、ステータス欄（8行）の下にメッセージ欄が重ならないよう、
            // メッセージ欄の上端を下げる（下端はそのまま）
            int consoleTop = labelStatus.Top + labelStatus.Font.Height * 8 + 6;
            if (textBoxConsole.Top < consoleTop)
            {
                int bottom = textBoxConsole.Bottom;
                textBoxConsole.Top = consoleTop;
                textBoxConsole.Height = bottom - consoleTop;
            }

            // 戦闘ビューを画面左に追加し、既存のコントロールを右へずらす
            int shift = BattleWidth + 12;
            foreach (Control c in this.Controls)
                if (!(c is MenuStrip)) c.Left += shift;
            battlePic = new PictureBox();
            battlePic.Location = new Point(12, 25);
            battlePic.Size = new Size(BattleWidth, pic.Height);
            battlePic.BackColor = Color.Black;
            this.Controls.Add(battlePic);
            // ウィンドウの幅は、一番右の部品（メッセージ欄など）の右に 12 ピクセルの余白を残す
            int right = 0;
            foreach (Control c in this.Controls)
                if (!(c is MenuStrip) && c != listBoxItemlist) right = Math.Max(right, c.Right);
            this.ClientSize = new Size(right + 12, this.ClientSize.Height);
            battleView = new BattleView(battlePic, logic);
            battleView.Play(new List<CombatEvent>());

            // 物理キーでも操作できるよう、どのボタンにフォーカスがあってもフォームが先にキーを受け取る
            this.KeyPreview = true;

            // 持ち物の一覧はボタン（＜ など）に重なる位置にあるので、一番手前に出す
            // （Designer では一覧よりボタンを先に追加しており、先に追加した部品ほど手前に表示されるため）
            listBoxItemlist.BringToFront();

            magicTimer = new System.Windows.Forms.Timer();
            magicTimer.Interval = 1000;
            magicTimer.Tick += (s, ev) =>
            {
                magicTimer.Stop();
                logic.magicEffects.Clear();
                show();
            };

            show();
        }

        private static void SetMenuFont(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.Font = UiFont;
                if (item is ToolStripMenuItem m) SetMenuFont(m.DropDownItems);
            }
        }

        private static int entityPriority(Entity e)
        {
            if (e.graph == '@' && !e.isCompanion) return 4; // Hero
            if (e.graph == '@' && e.isCompanion)  return 3; // Companion
            if (e is Gem)                          return 0; // 宝石はアイテム扱い（hit=1だが生物ではない）
            if (e is Altar)                        return 0; // 祭壇はアイテム扱い
            if (e.hit > 0)                         return 2; // 生きている敵
            if (e.graph == '%')                    return 1; // 死体
            return 0;                                        // アイテム
        }

        private void show(){
            //描画先とするImageオブジェクトを作成する
            Bitmap canvas = new Bitmap(pic.Width, pic.Height);
            //ImageオブジェクトのGraphicsオブジェクトを作成する
            Graphics g = Graphics.FromImage(canvas);
            // 下地を地図欄の背景色で塗る。透明のままだと文字のふちが「透明な黒」と混ざり、黒い縁取りが出る
            g.Clear(pic.BackColor);

            // 空間□と壁■を描画する
            for (int y = 0; y < Constant.NGRID; y++)
            {
                for (int x = 0; x < Constant.NGRID; x++)
                {
                    if (logic.maze.isVisible(x, y)) // 見えるところだけ描画
                    {

                        if (logic.maze.isPit(x, y))
                        {
                            // 穴タイル：暗い灰色で塗りつぶし
                            g.FillRectangle(Brushes.DarkSlateGray, Dots * x, Dots * y, Dots, Dots);
                        }
                        else if (logic.maze.isWall(x, y))
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
            // 同じマスに複数エンティティがいる場合、優先度の高いものだけ描画する
            // 文字はマスに収まるようピクセルで大きさを決め、マスの中央に描く
            // （ポイント指定だと画面では大きくなり、_ などが下のマスにはみ出していた）
            // Consolas の太字（日本語フォントだと \ が ¥ になり、魔法の斜めの記号が崩れる）
            Font fnt = new Font("Consolas", Dots * 0.9f, FontStyle.Bold, GraphicsUnit.Pixel);
            StringFormat cellFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            Func<int, int, RectangleF> cell = (cx, cy) => new RectangleF(Dots * cx, Dots * cy, Dots, Dots);
            Dictionary<string, Entity> cellTop = new Dictionary<string, Entity>();
            foreach (Entity e in logic.entitylist)
            {
                bool seeable = logic.isEntitySeeable(e);
                // 階段は一度見たら遠ざかっても表示し続ける
                if (!seeable && (e.graph == '>' || e.graph == '<' || e.graph == '_')) seeable = logic.maze.isVisible(e.xpos, e.ypos);
                if (!seeable) continue;
                string key = e.xpos + "," + e.ypos;
                if (!cellTop.ContainsKey(key) || entityPriority(e) > entityPriority(cellTop[key]))
                    cellTop[key] = e;
            }
            foreach (Entity e in cellTop.Values)
            {
                if (e is Gem gem)
                {
                    using (Brush brush = new SolidBrush(gem.DisplayColor))
                        g.DrawString(e.graph.ToString(), fnt, brush, cell(e.xpos, e.ypos), cellFormat);
                }
                else if (e is Altar altar)
                {
                    System.Drawing.Color altarColor = altar.embeddedGem != null
                        ? altar.embeddedGem.DisplayColor
                        : System.Drawing.Color.Gray;
                    using (Brush brush = new SolidBrush(altarColor))
                        g.DrawString("_", fnt, brush, cell(e.xpos, e.ypos), cellFormat);
                }
                else
                {
                    Brush brush = e.isCompanion ? Brushes.Blue : Brushes.Red;
                    g.DrawString(e.graph.ToString(), fnt, brush, cell(e.xpos, e.ypos), cellFormat);
                }
            }

            // 魔法エフェクトを描画する（シアン色）
            foreach (MagicEffect ef in logic.magicEffects)
            {
                for (int step = 1; step <= 2; step++)
                {
                    int ex = ef.fromX + step * ef.dx;
                    int ey = ef.fromY + step * ef.dy;
                    if (ex < 0 || ex >= Constant.NGRID || ey < 0 || ey >= Constant.NGRID) break;
                    if (logic.maze.isWall(ex, ey)) break;
                    if (logic.maze.isVisible(ex, ey))
                        g.DrawString(ef.symbol.ToString(), fnt, Brushes.Cyan, cell(ex, ey), cellFormat);
                }
            }

            //リソースを解放する
            fnt.Dispose();
            cellFormat.Dispose();
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
            labelStatus.Text += "Floor  = " + logic.floor + "\n";
            for (int i = 0; i < logic.companions.Count; i++)
            {
                Companion c = logic.companions[i] as Companion;
                if (c == null) continue;
                if (c.isInactive)
                {
                    labelStatus.Text += "COM" + (i + 1) + " (別フロア)\n";
                    continue;
                }
                labelStatus.Text += "COM" + (i + 1) + " HP= " + c.hit + "/" + c.hitmax
                                  + " MP= " + c.mp + "/" + c.mpmax + "\n";
            }

            // 宝石クエスト状況（1行でコンパクト表示）
            GemQuest q = logic.gemQuest;
            labelStatus.Text += "宝石(春夏秋冬)："
                              + (q.roseQuartzEmbedded  ? "★" : "☆")
                              + (q.sapphireEmbedded    ? "★" : "☆")
                              + (q.amberEmbedded       ? "★" : "☆")
                              + (q.aquamarineEmbedded  ? "★" : "☆")
                              + (q.IsCompleted ? " 完了！" : "") + "\n";

            // REVISIT 2015/09/06 Logic logic 中でゲームオーバー判定したほうが良いのだけど…
            if (logic.hero.hit <= 0)
            {
                ShowInfo("Rogue Like", new[,] { { "やられた", "" } });
                // スタティックコンストラクタを再実行したいので、logic.init() ではなく、アプリケーション再実行する
                Application.Restart();
//                logic.init();
//                show();
            }
        }

        private void afterAction()
        {
            // show() はゲームオーバー時にダイアログを出すので、先に戦闘アニメを開始しておく
            battleView.Play(CombatLog.Drain());
            show();
            if (logic.magicEffects.Count > 0)
            {
                magicTimer.Stop();
                magicTimer.Start();
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
            afterAction();
        }

        private void buttonLeft_Click(object sender, EventArgs e)
        {
            logic.ctrlLeft();
            afterAction();
        }

        private void buttonRight_Click(object sender, EventArgs e)
        {
            logic.ctrlRight();
            afterAction();
        }

        private void buttonDown_Click(object sender, EventArgs e)
        {
            logic.ctrlDown();
            afterAction();
        }

        private void buttonStairDown_Click(object sender, EventArgs e)
        {
            logic.ctrlStairDown();
            afterAction();
        }

        private void buttonStairUp_Click(object sender, EventArgs e)
        {
            logic.ctrlStairUp();
            afterAction();
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
            afterAction();
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
            afterAction();
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
            afterAction();
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
            afterAction();
        }

        private void buttonTakeOffWeapon_Click(object sender, EventArgs e)
        {
            logic.ctrlTakeOffWeapon();
            afterAction();
        }

        private void buttonTakeOffArmor_Click(object sender, EventArgs e)
        {
            logic.ctrlTakeOffArmor();
            afterAction();
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
            afterAction();
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            logic.ctrlLoad();
            afterAction();
        }

        private void toolStripMenuItemHelp_Click(object sender, EventArgs e)
        {
            ShowInfo("roguelikeのヘルプ", new[,] {
                { "↑ ↓ ← →", "上下左右へ移動（テンキーの 8 2 4 6 ＝NumLock 解除時、h j k l でも移動できる）" },
                { "> <", "下り階段・上り階段" },
                { "u", "アイテムを使う（i で表示＆選択してから）\n4階の祭壇の上では、選んだ宝石を祭壇にはめ込む" },
                { "i", "アイテムの一覧表示・非表示の切り替え（u, d, w, W の前に表示と選択）\n一覧が開いている間は ↑↓（k j）で選び、Enter で使う、Esc か i で閉じる" },
                { "d", "アイテムを落とす" },
                { "w", "武器を構える" },
                { "W", "鎧を着る" },
                { "t", "武器を解除する" },
                { "T", "鎧を脱ぐ" },
            });
        }

        // 画面の部品と同じフォントで知らせを出す（MessageBox は Windows 標準のフォントになり、桁もそろわないため）。
        // rows は「キー」「説明」の2列の表。1列だけの知らせは、説明の列を空にする
        private void ShowInfo(string title, string[,] rows)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = title;
                dlg.Font = UiFont;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.AutoSize = true;
                dlg.AutoSizeMode = AutoSizeMode.GrowAndShrink;

                TableLayoutPanel table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(12, 12, 12, 4) };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                Font keyFont = new Font("Consolas", 10, FontStyle.Bold);   // キーは地図と同じ Consolas（記号が崩れない）
                for (int i = 0; i < rows.GetLength(0); i++)
                {
                    table.Controls.Add(new Label { Text = rows[i, 0], AutoSize = true, Font = rows[i, 1] == "" ? UiFont : keyFont, Margin = new Padding(3, 4, 12, 4) }, 0, i);
                    table.Controls.Add(new Label { Text = rows[i, 1], AutoSize = true, Margin = new Padding(3, 4, 3, 4) }, 1, i);
                }
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Font = UiFont, Size = new Size(80, 28), Anchor = AnchorStyles.Right, Margin = new Padding(3, 8, 3, 8) };
                table.Controls.Add(ok, 1, rows.GetLength(0));
                dlg.Controls.Add(table);
                dlg.AcceptButton = ok;
                dlg.ShowDialog(this);
            }
        }

        //
        // キーボード操作（画面のボタンと同じ処理を呼ぶ）
        //

        // 矢印キー（NumLock を解除したテンキーの 8・2・4・6 も同じキーとして届く）と Esc。
        // 矢印キーはボタン間のフォーカス移動に使われてしまうので、ここで先に受け取る
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & (Keys.Control | Keys.Alt)) == 0)
            {
                switch (keyData & Keys.KeyCode)
                {
                    case Keys.Up: MoveKey(0); return true;
                    case Keys.Down: MoveKey(1); return true;
                    case Keys.Left: MoveKey(2); return true;
                    case Keys.Right: MoveKey(3); return true;
                    case Keys.Escape:
                        if (listBoxItemlist.Visible) { listBoxItemlist.Hide(); return true; }
                        break;
                    // Enter・Space は、そのままだとフォーカスが残っているボタン（最後にクリックしたボタン）を押してしまうので受け取る。
                    // 持ち物の一覧が開いているときの Enter は、選んだ物を使う（u と同じ）
                    case Keys.Enter:
                        if (listBoxItemlist.Visible) buttonUse_Click(this, EventArgs.Empty);
                        return true;
                    case Keys.Space:
                        return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // 文字キー（KeyPreview で、どのボタンにフォーカスがあってもフォームが先に受け取る）。大文字・小文字を区別する
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.Handled || ModifierKeys.HasFlag(Keys.Control) || ModifierKeys.HasFlag(Keys.Alt)) return;
            e.Handled = true;
            switch (e.KeyChar)
            {
                case 'k': MoveKey(0); break;
                case 'j': MoveKey(1); break;
                case 'h': MoveKey(2); break;
                case 'l': MoveKey(3); break;
                case '>': buttonStairDown_Click(this, EventArgs.Empty); break;
                case '<': buttonStairUp_Click(this, EventArgs.Empty); break;
                case 'i':
                    buttonInventory_Click(this, EventArgs.Empty);
                    // キーで開いたときは先頭の物を選んだ状態にする（↑↓ で選び直せる）
                    if (listBoxItemlist.Visible && listBoxItemlist.Items.Count > 0) listBoxItemlist.SelectedIndex = 0;
                    break;
                case 'u': buttonUse_Click(this, EventArgs.Empty); break;
                case 'd': buttonDrop_Click(this, EventArgs.Empty); break;
                case 'w': buttonWield_Click(this, EventArgs.Empty); break;
                case 'W': buttonWear_Click(this, EventArgs.Empty); break;
                case 't': buttonTakeOffWeapon_Click(this, EventArgs.Empty); break;
                case 'T': buttonTakeOffArmor_Click(this, EventArgs.Empty); break;
                default: e.Handled = false; break;
            }
        }

        // 方向キー 0=上 1=下 2=左 3=右。持ち物の一覧が開いている間は、上下で一覧の選択を動かし Hero は動かさない
        private void MoveKey(int dir)
        {
            if (listBoxItemlist.Visible)
            {
                int n = listBoxItemlist.Items.Count;
                if (n == 0 || dir > 1) return;
                int i = listBoxItemlist.SelectedIndex;
                if (dir == 0) i = (i <= 0) ? 0 : i - 1;
                else i = (i < 0) ? 0 : Math.Min(n - 1, i + 1);
                listBoxItemlist.SelectedIndex = i;
                return;
            }
            switch (dir)
            {
                case 0: buttonUp_Click(this, EventArgs.Empty); break;
                case 1: buttonDown_Click(this, EventArgs.Empty); break;
                case 2: buttonLeft_Click(this, EventArgs.Empty); break;
                case 3: buttonRight_Click(this, EventArgs.Empty); break;
            }
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
