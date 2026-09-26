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
    // 戦闘ビューのキャラクターデザイン
    // 新しい敵を追加したら Create() に分岐とデザインを追加すること（未登録は GenericFig で表示される）
    //
    static class EnemyDesigns
    {
        public static Figure Create(Entity e, int companionIndex)
        {
            Figure f;
            if (e is Hero) f = new HeroFig();
            else if (e is Companion) f = new CompanionFig(companionIndex);
            else if (e is Kobold) f = new KoboldFig();
            else if (e is Orc) f = new OrcFig();
            else if (e is Bat) f = new BatFig();
            else if (e is Acid) f = new AcidFig();
            else if (e is Ice) f = new IceFig();
            else if (e is Dwarf) f = new DwarfFig();
            else if (e is Hobbit) f = new HobbitFig(e.name == "Bilbo");
            else if (e is Dragon) f = new DragonFig();
            else if (e is Siren) f = new SirenFig();
            else if (e is Polyphemus) f = new PolyphemusFig();
            else if (e is CursedSailor) f = new CursedSailorFig();
            else if (e is Scylla) f = new ScyllaFig();
            else if (e is Circe) f = new CirceFig();
            else if (e is Shade) f = new ShadeFig();
            else if (e is Teiresias) f = new TeiresiasFig();
            else f = new GenericFig();
            f.entity = e;
            return f;
        }
    }

    // ================================================================
    // パーティ
    // ================================================================

    class PartyFig : Humanoid
    {
        public override PointF Mouth { get { return new PointF(34f * height, -74f * height); } }

        protected override void DrawTorso(Graphics g, Rig r, Anim a, Color c, float w)
        {
            if (entity != null && entity.armor != null && !a.pig)
            {
                // 鎧: 胴を太く、胸当てと肩当て
                Line(g, c, w * 2.0f, r.hip, r.neckTop);
                Line(g, Tone(Color.FromArgb(170, 175, 185), a), w * 1.1f, Mid(r.hip, r.neckTop, 0.35f), Mid(r.hip, r.neckTop, 0.9f));
                Poly(g, Tone(Color.FromArgb(150, 155, 165), a),
                    new PointF(r.shoulder.X - 5, r.shoulder.Y + 1), new PointF(r.shoulder.X + 6, r.shoulder.Y - 1),
                    new PointF(r.shoulder.X + 3, r.shoulder.Y + 7));
            }
            else
            {
                Line(g, c, w, r.hip, r.neckTop);
            }
        }
    }

    class HeroFig : PartyFig
    {
        public HeroFig() { color = Color.FromArgb(230, 40, 60); }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            base.DrawHead(g, r, a, c, w);
            // 鉢巻き
            Line(g, c, w * 0.7f, new PointF(r.head.X - r.headR, r.head.Y - r.headR * 0.35f), new PointF(r.head.X - r.headR * 2.0f, r.head.Y + r.headR * 0.1f + (float)Math.Sin(a.time * 7) * 1.5f));
        }
    }

    class CompanionFig : PartyFig
    {
        public CompanionFig(int index)
        {
            color = index == 0 ? Color.FromArgb(70, 110, 235) : Color.FromArgb(40, 160, 255);
            height = 0.95f;
            defaultWeapon = WeaponKind.Staff;
            staffOrb = Color.Cyan;
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            // フード
            PointF h = r.head; float R = r.headR;
            Poly(g, Tone(Darken(color, 0.75f), a),
                new PointF(h.X + R * 0.9f, h.Y - R * 0.6f), new PointF(h.X - R * 0.3f, h.Y - R * 2.0f),
                new PointF(h.X - R * 1.5f, h.Y + R * 0.2f), new PointF(h.X - R * 0.6f, h.Y + R * 1.0f));
            base.DrawHead(g, r, a, c, w);
        }
    }

    // ================================================================
    // 1〜4階
    // ================================================================

    class KoboldFig : Humanoid
    {
        public KoboldFig()
        {
            height = 0.7f; color = Color.FromArgb(200, 150, 30); leanBias = 0.3f; defaultWeapon = WeaponKind.Knife;
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            PointF h = r.head; float R = r.headR;
            Poly(g, c, new PointF(h.X - R * 0.2f, h.Y - R * 0.7f), new PointF(h.X - R * 0.8f, h.Y - R * 1.9f), new PointF(h.X - R * 0.9f, h.Y - R * 0.4f));
            Circle(g, c, h, R);
            Poly(g, c, new PointF(h.X + R * 0.6f, h.Y - R * 0.5f), new PointF(h.X + R * 2.1f, h.Y + R * 0.25f), new PointF(h.X + R * 0.6f, h.Y + R * 0.7f));
            Circle(g, Tone(Color.FromArgb(40, 20, 10), a), new PointF(h.X + R * 2.0f, h.Y + R * 0.2f), R * 0.2f);
            Circle(g, Tone(Color.Red, a), new PointF(h.X + R * 0.45f, h.Y - R * 0.25f), R * 0.18f);
        }
    }

    class OrcFig : Humanoid
    {
        public OrcFig()
        {
            height = 1.15f; color = Color.FromArgb(110, 150, 40); thick = 5f; defaultWeapon = WeaponKind.Club;
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            PointF h = r.head; float R = r.headR;
            using (Brush b = new SolidBrush(c)) g.FillRectangle(b, h.X - R, h.Y - R * 0.95f, R * 2, R * 1.9f);
            using (Pen p = new Pen(Tone(Darken(color, 0.5f), a), 1.5f)) g.DrawRectangle(p, h.X - R, h.Y - R * 0.95f, R * 2, R * 1.9f);
            Line(g, Tone(Darken(color, 0.4f), a), 2.5f, new PointF(h.X + R * 0.1f, h.Y - R * 0.45f), new PointF(h.X + R * 1.05f, h.Y - R * 0.25f));
            Circle(g, Tone(Color.OrangeRed, a), new PointF(h.X + R * 0.6f, h.Y - R * 0.05f), R * 0.16f);
            Color tusk = Tone(Color.Ivory, a);
            Line(g, tusk, 2f, new PointF(h.X + R * 0.5f, h.Y + R * 0.85f), new PointF(h.X + R * 0.6f, h.Y + R * 0.3f));
            Line(g, tusk, 2f, new PointF(h.X + R * 0.95f, h.Y + R * 0.85f), new PointF(h.X + R * 1.05f, h.Y + R * 0.35f));
        }
    }

    class BatFig : Figure
    {
        public BatFig() { height = 0.4f; }
        public override float Top { get { return -104f; } }
        public override float CenterY { get { return -78f; } }
        public override float HalfWidth { get { return 30f; } }
        public override PointF Mouth { get { return new PointF(8f, -78f); } }
        protected override bool CustomDeath { get { return true; } }

        protected override void Draw(Graphics g, Anim a)
        {
            float alt = -78f + (float)Math.Sin(a.time * 4) * 4f - a.windup * 12f + a.strike * 52f;
            alt = Lerp(alt, -5f, a.fall);
            float flap = a.fall > 0 ? -0.3f * a.fall : (float)Math.Sin(a.time * 22);
            PointF b = new PointF(0, alt);

            Color wing = Tone(Color.FromArgb(80, 35, 115), a);
            Color rim = Tone(Color.FromArgb(170, 130, 210), a);
            for (int s = -1; s <= 1; s += 2)
            {
                PointF[] pts = {
                    new PointF(b.X + s * 4, b.Y - 2),
                    new PointF(b.X + s * 14, b.Y - 9 - 10 * flap),
                    new PointF(b.X + s * 27, b.Y - 5 - 13 * flap),
                    new PointF(b.X + s * 32, b.Y + 4 - 9 * flap),
                    new PointF(b.X + s * 24, b.Y + 1 - 5 * flap),
                    new PointF(b.X + s * 18, b.Y + 6 - 3 * flap),
                    new PointF(b.X + s * 11, b.Y + 2),
                    new PointF(b.X + s * 4, b.Y + 4),
                };
                Poly(g, wing, pts);
                using (Pen p = MakePen(rim, 1.2f)) g.DrawPolygon(p, pts);
            }
            Color body = Tone(Color.FromArgb(65, 28, 90), a);
            using (Brush br = new SolidBrush(body)) g.FillEllipse(br, b.X - 7, b.Y - 6, 14, 12);
            Poly(g, body, new PointF(b.X - 4, b.Y - 4), new PointF(b.X - 2, b.Y - 12), new PointF(b.X + 1, b.Y - 5));
            Poly(g, body, new PointF(b.X + 1, b.Y - 5), new PointF(b.X + 4, b.Y - 12), new PointF(b.X + 5, b.Y - 3));
            if (a.fall < 0.5f)
            {
                Circle(g, Tone(Color.Red, a), new PointF(b.X + 2, b.Y - 2), 1.6f);
                Circle(g, Tone(Color.Red, a), new PointF(b.X + 5.5f, b.Y - 2), 1.6f);
            }
            Line(g, Tone(Color.White, a), 1f, new PointF(b.X + 3, b.Y + 3), new PointF(b.X + 3.5f, b.Y + 6));
            Line(g, Tone(Color.White, a), 1f, new PointF(b.X + 5.5f, b.Y + 3), new PointF(b.X + 6f, b.Y + 6));
        }
    }

    class AcidFig : Figure
    {
        public AcidFig() { height = 0.5f; }
        public override float Top { get { return -36f; } }
        public override float HalfWidth { get { return 26f; } }
        public override PointF Mouth { get { return new PointF(22f, -18f); } }
        protected override bool CustomDeath { get { return true; } }

        protected override void Draw(Graphics g, Anim a)
        {
            float wob = (float)Math.Sin(a.time * 5);
            float w = 24f * (1 + 0.15f * a.windup - 0.05f * a.strike + 0.03f * wob) * (1 + 0.8f * a.fall);
            float h = 30f * (1 - 0.2f * a.windup + 0.15f * a.strike - 0.03f * wob) * (1 - 0.85f * a.fall);
            float lean = a.strike * 10f - a.recoil * 6f;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddBezier(-w, 0, -w, -h * 0.7f, -w * 0.3f + lean, -h, lean, -h);
                path.AddBezier(lean, -h, w * 0.3f + lean, -h, w, -h * 0.7f, w, 0);
                path.CloseFigure();
                using (Brush b = new SolidBrush(Tone(Color.FromArgb(170, 154, 205, 50), a))) g.FillPath(b, path);
                using (Pen p = MakePen(Tone(Color.FromArgb(90, 130, 20), a), 2f)) g.DrawPath(p, path);
            }
            for (int i = 0; i < 3; i++)
            {
                float ph = (a.time * 0.6f + i / 3f) % 1f;
                Circle(g, Tone(Color.FromArgb((int)(160 * (1 - ph)), 230, 255, 180), a), new PointF(-8 + i * 8, -3 - ph * h * 0.8f), 2.5f);
            }
            if (a.fall < 0.3f)
            {
                Circle(g, Tone(Color.FromArgb(40, 60, 10), a), new PointF(lean + w * 0.15f, -h * 0.6f), 2f);
                Circle(g, Tone(Color.FromArgb(40, 60, 10), a), new PointF(lean + w * 0.45f, -h * 0.6f), 2f);
            }
            if (a.strike > 0.35f && a.fall == 0)
            {
                for (int i = 0; i < 3; i++)
                    Circle(g, Tone(Color.FromArgb(200, 180, 240, 60), a), new PointF(w + 6 + a.strike * 30 + i * 7, -h * 0.5f - i * 4 + a.strike * 8), 3f - i * 0.5f);
            }
        }
    }

    class IceFig : Figure
    {
        public IceFig() { height = 0.6f; }
        public override float Top { get { return -40f; } }
        public override float HalfWidth { get { return 28f; } }
        public override PointF Mouth { get { return new PointF(24f, -22f); } }
        protected override bool CustomDeath { get { return true; } }

        protected override void Draw(Graphics g, Anim a)
        {
            Color fill = Tone(Color.FromArgb(200, 224, 255, 255), a);
            Color edge = Tone(Color.SteelBlue, a);
            float wob = (float)Math.Sin(a.time * 6);
            float w = 26f * (1 + 0.04f * wob + 0.1f * a.windup), h = 36f * (1 - 0.04f * wob - 0.1f * a.windup + 0.08f * a.strike);

            if (a.fall > 0)
            {
                // 砕けて破片が散る
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * 0.8f;
                    PointF c0 = new PointF((float)Math.Cos(ang) * 10, -h / 2 + (float)Math.Sin(ang) * 10);
                    PointF c = new PointF(c0.X + (float)Math.Cos(ang) * a.fall * 40, Lerp(c0.Y, -3, a.fall));
                    float s = 7f, rot = ang + a.fall * 4;
                    Poly(g, Tone(Color.FromArgb((int)(220 * (1 - a.fall * 0.5f)), 224, 255, 255), a),
                        Add(c, Dir(rot, s)), Add(c, Dir(rot + 2.2f, s * 0.7f)), Add(c, Dir(rot + 4.1f, s * 0.9f)));
                }
                return;
            }

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddBezier(-w, 0, -w, -h * 1.1f, w, -h * 1.1f, w, 0);
                for (int i = 0; i < 4; i++)
                {
                    float x0 = w - i * w / 2, x1 = w - (i + 1) * w / 2;
                    path.AddBezier(x0, 0, x0 - w / 8, 5, x1 + w / 8, 5, x1, 0);
                }
                path.CloseFigure();
                using (Brush b = new SolidBrush(fill)) g.FillPath(b, path);
                using (Pen p = MakePen(edge, 2f)) g.DrawPath(p, path);
            }
            Color crystal = Tone(Color.FromArgb(230, 255, 255, 255), a);
            DrawCrystal(g, crystal, new PointF(-9, -16), 5);
            DrawCrystal(g, crystal, new PointF(3, -28), 4);
            Circle(g, Tone(Color.MidnightBlue, a), new PointF(9, -19), 2f);
            Circle(g, Tone(Color.MidnightBlue, a), new PointF(16, -19), 2f);

            if (a.strike > 0.1f)
            {
                for (int i = 0; i < 6; i++)
                {
                    float d = a.strike * 45 * (i + 1) / 6f;
                    Circle(g, Color.FromArgb((int)(200 * a.strike), 245, 250, 255), new PointF(w + d, -h * 0.5f + (float)Math.Sin(i * 2.1f) * 6), 2.2f);
                }
            }
        }

        static void DrawCrystal(Graphics g, Color c, PointF p, float s)
        {
            for (int i = 0; i < 3; i++)
                Line(g, c, 1.2f, Add(p, Dir(i * 1.047f, s)), Add(p, Dir(i * 1.047f + 3.14f, s)));
        }
    }

    class DwarfFig : Humanoid
    {
        public DwarfFig()
        {
            height = 0.75f; color = Color.FromArgb(170, 95, 50); thick = 5.5f; defaultWeapon = WeaponKind.Pick;
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            PointF h = r.head; float R = r.headR;
            Circle(g, Tone(Color.FromArgb(230, 190, 150), a), h, R);
            // ひげ
            Poly(g, Tone(Color.FromArgb(200, 200, 200), a),
                new PointF(h.X - R * 0.3f, h.Y + R * 0.1f), new PointF(h.X + R * 1.05f, h.Y + R * 0.2f), new PointF(h.X + R * 0.3f, h.Y + R * 2.4f));
            // 兜
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(150, 150, 160), a)))
                g.FillPie(b, h.X - R * 1.1f, h.Y - R * 1.2f, R * 2.2f, R * 2.2f, 180, 180);
            Line(g, Tone(Color.FromArgb(120, 120, 130), a), 2f, new PointF(h.X + R * 0.7f, h.Y - R * 0.1f), new PointF(h.X + R * 0.75f, h.Y + R * 0.4f));
            Circle(g, Tone(Color.Black, a), new PointF(h.X + R * 0.4f, h.Y - R * 0.05f), R * 0.13f);
        }
    }

    class HobbitFig : Humanoid
    {
        readonly Color vest;
        public HobbitFig(bool bilbo)
        {
            height = 0.65f; color = Color.FromArgb(222, 184, 135); defaultWeapon = WeaponKind.Knife;
            vest = bilbo ? Color.FromArgb(200, 40, 40) : Color.ForestGreen;
        }

        protected override void DrawTorso(Graphics g, Rig r, Anim a, Color c, float w)
        {
            Line(g, c, w, r.hip, r.neckTop);
            Line(g, Tone(vest, a), w * 2.3f, Mid(r.hip, r.neckTop, 0.1f), Mid(r.hip, r.neckTop, 0.9f));
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            PointF h = r.head; float R = r.headR;
            Circle(g, c, h, R);
            Color hair = Tone(Color.FromArgb(120, 70, 30), a);
            for (int i = 0; i < 5; i++)
                Circle(g, hair, Add(h, Dir(2.0f + i * 0.55f, R * 0.95f)), R * 0.42f);
            Circle(g, Tone(Color.Black, a), new PointF(h.X + R * 0.45f, h.Y - R * 0.1f), R * 0.14f);
        }

        protected override void DrawFeet(Graphics g, Rig r, Anim a, Color c, float w)
        {
            Color foot = Tone(Color.FromArgb(160, 110, 60), a);
            using (Brush b = new SolidBrush(Tone(Darken(Color.FromArgb(160, 110, 60), 0.7f), a)))
                g.FillEllipse(b, r.lFoot.X - 2, r.lFoot.Y - 3, 9, 4.5f);
            using (Brush b = new SolidBrush(foot))
                g.FillEllipse(b, r.rFoot.X - 2, r.rFoot.Y - 3, 9, 4.5f);
        }
    }

    class DragonFig : Figure
    {
        const float S = 0.85f;  // 横幅が大きいので縮小して描く
        public DragonFig() { height = 1.6f; }
        public override float Top { get { return -122f * S; } }
        public override float CenterY { get { return -55f * S; } }
        public override float HalfWidth { get { return 60f * S; } }
        public override PointF Mouth { get { return new PointF(78f * S, -97f * S); } }
        public override float MaxLunge { get { return 16f; } }
        protected override bool CustomDeath { get { return true; } }

        protected override void Draw(Graphics g, Anim a)
        {
            GraphicsState st = g.Save();
            g.ScaleTransform(S, S);
            g.TranslateTransform(0, 14f * a.fall);

            Color body = Tone(Color.FromArgb(185, 35, 35), a);
            Color dark = Tone(Color.FromArgb(110, 15, 15), a);
            Color belly = Tone(Color.FromArgb(230, 150, 90), a);
            float flap = (float)Math.Sin(a.time * 3) * 6 * (1 - a.fall);
            float breath = (float)Math.Sin(a.time * 2) * 1.5f;

            // 奥の翼
            DrawWing(g, new PointF(-12, -60 + breath), flap * 0.7f - 6, Tone(Color.FromArgb(200, 90, 10, 10), a), dark);
            // 奥の脚
            Lines(g, dark, 7, new PointF(4, -34), new PointF(8, -14), new PointF(14, 0));
            Lines(g, dark, 7, new PointF(-44, -34), new PointF(-50, -14), new PointF(-42, 0));
            // 尾
            PointF[] tail = { new PointF(-52, -46), new PointF(-82, -40), new PointF(-96, -14), new PointF(-122, -20) };
            using (Pen p = MakePen(body, 9)) g.DrawCurve(p, tail);
            Poly(g, dark, new PointF(-122, -26), new PointF(-134, -18), new PointF(-120, -14));
            // 胴
            using (Brush b = new SolidBrush(body)) g.FillEllipse(b, -58, -60 + breath, 84, 36);
            using (Pen p = MakePen(belly, 4)) g.DrawArc(p, -52, -58 + breath, 72, 32, 20, 120);
            for (int i = 0; i < 5; i++)
                Poly(g, dark, new PointF(-40 + i * 12, -58 + breath), new PointF(-35 + i * 12, -68 + breath), new PointF(-30 + i * 12, -58 + breath));
            // 手前の脚
            Lines(g, body, 8, new PointF(12, -34), new PointF(18, -14), new PointF(24, 0));
            Lines(g, body, 8, new PointF(-36, -34), new PointF(-40, -14), new PointF(-32, 0));

            // 首と頭（振りかぶりで後ろへ、噛みつきで前へ、死亡で垂れる）
            float hx = 48 - a.windup * 8 + a.strike * 20 - a.recoil * 8;
            float hy = -100 - a.windup * 10 + a.strike * 10 + breath;
            hx = Lerp(hx, 70, a.fall); hy = Lerp(hy, -10, a.fall);
            PointF[] neck = { new PointF(18, -52 + breath), new PointF(36, -66), new PointF(hx - 14, (hy - 66) / 2 - 20), new PointF(hx, hy) };
            using (Pen p = MakePen(body, 10)) g.DrawCurve(p, neck);
            float jaw = Math.Max(a.strike, a.windup * 0.5f) * 0.5f;
            PointF[] upper = {
                new PointF(hx - 6, hy - 8), new PointF(hx + 14, hy - 7), new PointF(hx + 30, hy - 2),
                new PointF(hx + 30, hy + 2), new PointF(hx + 12, hy + 1), new PointF(hx - 4, hy + 4),
            };
            Poly(g, body, upper);
            PointF jb = new PointF(hx - 2, hy + 3);
            Poly(g, dark, jb, Add(jb, Dir(1.57f + jaw, 30)), Add(jb, Dir(1.2f + jaw, 12)));
            Lines(g, dark, 3, new PointF(hx - 4, hy - 7), new PointF(hx - 16, hy - 18));
            Lines(g, dark, 3, new PointF(hx + 2, hy - 8), new PointF(hx - 8, hy - 20));
            if (a.fall < 0.5f) Circle(g, Tone(Color.Gold, a), new PointF(hx + 8, hy - 3), 2.2f);
            else Line(g, dark, 1.5f, new PointF(hx + 5, hy - 3), new PointF(hx + 11, hy - 3));

            // 手前の翼
            DrawWing(g, new PointF(-6, -58 + breath), flap, Tone(Color.FromArgb(210, 150, 25, 25), a), dark);
            g.Restore(st);
        }

        static void DrawWing(Graphics g, PointF s, float f, Color fill, Color rib)
        {
            PointF[] pts = {
                s, new PointF(s.X + 4, s.Y - 52 + f), new PointF(s.X - 14, s.Y - 74 + f), new PointF(s.X - 34, s.Y - 64 + f),
                new PointF(s.X - 54, s.Y - 42 + f * 0.5f), new PointF(s.X - 44, s.Y - 22), new PointF(s.X - 32, s.Y - 28),
                new PointF(s.X - 24, s.Y - 10), new PointF(s.X - 12, s.Y - 18),
            };
            Poly(g, fill, pts);
            using (Pen p = MakePen(rib, 2)) { g.DrawLine(p, s, pts[1]); g.DrawLine(p, pts[1], pts[2]); g.DrawLine(p, pts[1], pts[3]); g.DrawLine(p, pts[1], pts[4]); }
        }
    }

    // ================================================================
    // 5〜7階（オデュッセイア）
    // ================================================================

    class SirenFig : Humanoid
    {
        public SirenFig()
        {
            height = 0.9f; color = Color.FromArgb(221, 160, 221); lift = 12f; defaultWeapon = WeaponKind.Claw;
        }
        public override float Top { get { return -100f * height - lift; } }
        public override float MaxLunge { get { return 6f; } }
        public override PointF Mouth { get { return new PointF(12f, -86f); } }
        protected override bool CustomDeath { get { return true; } }

        protected override void Draw(Graphics g, Anim a)
        {
            // 岩は動かさず、本体だけが岩から落ちる
            Anim rockAnim = new Anim { time = a.time, dir = a.dir, alpha = a.alpha };
            using (Brush b = new SolidBrush(Tone(Color.FromArgb(105, 105, 115), rockAnim))) g.FillEllipse(b, -26, -15, 52, 20);
            using (Pen p = new Pen(Tone(Color.FromArgb(70, 70, 80), rockAnim), 1.5f)) g.DrawArc(p, -26, -15, 52, 20, 180, 180);

            GraphicsState st = g.Save();
            if (a.fall > 0)
            {
                g.TranslateTransform(5f * a.fall, lift * a.fall);
                g.RotateTransform(-86f * a.fall, MatrixOrder.Prepend);
                g.TranslateTransform(40f * a.fall, 0);
            }
            base.Draw(g, a);
            g.Restore(st);
        }

        protected override void DrawBehind(Graphics g, Rig r, Anim a)
        {
            // 羽根の翼
            PointF s = r.shoulder;
            float f = (float)Math.Sin(a.time * 3) * 3;
            PointF[] wing = {
                s, new PointF(s.X - 20, s.Y - 20 + f), new PointF(s.X - 34, s.Y - 8 + f), new PointF(s.X - 30, s.Y + 6),
                new PointF(s.X - 24, s.Y + 14), new PointF(s.X - 14, s.Y + 16),
            };
            Poly(g, Tone(Color.FromArgb(215, 195, 225), a), wing);
            using (Pen p = MakePen(Tone(Color.FromArgb(160, 130, 175), a), 1.2f))
                for (int i = 2; i < wing.Length; i++) g.DrawLine(p, s, wing[i]);
            // 長い髪
            PointF h = r.head;
            using (Pen p = MakePen(Tone(Color.FromArgb(130, 60, 150), a), 2.2f))
                for (int i = 0; i < 3; i++)
                    g.DrawBezier(p, h.X - 2, h.Y - r.headR, h.X - 14 - i * 3, h.Y - 4, h.X - 8 - i * 4, h.Y + 12 + (float)Math.Sin(a.time * 2 + i) * 3, h.X - 16 - i * 4, h.Y + 26);
        }

        protected override void DrawFeet(Graphics g, Rig r, Anim a, Color c, float w)
        {
            Color talon = Tone(Color.FromArgb(230, 200, 120), a);
            foreach (PointF f in new[] { r.lFoot, r.rFoot })
                for (int i = -1; i <= 1; i++) Line(g, talon, 1.2f, f, new PointF(f.X + 3 + i * 3, f.Y + 1));
        }
    }

    class PolyphemusFig : Humanoid
    {
        public PolyphemusFig()
        {
            height = 2.0f; color = Color.FromArgb(205, 133, 63); thick = 4.5f; headScale = 1.25f; defaultWeapon = WeaponKind.Club;
        }
        public override PointF Mouth { get { return new PointF(20f, -180f); } }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            PointF h = r.head; float R = r.headR;
            Circle(g, c, h, R);
            using (Pen p = MakePen(Tone(Darken(color, 0.55f), a), 2f)) g.DrawEllipse(p, h.X - R, h.Y - R, R * 2, R * 2);
            PointF eye = new PointF(h.X + R * 0.3f, h.Y - R * 0.15f);
            bool squint = a.recoil > 0.3f || a.fall > 0;
            Line(g, Tone(Darken(color, 0.4f), a), 4f, new PointF(eye.X - R * 0.6f, eye.Y - R * 0.55f), new PointF(eye.X + R * 0.6f, eye.Y - R * 0.45f));
            if (squint)
            {
                Line(g, Tone(Color.FromArgb(60, 20, 10), a), 3f, new PointF(eye.X - R * 0.45f, eye.Y), new PointF(eye.X + R * 0.45f, eye.Y));
            }
            else
            {
                using (Brush b = new SolidBrush(Tone(Color.WhiteSmoke, a))) g.FillEllipse(b, eye.X - R * 0.48f, eye.Y - R * 0.36f, R * 0.96f, R * 0.72f);
                Circle(g, Tone(Color.Black, a), new PointF(eye.X + R * 0.12f, eye.Y), R * 0.2f);
            }
            Line(g, Tone(Darken(color, 0.4f), a), 2.5f, new PointF(h.X + R * 0.1f, h.Y + R * 0.55f), new PointF(h.X + R * 0.75f, h.Y + R * 0.5f));
        }
    }

    class CursedSailorFig : Humanoid
    {
        public CursedSailorFig() { color = Color.FromArgb(150, 195, 150); defaultWeapon = WeaponKind.Cutlass; }
        protected override bool CustomDeath { get { return true; } }

        protected override void Draw(Graphics g, Anim a)
        {
            if (a.fall <= 0) { base.Draw(g, a); return; }

            // バラバラに崩れる: 骨を1本ずつ地面へ散らばらせる
            Anim still = new Anim { time = 0, recoil = 1, fall = a.fall, alpha = a.alpha, dir = a.dir };
            Rig r = BuildRig(still);
            PointF[][] bones = {
                new[] { r.hip, r.lKnee }, new[] { r.lKnee, r.lFoot }, new[] { r.hip, r.rKnee }, new[] { r.rKnee, r.rFoot },
                new[] { r.hip, r.neckTop }, new[] { r.shoulder, r.lElbow }, new[] { r.lElbow, r.lHand },
                new[] { r.shoulder, r.rElbow }, new[] { r.rElbow, r.rHand },
            };
            Color c = Tone(color, a);
            for (int i = 0; i < bones.Length; i++)
            {
                float sx = -34 + i * 8;
                float len = 12 + (i % 3) * 4;
                PointF t1 = new PointF(sx, -2), t2 = new PointF(sx + len * (float)Math.Cos(i * 1.3f), -2 - Math.Abs(len * (float)Math.Sin(i * 1.3f)) * 0.2f);
                Line(g, c, 3f, Mid(bones[i][0], t1, a.fall), Mid(bones[i][1], t2, a.fall));
            }
            PointF skull = Mid(r.head, new PointF(28, -r.headR), a.fall);
            Circle(g, Tone(Color.FromArgb(225, 225, 205), a), skull, r.headR);
            Circle(g, Tone(Color.FromArgb(30, 30, 30), a), new PointF(skull.X + r.headR * 0.3f, skull.Y - r.headR * 0.1f), r.headR * 0.25f);
        }

        protected override void DrawTorso(Graphics g, Rig r, Anim a, Color c, float w)
        {
            Line(g, c, w, r.hip, r.neckTop);
            float cs = (float)Math.Cos(r.lean), sn = (float)Math.Sin(r.lean);
            for (int i = 0; i < 3; i++)
            {
                PointF p = Mid(r.hip, r.shoulder, 0.45f + i * 0.18f);
                Line(g, c, 2f, new PointF(p.X - 7 * cs, p.Y - 7 * sn), new PointF(p.X + 7 * cs, p.Y + 7 * sn));
            }
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            PointF h = r.head; float R = r.headR;
            // バンダナの端（はためく）
            float f = (float)Math.Sin(a.time * 8) * 2;
            Color red = Tone(Color.FromArgb(170, 30, 30), a);
            Lines(g, red, 2.2f, new PointF(h.X - R * 0.8f, h.Y - R * 0.4f), new PointF(h.X - R * 1.8f, h.Y - R * 0.1f + f), new PointF(h.X - R * 2.4f, h.Y + R * 0.3f - f));
            Circle(g, Tone(Color.FromArgb(225, 225, 205), a), h, R);
            using (Brush b = new SolidBrush(red)) g.FillPie(b, h.X - R * 1.05f, h.Y - R * 1.1f, R * 2.1f, R * 2.0f, 180, 180);
            Circle(g, Tone(Color.FromArgb(30, 30, 30), a), new PointF(h.X + R * 0.45f, h.Y + R * 0.1f), R * 0.28f);
            Circle(g, Tone(Color.FromArgb(30, 30, 30), a), new PointF(h.X - R * 0.05f, h.Y + R * 0.1f), R * 0.2f);
            Line(g, Tone(Color.FromArgb(60, 60, 60), a), 1.2f, new PointF(h.X + R * 0.1f, h.Y + R * 0.7f), new PointF(h.X + R * 0.8f, h.Y + R * 0.6f));
        }
    }

    class ScyllaFig : Figure
    {
        public ScyllaFig() { height = 1.4f; }
        public override float Top { get { return -140f; } }
        public override float CenterY { get { return -70f; } }
        public override float HalfWidth { get { return 48f; } }
        public override float MaxLunge { get { return 0f; } }
        public override PointF Mouth { get { return new PointF(50f, -78f); } }
        protected override bool CustomDeath { get { return true; } }

        // 犬の首の根本からの先端位置（0と4が手前のパーティへ噛みつく）
        static readonly PointF[] HeadBase = {
            new PointF(48, -80), new PointF(34, -112), new PointF(-38, -104), new PointF(-54, -74), new PointF(58, -50), new PointF(-60, -40),
        };

        protected override void Draw(Graphics g, Anim a)
        {
            GraphicsState st = g.Save();
            if (a.fall > 0)
            {
                g.SetClip(new RectangleF(-200, -300, 400, 300));   // 水面より下は見えない
                g.TranslateTransform(0, 150 * a.fall);
            }
            Color skin = Tone(Color.FromArgb(60, 179, 113), a);
            Color dog = Tone(Color.FromArgb(50, 125, 85), a);
            Color dogDark = Tone(Color.FromArgb(30, 80, 55), a);
            PointF waist = new PointF(0, -52);

            // 触手
            for (int i = 0; i < 6; i++)
            {
                float ex = -40 + i * 16, sw = (float)Math.Sin(a.time * 2 + i) * 6;
                PointF[] t = { new PointF(waist.X - 8 + i * 3, waist.Y + 4), new PointF(ex * 0.5f + sw, -28), new PointF(ex + sw, -8), new PointF(ex + 6 - sw, -1) };
                using (Pen p = MakePen(dogDark, 6)) g.DrawCurve(p, t);
            }
            // 犬の首と頭
            for (int i = 0; i < 6; i++)
            {
                PointF tip = HeadBase[i];
                float sw = (float)Math.Sin(a.time * 2.5f + i * 1.3f) * 4;
                tip = new PointF(tip.X + sw, tip.Y + (float)Math.Cos(a.time * 2 + i) * 3);
                if (i == 0) tip = new PointF(tip.X + a.strike * 55 - a.windup * 10, tip.Y + a.strike * 25 - a.windup * 12);
                if (i == 4) tip = new PointF(tip.X + a.strike * 85 - a.windup * 8, tip.Y + a.strike * 20 - a.windup * 10);
                PointF ctrl = new PointF(tip.X * 0.4f, (waist.Y + tip.Y) / 2 - 18);
                using (Pen p = MakePen(dog, 4.5f)) g.DrawCurve(p, new[] { new PointF(waist.X, waist.Y - 2), ctrl, tip });
                DrawDogHead(g, tip, tip.X >= 0 ? 1 : -1, (i == 0 || i == 4) ? a.strike : 0.15f, dog, dogDark, a);
            }
            // 上半身（女性）
            PointF neck = new PointF(0, -96), head = new PointF(1, -108);
            using (Pen p = MakePen(Tone(Color.FromArgb(40, 90, 60), a), 2.2f))
                for (int i = 0; i < 3; i++)
                    g.DrawBezier(p, head.X - 3, head.Y - 9, head.X - 16 - i * 3, head.Y - 2, head.X - 10 - i * 3, head.Y + 14, head.X - 18 - i * 4, head.Y + 28 + (float)Math.Sin(a.time * 2 + i) * 3);
            // 女性の胴（横向き）: くびれた腰から胸がふくらみ、肩へ細くなる。胸には海藻の帯
            float breath = (float)Math.Sin(a.time * 2.5f) * 0.6f;
            using (GraphicsPath torso = new GraphicsPath())
            {
                torso.AddLine(-6, -52, -5, -70);                                            // 背中側
                torso.AddBezier(-5, -70, -7, -80, -6, -90, -3, -95);                        // 背中〜肩
                torso.AddLine(-3, -95, 3, -95);                                             // 肩
                torso.AddBezier(3, -95, 5, -91, 12 + breath, -87, 11 + breath, -81);        // 胸の上側
                torso.AddBezier(11 + breath, -81, 10 + breath, -76, 5, -75, 4, -71);        // 胸の下側
                torso.AddBezier(4, -71, 2, -64, 3, -58, 6, -52);                            // くびれ〜腰
                torso.CloseFigure();
                using (Brush b = new SolidBrush(skin)) g.FillPath(b, torso);
                using (Pen p = MakePen(Tone(Color.FromArgb(40, 120, 80), a), 1.2f)) g.DrawPath(p, torso);
            }
            Poly(g, Tone(Color.FromArgb(35, 100, 60), a),
                new PointF(-6, -86), new PointF(8 + breath, -88), new PointF(12 + breath, -82),
                new PointF(9 + breath, -77), new PointF(-5, -78));
            Line(g, skin, 3.5f, new PointF(0, -95), neck);
            float arm = (float)Math.Sin(a.time * 3) * 0.3f;
            Lines(g, skin, 3.5f, new PointF(0, -90), Add(new PointF(0, -90), Dir(2.3f + arm, 16)), Add(Add(new PointF(0, -90), Dir(2.3f + arm, 16)), Dir(2.9f + arm, 13)));
            Lines(g, skin, 3.5f, new PointF(0, -90), Add(new PointF(0, -90), Dir(-2.1f - arm, 16)), Add(Add(new PointF(0, -90), Dir(-2.1f - arm, 16)), Dir(-2.7f, 13)));
            Circle(g, skin, head, 10);
            Circle(g, Tone(Color.Black, a), new PointF(head.X + 5, head.Y - 2), 1.4f);
            // 波
            using (Pen p = MakePen(Color.FromArgb((int)(200 * a.alpha), 120, 170, 220), 2))
            {
                PointF[] wave = new PointF[12];
                for (int i = 0; i < wave.Length; i++) wave[i] = new PointF(-60 + i * 11, -2 + (float)Math.Sin(a.time * 3 + i) * 2.5f);
                g.DrawCurve(p, wave);
            }
            g.Restore(st);
        }

        static void DrawDogHead(Graphics g, PointF p, int side, float open, Color c, Color dark, Anim a)
        {
            float jaw = 0.25f + open * 0.5f;
            Poly(g, c, new PointF(p.X - side * 4, p.Y - 5), new PointF(p.X + side * 13, p.Y - 2 - jaw * 4), new PointF(p.X + side * 2, p.Y + 3));
            Poly(g, dark, new PointF(p.X - side * 2, p.Y + 2), new PointF(p.X + side * 11, p.Y + 2 + jaw * 8), new PointF(p.X + side * 1, p.Y + 6));
            Poly(g, dark, new PointF(p.X - side * 4, p.Y - 5), new PointF(p.X - side * 7, p.Y - 12), new PointF(p.X - side * 1, p.Y - 6));
            Circle(g, Tone(Color.Yellow, a), new PointF(p.X + side * 3, p.Y - 3), 1.3f);
        }
    }

    // ローブを着た人型（キルケー・テイレシアス）
    class RobedFig : Humanoid
    {
        protected Color robe, hair, skin = Color.FromArgb(240, 210, 180);
        public RobedFig() { drawLegs = false; defaultWeapon = WeaponKind.Staff; }
        protected override bool CustomDeath { get { return true; } }
        public override PointF Mouth { get { return new PointF(40f * height, -78f * height); } }

        protected override void Draw(Graphics g, Anim a)
        {
            GraphicsState st = g.Save();
            if (a.fall > 0) g.ScaleTransform(1, 1 - 0.8f * a.fall);   // ローブが崩れ落ちる
            base.Draw(g, a);
            g.Restore(st);
        }

        protected override void DrawTorso(Graphics g, Rig r, Anim a, Color c, float w)
        {
            float sway = (float)Math.Sin(a.time * 2) * 2;
            float cs = (float)Math.Cos(r.lean), sn = (float)Math.Sin(r.lean);
            PointF[] pts = {
                new PointF(r.shoulder.X - 7 * cs, r.shoulder.Y - 7 * sn), new PointF(r.shoulder.X + 7 * cs, r.shoulder.Y + 7 * sn),
                new PointF(r.hip.X + 12, r.hip.Y), new PointF(22 + sway, 0), new PointF(-20 + sway, 0), new PointF(r.hip.X - 11, r.hip.Y),
            };
            Poly(g, Tone(robe, a), pts);
            using (Pen p = MakePen(Tone(Darken(robe, 0.6f), a), 1.5f)) g.DrawPolygon(p, pts);
            Line(g, Tone(Color.FromArgb(212, 175, 55), a), 2f, new PointF(r.hip.X - 11, r.hip.Y), new PointF(r.hip.X + 12, r.hip.Y));
        }
    }

    class CirceFig : RobedFig
    {
        public CirceFig()
        {
            color = Color.FromArgb(170, 60, 220); robe = Color.FromArgb(148, 0, 211); hair = Color.FromArgb(40, 15, 50);
            staffOrb = Color.Magenta;
        }

        protected override void DrawBehind(Graphics g, Rig r, Anim a)
        {
            PointF h = r.head;
            using (Pen p = MakePen(Tone(hair, a), 2.5f))
                for (int i = 0; i < 4; i++)
                    g.DrawBezier(p, h.X, h.Y - r.headR, h.X - 14 - i * 2, h.Y - 6, h.X - 8 - i * 3, h.Y + 14, h.X - 12 - i * 3, h.Y + 34 + (float)Math.Sin(a.time * 2 + i) * 3);
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            PointF h = r.head; float R = r.headR;
            Circle(g, Tone(skin, a), h, R);
            using (Brush b = new SolidBrush(Tone(hair, a))) g.FillPie(b, h.X - R * 1.05f, h.Y - R * 1.1f, R * 2.1f, R * 1.9f, 170, 200);
            Circle(g, Tone(Color.FromArgb(120, 0, 160), a), new PointF(h.X + R * 0.5f, h.Y - R * 0.05f), R * 0.15f);
            Line(g, Tone(Color.FromArgb(200, 50, 90), a), 1.4f, new PointF(h.X + R * 0.5f, h.Y + R * 0.5f), new PointF(h.X + R * 0.85f, h.Y + R * 0.45f));
        }
    }

    class TeiresiasFig : RobedFig
    {
        public TeiresiasFig()
        {
            height = 0.95f; color = Color.FromArgb(230, 230, 220); robe = Color.FromArgb(220, 220, 210); hair = Color.WhiteSmoke;
            leanBias = 0.12f;
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            PointF h = r.head; float R = r.headR;
            using (Pen p = MakePen(Tone(hair, a), 2f))
                for (int i = 0; i < 3; i++) g.DrawBezier(p, h.X - R * 0.5f, h.Y - R * 0.8f, h.X - R * 1.4f, h.Y, h.X - R * 1.2f - i * 2, h.Y + R, h.X - R * 1.5f - i * 2, h.Y + R * 1.8f);
            Circle(g, Tone(skin, a), h, R);
            // 長い白ひげ
            Poly(g, Tone(Color.White, a), new PointF(h.X - R * 0.2f, h.Y + R * 0.2f), new PointF(h.X + R * 1.0f, h.Y + R * 0.3f), new PointF(h.X + R * 0.2f, h.Y + R * 3.0f));
            // 閉じた目
            Line(g, Tone(Color.FromArgb(90, 70, 60), a), 1.5f, new PointF(h.X + R * 0.25f, h.Y - R * 0.15f), new PointF(h.X + R * 0.75f, h.Y - R * 0.1f));
        }
    }

    class ShadeFig : Humanoid
    {
        public ShadeFig() { color = Color.FromArgb(225, 230, 245); drawLegs = false; defaultWeapon = WeaponKind.None; }
        protected override bool CustomDeath { get { return true; } }

        protected override void Draw(Graphics g, Anim a)
        {
            float saved = a.alpha;
            a.alpha *= 0.45f * (1 - a.fall);
            lift = 8 + (float)Math.Sin(a.time * 2.2f) * 5 + a.fall * 30;   // 死亡時は霧散しながら昇る
            base.Draw(g, a);
            a.alpha = saved;
        }

        protected override void DrawBehind(Graphics g, Rig r, Anim a)
        {
            // 足の代わりの揺らめく尾
            float t = a.time * 4;
            PointF[] tail = {
                new PointF(r.hip.X - 8, r.hip.Y - 4), new PointF(r.hip.X + 9, r.hip.Y - 4),
                new PointF(r.hip.X + 6 + (float)Math.Sin(t) * 4, r.hip.Y + 18),
                new PointF(r.hip.X + (float)Math.Sin(t + 1) * 6, r.hip.Y + 34),
                new PointF(r.hip.X - 6 + (float)Math.Sin(t + 2) * 4, r.hip.Y + 16),
            };
            using (Brush b = new SolidBrush(Tone(color, a))) g.FillClosedCurve(b, tail);
        }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            Circle(g, c, r.head, r.headR);
            Circle(g, Tone(Color.FromArgb(30, 30, 50), a), new PointF(r.head.X + r.headR * 0.2f, r.head.Y - r.headR * 0.1f), r.headR * 0.22f);
            Circle(g, Tone(Color.FromArgb(30, 30, 50), a), new PointF(r.head.X + r.headR * 0.65f, r.head.Y - r.headR * 0.1f), r.headR * 0.18f);
        }
    }

    // 未登録の敵: 頭に表示グラフの文字を描いた灰色のスティックマン
    class GenericFig : Humanoid
    {
        public GenericFig() { color = Color.Silver; }

        protected override void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            Circle(g, Tone(Color.FromArgb(70, 70, 70), a), r.head, r.headR);
            Ring(g, c, w * 0.8f, r.head, r.headR);
            if (entity != null) Text(g, entity.graphOrig.ToString(), 9f, c, r.head, a);
        }
    }
}
