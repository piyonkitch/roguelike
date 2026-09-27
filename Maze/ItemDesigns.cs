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
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Maze
{
    //
    // 戦闘ビューに出す「物」の絵（拾えるもの・祭壇・落とし穴・階段・岩の壁）
    // 座標系は Figure と同じ（足元中心が原点、右向き、Hero の身長が 100）
    // 新しいアイテムを追加したら Create() に絵を追加すること（未登録は null＝戦闘ビューに出ない）
    //
    static class ItemDesigns
    {
        public static Figure Create(Entity e)
        {
            Figure f = null;
            if (e is MolyRoot) f = new MolyFig();
            else if (e is Potion p) f = new PotionFig(PotionColor(p.appearance));
            else if (e is Scroll s) f = new ScrollFig(s.label);
            else if (e is Gem gm) f = new GemFig(gm.DisplayColor, gm.isLarge);
            else if (e is Gold gd) f = new GoldFig(gd.hit);
            else if (e is Weapon w) f = new WeaponItemFig(WeaponArt.KindOf(w), w.engraveName == "Sting", w.isRust);
            else if (e is Armor ar) f = new ArmorFig(ar.name, ar.isRust);
            else if (e is Altar al) f = new AltarFig(al);
            else if (IsCorpse(e)) f = new CorpseFig(e);
            if (f != null) f.entity = e;
            return f;
        }

        // 床に落ちていて拾えるもの（死体・モーリュの根を含む）
        public static bool IsPickable(Entity e)
        {
            return "$%!?)[*".IndexOf(e.graph) >= 0;
        }

        // 生き物の死体（表示記号が % に変わった、HP 0 の生き物）
        public static bool IsCorpse(Entity e)
        {
            return e.hit <= 0 && !(e is MolyRoot) && !(e is Gold) && !(e is Weapon) && !(e is Armor)
                && !(e is Potion) && !(e is Scroll) && !(e is Gem) && !(e is Altar);
        }

        // 戦闘ビューで物の下に出す名前（識別状態に従う今の名前）
        public static string LabelFor(Entity e)
        {
            if (e is Gold) return "$" + e.hit;
            if (e is Altar) return "祭壇";
            if (IsCorpse(e)) return e.name + "の死体";
            return e.name;
        }

        // 未識別名（Green Potion など）の色。識別後も瓶の色は変わらない（見た目の性質なので）
        public static Color PotionColor(string appearance)
        {
            if (appearance == null) return Color.Gray;
            if (appearance.StartsWith("Green")) return Color.FromArgb(60, 200, 90);
            if (appearance.StartsWith("Red")) return Color.FromArgb(225, 40, 45);
            if (appearance.StartsWith("Gold")) return Color.FromArgb(245, 200, 40);
            if (appearance.StartsWith("Black")) return Color.FromArgb(25, 25, 30);
            if (appearance.StartsWith("Purple")) return Color.FromArgb(150, 60, 210);
            return Color.Gray;
        }

        public static Color Lighten(Color c, float k)
        {
            return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * k), (int)(c.G + (255 - c.G) * k), (int)(c.B + (255 - c.B) * k));
        }

        public static Color Blend(Color a, Color b, float k)
        {
            return Color.FromArgb(a.A, (int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
        }
    }

    // ================================================================
    // 拾えるもの
    // ================================================================

    // ポーション: 丸いフラスコ。液体の色が未識別名の色
    class PotionFig : Figure
    {
        readonly Color liquid;
        public PotionFig(Color liquid) { this.liquid = liquid; height = 0.3f; }
        public override float Top { get { return -30f; } }
        public override float CenterY { get { return -12f; } }
        public override float HalfWidth { get { return 11f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            PointF c = new PointF(0, -10);
            float R = 9.5f;
            // 液体（揺れる）。瓶の丸い部分で切り抜く
            float level = c.Y - R * 0.3f + (float)Math.Sin(a.time * 3) * 1.2f;
            GraphicsState st = g.Save();
            using (GraphicsPath clip = new GraphicsPath())
            {
                clip.AddEllipse(c.X - R, c.Y - R, R * 2, R * 2);
                g.SetClip(clip, CombineMode.Intersect);
            }
            using (Brush b = new SolidBrush(Color.FromArgb(40, 230, 240, 255))) g.FillEllipse(b, c.X - R, c.Y - R, R * 2, R * 2);
            using (Brush b = new SolidBrush(Tone(liquid, a))) g.FillRectangle(b, -R, level, R * 2, R * 2);
            using (Pen p = MakePen(Tone(ItemDesigns.Lighten(liquid, 0.4f), a), 1f)) g.DrawLine(p, -R, level, R, level);
            g.Restore(st);

            Color glass = Tone(Color.FromArgb(220, 210, 230, 245), a);
            // 首とコルク栓
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(60, 230, 240, 255), a))) g.FillRectangle(b, -3, -26, 6, 8);
            using (Pen p = MakePen(glass, 1.2f)) g.DrawRectangle(p, -3, -26, 6, 8);
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(160, 110, 60), a))) g.FillRectangle(b, -3.5f, -30, 7, 4.5f);
            Ring(g, glass, 1.5f, c, R);
            // ハイライト
            using (Pen p = MakePen(Tone(Color.FromArgb(200, 255, 255, 255), a), 1.5f))
                g.DrawArc(p, c.X - R * 0.7f, c.Y - R * 0.7f, R * 1.4f, R * 1.4f, 200, 50);
        }
    }

    // 巻物: 丸めた羊皮紙に封蝋。紙の上にラベル文字
    class ScrollFig : Figure
    {
        readonly string label;
        public ScrollFig(string label) { this.label = label; height = 0.2f; }
        public override float Top { get { return -18f; } }
        public override float CenterY { get { return -9f; } }
        public override float HalfWidth { get { return 15f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            Color paper = Tone(Color.FromArgb(238, 225, 188), a);
            Color dark = Tone(Color.FromArgb(185, 160, 115), a);
            using (Brush b = new SolidBrush(paper)) g.FillRectangle(b, -13, -16, 26, 13);
            using (Pen p = MakePen(dark, 1f)) g.DrawRectangle(p, -13, -16, 26, 13);
            using (Brush b = new SolidBrush(dark))
            {
                g.FillEllipse(b, -16, -17, 6, 15);
                g.FillEllipse(b, 10, -17, 6, 15);
            }
            using (Pen p = MakePen(Tone(Color.FromArgb(150, 125, 85), a), 0.8f))
            {
                g.DrawEllipse(p, -16, -17, 6, 15);
                g.DrawEllipse(p, 10, -17, 6, 15);
            }
            if (!string.IsNullOrEmpty(label)) Text(g, label, 5.5f, Tone(Color.FromArgb(90, 45, 20), a), new PointF(-1, -9.5f), a);
            // 封蝋
            Circle(g, Tone(Color.FromArgb(185, 30, 35), a), new PointF(8, -4), 2.6f);
        }
    }

    // 宝石: カットした宝石。色は DisplayColor、大きな原石は大きく光る
    class GemFig : Figure
    {
        readonly Color color;
        readonly bool large;
        public GemFig(Color color, bool large) { this.color = color; this.large = large; height = 0.25f; }
        float Size { get { return large ? 11f : 7f; } }
        public override float Top { get { return -2.2f * Size - 4; } }
        public override float CenterY { get { return -1.1f * Size; } }
        public override float HalfWidth { get { return Size + 2; } }

        protected override void Draw(Graphics g, Anim a)
        {
            float s = Size;
            Color baseC = Tone(color, a);
            Color light = Tone(ItemDesigns.Lighten(color, 0.45f), a);
            Color dark = Tone(Darken(color, 0.6f), a);
            if (large)
            {
                using (GraphicsPath halo = new GraphicsPath())
                {
                    halo.AddEllipse(-s * 2, -s * 2.6f, s * 4, s * 3.2f);
                    using (PathGradientBrush b = new PathGradientBrush(halo))
                    {
                        b.CenterColor = Color.FromArgb((int)(110 * a.alpha), color);
                        b.SurroundColors = new[] { Color.FromArgb(0, color) };
                        g.FillPath(b, halo);
                    }
                }
            }
            PointF tl = new PointF(-0.55f * s, -1.9f * s), tr = new PointF(0.55f * s, -1.9f * s);
            PointF gl = new PointF(-s, -1.4f * s), gr = new PointF(s, -1.4f * s), tip = new PointF(0, -0.1f * s);
            Poly(g, light, tl, tr, gr, gl);                 // 上面（クラウン）
            Poly(g, baseC, gl, gr, tip);                     // 下面（パビリオン）
            Poly(g, dark, new PointF(0, -1.4f * s), gr, tip);
            Color edge = Tone(Darken(color, 0.45f), a);
            using (Pen p = MakePen(edge, 0.9f))
            {
                g.DrawPolygon(p, new[] { tl, tr, gr, tip, gl });
                g.DrawLine(p, gl, gr);
                g.DrawLine(p, tl, new PointF(-0.3f * s, -1.4f * s));
                g.DrawLine(p, tr, new PointF(0.3f * s, -1.4f * s));
                g.DrawLine(p, new PointF(-0.3f * s, -1.4f * s), tip);
                g.DrawLine(p, new PointF(0.3f * s, -1.4f * s), tip);
            }
            // きらめき
            float tw = (float)Math.Abs(Math.Sin(a.time * 3 + s)) * 3.5f;
            PointF sp = new PointF(0.45f * s, -1.95f * s);
            Color wc = Color.FromArgb((int)(230 * a.alpha), 255, 255, 255);
            Line(g, wc, 1f, new PointF(sp.X - tw, sp.Y), new PointF(sp.X + tw, sp.Y));
            Line(g, wc, 1f, new PointF(sp.X, sp.Y - tw), new PointF(sp.X, sp.Y + tw));
        }
    }

    // 金貨の山。枚数（額）で山の高さが変わる
    class GoldFig : Figure
    {
        readonly int coins;
        public GoldFig(int amount) { coins = Math.Max(2, Math.Min(7, amount / 2)); height = 0.15f; }
        public override float Top { get { return -16f; } }
        public override float CenterY { get { return -6f; } }
        public override float HalfWidth { get { return 13f; } }

        static readonly PointF[] Pile = {
            new PointF(0, 0), new PointF(-7, 0), new PointF(7, 0), new PointF(-3.5f, -3), new PointF(3.5f, -3), new PointF(0, -6), new PointF(0, -9),
        };

        protected override void Draw(Graphics g, Anim a)
        {
            Color gold = Tone(Color.FromArgb(255, 210, 40), a);
            Color rim = Tone(Color.FromArgb(170, 120, 10), a);
            Color shine = Tone(Color.FromArgb(255, 245, 170), a);
            for (int i = 0; i < coins; i++)
            {
                PointF p = Pile[i];
                using (Brush b = new SolidBrush(gold)) g.FillEllipse(b, p.X - 4.5f, p.Y - 4.5f, 9, 4);
                using (Pen pen = MakePen(rim, 0.9f)) g.DrawEllipse(pen, p.X - 4.5f, p.Y - 4.5f, 9, 4);
                Line(g, shine, 0.8f, new PointF(p.X - 2, p.Y - 3.4f), new PointF(p.X + 1, p.Y - 3.8f));
            }
        }
    }

    // 地面に置かれた武器（手に持つ武器と同じ描き分け。錆びは赤茶色、Sting は青白い縁取り）
    class WeaponItemFig : Figure
    {
        readonly WeaponKind kind;
        readonly bool sting, rust;
        public WeaponItemFig(WeaponKind kind, bool sting, bool rust) { this.kind = kind; this.sting = sting; this.rust = rust; height = 0.15f; }
        float Len { get { return Humanoid.WeaponLength(kind) * 1.15f; } }
        public override float Top { get { return -14f; } }
        public override float CenterY { get { return -5f; } }
        public override float HalfWidth { get { return Len * 0.5f + 4; } }

        protected override void Draw(Graphics g, Anim a)
        {
            WeaponArt.Draw(g, kind, new PointF(-Len * 0.45f, -3), 1.45f, 1.15f, a, sting, rust, Color.Empty);
        }
    }

    // 防具: 胸当て。種類で模様を描き分け、錆びは赤茶色
    class ArmorFig : Figure
    {
        readonly string kindName;
        readonly bool rust;
        public ArmorFig(string name, bool rust) { kindName = name ?? ""; this.rust = rust; height = 0.3f; }
        public override float Top { get { return -30f; } }
        public override float CenterY { get { return -15f; } }
        public override float HalfWidth { get { return 13f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            PointF[] shape = {
                new PointF(-12, -26), new PointF(-6, -28), new PointF(6, -28), new PointF(12, -26),
                new PointF(13, -17), new PointF(10, -3), new PointF(-10, -3), new PointF(-13, -17),
            };
            Color baseC;
            if (kindName.Contains("Leather")) baseC = Color.FromArgb(139, 90, 43);
            else if (kindName.Contains("Ring")) baseC = Color.FromArgb(150, 150, 160);
            else if (kindName.Contains("Scale")) baseC = Color.FromArgb(125, 135, 145);
            else if (kindName.Contains("Chain")) baseC = Color.FromArgb(170, 170, 180);
            else if (kindName.Contains("Banded")) baseC = Color.FromArgb(180, 185, 195);
            else baseC = Color.FromArgb(205, 210, 220);   // Plate Mail
            if (rust) baseC = ItemDesigns.Blend(baseC, Color.FromArgb(150, 80, 50), 0.55f);
            Color fill = Tone(baseC, a);
            Color line = Tone(Darken(baseC, 0.55f), a);
            Color hi = Tone(ItemDesigns.Lighten(baseC, 0.35f), a);

            Poly(g, fill, shape);
            GraphicsState st = g.Save();
            using (GraphicsPath clip = new GraphicsPath())
            {
                clip.AddPolygon(shape);
                g.SetClip(clip, CombineMode.Intersect);
            }
            using (Pen p = MakePen(line, 0.8f))
            {
                if (kindName.Contains("Leather"))
                {
                    p.DashStyle = DashStyle.Dash;
                    g.DrawLine(p, -6, -26, -6, -4);
                    g.DrawLine(p, 6, -26, 6, -4);
                }
                else if (kindName.Contains("Ring"))
                {
                    for (float y = -24; y < -3; y += 4)
                        for (float x = -12 + (((int)(y / 4)) % 2 == 0 ? 0 : 2); x < 13; x += 4)
                            g.DrawEllipse(p, x - 1.4f, y - 1.4f, 2.8f, 2.8f);
                }
                else if (kindName.Contains("Scale"))
                {
                    for (float y = -26; y < -3; y += 3.5f)
                        for (float x = -13 + (((int)(y / 3.5f)) % 2 == 0 ? 0 : 2); x < 13; x += 4)
                            g.DrawArc(p, x - 2, y - 1, 4, 4, 0, 180);
                }
                else if (kindName.Contains("Chain"))
                {
                    for (float k = -40; k < 40; k += 3)
                    {
                        g.DrawLine(p, k, -30, k + 30, 0);
                        g.DrawLine(p, k, -30, k - 30, 0);
                    }
                }
                else if (kindName.Contains("Banded"))
                {
                    using (Brush b = new SolidBrush(line))
                        for (float y = -23; y < -4; y += 6) g.FillRectangle(b, -14, y, 28, 2.2f);
                }
                else
                {
                    Line(g, hi, 2.2f, new PointF(0, -27), new PointF(0, -4));   // 板金の稜線
                    Line(g, hi, 1.2f, new PointF(-8, -24), new PointF(-7, -8));
                }
            }
            g.Restore(st);
            using (Pen p = MakePen(line, 1.2f)) g.DrawPolygon(p, shape);
            using (Pen p = MakePen(line, 1.4f)) g.DrawArc(p, -6, -31, 12, 7, 0, 180);   // 首まわり
        }
    }

    // 生き物の死体: その生き物が倒れた姿を小さく描く
    class CorpseFig : Figure
    {
        readonly Figure inner;
        public CorpseFig(Entity creature) { inner = EnemyDesigns.Create(creature, 0); height = 0.3f; }
        public override float Top { get { return -20f; } }
        public override float CenterY { get { return -6f; } }
        public override float HalfWidth { get { return 28f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            GraphicsState st = g.Save();
            g.ScaleTransform(0.55f, 0.55f);
            inner.Render(g, new Anim { time = a.time, dir = a.dir, fall = 1, recoil = 1, alpha = a.alpha });
            g.Restore(st);
        }
    }

    // モーリュの根: 黒い根に乳白色の花（『オデュッセイア』第10歌）
    class MolyFig : Figure
    {
        public MolyFig() { height = 0.3f; }
        public override float Top { get { return -30f; } }
        public override float CenterY { get { return -14f; } }
        public override float HalfWidth { get { return 10f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            Color root = Tone(Color.FromArgb(35, 30, 30), a);
            using (Brush b = new SolidBrush(root)) g.FillEllipse(b, -5, -7, 10, 6);
            Line(g, root, 1.2f, new PointF(-3, -3), new PointF(-9, 0));
            Line(g, root, 1.2f, new PointF(2, -3), new PointF(8, 1));
            Line(g, root, 1f, new PointF(0, -2), new PointF(1, 1));
            Color stem = Tone(Color.FromArgb(80, 135, 70), a);
            Line(g, stem, 1.5f, new PointF(0, -6), new PointF(1, -22));
            Poly(g, stem, new PointF(0, -12), new PointF(-8, -16), new PointF(-1, -15));
            Poly(g, stem, new PointF(1, -16), new PointF(8, -20), new PointF(1, -19));
            PointF fc = new PointF(1, -24);
            float sway = (float)Math.Sin(a.time * 2) * 0.8f;
            fc = new PointF(fc.X + sway, fc.Y);
            Color petal = Tone(Color.FromArgb(250, 248, 235), a);
            for (int i = 0; i < 5; i++) Circle(g, petal, Add(fc, Dir(i * 1.2566f, 3f)), 2.6f);
            Circle(g, Tone(Color.FromArgb(240, 210, 90), a), fc, 1.6f);
        }
    }

    // ================================================================
    // 祭壇・落とし穴・階段・岩の壁（Entity ではないものも含む）
    // ================================================================

    // 祭壇: 石の台座。空なら灰色のくぼみ、嵌め込み済みなら宝石の色に光る
    class AltarFig : Figure
    {
        readonly Altar altar;
        public AltarFig(Altar altar) { this.altar = altar; height = 0.36f; }
        public override float Top { get { return -36f; } }
        public override float CenterY { get { return -20f; } }
        public override float HalfWidth { get { return 19f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(115, 110, 105), a))) g.FillRectangle(b, -17, -6, 34, 6);
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(140, 135, 128), a))) g.FillRectangle(b, -10, -28, 20, 22);
            using (Pen p = MakePen(Tone(Color.FromArgb(110, 105, 100), a), 0.8f))
            {
                g.DrawLine(p, -5, -27, -5, -7);
                g.DrawLine(p, 5, -27, 5, -7);
            }
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(165, 160, 152), a))) g.FillRectangle(b, -19, -34, 38, 6);

            PointF sock = new PointF(0, -18);
            bool embedded = altar != null && altar.embeddedGem != null && !a.before;
            if (embedded)
            {
                Color gc = altar.embeddedGem.DisplayColor;
                float pulse = 0.7f + 0.3f * (float)Math.Sin(a.time * 3);
                Circle(g, Color.FromArgb((int)(120 * pulse * a.alpha), gc), sock, 8f);
                Circle(g, Tone(gc, a), sock, 4.5f);
                Circle(g, Color.FromArgb((int)(200 * a.alpha), 255, 255, 255), new PointF(sock.X - 1.5f, sock.Y - 1.5f), 1.2f);
            }
            else
            {
                Circle(g, Tone(Color.FromArgb(55, 52, 50), a), sock, 4.5f);
            }
        }
    }

    // 落とし穴: 地面に開いた黒い穴
    class PitFig : Figure
    {
        public PitFig() { height = 0.15f; }
        public override float Top { get { return -12f; } }
        public override float CenterY { get { return -3f; } }
        public override float HalfWidth { get { return 30f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(62, 54, 46), a))) g.FillEllipse(b, -30, -8, 60, 14);
            using (GraphicsPath hole = new GraphicsPath())
            {
                hole.AddEllipse(-25, -6.5f, 50, 11);
                using (PathGradientBrush b = new PathGradientBrush(hole))
                {
                    b.CenterColor = Color.FromArgb((int)(255 * a.alpha), 0, 0, 0);
                    b.SurroundColors = new[] { Tone(Color.FromArgb(35, 30, 26), a) };
                    g.FillPath(b, hole);
                }
            }
            Color stone = Tone(Color.FromArgb(120, 115, 108), a);
            float[] xs = { -27, -18, -6, 8, 19, 27 };
            for (int i = 0; i < xs.Length; i++)
                using (Brush b = new SolidBrush(stone)) g.FillEllipse(b, xs[i] - 3, (i % 2 == 0 ? -8 : 3) - 1.5f, 6, 3.5f);
        }
    }

    // カリュブディス: 海面で回転する渦
    class CharybdisFig : Figure
    {
        public CharybdisFig() { height = 0.2f; }
        public override float Top { get { return -14f; } }
        public override float CenterY { get { return -3f; } }
        public override float HalfWidth { get { return 38f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(20, 62, 105), a))) g.FillEllipse(b, -38, -10, 76, 18);
            for (int arm = 0; arm < 3; arm++)
            {
                PointF[] pts = new PointF[16];
                for (int i = 0; i < pts.Length; i++)
                {
                    float r = 34 - i * 2.1f;
                    double th = arm * 2 * Math.PI / 3 + r * 0.18 + a.time * 3;
                    pts[i] = new PointF((float)Math.Cos(th) * r, -1 + (float)Math.Sin(th) * r * 0.24f);
                }
                using (Pen p = MakePen(Tone(Color.FromArgb(190, 170, 215, 245), a), 1.5f)) g.DrawCurve(p, pts);
            }
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(5, 10, 20), a))) g.FillEllipse(b, -7, -3.5f, 14, 5);
            for (int i = 0; i < 8; i++)
            {
                double th = i * 0.785 - a.time * 2;
                Circle(g, Tone(Color.FromArgb(200, 240, 250, 255), a), new PointF((float)Math.Cos(th) * 36, -1 + (float)Math.Sin(th) * 8.5f), 1.3f);
            }
        }
    }

    // 階段（待機画面の背景）。下り階段は暗い入口へ続く段、上り階段は明るい出口へ登る段
    class StairFig : Figure
    {
        readonly bool down;
        public StairFig(bool down) { this.down = down; height = 0.8f; }
        public override float Top { get { return -80f; } }
        public override float CenterY { get { return -40f; } }
        public override float HalfWidth { get { return 30f; } }

        protected override void Draw(Graphics g, Anim a)
        {
            Color stone = Tone(Color.FromArgb(100, 95, 100), a);
            Color stoneHi = Tone(Color.FromArgb(150, 145, 150), a);
            if (down)
            {
                using (Brush b = new SolidBrush(stone)) g.FillRectangle(b, -28, -70, 56, 70);
                using (Brush b = new SolidBrush(stone)) g.FillPie(b, -28, -92, 56, 44, 180, 180);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(-20, -80, 40, 80),
                           Tone(Color.FromArgb(40, 38, 45), a), Tone(Color.FromArgb(0, 0, 0), a), 90f))
                {
                    g.FillRectangle(b, -20, -64, 40, 64);
                    g.FillPie(b, -20, -80, 40, 32, 180, 180);
                }
                for (int i = 0; i < 6; i++)
                {
                    float y = -4 - i * 7, w = 19 - i * 2.2f;
                    Line(g, Color.FromArgb((int)((170 - i * 25) * a.alpha), 150, 145, 150), 1.5f, new PointF(-w, y), new PointF(w, y));
                }
            }
            else
            {
                PointF[] steps = new PointF[16];
                int k = 0;
                steps[k++] = new PointF(-30, 0);
                for (int i = 0; i < 7; i++)
                {
                    steps[k++] = new PointF(-30 + i * 8, -(i + 1) * 10);
                    steps[k++] = new PointF(-22 + i * 8, -(i + 1) * 10);
                }
                steps[k++] = new PointF(30, 0);
                Poly(g, stone, steps);
                for (int i = 0; i < 7; i++)
                    Line(g, stoneHi, 1.5f, new PointF(-30 + i * 8, -(i + 1) * 10), new PointF(-22 + i * 8, -(i + 1) * 10));
                using (Brush b = new SolidBrush(Tone(Color.FromArgb(230, 222, 185), a))) g.FillRectangle(b, 22, -92, 12, 22);
            }
        }
    }

    // 岩の壁（Dwarf の壁掘り）。Anim.strike をひびの量、Anim.fall を崩れる量として使う
    class WallFig : Figure
    {
        public WallFig() { height = 0.95f; }
        public override float Top { get { return -95f; } }
        public override float CenterY { get { return -45f; } }
        public override float HalfWidth { get { return 24f; } }
        protected override bool CustomDeath { get { return true; } }

        protected override void Draw(Graphics g, Anim a)
        {
            float crumble = a.fall;
            int k = 0;
            for (int row = 0; row < 8; row++)
            {
                float y = -(row + 1) * 12;
                float x0 = (row % 2 == 0) ? -24 : -32;
                for (float x = x0; x < 24; x += 16, k++)
                {
                    float bx = Math.Max(x, -24), bw = Math.Min(x + 16, 24) - bx;
                    if (bw <= 0) continue;
                    int v = (k * 37) % 25;
                    Color c = Tone(Color.FromArgb(100 + v, 92 + v, 84 + v), a);
                    GraphicsState st = g.Save();
                    if (crumble > 0)
                    {
                        // 崩れた石は地面へ落ちて散らばる
                        float ty = Lerp(0, -y - 6 - (k % 3) * 3, crumble);
                        float tx = ((k % 5) - 2) * 7 * crumble;
                        g.TranslateTransform(bx + bw / 2 + tx, y + 6 + ty);
                        g.RotateTransform(((k % 7) - 3) * 15 * crumble);
                        g.TranslateTransform(-(bx + bw / 2), -(y + 6));
                    }
                    using (Brush b = new SolidBrush(c)) g.FillRectangle(b, bx, y, bw, 12);
                    using (Pen p = MakePen(Tone(Color.FromArgb(60, 55, 50), a), 0.8f)) g.DrawRectangle(p, bx, y, bw, 12);
                    g.Restore(st);
                }
            }
            // ひび（叩かれた側＝前面から広がる）
            if (a.strike > 0 && crumble < 0.3f)
            {
                Color crack = Tone(Color.FromArgb(30, 25, 20), a);
                int n = (int)Math.Ceiling(a.strike * 4);
                for (int i = 0; i < n; i++)
                {
                    float cy = -30 - i * 13;
                    Lines(g, crack, 1.2f, new PointF(24, cy), new PointF(16, cy - 5), new PointF(10, cy + 3), new PointF(3, cy - 4));
                }
            }
        }
    }
}
