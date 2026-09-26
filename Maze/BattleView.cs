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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Maze
{
    enum CombatKind
    {
        Hit,        // 近接ヒット
        Crit,       // クリティカルヒット
        Miss,       // はじき返された
        Barrier,    // サファイアの結界に阻まれた
        Pass,       // すり抜けた（Shade）
        Magic,      // Companion の魔法
        Breath,     // Dragon の炎
        Freeze,     // Ice Jerry の凍結
        Charm,      // Siren の魅了
        Polymorph,  // Circe の豚化
        Meet,       // Teiresias との対面（クエスト完了）
    }

    class CombatEvent
    {
        public Entity attacker;
        public Entity defender;
        public CombatKind kind;
        public int damage;
        public bool killed;     // この攻撃で defender が倒れた（Drain 時に判定）
    }

    //
    // 戦闘イベントの記録先。Entity から Logic を参照せずに記録できるよう静的にしている。
    // セーブ対象外（Form の afterAction() で毎回 Drain される）
    //
    static class CombatLog
    {
        static readonly List<CombatEvent> events = new List<CombatEvent>();

        public static void Add(Entity attacker, Entity defender, CombatKind kind, int damage)
        {
            if (attacker == null || defender == null) return;
            if (events.Count >= 64) events.RemoveAt(0);
            events.Add(new CombatEvent { attacker = attacker, defender = defender, kind = kind, damage = damage });
        }

        public static List<CombatEvent> Drain()
        {
            List<CombatEvent> list = new List<CombatEvent>(events);
            events.Clear();
            // 各 defender の最後のイベントに撃破フラグを付ける
            HashSet<Entity> seen = new HashSet<Entity>();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                CombatEvent ev = list[i];
                if (seen.Contains(ev.defender)) continue;
                seen.Add(ev.defender);
                if (ev.kind != CombatKind.Meet && ev.defender.hit <= 0) ev.killed = true;
            }
            return list;
        }
    }

    //
    // 画面左の戦闘ビュー。入力はブロックせず、Play() が呼ばれるたびに新しい場面へ即座に切り替える。
    //
    class BattleView
    {
        const float BaseScale = 1.05f;
        const float FallMs = 300f;

        class Actor
        {
            public Entity e;
            public Figure fig;
            public float x;
            public int dir;
            public float phase;
            public string label;
        }

        class Sched
        {
            public CombatEvent ev;
            public Actor atk, def;
            public float start, dur;
        }

        class Lane
        {
            public float groundY, scale;
            public List<Actor> actors = new List<Actor>();  // 奥から手前の順
            public List<Sched> evs = new List<Sched>();
        }

        readonly PictureBox pic;
        readonly Logic logic;
        readonly Timer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        long sceneStart;
        List<Lane> lanes = new List<Lane>();
        Bitmap bg;
        int bgFloor = -1, bgLaneCount = -1;

        public BattleView(PictureBox pic, Logic logic)
        {
            this.pic = pic;
            this.logic = logic;
            pic.Paint += OnPaint;
            timer = new Timer();
            timer.Interval = 33;
            timer.Tick += (s, e) => pic.Invalidate();
            timer.Start();
        }

        public void Play(List<CombatEvent> events)
        {
            events = events.Where(ev => ev.attacker == logic.hero || ev.defender == logic.hero ||
                                        logic.isEntitySeeable(ev.attacker) || logic.isEntitySeeable(ev.defender)).ToList();
            lanes = events.Count > 0 ? BuildCombat(events) : BuildIdle();
            sceneStart = clock.ElapsedMilliseconds;
            pic.Invalidate();
        }

        //
        // 場面の組み立て
        //

        Entity FocusOf(CombatEvent ev)
        {
            bool ap = ev.attacker.isPartyMember, dp = ev.defender.isPartyMember;
            if (ap && !dp) return ev.defender;
            if (!ap && dp) return ev.attacker;
            return ev.defender;
        }

        Actor MakeActor(Entity e, float x, int dir, int index)
        {
            int ci = logic.companions != null ? logic.companions.IndexOf(e) : -1;
            Actor a = new Actor { e = e, x = x, dir = dir, phase = index * 0.7f };
            a.fig = EnemyDesigns.Create(e, Math.Max(0, ci));
            if (e == logic.hero) a.label = "Hero";
            else if (ci >= 0) a.label = "COM" + (ci + 1);
            else a.label = e.name;
            if (e is Dragon) a.x -= 22;
            return a;
        }

        static readonly float[][] Grounds = { new[] { 285f }, new[] { 162f, 314f }, new[] { 120f, 220f, 320f } };
        static readonly float[] Scales = { 1f, 0.6f, 0.45f };
        static readonly float[] LeftX = { 112f, 70f, 30f };
        const float RightX = 255f;

        List<Lane> BuildCombat(List<CombatEvent> events)
        {
            List<Entity> order = new List<Entity>();
            Dictionary<Entity, List<CombatEvent>> groups = new Dictionary<Entity, List<CombatEvent>>();
            foreach (CombatEvent ev in events)
            {
                Entity f = FocusOf(ev);
                if (!groups.ContainsKey(f)) { groups[f] = new List<CombatEvent>(); order.Add(f); }
                groups[f].Add(ev);
            }
            // Hero が関わる戦いを優先して最大3組
            List<Entity> shown = order
                .OrderBy(f => groups[f].Any(ev => ev.attacker == logic.hero || ev.defender == logic.hero) ? 0 : 1)
                .Take(3).ToList();

            List<Lane> result = new List<Lane>();
            int n = shown.Count;
            for (int li = 0; li < n; li++)
            {
                Entity focus = shown[li];
                List<CombatEvent> evs = groups[focus];
                if (evs.Count > 4) evs = evs.Skip(evs.Count - 4).ToList();

                Lane lane = new Lane { groundY = Grounds[n - 1][li], scale = Scales[n - 1] };
                List<Entity> others = evs.Select(ev => ev.attacker == focus ? ev.defender : ev.attacker)
                                         .Distinct()
                                         .OrderBy(e => e == logic.hero ? 0 : (e.isCompanion ? 1 : 2))
                                         .Take(3).ToList();
                Dictionary<Entity, Actor> map = new Dictionary<Entity, Actor>();
                for (int i = others.Count - 1; i >= 0; i--)
                {
                    Actor a = MakeActor(others[i], LeftX[i], 1, i);
                    lane.actors.Add(a);
                    map[others[i]] = a;
                }
                Actor fa = MakeActor(focus, RightX, -1, 3);
                lane.actors.Add(fa);
                map[focus] = fa;

                float dur = evs.Count > 1 ? 320f : 400f;
                Sched prev = null;
                foreach (CombatEvent ev in evs)
                {
                    if (!map.ContainsKey(ev.attacker) || !map.ContainsKey(ev.defender)) continue;
                    Sched s = new Sched { ev = ev, atk = map[ev.attacker], def = map[ev.defender], dur = dur };
                    if (prev == null) s.start = 0;
                    else if (ev.attacker == prev.ev.attacker && ev.attacker is Scylla) s.start = prev.start;   // スキュラは2つの頭で同時に噛む
                    else if (ev.attacker == prev.ev.attacker || ev.attacker == prev.ev.defender) s.start = prev.start + prev.dur;
                    else s.start = prev.start + prev.dur * 0.5f;
                    lane.evs.Add(s);
                    prev = s;
                }
                result.Add(lane);
            }
            return result;
        }

        List<Lane> BuildIdle()
        {
            Lane lane = new Lane { groundY = Grounds[0][0], scale = Scales[0] };
            List<Entity> party = new List<Entity>();
            if (logic.hero != null && logic.hero.hit > 0) party.Add(logic.hero);
            if (logic.companions != null)
                foreach (Entity c in logic.companions)
                    if (c.hit > 0 && !(c is Companion cc && cc.isInactive)) party.Add(c);
            for (int i = Math.Min(party.Count, 3) - 1; i >= 0; i--) lane.actors.Add(MakeActor(party[i], LeftX[i], 1, i));

            // 視界内で一番近い生き物を向かい側に立たせる
            Entity near = null;
            double best = double.MaxValue;
            if (logic.entitylist != null && logic.hero != null)
            {
                foreach (Entity e in logic.entitylist)
                {
                    if (e.isPartyMember || e.hit <= 0) continue;
                    if (!char.IsLetter(e.graph) && !(e is Teiresias)) continue;
                    if (!logic.isEntitySeeable(e)) continue;
                    double d = Math.Pow(e.xpos - logic.hero.xpos, 2) + Math.Pow(e.ypos - logic.hero.ypos, 2);
                    if (d < best) { best = d; near = e; }
                }
            }
            if (near != null) lane.actors.Add(MakeActor(near, RightX, -1, 3));
            return new List<Lane> { lane };
        }

        //
        // タイムライン
        //

        static float Ease(float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return 1 - (1 - t) * (1 - t);
        }

        static float Clamp01(float t) { return t < 0 ? 0 : (t > 1 ? 1 : t); }

        static void AttackCurve(float lt, float D, out float w, out float k)
        {
            w = k = 0;
            if (lt < 0 || lt >= D) return;
            float wEnd = 0.375f * D, sEnd = 0.55f * D;
            if (lt < wEnd) { w = Ease(lt / wEnd); }
            else if (lt < sEnd) { w = 1; k = Ease((lt - wEnd) / (sEnd - wEnd)); }
            else { w = k = 1 - Ease((lt - sEnd) / (D - sEnd)); }
        }

        static bool IsMelee(CombatKind k)
        {
            return k == CombatKind.Hit || k == CombatKind.Crit || k == CombatKind.Miss || k == CombatKind.Barrier || k == CombatKind.Pass;
        }

        static float Amplitude(CombatEvent ev)
        {
            switch (ev.kind)
            {
                case CombatKind.Crit: return 1.3f;
                case CombatKind.Hit:
                case CombatKind.Magic:
                case CombatKind.Breath: return ev.damage > 0 ? 1f : 0f;
                case CombatKind.Miss:
                case CombatKind.Barrier: return 0.35f;
                case CombatKind.Polymorph: return 0.3f;
                case CombatKind.Freeze: return 0.2f;
                default: return 0f;
            }
        }

        float LungeDist(Sched s, Lane lane)
        {
            float S = BaseScale * lane.scale;
            float gap = Math.Abs(s.def.x - s.atk.x) - (s.def.fig.HalfWidth + 22f) * S;
            return Math.Max(0, Math.Min(gap, s.atk.fig.MaxLunge * S));
        }

        Anim ComputeAnim(Lane lane, Actor actor, float t, float time, out float off)
        {
            Anim a = new Anim { time = time + actor.phase, dir = actor.dir };
            off = 0;
            bool pendFreeze = false, pendCharm = false, pendPig = false;
            bool hitFreeze = false, hitCharm = false, hitPig = false;

            foreach (Sched s in lane.evs)
            {
                float lt = t - s.start, D = s.dur;
                CombatKind kind = s.ev.kind;
                if (s.atk == actor)
                {
                    float w, k;
                    AttackCurve(lt, D, out w, out k);
                    a.windup = Math.Max(a.windup, w);
                    a.strike = Math.Max(a.strike, k);
                    if (IsMelee(kind)) off += actor.dir * k * LungeDist(s, lane);
                    if ((kind == CombatKind.Magic || kind == CombatKind.Polymorph || kind == CombatKind.Meet) && lt >= 0 && lt < D) a.cast = true;
                }
                if (s.def == actor)
                {
                    float it = lt - 0.5f * D;
                    bool pending = it < 0;
                    if (kind == CombatKind.Freeze) { if (pending) pendFreeze = true; else hitFreeze = true; }
                    if (kind == CombatKind.Charm) { if (pending) pendCharm = true; else hitCharm = true; }
                    if (kind == CombatKind.Polymorph) { if (it < 0.2f * D) pendPig = true; else hitPig = true; }
                    if (pending) continue;

                    float amp = Amplitude(s.ev);
                    float r = 0;
                    if (it < 0.15f * D) r = Ease(it / (0.15f * D));
                    else if (it < 0.5f * D) r = 1 - Ease((it - 0.15f * D) / (0.35f * D));
                    if (s.ev.killed && it >= 0.15f * D) r = 1;
                    a.recoil = Math.Max(a.recoil, r * amp);
                    off -= actor.dir * r * amp * 10f * lane.scale;

                    if (s.ev.damage > 0 && it < 0.3f * D)
                    {
                        float fade = 1 - it / (0.3f * D);
                        a.flash = Math.Max(a.flash, fade);
                        off += (float)Math.Sin(it * 0.8f) * 3f * fade;
                    }
                    if (kind == CombatKind.Pass && it < 0.45f * D)
                        a.alpha = Math.Min(a.alpha, 0.3f + 0.4f * (float)Math.Abs(Math.Sin(it * 0.05f)));
                    if (s.ev.killed)
                    {
                        float f = (lt - D) / FallMs;
                        if (f > 0) a.fall = Math.Max(a.fall, Ease(f));
                    }
                }
            }

            // 状態異常の表示は生きている間だけ。死んだ Entity は move() で frozen 等が減らないため、
            // 凍結中に倒された敵は frozen > 0 のまま残る（死体に氷のブロックが出てしまう）
            Entity e = actor.e;
            bool alive = e.hit > 0;
            a.frozen = alive && ((e.frozen > 0 && !pendFreeze) || hitFreeze);
            a.charmed = alive && ((e.charmed > 0 && !pendCharm) || hitCharm);
            a.pig = (e.polymorphed > 0 && !pendPig) || hitPig;
            return a;
        }

        //
        // 描画
        //

        void OnPaint(object sender, PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            long now = clock.ElapsedMilliseconds;
            float t = now - sceneStart;
            float time = now / 1000f;

            EnsureBackground();
            g.DrawImageUnscaled(bg, 0, 0);

            PointF shake = ComputeShake(t);
            g.TranslateTransform(shake.X, shake.Y);
            foreach (Lane lane in lanes) DrawLane(g, lane, t, time);
            g.ResetTransform();

            DrawOutlined(g, "地下" + logic.floor + "階  " + FloorTitle(logic.floor), 9f, Color.FromArgb(220, 230, 220, 200), new PointF(6, 4), false);
        }

        static string FloorTitle(int floor)
        {
            switch (floor)
            {
                case 5: return "一つ目の巨人の洞窟";
                case 6: return "スキュラとカリュブディスの海峡";
                case 7: return "キルケーの島と冥府";
                default: return "迷宮";
            }
        }

        void DrawLane(Graphics g, Lane lane, float t, float time)
        {
            float S = BaseScale * lane.scale;
            Dictionary<Actor, Anim> anims = new Dictionary<Actor, Anim>();
            Dictionary<Actor, float> offs = new Dictionary<Actor, float>();
            foreach (Actor a in lane.actors)
            {
                float off;
                anims[a] = ComputeAnim(lane, a, t, time, out off);
                offs[a] = off;
            }

            // 影
            using (Brush b = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                foreach (Actor a in lane.actors)
                {
                    float hw = a.fig.HalfWidth * S * (1 + anims[a].fall * 0.8f);
                    g.FillEllipse(b, a.x + offs[a] - hw, lane.groundY - 3 * lane.scale - 1, hw * 2, 6 * lane.scale + 2);
                }

            // テイレシアスの金色の光（キャラより奥）
            foreach (Sched s in lane.evs)
                if (s.ev.kind == CombatKind.Meet) DrawMeetGlow(g, lane, s, t);

            // キャラ（奥から手前へ。攻撃中のキャラを最前面に）
            List<Actor> drawOrder = lane.actors.OrderBy(a => anims[a].strike > 0 ? 1 : 0).ToList();
            foreach (Actor a in drawOrder)
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(a.x + offs[a], lane.groundY);
                g.ScaleTransform(a.dir * S, S);
                a.fig.Render(g, anims[a]);
                g.Restore(st);
            }

            foreach (Sched s in lane.evs) DrawEffect(g, lane, s, t, offs);
            foreach (Actor a in lane.actors) DrawLabel(g, lane, a, t, offs[a]);
            DrawTexts(g, lane, t);
        }

        PointF MouthOf(Actor a, Lane lane, float off)
        {
            float S = BaseScale * lane.scale;
            PointF m = a.fig.Mouth;
            return new PointF(a.x + off + a.dir * m.X * S, lane.groundY + m.Y * S);
        }

        PointF CenterOf(Actor a, Lane lane, float off)
        {
            return new PointF(a.x + off, lane.groundY + a.fig.CenterY * BaseScale * lane.scale);
        }

        PointF HeadOf(Actor a, Lane lane, float off)
        {
            return new PointF(a.x + off, lane.groundY + a.fig.Top * BaseScale * lane.scale);
        }

        static PointF LerpP(PointF a, PointF b, float t) { return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }

        void DrawEffect(Graphics g, Lane lane, Sched s, float t, Dictionary<Actor, float> offs)
        {
            float lt = t - s.start, D = s.dur, it = lt - 0.5f * D;
            if (lt < 0 || lt > D * 2.5f) return;
            float S = BaseScale * lane.scale;
            PointF from = MouthOf(s.atk, lane, offs[s.atk]);
            PointF to = CenterOf(s.def, lane, offs[s.def]);
            float dx = to.X - from.X, dy = to.Y - from.Y, len = (float)Math.Sqrt(dx * dx + dy * dy) + 0.001f;
            PointF perp = new PointF(-dy / len, dx / len);

            switch (s.ev.kind)
            {
                case CombatKind.Hit:
                case CombatKind.Crit:
                case CombatKind.Magic:
                    if (s.ev.kind == CombatKind.Magic)
                    {
                        float u = (lt - 0.3f * D) / (0.2f * D);
                        for (int i = 4; i >= 0; i--)
                        {
                            float ui = u - i * 0.08f;
                            if (ui < 0 || ui > 1) continue;
                            int al = 230 - i * 45;
                            float r = (5f - i * 0.8f) * S + 1;
                            using (Brush b = new SolidBrush(Color.FromArgb(al, 120, 255, 255)))
                                g.FillEllipse(b, from.X + dx * ui - r, from.Y + dy * ui - r, r * 2, r * 2);
                        }
                        if (it >= 0 && it < 0.3f * D)
                        {
                            float r = (6 + it * 0.25f) * S;
                            using (Pen p = new Pen(Color.FromArgb((int)(220 * (1 - it / (0.3f * D))), 0, 255, 255), 2.5f))
                                g.DrawEllipse(p, to.X - r, to.Y - r, r * 2, r * 2);
                        }
                    }
                    if (it >= 0 && it < 0.18f * D && s.ev.damage > 0)
                    {
                        bool crit = s.ev.kind == CombatKind.Crit;
                        float k = it / (0.18f * D);
                        float r0 = 5 * S, r1 = (crit ? 26 : 16) * S * (0.5f + k);
                        Color c = crit ? Color.FromArgb((int)(255 * (1 - k)), 255, 220, 60) : Color.FromArgb((int)(255 * (1 - k)), 255, 255, 230);
                        using (Pen p = new Pen(c, crit ? 3f : 2f))
                            for (int i = 0; i < 8; i++)
                            {
                                double ang = i * Math.PI / 4 + 0.3;
                                g.DrawLine(p, to.X + (float)Math.Cos(ang) * r0, to.Y + (float)Math.Sin(ang) * r0,
                                              to.X + (float)Math.Cos(ang) * r1, to.Y + (float)Math.Sin(ang) * r1);
                            }
                    }
                    break;

                case CombatKind.Barrier:
                    if (it > -0.05f * D && it < 0.4f * D)
                    {
                        float k = Clamp01((it + 0.05f * D) / (0.45f * D));
                        PointF c = new PointF(s.def.x + offs[s.def] + s.def.dir * (s.def.fig.HalfWidth + 8) * S, to.Y);
                        float r = 22 * S;
                        PointF[] hex = new PointF[6];
                        for (int i = 0; i < 6; i++) hex[i] = new PointF(c.X + (float)Math.Cos(i * Math.PI / 3) * r * 0.55f, c.Y + (float)Math.Sin(i * Math.PI / 3) * r);
                        int al = (int)(200 * (1 - k));
                        using (Brush b = new SolidBrush(Color.FromArgb(al / 3, 80, 140, 255))) g.FillPolygon(b, hex);
                        using (Pen p = new Pen(Color.FromArgb(al, 140, 190, 255), 2.5f)) g.DrawPolygon(p, hex);
                    }
                    break;

                case CombatKind.Breath:
                    if (lt > 0.3f * D && lt < 0.9f * D)
                    {
                        float grow = Clamp01((lt - 0.3f * D) / (0.15f * D));
                        float fade = lt > 0.7f * D ? 1 - (lt - 0.7f * D) / (0.2f * D) : 1;
                        float spread = 18 * S * grow + 4;
                        PointF tip = new PointF(from.X + dx * grow * 1.15f, from.Y + dy * grow * 1.15f);
                        PointF[] cone = {
                            from,
                            new PointF(tip.X + perp.X * spread, tip.Y + perp.Y * spread),
                            new PointF(tip.X + dx / len * 10 * S, tip.Y + dy / len * 10 * S),
                            new PointF(tip.X - perp.X * spread, tip.Y - perp.Y * spread),
                        };
                        if (grow > 0.05f)
                            using (LinearGradientBrush b = new LinearGradientBrush(from, tip,
                                       Color.FromArgb((int)(240 * fade), 255, 240, 120), Color.FromArgb((int)(170 * fade), 255, 70, 0)))
                                g.FillPolygon(b, cone);
                        for (int i = 0; i < 6; i++)
                        {
                            float u = ((lt * 0.004f + i / 6f) % 1f) * grow;
                            float wob = (float)Math.Sin(lt * 0.05f + i * 2) * spread * 0.6f * u;
                            float r = (3 + 5 * u) * S;
                            using (Brush b = new SolidBrush(Color.FromArgb((int)(200 * fade), 255, 200, 50)))
                                g.FillEllipse(b, from.X + dx * u + perp.X * wob - r, from.Y + dy * u + perp.Y * wob - r, r * 2, r * 2);
                        }
                    }
                    break;

                case CombatKind.Freeze:
                    for (int i = 0; i < 10; i++)
                    {
                        float u = (lt - 0.25f * D) / (0.3f * D) - i * 0.06f;
                        if (u < 0 || u > 1) continue;
                        float j = (float)Math.Sin(i * 1.7f) * 8 * S;
                        using (Brush b = new SolidBrush(Color.FromArgb(220, 220, 250, 255)))
                            g.FillEllipse(b, from.X + dx * u + perp.X * j - 2.5f, from.Y + dy * u + perp.Y * j - 2.5f, 5, 5);
                    }
                    break;

                case CombatKind.Charm:
                    using (Font f = new Font("MS UI Gothic", 10 + 6 * lane.scale, FontStyle.Bold))
                        for (int i = 0; i < 3; i++)
                        {
                            float u = (lt - 0.15f * D) / (0.4f * D) - i * 0.15f;
                            if (u < 0 || u > 1) continue;
                            float wv = (float)Math.Sin(u * 10 + i) * 10 * S;
                            using (Brush b = new SolidBrush(Color.FromArgb(230, 255, 130, 200)))
                                g.DrawString("♪", f, b, from.X + dx * u + perp.X * wv - 6, from.Y + dy * u + perp.Y * wv - 10);
                        }
                    break;

                case CombatKind.Polymorph:
                    if (lt > 0.3f * D && lt < 0.8f * D)
                    {
                        float head = Clamp01((lt - 0.3f * D) / (0.2f * D));
                        float fade = lt > 0.6f * D ? 1 - (lt - 0.6f * D) / (0.2f * D) : 1;
                        PointF[] pts = new PointF[20];
                        for (int i = 0; i < pts.Length; i++)
                        {
                            float u = head * i / (pts.Length - 1);
                            float wv = (float)Math.Sin(i * 1.2f - lt * 0.03f) * 6 * S;
                            pts[i] = new PointF(from.X + dx * u + perp.X * wv, from.Y + dy * u + perp.Y * wv);
                        }
                        using (Pen p = new Pen(Color.FromArgb((int)(230 * fade), 220, 80, 255), 2.5f)) g.DrawCurve(p, pts);
                    }
                    if (it >= 0 && it < 0.5f * D)
                    {
                        float k = it / (0.5f * D);
                        for (int i = 0; i < 6; i++)
                        {
                            float r = (8 + k * 14) * S;
                            double ang = i * Math.PI / 3 + k;
                            float cx = to.X + (float)Math.Cos(ang) * r * 0.8f, cy = to.Y + (float)Math.Sin(ang) * r * 0.8f;
                            using (Brush b = new SolidBrush(Color.FromArgb((int)(180 * (1 - k)), 200, 200, 210)))
                                g.FillEllipse(b, cx - r * 0.6f, cy - r * 0.6f, r * 1.2f, r * 1.2f);
                        }
                    }
                    break;
            }
        }

        void DrawMeetGlow(Graphics g, Lane lane, Sched s, float t)
        {
            float lt = t - s.start;
            if (lt < 0) return;
            float k = Clamp01(lt / s.dur);
            float S = BaseScale * lane.scale;
            PointF c = CenterOf(s.atk, lane, 0);
            float r = 70 * S * (0.9f + 0.1f * (float)Math.Sin(lt * 0.004f));
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(c.X - r, c.Y - r, r * 2, r * 2);
                using (PathGradientBrush b = new PathGradientBrush(path))
                {
                    b.CenterColor = Color.FromArgb((int)(150 * k), 255, 215, 100);
                    b.SurroundColors = new[] { Color.FromArgb(0, 255, 215, 100) };
                    g.FillPath(b, path);
                }
            }
        }

        void DrawLabel(Graphics g, Lane lane, Actor a, float t, float off)
        {
            // 着弾前のダメージは HP に反映しない
            int hp = a.e.hit;
            foreach (Sched s in lane.evs)
                if (s.def == a && t - s.start < 0.5f * s.dur) hp += s.ev.damage;
            hp = Math.Max(0, Math.Min(hp, a.e.hitmax));

            float x = a.x + off, y = lane.groundY + 4 * lane.scale + 1;
            float fs = lane.scale >= 0.9f ? 8.5f : 7f;
            DrawOutlined(g, a.label, fs, Color.FromArgb(235, 235, 235), new PointF(x, y), true);
            float bw = 38 * Math.Max(0.6f, lane.scale), bh = 3.5f, by = y + fs * 1.6f + 1;
            float ratio = a.e.hitmax > 0 ? (float)hp / a.e.hitmax : 0;
            Color hc = ratio > 0.5f ? Color.LimeGreen : (ratio > 0.25f ? Color.Gold : Color.Red);
            using (Brush b = new SolidBrush(Color.FromArgb(160, 0, 0, 0))) g.FillRectangle(b, x - bw / 2 - 1, by - 1, bw + 2, bh + 2);
            using (Brush b = new SolidBrush(hc)) g.FillRectangle(b, x - bw / 2, by, bw * ratio, bh);
        }

        void DrawTexts(Graphics g, Lane lane, float t)
        {
            float fs = 10f + 4f * lane.scale;
            for (int si = 0; si < lane.evs.Count; si++)
            {
                Sched s = lane.evs[si];
                float lt = t - s.start, it = lt - 0.5f * s.dur;
                // 同じ相手への直前のテキストと重ならないよう段をずらす
                int stack = 0;
                for (int pj = 0; pj < si; pj++)
                    if (lane.evs[pj].def == s.def && s.start - lane.evs[pj].start < 700) stack++;

                string text; Color c;
                TextFor(s.ev, out text, out c);
                Actor at = s.ev.kind == CombatKind.Meet ? s.atk : s.def;
                if (it >= 0 && it < 900 && text != null)
                {
                    float al = it < 600 ? 1 : 1 - (it - 600) / 300f;
                    PointF h = HeadOf(at, lane, 0);
                    DrawOutlined(g, text, s.ev.kind == CombatKind.Crit ? fs + 2 : fs,
                                 Color.FromArgb((int)(255 * al), c), new PointF(h.X, h.Y - 14 - it * 0.03f - stack * 15), true);
                }
                float kt = lt - s.dur - FallMs;
                if (s.ev.killed && kt >= 0 && kt < 900)
                {
                    float al = kt < 600 ? 1 : 1 - (kt - 600) / 300f;
                    PointF h = CenterOf(s.def, lane, 0);
                    string msg = s.def.e.isPartyMember ? "倒れた…" : "撃破！";
                    DrawOutlined(g, msg, fs, Color.FromArgb((int)(255 * al), 255, 90, 70), new PointF(h.X, h.Y - kt * 0.02f), true);
                }
            }
        }

        static void TextFor(CombatEvent ev, out string text, out Color c)
        {
            switch (ev.kind)
            {
                case CombatKind.Hit: text = "-" + ev.damage; c = Color.White; break;
                case CombatKind.Crit: text = "CRITICAL -" + ev.damage; c = Color.Gold; break;
                case CombatKind.Miss: text = "はじかれた"; c = Color.Silver; break;
                case CombatKind.Barrier: text = "結界！"; c = Color.LightSkyBlue; break;
                case CombatKind.Pass: text = "すり抜けた"; c = Color.Lavender; break;
                case CombatKind.Magic: text = "-" + ev.damage; c = Color.Cyan; break;
                case CombatKind.Breath:
                    if (ev.damage > 0) { text = "-" + ev.damage; c = Color.Orange; }
                    else { text = "かわした"; c = Color.Silver; }
                    break;
                case CombatKind.Freeze: text = "凍結！"; c = Color.PaleTurquoise; break;
                case CombatKind.Charm: text = "魅了！"; c = Color.HotPink; break;
                case CombatKind.Polymorph: text = "豚化！"; c = Color.Pink; break;
                case CombatKind.Meet: text = "予言者テイレシアス"; c = Color.Gold; break;
                default: text = null; c = Color.White; break;
            }
        }

        static void DrawOutlined(Graphics g, string s, float size, Color c, PointF p, bool center)
        {
            using (Font f = new Font("MS UI Gothic", size, FontStyle.Bold))
            {
                SizeF sz = g.MeasureString(s, f);
                float x = center ? p.X - sz.Width / 2 : p.X, y = p.Y;
                using (Brush sh = new SolidBrush(Color.FromArgb(c.A * 3 / 4, 0, 0, 0)))
                {
                    g.DrawString(s, f, sh, x - 1, y); g.DrawString(s, f, sh, x + 1, y);
                    g.DrawString(s, f, sh, x, y - 1); g.DrawString(s, f, sh, x, y + 1);
                }
                using (Brush b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
            }
        }

        PointF ComputeShake(float t)
        {
            float y = 0;
            foreach (Lane lane in lanes)
                foreach (Sched s in lane.evs)
                {
                    float lt = t - s.start, it = lt - 0.5f * s.dur;
                    bool heavy = s.atk.e is Polyphemus || (s.atk.e is Dragon && s.ev.kind == CombatKind.Breath);
                    if (heavy && s.ev.damage > 0 && it >= 0 && it < 0.4f * s.dur)
                        y += (float)Math.Sin(it * 0.9f) * 5f * (1 - it / (0.4f * s.dur)) * lane.scale;
                    // 巨体が倒れた地響き
                    float kt = lt - s.dur - FallMs;
                    if (s.ev.killed && (s.def.e is Polyphemus || s.def.e is Dragon) && kt >= 0 && kt < 200)
                        y += (float)Math.Sin(kt * 0.9f) * 4f * (1 - kt / 200f);
                }
            return new PointF(0, y);
        }

        //
        // 背景（フロアと場面の段数ごとにキャッシュ）
        //
        void EnsureBackground()
        {
            if (bg != null && bgFloor == logic.floor && bgLaneCount == lanes.Count) return;
            bgFloor = logic.floor;
            bgLaneCount = lanes.Count;
            if (bg != null) bg.Dispose();
            int W = Math.Max(1, pic.Width), H = Math.Max(1, pic.Height);
            bg = new Bitmap(W, H);
            using (Graphics g = Graphics.FromImage(bg))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color top, bottom, ground;
                int floor = logic.floor;
                if (floor == 5) { top = Color.FromArgb(62, 46, 32); bottom = Color.FromArgb(28, 20, 14); ground = Color.FromArgb(78, 60, 42); }
                else if (floor == 6) { top = Color.FromArgb(24, 44, 76); bottom = Color.FromArgb(10, 20, 38); ground = Color.FromArgb(46, 52, 62); }
                else if (floor == 7) { top = Color.FromArgb(40, 18, 52); bottom = Color.FromArgb(10, 5, 16); ground = Color.FromArgb(44, 32, 50); }
                else { top = Color.FromArgb(62, 60, 70); bottom = Color.FromArgb(28, 27, 32); ground = Color.FromArgb(70, 62, 55); }

                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, W, H), top, bottom, 90f))
                    g.FillRectangle(b, 0, 0, W, H);

                Random rnd = new Random(floor * 7919);
                if (floor >= 1 && floor <= 4)
                {
                    using (Pen p = new Pen(Color.FromArgb(22, 255, 255, 255), 1))
                        for (int y = 20, row = 0; y < H; y += 22, row++)
                        {
                            g.DrawLine(p, 0, y, W, y);
                            for (int x = (row % 2) * 24; x < W; x += 48) g.DrawLine(p, x, y, x, y + 22);
                        }
                    foreach (int tx in new[] { 40, W - 40 }) DrawTorch(g, tx, 58);
                }
                else if (floor == 5)
                {
                    using (Brush b = new SolidBrush(Color.FromArgb(96, 74, 52)))
                        for (int i = 0; i < 14; i++)
                        {
                            float x = rnd.Next(W), w = 8 + rnd.Next(14), h = 20 + rnd.Next(50);
                            g.FillPolygon(b, new[] { new PointF(x - w / 2, 0), new PointF(x + w / 2, 0), new PointF(x, h) });
                        }
                }
                else if (floor == 6)
                {
                    float hz = H * 0.3f;
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, hz, W, H - hz), Color.FromArgb(30, 70, 115), Color.FromArgb(12, 30, 55), 90f))
                        g.FillRectangle(b, 0, hz, W, H - hz);
                    using (Pen p = new Pen(Color.FromArgb(50, 170, 210, 255), 1))
                        for (int i = 0; i < 40; i++)
                        {
                            float x = rnd.Next(W), y = hz + 6 + rnd.Next((int)(H - hz));
                            g.DrawArc(p, x, y, 14, 5, 200, 140);
                        }
                    using (Brush b = new SolidBrush(Color.FromArgb(20, 24, 32)))
                    {
                        g.FillPolygon(b, new[] { new PointF(0, 20), new PointF(40, 60), new PointF(28, H * 0.5f), new PointF(0, H * 0.6f) });
                        g.FillPolygon(b, new[] { new PointF(W, 10), new PointF(W - 50, 70), new PointF(W - 30, H * 0.5f), new PointF(W, H * 0.55f) });
                    }
                }
                else if (floor == 7)
                {
                    for (int i = 0; i < 12; i++)
                        using (Brush b = new SolidBrush(Color.FromArgb(22, 150, 90, 190)))
                            g.FillEllipse(b, rnd.Next(W) - 60, rnd.Next(H) - 20, 120 + rnd.Next(80), 30 + rnd.Next(30));
                    using (Brush b = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                        foreach (int px in new[] { 30, W - 50 }) g.FillRectangle(b, px, 20, 20, H);
                }

                // 各段の地面とスポットライト
                int n = Math.Max(1, lanes.Count);
                for (int i = 0; i < n; i++)
                {
                    float gy = Grounds[n - 1][i], sc = Scales[n - 1];
                    float lh = 150 * sc;
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(20, gy - lh, W - 40, lh * 1.3f);
                        using (PathGradientBrush b = new PathGradientBrush(path))
                        {
                            b.CenterColor = Color.FromArgb(50, 255, 235, 200);
                            b.SurroundColors = new[] { Color.FromArgb(0, 255, 235, 200) };
                            g.FillPath(b, path);
                        }
                    }
                    float gh = (i == n - 1) ? H - gy : 24 * sc;
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(0, gy - 1, W, gh + 2), ground, Color.FromArgb(0, ground), 90f))
                        g.FillRectangle(b, 0, gy, W, gh);
                    using (Pen p = new Pen(Color.FromArgb(90, 255, 255, 255), 1)) g.DrawLine(p, 0, gy, W, gy);
                }
            }
        }

        static void DrawTorch(Graphics g, float x, float y)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(x - 45, y - 45, 90, 90);
                using (PathGradientBrush b = new PathGradientBrush(path))
                {
                    b.CenterColor = Color.FromArgb(70, 255, 160, 60);
                    b.SurroundColors = new[] { Color.FromArgb(0, 255, 160, 60) };
                    g.FillPath(b, path);
                }
            }
            using (Pen p = new Pen(Color.FromArgb(90, 70, 50), 3)) g.DrawLine(p, x, y + 4, x, y + 18);
            using (Brush b = new SolidBrush(Color.FromArgb(255, 140, 30))) g.FillEllipse(b, x - 4, y - 8, 8, 12);
            using (Brush b = new SolidBrush(Color.FromArgb(255, 230, 120))) g.FillEllipse(b, x - 2, y - 3, 4, 6);
        }
    }
}
