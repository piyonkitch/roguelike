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
    // 戦闘ビュー用のスティックマン描画基盤
    //
    // 座標系: 足元中心が原点、右向き（+x が前方）、y は下向き正。Hero の身長が 100。
    // 左向きのキャラは BattleView 側で ScaleTransform(-1, 1) して描く。
    //

    // 関節角度（ラジアン）。「真下=0、前方=+π/2、真上=π」
    struct Pose
    {
        public float lean;          // 胴の前傾
        public float rUA, rFA;      // 手前側（武器を持つ側）の上腕・前腕（前腕は上腕からの相対角）
        public float lUA, lFA;      // 奥側の上腕・前腕
        public float rTh, rSh;      // 手前側の大腿・下腿（下腿は大腿からの相対角）
        public float lTh, lSh;      // 奥側の大腿・下腿
        public float wRel;          // 武器の向き（前腕からの相対角）

        public Pose(float lean, float rUA, float rFA, float lUA, float lFA,
                    float rTh, float rSh, float lTh, float lSh, float wRel)
        {
            this.lean = lean; this.rUA = rUA; this.rFA = rFA; this.lUA = lUA; this.lFA = lFA;
            this.rTh = rTh; this.rSh = rSh; this.lTh = lTh; this.lSh = lSh; this.wRel = wRel;
        }

        // 線形補間（Linear Interpolation）: ポーズ a と b の間の姿勢を返す。
        // 全関節の角度を a + (b - a) × t で求める。t=0 で a、t=1 で b、t=0.5 でちょうど中間の姿勢。
        // BuildRig() で「待機→振りかぶり→打ち込み→のけぞり」を毎フレーム補間し、滑らかな動きにしている
        public static Pose Lerp(Pose a, Pose b, float t)
        {
            return new Pose(
                a.lean + (b.lean - a.lean) * t,
                a.rUA + (b.rUA - a.rUA) * t, a.rFA + (b.rFA - a.rFA) * t,
                a.lUA + (b.lUA - a.lUA) * t, a.lFA + (b.lFA - a.lFA) * t,
                a.rTh + (b.rTh - a.rTh) * t, a.rSh + (b.rSh - a.rSh) * t,
                a.lTh + (b.lTh - a.lTh) * t, a.lSh + (b.lSh - a.lSh) * t,
                a.wRel + (b.wRel - a.wRel) * t);
        }
    }

    enum WeaponKind { None, Dagger, Knife, ShortSword, LongSword, Vorpal, Mace, Club, Pick, Staff, Cutlass, Claw }

    // 1フレーム分のアニメーション状態（BattleView が毎フレーム計算して渡す）
    class Anim
    {
        public float time;          // 秒。常に増え続ける（待機モーション用）
        public int dir = 1;         // 向き（+1 右向き / -1 左向き）。文字を描くときの反転補正用
        public float windup;        // 振りかぶり 0..1
        public float strike;        // 打ち込み 0..1
        public float recoil;        // のけぞり 0..1
        public float fall;          // 死亡の倒れ込み 0..1
        public float flash;         // 被弾の白フラッシュ 0..1
        public float alpha = 1f;    // 全体の不透明度
        public bool cast;           // 魔法の詠唱ポーズ
        public bool pig;            // 豚化中
        public bool charmed;        // 魅了中
        public bool frozen;         // 凍結中
        public float crouch;        // かがむ（物を拾う） 0..1
        public float drink;         // 瓶を口元へ傾ける 0..1
        public float read;          // 巻物を両手で広げる 0..1
        public float wave;          // 手を振る 0..1
        public float cheer;         // 両手を上げて喜ぶ 0..1
        public bool hideWeapon;     // 武器を描かない（瓶・巻物・宝石を持っている間）
        public bool before;         // 出来事が起きる前の見た目で描く（祭壇の嵌め込み前など）
    }

    // 骨格の各点（ローカル座標）
    class Rig
    {
        public PointF hip, shoulder, neckTop, head;
        public PointF rElbow, rHand, lElbow, lHand;
        public PointF rKnee, rFoot, lKnee, lFoot;
        public float headR;         // 頭の半径
        public float weaponAngle;   // 武器の向き（真下=0 の角度）
        public float lean;
    }

    abstract class Figure
    {
        public Entity entity;
        public float height = 1f;                       // Hero=1 とした身長比

        public virtual float Top { get { return -100f * height; } }     // 頭頂の y
        public virtual float CenterY { get { return Top * 0.5f; } }     // 被弾位置の y
        public virtual float HalfWidth { get { return 18f * height; } }
        public virtual PointF Mouth { get { return new PointF(26f * height, -64f * height); } } // 飛び道具の発射点
        public virtual float MaxLunge { get { return 1000f; } }         // 踏み込みの上限（px, ローカル単位）
        protected virtual bool CustomDeath { get { return false; } }    // 死亡演出を自前で描くか

        public void Render(Graphics g, Anim a)
        {
            GraphicsState st = g.Save();
            if (!CustomDeath && a.fall > 0)
            {
                // 足元を軸に後ろへ倒れる（倒れた体が元の立ち位置あたりに収まるよう前へずらす）
                g.TranslateTransform(45f * height * a.fall, -3f * a.fall);
                g.RotateTransform(-86f * a.fall);
            }
            Draw(g, a);
            g.Restore(st);

            if (a.frozen) DrawIceBlock(g);
            if (a.charmed && a.fall <= 0) DrawHeart(g, new PointF(0, Top - 12f), 7f, a);
        }

        protected abstract void Draw(Graphics g, Anim a);

        //
        // 描画ユーティリティ
        //
        // 線形補間（Linear Interpolation）: a と b の間の値を a + (b - a) × t で返す。
        // t=0 で a、t=1 で b、t=0.5 でちょうど中間（例: Lerp(10, 30, 0.25) = 15）
        internal static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        internal static PointF Dir(float angle, float len)
        {
            return new PointF((float)Math.Sin(angle) * len, (float)Math.Cos(angle) * len);
        }

        internal static PointF Add(PointF a, PointF b) { return new PointF(a.X + b.X, a.Y + b.Y); }

        internal static PointF Mid(PointF a, PointF b, float t)
        {
            return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        // 死亡で灰色、被弾で白、alpha を反映した色
        internal static Color Tone(Color c, Anim a, float alphaMul = 1f)
        {
            float gray = Math.Min(1f, a.fall) * 0.7f;
            float lum = (c.R * 0.30f + c.G * 0.59f + c.B * 0.11f) * 0.7f;
            float r = Lerp(c.R, lum, gray), gg = Lerp(c.G, lum, gray), b = Lerp(c.B, lum, gray);
            r = Lerp(r, 255, a.flash); gg = Lerp(gg, 255, a.flash); b = Lerp(b, 255, a.flash);
            int al = (int)(c.A * a.alpha * alphaMul);
            return Color.FromArgb(Clamp(al), Clamp((int)r), Clamp((int)gg), Clamp((int)b));
        }

        internal static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }

        internal static Color Darken(Color c, float k)
        {
            return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        internal static Pen MakePen(Color c, float w)
        {
            Pen p = new Pen(c, w);
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
            return p;
        }

        internal static void Line(Graphics g, Color c, float w, PointF p1, PointF p2)
        {
            using (Pen p = MakePen(c, w)) g.DrawLine(p, p1, p2);
        }

        internal static void Lines(Graphics g, Color c, float w, params PointF[] pts)
        {
            using (Pen p = MakePen(c, w)) g.DrawLines(p, pts);
        }

        internal static void Circle(Graphics g, Color fill, PointF c, float r)
        {
            using (Brush b = new SolidBrush(fill)) g.FillEllipse(b, c.X - r, c.Y - r, r * 2, r * 2);
        }

        internal static void Ring(Graphics g, Color col, float w, PointF c, float r)
        {
            using (Pen p = MakePen(col, w)) g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2);
        }

        internal static void Poly(Graphics g, Color fill, params PointF[] pts)
        {
            using (Brush b = new SolidBrush(fill)) g.FillPolygon(b, pts);
        }

        // 左向きでも読めるように文字を描く
        internal static void Text(Graphics g, string s, float size, Color c, PointF center, Anim a)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(center.X, center.Y);
            if (a.dir < 0) g.ScaleTransform(-1, 1);
            using (Font f = new Font("Meiryo UI", size, FontStyle.Bold))
            using (Brush b = new SolidBrush(c))
            {
                SizeF sz = g.MeasureString(s, f);
                g.DrawString(s, f, b, -sz.Width / 2, -sz.Height / 2);
            }
            g.Restore(st);
        }

        protected void DrawIceBlock(Graphics g)
        {
            RectangleF r = new RectangleF(-HalfWidth - 6, Top - 6, (HalfWidth + 6) * 2, -Top + 8);
            using (Brush b = new SolidBrush(Color.FromArgb(90, 180, 230, 255))) g.FillRectangle(b, r);
            using (Pen p = new Pen(Color.FromArgb(200, 220, 245, 255), 2)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            using (Pen p = new Pen(Color.FromArgb(160, 255, 255, 255), 1.5f))
            {
                g.DrawLine(p, r.X + 5, r.Y + 8, r.X + 14, r.Y + 3);
                g.DrawLine(p, r.Right - 12, r.Bottom - 6, r.Right - 4, r.Bottom - 14);
            }
        }

        internal static void DrawHeart(Graphics g, PointF c, float s, Anim a)
        {
            float bob = (float)Math.Sin(a.time * 6) * 2f;
            c = new PointF(c.X, c.Y + bob);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddBezier(c.X, c.Y + s * 0.9f, c.X - s * 1.4f, c.Y, c.X - s * 0.8f, c.Y - s * 1.1f, c.X, c.Y - s * 0.4f);
                path.AddBezier(c.X, c.Y - s * 0.4f, c.X + s * 0.8f, c.Y - s * 1.1f, c.X + s * 1.4f, c.Y, c.X, c.Y + s * 0.9f);
                using (Brush b = new SolidBrush(Color.FromArgb((int)(230 * a.alpha), 255, 105, 180))) g.FillPath(b, path);
            }
        }

        // 豚の頭（豚化中のパーティメンバー用）
        internal static void DrawPigHead(Graphics g, PointF c, float r, Anim a)
        {
            Color pink = Tone(Color.FromArgb(255, 182, 193), a);
            Color dark = Tone(Color.FromArgb(200, 110, 130), a);
            Poly(g, pink, new PointF(c.X - r * 0.7f, c.Y - r * 0.6f), new PointF(c.X - r * 0.3f, c.Y - r * 1.5f), new PointF(c.X, c.Y - r * 0.8f));
            Circle(g, pink, c, r * 1.1f);
            PointF snout = new PointF(c.X + r * 0.9f, c.Y + r * 0.2f);
            using (Brush b = new SolidBrush(dark)) g.FillEllipse(b, snout.X - r * 0.35f, snout.Y - r * 0.45f, r * 0.7f, r * 0.9f);
            Circle(g, Tone(Color.FromArgb(120, 50, 60), a), new PointF(snout.X, snout.Y - r * 0.18f), r * 0.1f);
            Circle(g, Tone(Color.FromArgb(120, 50, 60), a), new PointF(snout.X, snout.Y + r * 0.18f), r * 0.1f);
            Circle(g, Tone(Color.Black, a), new PointF(c.X + r * 0.35f, c.Y - r * 0.35f), r * 0.14f);
        }
    }

    //
    // 人型スティックマン。多くのキャラはこれを継承して頭・胴・装飾だけ差し替える
    //
    class Humanoid : Figure
    {
        protected Color color = Color.Gainsboro;
        protected float thick = 3.5f;
        protected WeaponKind defaultWeapon = WeaponKind.None;
        protected float headScale = 1f;
        protected float leanBias = 0f;
        protected bool drawLegs = true;
        protected float lift = 0f;          // 足元を持ち上げる量（岩の上・浮遊）
        protected Color staffOrb = Color.Empty;

        // 振り下ろし系（剣・棍棒など）
        static readonly Pose SwingIdle   = new Pose(0.05f, 0.45f, 0.9f, -0.2f, 0.5f, 0.2f, -0.15f, -0.2f, -0.1f, 0.6f);
        static readonly Pose SwingWindup = new Pose(-0.12f, 2.9f, 0.5f, 0.7f, 0.9f, 0.35f, -0.2f, -0.35f, -0.1f, 0.9f);
        static readonly Pose SwingStrike = new Pose(0.4f, 1.25f, 0.15f, -0.5f, 0.3f, 0.75f, -0.75f, -0.5f, 0.05f, -0.1f);
        // 突き系（短剣・爪・素手）
        static readonly Pose ThrustIdle   = new Pose(0.05f, 0.4f, 1.1f, -0.2f, 0.6f, 0.2f, -0.15f, -0.2f, -0.1f, 0.1f);
        static readonly Pose ThrustWindup = new Pose(-0.05f, 0.2f, 1.9f, 0.9f, 0.5f, 0.3f, -0.2f, -0.3f, -0.1f, -0.5f);
        static readonly Pose ThrustStrike = new Pose(0.35f, 1.5f, 0.05f, -0.4f, 0.2f, 0.7f, -0.7f, -0.5f, 0f, 0.05f);
        // 詠唱（杖・魔法）
        static readonly Pose CastIdle   = new Pose(0.03f, 0.35f, 0.9f, -0.2f, 0.5f, 0.15f, -0.1f, -0.15f, -0.05f, 1.8f);
        static readonly Pose CastWindup = new Pose(-0.1f, 2.6f, 0.2f, 0.4f, 0.6f, 0.25f, -0.15f, -0.25f, -0.05f, 0.35f);
        static readonly Pose CastStrike = new Pose(0.2f, 1.55f, 0.0f, -0.3f, 0.4f, 0.45f, -0.4f, -0.35f, 0f, 0f);
        // のけぞり
        static readonly Pose Recoil = new Pose(-0.35f, -0.4f, 0.6f, -0.6f, 0.4f, 0.1f, -0.3f, -0.35f, -0.2f, 0.8f);
        // 拾う・飲む・読む・手を振る・喜ぶ
        static readonly Pose Crouch = new Pose(0.55f, 0.7f, 0.2f, 0.3f, 0.3f, 1.0f, -1.8f, 0.2f, -1.3f, 0.6f);
        static readonly Pose Drink  = new Pose(-0.15f, 2.2f, 2.2f, -0.2f, 0.5f, 0.2f, -0.15f, -0.2f, -0.1f, 0f);
        static readonly Pose Read   = new Pose(0.05f, 1.1f, 0.7f, 1.0f, 0.8f, 0.2f, -0.15f, -0.2f, -0.1f, 0f);
        static readonly Pose Wave   = new Pose(0f, 2.75f, 0.3f, -0.2f, 0.4f, 0.2f, -0.15f, -0.2f, -0.1f, 0.6f);
        static readonly Pose Cheer  = new Pose(-0.1f, 2.9f, 0.2f, 2.7f, 0.2f, 0.3f, -0.2f, -0.3f, -0.1f, 0.6f);

        protected WeaponKind CurrentWeapon(Anim a)
        {
            if (a.pig) return WeaponKind.None;
            Weapon w = entity != null ? entity.weapon : null;
            if (w == null) return defaultWeapon;
            return WeaponArt.KindOf(w);
        }

        // 効果音用: 今持っている武器の種類
        public WeaponKind WeaponForSound { get { return CurrentWeapon(new Anim()); } }

        internal static bool IsThrust(WeaponKind w)
        {
            return w == WeaponKind.None || w == WeaponKind.Dagger || w == WeaponKind.Knife || w == WeaponKind.Claw;
        }

        protected Rig BuildRig(Anim a)
        {
            WeaponKind w = CurrentWeapon(a);
            Pose idle, wind, strike;
            if (a.cast)
            {
                idle = (w == WeaponKind.Staff) ? CastIdle : (IsThrust(w) ? ThrustIdle : SwingIdle);
                wind = CastWindup; strike = CastStrike;
            }
            else if (IsThrust(w)) { idle = ThrustIdle; wind = ThrustWindup; strike = ThrustStrike; }
            else { idle = (w == WeaponKind.Staff) ? CastIdle : SwingIdle; wind = SwingWindup; strike = SwingStrike; }

            Pose p = idle;
            p.lean += (float)Math.Sin(a.time * 2.5f) * 0.025f;          // 呼吸
            p.rUA += (float)Math.Sin(a.time * 2.5f + 1f) * 0.04f;
            p = Pose.Lerp(p, wind, a.windup);
            p = Pose.Lerp(p, strike, a.strike);
            p = Pose.Lerp(p, Crouch, a.crouch);
            p = Pose.Lerp(p, Drink, a.drink);
            p = Pose.Lerp(p, Read, a.read);
            if (a.wave > 0)
            {
                Pose wv = Wave;
                wv.rFA += (float)Math.Sin(a.time * 12) * 0.5f;   // 手首を左右に振る
                p = Pose.Lerp(p, wv, a.wave);
            }
            p = Pose.Lerp(p, Cheer, a.cheer);
            p = Pose.Lerp(p, Recoil, a.recoil);

            float H = height;
            float th = 24 * H, sh = 24 * H, spine = 30 * H, ua = 17 * H, fa = 15 * H, neck = 3 * H;
            Rig r = new Rig();
            r.headR = 9 * H * headScale;
            r.lean = p.lean + leanBias;

            PointF rk = Dir(p.rTh, th), rf = Add(rk, Dir(p.rTh + p.rSh, sh));
            PointF lk = Dir(p.lTh, th), lf = Add(lk, Dir(p.lTh + p.lSh, sh));
            float legY = drawLegs ? Math.Max(rf.Y, lf.Y) : 48 * H;
            r.hip = new PointF(0, -legY - lift);
            r.rKnee = Add(r.hip, rk); r.rFoot = Add(r.hip, rf);
            r.lKnee = Add(r.hip, lk); r.lFoot = Add(r.hip, lf);

            PointF up = new PointF((float)Math.Sin(r.lean), -(float)Math.Cos(r.lean));
            r.shoulder = new PointF(r.hip.X + up.X * spine, r.hip.Y + up.Y * spine);
            r.neckTop = new PointF(r.shoulder.X + up.X * neck, r.shoulder.Y + up.Y * neck);
            r.head = new PointF(r.neckTop.X + up.X * r.headR, r.neckTop.Y + up.Y * r.headR);

            r.rElbow = Add(r.shoulder, Dir(p.rUA, ua)); r.rHand = Add(r.rElbow, Dir(p.rUA + p.rFA, fa));
            r.lElbow = Add(r.shoulder, Dir(p.lUA, ua)); r.lHand = Add(r.lElbow, Dir(p.lUA + p.lFA, fa));
            r.weaponAngle = p.rUA + p.rFA + p.wRel;
            return r;
        }

        protected override void Draw(Graphics g, Anim a)
        {
            Rig r = BuildRig(a);
            Color c = Tone(color, a);
            Color back = Tone(Darken(color, 0.65f), a);
            float w = thick * Math.Max(1f, height * 0.8f);

            DrawBehind(g, r, a);
            if (drawLegs) Lines(g, back, w, r.hip, r.lKnee, r.lFoot);
            Lines(g, back, w, r.shoulder, r.lElbow, r.lHand);
            DrawTorso(g, r, a, c, w);
            if (drawLegs) { Lines(g, c, w, r.hip, r.rKnee, r.rFoot); DrawFeet(g, r, a, c, w); }
            if (a.pig) DrawPigHead(g, r.head, r.headR, a); else DrawHead(g, r, a, c, w);
            DrawWeapon(g, r, a, CurrentWeapon(a));
            Lines(g, c, w, r.shoulder, r.rElbow, r.rHand);
            DrawFront(g, r, a, c, w);
        }

        protected virtual void DrawBehind(Graphics g, Rig r, Anim a) { }
        protected virtual void DrawFront(Graphics g, Rig r, Anim a, Color c, float w) { }
        protected virtual void DrawFeet(Graphics g, Rig r, Anim a, Color c, float w) { }

        protected virtual void DrawTorso(Graphics g, Rig r, Anim a, Color c, float w)
        {
            Line(g, c, w, r.hip, r.neckTop);
        }

        protected virtual void DrawHead(Graphics g, Rig r, Anim a, Color c, float w)
        {
            Ring(g, c, w * 0.8f, r.head, r.headR);
            // 目（前方寄りに1点）
            Circle(g, c, new PointF(r.head.X + r.headR * 0.45f, r.head.Y - r.headR * 0.15f), Math.Max(1.2f, r.headR * 0.14f));
        }

        //
        // 武器
        //
        protected void DrawWeapon(Graphics g, Rig r, Anim a, WeaponKind kind)
        {
            if (a.hideWeapon) return;
            float H = height, wt = Math.Max(1f, H * 0.8f);

            // 振り下ろしの剣閃
            if (a.strike > 0.55f && !a.cast && !IsThrust(kind) && kind != WeaponKind.Staff)
            {
                int al = (int)(170 * (a.strike - 0.55f) / 0.45f * a.alpha);
                float R = (17 + 15) * H + WeaponLength(kind) * H * 0.85f;
                PointF[] arc = new PointF[9];
                for (int i = 0; i < arc.Length; i++)
                {
                    float t = Lerp(2.8f, 1.1f, i / 8f);
                    arc[i] = Add(r.shoulder, Dir(t, R));
                }
                using (Pen p = MakePen(Color.FromArgb(Clamp(al), 255, 255, 240), 3f * wt)) g.DrawCurve(p, arc);
            }

            if (kind == WeaponKind.None)
            {
                Circle(g, Tone(color, a), r.rHand, 2.4f * H);   // 素手
                return;
            }
            Weapon w = entity != null ? entity.weapon : null;
            bool sting = w != null && w.engraveName == "Sting";
            bool rust = w != null && w.isRust;
            WeaponArt.Draw(g, kind, r.rHand, r.weaponAngle, H, a, sting, rust, staffOrb);
        }

        internal static float WeaponLength(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Dagger: return 13;
                case WeaponKind.Knife: return 10;
                case WeaponKind.ShortSword: return 25;
                case WeaponKind.LongSword: return 34;
                case WeaponKind.Vorpal: return 40;
                case WeaponKind.Mace: return 20;
                case WeaponKind.Club: return 26;
                case WeaponKind.Pick: return 22;
                case WeaponKind.Staff: return 34;
                case WeaponKind.Cutlass: return 27;
                default: return 4;
            }
        }
    }

    //
    // 武器の絵。人型が手に持つ武器と、戦闘ビューの地面に置かれた武器（ItemDesigns）で共用する。
    // hand を持ち手の位置、ang を刃先の向き（真下=0 の角度）、H を大きさの倍率として描く
    //
    static class WeaponArt
    {
        public static WeaponKind KindOf(Weapon w)
        {
            if (w.engraveName == "Sting") return WeaponKind.Dagger;
            string n = w.name;
            if (n.Contains("Dagger")) return WeaponKind.Dagger;
            if (n.Contains("Mace")) return WeaponKind.Mace;
            if (n.Contains("Short Sword")) return WeaponKind.ShortSword;
            if (n.Contains("Long Sword")) return WeaponKind.LongSword;
            if (n.Contains("Vopal")) return WeaponKind.Vorpal;
            return WeaponKind.ShortSword;
        }

        public static void Draw(Graphics g, WeaponKind kind, PointF h, float ang, float H, Anim a, bool sting, bool rust, Color staffOrb)
        {
            float wt = Math.Max(1f, H * 0.8f);
            // 錆びた刃は赤茶色
            Color steel = Figure.Tone(rust ? Color.FromArgb(165, 90, 55) : Color.FromArgb(215, 220, 230), a);
            Color wood = Figure.Tone(Color.FromArgb(139, 90, 43), a);
            Color gold = Figure.Tone(Color.FromArgb(212, 175, 55), a);

            switch (kind)
            {
                case WeaponKind.Dagger:
                case WeaponKind.Knife:
                {
                    float len = Humanoid.WeaponLength(kind) * H;
                    PointF tip = Figure.Add(h, Figure.Dir(ang, len));
                    if (sting)
                        Figure.Line(g, Color.FromArgb((int)(120 * a.alpha), 135, 206, 250), 5.5f * wt, h, tip);
                    Figure.Line(g, steel, 1.8f * wt, h, tip);
                    Figure.Line(g, gold, 1.8f * wt, Figure.Add(h, Figure.Dir(ang + 1.57f, 3 * H)), Figure.Add(h, Figure.Dir(ang - 1.57f, 3 * H)));
                    break;
                }
                case WeaponKind.ShortSword:
                case WeaponKind.LongSword:
                case WeaponKind.Vorpal:
                {
                    float len = Humanoid.WeaponLength(kind) * H;
                    PointF tip = Figure.Add(h, Figure.Dir(ang, len));
                    if (kind == WeaponKind.Vorpal)
                        Figure.Line(g, Color.FromArgb((int)(110 * a.alpha), 200, 120, 255), 7f * wt, h, tip);
                    Figure.Line(g, steel, (kind == WeaponKind.ShortSword ? 2.2f : 2.6f) * wt, h, tip);
                    Figure.Line(g, gold, 2f * wt, Figure.Add(h, Figure.Dir(ang + 1.57f, 4.5f * H)), Figure.Add(h, Figure.Dir(ang - 1.57f, 4.5f * H)));
                    Figure.Line(g, wood, 2f * wt, h, Figure.Add(h, Figure.Dir(ang, -4 * H)));
                    break;
                }
                case WeaponKind.Mace:
                {
                    PointF head = Figure.Add(h, Figure.Dir(ang, 18 * H));
                    Figure.Line(g, wood, 2.2f * wt, Figure.Add(h, Figure.Dir(ang, -3 * H)), head);
                    for (int i = 0; i < 6; i++)
                        Figure.Line(g, steel, 1.5f * wt, head, Figure.Add(head, Figure.Dir(ang + i * 1.047f, 6.5f * H)));
                    Figure.Circle(g, Figure.Tone(rust ? Color.FromArgb(140, 80, 50) : Color.FromArgb(128, 128, 140), a), head, 4.5f * H);
                    break;
                }
                case WeaponKind.Club:
                {
                    PointF mid = Figure.Add(h, Figure.Dir(ang, 12 * H));
                    PointF tip = Figure.Add(h, Figure.Dir(ang, 26 * H));
                    Figure.Line(g, wood, 3f * wt, Figure.Add(h, Figure.Dir(ang, -3 * H)), mid);
                    Figure.Line(g, wood, 6.5f * wt, mid, tip);
                    Figure.Circle(g, Figure.Tone(Color.FromArgb(100, 62, 30), a), Figure.Add(h, Figure.Dir(ang, 18 * H)), 1.8f * H);
                    break;
                }
                case WeaponKind.Pick:
                {
                    PointF head = Figure.Add(h, Figure.Dir(ang, 22 * H));
                    Figure.Line(g, wood, 2.2f * wt, Figure.Add(h, Figure.Dir(ang, -3 * H)), head);
                    Figure.Lines(g, steel, 2.4f * wt,
                        Figure.Add(head, Figure.Dir(ang + 1.9f, 9 * H)), Figure.Add(head, Figure.Dir(ang + 1.3f, 3 * H)), head,
                        Figure.Add(head, Figure.Dir(ang - 1.3f, 3 * H)), Figure.Add(head, Figure.Dir(ang - 1.9f, 9 * H)));
                    break;
                }
                case WeaponKind.Staff:
                {
                    PointF top = Figure.Add(h, Figure.Dir(ang, 34 * H));
                    Figure.Line(g, wood, 2.4f * wt, Figure.Add(h, Figure.Dir(ang, -14 * H)), top);
                    if (staffOrb != Color.Empty)
                    {
                        float glow = 0.6f + 0.4f * (float)Math.Sin(a.time * 5);
                        Figure.Circle(g, Color.FromArgb((int)(90 * glow * a.alpha), staffOrb), top, 7f * H);
                        Figure.Circle(g, Figure.Tone(staffOrb, a), top, 3.2f * H);
                    }
                    break;
                }
                case WeaponKind.Cutlass:
                {
                    PointF p1 = Figure.Add(h, Figure.Dir(ang, 10 * H));
                    PointF p2 = Figure.Add(Figure.Add(h, Figure.Dir(ang, 20 * H)), Figure.Dir(ang + 1.57f, 3 * H));
                    PointF p3 = Figure.Add(Figure.Add(h, Figure.Dir(ang, 27 * H)), Figure.Dir(ang + 1.57f, 7 * H));
                    using (Pen p = Figure.MakePen(steel, 3f * wt)) g.DrawCurve(p, new[] { h, p1, p2, p3 });
                    using (Pen p = Figure.MakePen(gold, 2f * wt)) g.DrawArc(p, h.X - 4 * H, h.Y - 4 * H, 8 * H, 8 * H, 0, 180);
                    break;
                }
                case WeaponKind.Claw:
                    for (int i = -1; i <= 1; i++)
                        Figure.Line(g, Figure.Tone(Color.FromArgb(240, 240, 220), a), 1.3f * wt, h, Figure.Add(h, Figure.Dir(ang + i * 0.35f, 6 * H)));
                    break;
            }
        }
    }
}
