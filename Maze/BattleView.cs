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
        Frostbite,  // 凍傷（Ice Jerry に凍らされている間に HP が減る。attacker は凍らせた Ice Jerry）
        Meet,       // Teiresias との対面（クエスト完了）
        // ここから下は戦闘以外の場面
        Pickup,     // 物を拾う（死体を食べる・金貨を拾うを含む）
        Use,        // Potion を飲む・Scroll を読む
        Fall,       // 落とし穴・カリュブディスに落ちる
        Dig,        // Dwarf の壁掘り
        Talk,       // Hobbit のあいさつ・Bilbo の依頼
        Give,       // Bilbo に Sting を渡す
        Embed,      // 祭壇に宝石を嵌め込む
        GemsComplete, // 4つの祭壇に宝石が揃った
    }

    // Potion・Scroll の効果（戦闘ビューの演出の種類）
    enum UseEffect { None, Healing, Poison, GainStrength, LoseStrength, Amnesia, Identify, EnchantWeapon, EnchantArmor, ProtectWeapon, ProtectArmor, Sleep }

    // Dwarf の壁掘りの段階
    enum DigStage { Knock, Break, Gold }

    class CombatEvent
    {
        public Entity attacker;     // 攻撃した者（戦闘以外の場面では、その場面の主役）
        public Entity defender;     // 攻撃された者（戦闘以外では相手。いなければ null）
        public CombatKind kind;
        public int damage;          // ダメージ（Pickup では金貨の額、Give では報酬の金貨）
        public bool killed;         // この攻撃で defender が倒れた（Drain 時に判定）
        public Entity item;         // Pickup・Use・Give・Embed の対象の物
        public UseEffect effect;    // Use の効果（各効果の関数が SetUseEffect で記録する）
        public bool success = true; // Use・Embed が効いたか
        public string text;         // Talk の台詞
        public int floor;           // Fall: 落ちる前の階
        public bool charybdis;      // Fall: カリュブディスの渦か
        public DigStage dig;        // Dig の段階
    }

    //
    // 戦闘イベントの記録先。Entity から Logic を参照せずに記録できるよう静的にしている。
    // セーブ対象外（Form の afterAction() で毎回 Drain される）
    //
    static class CombatLog
    {
        static readonly List<CombatEvent> events = new List<CombatEvent>();

        static void Push(CombatEvent ev)
        {
            if (events.Count >= 64) events.RemoveAt(0);
            events.Add(ev);
        }

        // 戦闘（と Teiresias との対面）か。これ以外は戦闘以外の場面
        public static bool IsCombat(CombatKind k) { return k <= CombatKind.Meet; }

        public static void Add(Entity attacker, Entity defender, CombatKind kind, int damage)
        {
            if (attacker == null || defender == null) return;
            Push(new CombatEvent { attacker = attacker, defender = defender, kind = kind, damage = damage });
        }

        public static void AddPickup(Entity who, Entity item)
        {
            Push(new CombatEvent { attacker = who, defender = item, item = item, kind = CombatKind.Pickup, damage = (item is Gold) ? item.hit : 0 });
        }

        public static void AddUse(Entity user, Entity item)
        {
            Push(new CombatEvent { attacker = user, item = item, kind = CombatKind.Use });
        }

        // Potion・Scroll の効果の関数から呼ぶ。直前の AddUse に効果の種類と成否を書き込む
        public static void SetUseEffect(Entity user, UseEffect effect, bool success)
        {
            for (int i = events.Count - 1; i >= 0; i--)
            {
                CombatEvent ev = events[i];
                if (ev.kind != CombatKind.Use || ev.attacker != user || ev.effect != UseEffect.None) continue;
                ev.effect = effect;
                ev.success = success;
                return;
            }
        }

        public static void AddFall(Entity who, int floor, bool charybdis)
        {
            Push(new CombatEvent { attacker = who, kind = CombatKind.Fall, floor = floor, charybdis = charybdis });
        }

        public static void AddDig(Entity dwarf, DigStage stage)
        {
            Push(new CombatEvent { attacker = dwarf, kind = CombatKind.Dig, dig = stage });
        }

        public static void AddTalk(Entity speaker, Entity listener, string text)
        {
            Push(new CombatEvent { attacker = speaker, defender = listener, kind = CombatKind.Talk, text = text });
        }

        public static void AddGive(Entity giver, Entity receiver, Entity item, int gold)
        {
            Push(new CombatEvent { attacker = giver, defender = receiver, item = item, damage = gold, kind = CombatKind.Give });
        }

        public static void AddEmbed(Entity who, Entity gem, Entity altar, bool success)
        {
            Push(new CombatEvent { attacker = who, defender = altar, item = gem, success = success, kind = CombatKind.Embed });
        }

        public static void AddGemsComplete(Entity hero)
        {
            Push(new CombatEvent { attacker = hero, kind = CombatKind.GemsComplete });
        }

        public static List<CombatEvent> Drain()
        {
            List<CombatEvent> list = new List<CombatEvent>(events);
            events.Clear();
            // 各 defender の最後の戦闘イベントに撃破フラグを付ける
            HashSet<Entity> seen = new HashSet<Entity>();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                CombatEvent ev = list[i];
                if (!IsCombat(ev.kind) || ev.kind == CombatKind.Meet || ev.defender == null) continue;
                if (seen.Contains(ev.defender)) continue;
                seen.Add(ev.defender);
                if (ev.defender.hit <= 0) ev.killed = true;
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
        const float FallMs = 300f;          // 撃破されて倒れ込む時間
        const float FallSceneMs = 700f;     // 穴に落ちる場面の長さ
        const float TitleMs = 1600f;        // 階名タイトルを出している時間

        class Actor
        {
            public Entity e;            // Entity でないもの（穴・岩の壁・階段）は null
            public Figure fig;
            public float x;
            public int dir;
            public float phase;
            public string label;
            public bool hpBar = true;   // HP バーを出すか（物・穴・壁・階段は出さない）
            public bool labelTop;       // 名前を絵の上に出す（階段）
            public bool backdrop;       // 背景として一番奥に描く（階段）
            public bool frontRow;       // 手前の地面に置く（待機画面の拾えるもの・祭壇）
            public int labelRow;        // 手前の列の名前を上下にずらして重ならないようにする
            public float labelX;        // 手前の列の名前の中心（LayoutFrontLabels が決める）
        }

        class Sched
        {
            public CombatEvent ev;
            public Actor atk, def;
            public float start, dur;
            public Figure itemFig;      // 手に持つ・飛んでいく物の絵（Use・Give・Embed）
        }

        class Lane
        {
            public float groundY, scale;
            public bool hero;           // Hero が関わる段（表示の優先度が高い）
            public List<Actor> actors = new List<Actor>();  // 奥から手前の順
            public List<Sched> evs = new List<Sched>();
        }

        // 1フレーム分の位置の動き（Anim は姿勢、Motion は位置・大きさ・回転）
        class Motion
        {
            public float off, yoff, rot;
            public float scale = 1f, alpha = 1f;
            public bool hidden;
        }

        readonly PictureBox pic;
        readonly Logic logic;
        readonly Timer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        long sceneStart;
        List<Lane> lanes = new List<Lane>();
        Bitmap bg;
        int bgFloor = -1, bgLaneCount = -1;
        int lastFloor = -1;             // 前回の場面の階（変わったら階名タイトルを出す）
        long titleStart = -100000;
        int fallFloor = -1;             // Hero が穴に落ちた場面は、落ちる前の階の背景で描く

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
            events = events.Where(Visible).ToList();
            long now = clock.ElapsedMilliseconds;
            CombatEvent heroFall = events.FirstOrDefault(ev => ev.kind == CombatKind.Fall && ev.attacker == logic.hero);
            fallFloor = heroFall != null ? heroFall.floor : -1;
            lanes = events.Count > 0 ? Build(events) : BuildIdle();
            sceneStart = now;
            if (events.Count > 0) SoundFx.PlayScene(CollectSounds());   // 待機画面では鳴らさない（前の音はそのまま）
            if (lastFloor != logic.floor)
            {
                // 階が変わった（階段・落下・ワープ・ロード）。落下の場面のあとに階名を出す
                if (lastFloor != -1) titleStart = now + (heroFall != null ? (long)FallSceneMs : 0);
                lastFloor = logic.floor;
            }
            pic.Invalidate();
        }

        //
        // 効果音: 表示する場面の各イベントから（音, 鳴らす時刻ms）を集める。時刻はアニメーションに合わせる
        //
        List<KeyValuePair<Sfx, float>> CollectSounds()
        {
            List<KeyValuePair<Sfx, float>> cues = new List<KeyValuePair<Sfx, float>>();
            foreach (Lane lane in lanes)
                foreach (Sched s in lane.evs)
                {
                    float t0 = s.start, D = s.dur;
                    CombatEvent ev = s.ev;
                    switch (ev.kind)
                    {
                        case CombatKind.Pickup:
                            // 物が手元に届く頃に鳴らす（金貨・ポーション・巻物のみ）
                            if (ev.item is Gold) Cue(cues, Sfx.Coin, t0 + 0.55f * D);
                            else if (ev.item is Potion) Cue(cues, Sfx.Potion, t0 + 0.55f * D);
                            else if (ev.item is Scroll) Cue(cues, Sfx.Scroll, t0 + 0.55f * D);
                            break;
                        case CombatKind.Breath: Cue(cues, Sfx.Breath, t0 + 0.3f * D); break;
                        case CombatKind.Freeze: Cue(cues, Sfx.IceFreeze, t0 + 0.3f * D); break;
                        case CombatKind.Frostbite: Cue(cues, Sfx.Frostbite, t0 + 0.5f * D); break;
                        case CombatKind.Charm: Cue(cues, Sfx.SirenSong, t0 + 0.15f * D); break;
                        case CombatKind.Polymorph: Cue(cues, Sfx.CirceWarp, t0 + 0.3f * D); break;
                        case CombatKind.Magic:
                            Cue(cues, Sfx.MagicCast, t0 + 0.25f * D);
                            if (ev.damage > 0) Cue(cues, Sfx.MagicHit, t0 + 0.5f * D);
                            break;
                        case CombatKind.Hit:
                        case CombatKind.Crit:
                        case CombatKind.Miss:
                        case CombatKind.Barrier:
                        case CombatKind.Pass:
                        {
                            Sfx? swing, hit;
                            AttackSounds(s.atk, out swing, out hit);
                            if (swing.HasValue) Cue(cues, swing.Value, t0 + 0.3f * D);
                            float impact = t0 + 0.5f * D;
                            if (ev.kind == CombatKind.Miss) Cue(cues, Sfx.Deflect, impact);
                            else if (ev.kind == CombatKind.Barrier) Cue(cues, Sfx.Barrier, impact);
                            else if (ev.kind == CombatKind.Pass) Cue(cues, Sfx.PassWhoosh, impact);
                            else
                            {
                                if (hit.HasValue && ev.damage > 0) Cue(cues, hit.Value, impact);
                                if (ev.kind == CombatKind.Crit) Cue(cues, Sfx.CritRing, impact + 20);
                            }
                            break;
                        }
                    }
                }
            return cues;
        }

        static void Cue(List<KeyValuePair<Sfx, float>> cues, Sfx s, float ms)
        {
            cues.Add(new KeyValuePair<Sfx, float>(s, Math.Max(0, ms)));
        }

        // 攻撃する者の音: 武器を持つ人型は武器ごとの音、持たない敵はその敵の攻撃の音
        static void AttackSounds(Actor a, out Sfx? swing, out Sfx? hit)
        {
            swing = null; hit = null;
            Entity e = a.e;
            if (e is Polyphemus) { swing = Sfx.SwingHeavy; hit = Sfx.GiantSmash; return; }
            if (e is Shade) { hit = Sfx.ShadeWhisper; return; }
            if (e is Bat || e is Dragon || e is Scylla) { hit = Sfx.Bite; return; }
            if (e is Acid) { hit = Sfx.AcidSpit; return; }
            if (e is Ice) { hit = Sfx.IceFreeze; return; }
            Humanoid h = a.fig as Humanoid;
            if (h == null) { hit = Sfx.HitFist; return; }
            switch (h.WeaponForSound)
            {
                case WeaponKind.Dagger:
                case WeaponKind.Knife: swing = Sfx.SwingLight; hit = Sfx.HitDagger; break;
                case WeaponKind.ShortSword: swing = Sfx.SwingHeavy; hit = Sfx.HitSword; break;
                case WeaponKind.LongSword: swing = Sfx.SwingHeavy; hit = Sfx.HitLongSword; break;
                case WeaponKind.Vorpal: swing = Sfx.SwingHeavy; hit = Sfx.HitVorpal; break;
                case WeaponKind.Mace:
                case WeaponKind.Club:
                case WeaponKind.Staff: swing = Sfx.SwingHeavy; hit = Sfx.HitBlunt; break;
                case WeaponKind.Pick: swing = Sfx.SwingHeavy; hit = Sfx.HitPick; break;
                case WeaponKind.Cutlass: swing = Sfx.SwingLight; hit = Sfx.HitCutlass; break;
                case WeaponKind.Claw: swing = Sfx.SwingLight; hit = Sfx.Scratch; break;
                default: hit = Sfx.HitFist; break;   // 素手
            }
        }

        bool Visible(CombatEvent ev)
        {
            Entity h = logic.hero;
            if (ev.kind == CombatKind.Pickup || ev.kind == CombatKind.Fall)
                return ev.attacker.isPartyMember || logic.isEntitySeeable(ev.attacker);
            if (!CombatLog.IsCombat(ev.kind)) return true;   // 使う・掘る・話す等は記録時に絞り込み済み
            return ev.attacker == h || ev.defender == h || logic.isEntitySeeable(ev.attacker) || logic.isEntitySeeable(ev.defender);
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

        // 物・穴・壁・階段など、HP バーのない登場物
        static Actor MakeObject(Figure fig, float x, int dir, string label, Entity e)
        {
            return new Actor { e = e, fig = fig, x = x, dir = dir, label = label, hpBar = false };
        }

        // 各段の地面の高さ（高さ 340 の画面での値。実際の画面の高さに比例させて使う → GroundY()）
        static readonly float[][] Grounds = { new[] { 285f }, new[] { 162f, 314f }, new[] { 120f, 220f, 320f } };
        static readonly float[] Scales = { 1f, 0.6f, 0.45f };
        static readonly float[] LeftX = { 112f, 70f, 30f };
        const float RightX = 255f;

        float GroundY(int lanes, int i)
        {
            return Grounds[lanes - 1][i] * pic.Height / 340f;
        }

        List<Lane> Build(List<CombatEvent> events)
        {
            List<Lane> all = new List<Lane>();
            all.AddRange(BuildCombat(events.Where(ev => CombatLog.IsCombat(ev.kind)).ToList()));
            all.AddRange(BuildSolo(events.Where(ev => !CombatLog.IsCombat(ev.kind)).ToList()));
            // Hero が関わる段を優先して最大3段
            List<Lane> shown = all.OrderBy(l => l.hero ? 0 : 1).Take(3).ToList();
            int n = shown.Count;
            for (int i = 0; i < n; i++)
            {
                shown[i].groundY = GroundY(n, i);
                shown[i].scale = Scales[n - 1];
            }
            return shown;
        }

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

            List<Lane> result = new List<Lane>();
            foreach (Entity focus in order)
            {
                List<CombatEvent> evs = groups[focus];
                if (evs.Count > 4) evs = evs.Skip(evs.Count - 4).ToList();

                Lane lane = new Lane { hero = evs.Any(ev => ev.attacker == logic.hero || ev.defender == logic.hero) };
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

        // 戦闘以外の場面。同じ人の「拾う」、同じ Dwarf の「掘る」は1つの段にまとめて順番に再生する
        List<Lane> BuildSolo(List<CombatEvent> events)
        {
            List<Lane> result = new List<Lane>();
            Dictionary<Tuple<CombatKind, Entity>, Lane> merge = new Dictionary<Tuple<CombatKind, Entity>, Lane>();
            foreach (CombatEvent ev in events)
            {
                bool mergeable = ev.kind == CombatKind.Pickup || ev.kind == CombatKind.Dig;
                Tuple<CombatKind, Entity> key = Tuple.Create(ev.kind, ev.attacker);
                Lane lane;
                if (mergeable && merge.TryGetValue(key, out lane))
                {
                    AppendSolo(lane, ev);
                    continue;
                }
                lane = NewSoloLane(ev);
                if (lane == null) continue;
                if (mergeable) merge[key] = lane;
                result.Add(lane);
            }
            return result;
        }

        static float SoloDuration(CombatKind k)
        {
            switch (k)
            {
                case CombatKind.Pickup: return 450f;
                case CombatKind.Use: return 1000f;
                case CombatKind.Fall: return FallSceneMs;
                case CombatKind.Dig: return 600f;
                case CombatKind.Talk: return 1200f;
                case CombatKind.Give: return 1000f;
                case CombatKind.Embed: return 1000f;
                default: return 1500f;   // GemsComplete
            }
        }

        Lane NewSoloLane(CombatEvent ev)
        {
            Lane lane = new Lane { hero = ev.attacker == logic.hero || ev.defender == logic.hero };
            Sched s = new Sched { ev = ev, start = 0, dur = SoloDuration(ev.kind) };
            switch (ev.kind)
            {
                case CombatKind.Pickup:
                {
                    s.atk = MakeActor(ev.attacker, 128, 1, 0);
                    lane.actors.Add(s.atk);
                    Figure f = ItemDesigns.Create(ev.item);
                    if (f != null)
                    {
                        s.def = MakeObject(f, 128 + 40, 1, null, ev.item);
                        lane.actors.Add(s.def);
                    }
                    break;
                }
                case CombatKind.Use:
                    s.atk = MakeActor(ev.attacker, 165, 1, 0);
                    s.itemFig = ItemDesigns.Create(ev.item);
                    lane.actors.Add(s.atk);
                    break;
                case CombatKind.Fall:
                {
                    Figure pit = ev.charybdis ? (Figure)new CharybdisFig() : new PitFig();
                    s.def = MakeObject(pit, 180, 1, ev.charybdis ? "カリュブディス" : "落とし穴", null);
                    s.atk = MakeActor(ev.attacker, 180, 1, 0);
                    s.atk.hpBar = false;
                    s.atk.label = null;
                    lane.actors.Add(s.def);
                    lane.actors.Add(s.atk);
                    break;
                }
                case CombatKind.Dig:
                    s.atk = MakeActor(ev.attacker, 130, 1, 0);
                    s.def = MakeObject(new WallFig(), 212, -1, null, null);
                    lane.actors.Add(s.def);
                    lane.actors.Add(s.atk);
                    break;
                case CombatKind.Talk:
                case CombatKind.Give:
                {
                    // 左にパーティメンバー、右に Hobbit
                    Entity party = ev.kind == CombatKind.Talk ? ev.defender : ev.attacker;
                    Entity hobbit = ev.kind == CombatKind.Talk ? ev.attacker : ev.defender;
                    if (party == null || hobbit == null) return null;
                    Actor pa = MakeActor(party, LeftX[0] + 18, 1, 0);
                    Actor ha = MakeActor(hobbit, RightX - 20, -1, 3);
                    lane.actors.Add(pa);
                    lane.actors.Add(ha);
                    s.atk = ev.kind == CombatKind.Talk ? ha : pa;
                    s.def = ev.kind == CombatKind.Talk ? pa : ha;
                    if (ev.kind == CombatKind.Give) s.itemFig = ItemDesigns.Create(ev.item);
                    break;
                }
                case CombatKind.Embed:
                {
                    s.atk = MakeActor(ev.attacker, 120, 1, 0);
                    Figure altar = ItemDesigns.Create(ev.defender);
                    if (altar == null) return null;
                    s.def = MakeObject(altar, 195, -1, "祭壇", ev.defender);
                    s.itemFig = ItemDesigns.Create(ev.item);
                    lane.actors.Add(s.def);
                    lane.actors.Add(s.atk);
                    break;
                }
                default:   // GemsComplete
                    s.atk = MakeActor(ev.attacker, 180, 1, 0);
                    lane.actors.Add(s.atk);
                    break;
            }
            lane.evs.Add(s);
            return lane;
        }

        void AppendSolo(Lane lane, CombatEvent ev)
        {
            Sched prev = lane.evs[lane.evs.Count - 1];
            Sched s = new Sched { ev = ev, atk = prev.atk, dur = SoloDuration(ev.kind) };
            if (ev.kind == CombatKind.Pickup)
            {
                // 続けて拾う物は少しずらして置き、前の物が手元に届く頃に拾い始める
                s.start = prev.start + 300f;
                Figure f = ItemDesigns.Create(ev.item);
                if (f != null)
                {
                    s.def = MakeObject(f, 168 + 12 * lane.evs.Count, 1, null, ev.item);
                    lane.actors.Add(s.def);
                }
            }
            else
            {
                s.start = prev.start + prev.dur;   // Dig: 叩く → 崩れる → 金貨
                s.def = prev.def;
            }
            lane.evs.Add(s);
        }

        List<Lane> BuildIdle()
        {
            Lane lane = new Lane { groundY = GroundY(1, 0), scale = Scales[0] };
            Entity hero = logic.hero;
            if (hero == null || logic.entitylist == null) return new List<Lane> { lane };

            // Hero が階段の上か隣にいれば、背景に階段を描く
            Entity stair = logic.entitylist.FirstOrDefault(e => (e is Stair || e is StairUp) &&
                Math.Abs(e.xpos - hero.xpos) + Math.Abs(e.ypos - hero.ypos) <= 1);
            if (stair != null)
            {
                bool down = stair is Stair;
                Actor st = MakeObject(new StairFig(down), 190, 1, down ? "下り階段" : "上り階段", stair);
                st.backdrop = true;
                st.labelTop = true;
                lane.actors.Add(st);
            }

            List<Entity> party = new List<Entity>();
            if (hero.hit > 0) party.Add(hero);
            if (logic.companions != null)
                foreach (Entity c in logic.companions)
                    if (c.hit > 0 && !(c is Companion cc && cc.isInactive)) party.Add(c);
            for (int i = Math.Min(party.Count, 3) - 1; i >= 0; i--) lane.actors.Add(MakeActor(party[i], LeftX[i], 1, i));

            // 向かい側: 視界内で一番近い生き物か、一番近い見えている穴
            Entity near = null;
            double best = double.MaxValue;
            foreach (Entity e in logic.entitylist)
            {
                if (e.isPartyMember || e.hit <= 0) continue;
                if (!char.IsLetter(e.graph) && !(e is Teiresias)) continue;
                if (!logic.isEntitySeeable(e)) continue;
                double d = Dist2(e.xpos, e.ypos);
                if (d < best) { best = d; near = e; }
            }
            int pitX = -1, pitY = -1;
            double bestPit = double.MaxValue;
            for (int y = 0; y < Constant.NGRID; y++)
                for (int x = 0; x < Constant.NGRID; x++)
                {
                    if (!logic.maze.isPit(x, y)) continue;
                    double d = Dist2(x, y);
                    if (d > Constant.VISION_DISTANCE * Constant.VISION_DISTANCE || d >= bestPit) continue;
                    if (!logic.isSeeThru(hero.xpos, hero.ypos, x, y)) continue;
                    bestPit = d; pitX = x; pitY = y;
                }
            bool opponent = false;
            if (pitX >= 0 && bestPit < best)
            {
                bool charybdis = logic.floor == 6;
                lane.actors.Add(MakeObject(charybdis ? (Figure)new CharybdisFig() : new PitFig(), RightX, -1,
                                           charybdis ? "カリュブディス" : "落とし穴", null));
                opponent = true;
            }
            else if (near != null)
            {
                lane.actors.Add(MakeActor(near, RightX, -1, 3));
                opponent = true;
            }

            // 手前の地面: 見えている拾えるものと祭壇を、Hero に近い順（足元を最優先）に並べる
            List<Entity> things = logic.entitylist
                .Where(e => (ItemDesigns.IsPickable(e) || e is Altar) && logic.isEntitySeeable(e))
                .OrderBy(e => Dist2(e.xpos, e.ypos))
                .Take(opponent ? 3 : 4).ToList();
            for (int i = 0; i < things.Count; i++)
            {
                Figure f = ItemDesigns.Create(things[i]);
                if (f == null) continue;
                Actor ia = MakeObject(f, 150 + i * 40, 1, ItemDesigns.LabelFor(things[i]), things[i]);
                ia.frontRow = true;
                ia.labelRow = i % 2;
                ia.phase = i * 0.9f;
                lane.actors.Add(ia);
            }
            return new List<Lane> { lane };
        }

        double Dist2(int x, int y)
        {
            return Math.Pow(x - logic.hero.xpos, 2) + Math.Pow(y - logic.hero.ypos, 2);
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

        // a〜b で 0→1、c〜d で 1→0 になる台形の曲線
        static float Hold(float lt, float a, float b, float c, float d)
        {
            if (lt < a || lt >= d) return 0;
            if (lt < b) return Ease((lt - a) / (b - a));
            if (lt < c) return 1;
            return 1 - Ease((lt - c) / (d - c));
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
                case CombatKind.Frostbite: return 0.6f;
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

        Anim ComputeAnim(Lane lane, Actor actor, float t, float time, Motion m)
        {
            Anim a = new Anim { time = time + actor.phase, dir = actor.dir };
            bool pendFreeze = false, pendCharm = false, pendPig = false;
            bool hitFreeze = false, hitCharm = false, hitPig = false;

            foreach (Sched s in lane.evs)
            {
                float lt = t - s.start, D = s.dur;
                CombatKind kind = s.ev.kind;
                if (!CombatLog.IsCombat(kind))
                {
                    ApplySolo(lane, actor, s, lt, a, m);
                    continue;
                }
                if (s.atk == actor && kind != CombatKind.Frostbite)   // 凍傷では凍らせた Ice Jerry は動かない
                {
                    float w, k;
                    AttackCurve(lt, D, out w, out k);
                    a.windup = Math.Max(a.windup, w);
                    a.strike = Math.Max(a.strike, k);
                    if (IsMelee(kind)) m.off += actor.dir * k * LungeDist(s, lane);
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
                    m.off -= actor.dir * r * amp * 10f * lane.scale;

                    if (s.ev.damage > 0 && it < 0.3f * D)
                    {
                        float fade = 1 - it / (0.3f * D);
                        a.flash = Math.Max(a.flash, fade);
                        m.off += (float)Math.Sin(it * 0.8f) * 3f * fade;
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
            if (e != null && actor.hpBar)
            {
                bool alive = e.hit > 0;
                a.frozen = alive && ((e.frozen > 0 && !pendFreeze) || hitFreeze);
                a.charmed = alive && ((e.charmed > 0 && !pendCharm) || hitCharm);
                a.pig = (e.polymorphed > 0 && !pendPig) || hitPig;
            }
            return a;
        }

        // 戦闘以外の場面の動き
        void ApplySolo(Lane lane, Actor actor, Sched s, float lt, Anim a, Motion m)
        {
            float D = s.dur, S = BaseScale * lane.scale;
            CombatEvent ev = s.ev;
            switch (ev.kind)
            {
                case CombatKind.Pickup:
                    if (s.atk == actor)
                        a.crouch = Math.Max(a.crouch, Hold(lt, 0, 0.3f * D, 0.6f * D, 0.9f * D));
                    if (s.def == actor && lt >= 0.25f * D)
                    {
                        // 足元から弧を描いて手元へ跳ね上がり、小さくなって消える
                        float u = Clamp01((lt - 0.25f * D) / (0.35f * D));
                        float handX = s.atk.x + s.atk.dir * 10 * S;
                        float handY = -50 * S * s.atk.fig.height;
                        m.off = (handX - actor.x) * Ease(u);
                        m.yoff = handY * Ease(u) - (float)Math.Sin(u * Math.PI) * 18 * S;
                        m.scale = 1 - Clamp01((lt - 0.6f * D) / (0.25f * D));
                        if (lt >= 0.85f * D) m.hidden = true;
                    }
                    break;

                case CombatKind.Use:
                    if (s.atk != actor || lt < 0) break;
                    if (lt < D) a.hideWeapon = true;
                    bool potion = ev.item is Potion;
                    float hold = Hold(lt, 0.05f * D, 0.3f * D, 0.55f * D, 0.75f * D);
                    if (potion) a.drink = Math.Max(a.drink, hold); else a.read = Math.Max(a.read, hold);
                    float after = lt - 0.55f * D;
                    if (after >= 0)
                    {
                        switch (ev.effect)
                        {
                            case UseEffect.Sleep: a.crouch = Math.Max(a.crouch, Ease(after / 250f)); break;          // 座り込む
                            case UseEffect.LoseStrength: a.crouch = Math.Max(a.crouch, 0.35f * Ease(after / 250f)); break;
                            case UseEffect.GainStrength: a.cheer = Math.Max(a.cheer, Hold(after, 0, 150, 350, 550)); break;
                            case UseEffect.Poison: a.recoil = Math.Max(a.recoil, 0.4f * Hold(after, 0, 80, 150, 300)); break;
                        }
                    }
                    break;

                case CombatKind.Fall:
                    if (s.atk != actor || lt < 0) break;
                    if (lt < 0.2f * D)
                    {
                        m.rot = (float)Math.Sin(lt * 0.06f) * 8;   // 縁でよろける
                    }
                    else
                    {
                        float u = Clamp01((lt - 0.2f * D) / (0.7f * D));
                        m.scale = 1 - 0.9f * Ease(u);
                        m.yoff = 10 * S * u;
                        m.alpha = 1 - u;
                        if (ev.charybdis) m.rot = u * 540;          // 渦に巻かれて回る
                        if (u >= 1) m.hidden = true;
                    }
                    break;

                case CombatKind.Dig:
                    if (s.atk == actor)
                    {
                        float w, k;
                        AttackCurve(lt, D, out w, out k);
                        a.windup = Math.Max(a.windup, w);
                        a.strike = Math.Max(a.strike, k);
                    }
                    if (s.def == actor && lt >= 0.5f * D)
                    {
                        float it = lt - 0.5f * D;
                        if (ev.dig == DigStage.Knock) a.strike = Math.Min(1f, a.strike + 0.35f);   // WallFig: ひびが増える
                        else a.fall = Math.Max(a.fall, Ease(it / (0.4f * D)));                        // WallFig: 崩れる
                        if (it < 120) m.off += (float)Math.Sin(it * 0.9f) * 2f * (1 - it / 120f);
                    }
                    if (s.def == actor) a.strike = Math.Max(a.strike, 0.3f);   // 前から叩かれていた跡
                    break;

                case CombatKind.Talk:
                    if (s.atk == actor && lt >= 0 && actor.e is Hobbit)   // 手を振るのは Hobbit だけ（乗組員・霊・キルケーは振らない）
                        a.wave = Math.Max(a.wave, Hold(lt, 0, 0.1f * D, D, D + 250));
                    break;

                case CombatKind.Give:
                    if (s.atk == actor && lt >= 0 && lt < D) a.hideWeapon = true;
                    if (s.atk == actor) a.read = Math.Max(a.read, Hold(lt, 0, 0.12f * D, 0.25f * D, 0.4f * D));
                    if (s.def == actor) a.cheer = Math.Max(a.cheer, Hold(lt, 0.45f * D, 0.6f * D, D, D + 300));
                    break;

                case CombatKind.Embed:
                    if (s.atk == actor)
                    {
                        if (lt >= 0 && lt < D) a.hideWeapon = true;
                        a.read = Math.Max(a.read, Hold(lt, 0.02f * D, 0.2f * D, 0.5f * D, 0.7f * D));
                    }
                    if (s.def == actor) a.before = !(ev.success && lt >= 0.5f * D);
                    break;

                case CombatKind.GemsComplete:
                    if (s.atk == actor) a.cheer = Math.Max(a.cheer, Hold(lt, 0.1f * D, 0.25f * D, 0.8f * D, D));
                    break;
            }
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

            int floorShown = (fallFloor > 0 && t < FallSceneMs) ? fallFloor : logic.floor;
            EnsureBackground(floorShown);
            g.DrawImageUnscaled(bg, 0, 0);
            DrawAnimatedBackground(g, floorShown, time);

            PointF shake = ComputeShake(t);
            g.TranslateTransform(shake.X, shake.Y);
            foreach (Lane lane in lanes) DrawLane(g, lane, t, time);
            g.ResetTransform();

            DrawOutlined(g, "地下" + floorShown + "階  " + FloorTitle(floorShown), 9f, Color.FromArgb(220, 230, 220, 200), new PointF(6, 4), false);
            DrawFloorTitle(g, now - titleStart);
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

        // 階が変わったときの階名タイトル（ふわっと出て消える）
        void DrawFloorTitle(Graphics g, float k)
        {
            if (k < 0 || k > TitleMs) return;
            float al = k < 300 ? k / 300f : (k < TitleMs - 400 ? 1f : 1 - (k - (TitleMs - 400)) / 400f);
            float W = pic.Width;
            using (Brush b = new SolidBrush(Color.FromArgb((int)(150 * al), 0, 0, 0))) g.FillRectangle(b, 0, 70, W, 70);
            using (Pen p = new Pen(Color.FromArgb((int)(160 * al), 220, 190, 120), 1))
            {
                g.DrawLine(p, 20, 74, W - 20, 74);
                g.DrawLine(p, 20, 136, W - 20, 136);
            }
            DrawOutlined(g, "地下" + logic.floor + "階", 18f, Color.FromArgb((int)(255 * al), 255, 240, 200), new PointF(W / 2, 80), true);
            DrawOutlined(g, FloorTitle(logic.floor), 11f, Color.FromArgb((int)(255 * al), 230, 210, 170), new PointF(W / 2, 112), true);
        }

        float GroundOf(Lane lane, Actor a)
        {
            return lane.groundY + (a.frontRow ? 18f * lane.scale : 0f);
        }

        void DrawLane(Graphics g, Lane lane, float t, float time)
        {
            float S = BaseScale * lane.scale;
            Dictionary<Actor, Anim> anims = new Dictionary<Actor, Anim>();
            Dictionary<Actor, Motion> motions = new Dictionary<Actor, Motion>();
            Dictionary<Actor, float> offs = new Dictionary<Actor, float>();
            foreach (Actor a in lane.actors)
            {
                Motion m = new Motion();
                anims[a] = ComputeAnim(lane, a, t, time, m);
                motions[a] = m;
                offs[a] = m.off;
            }

            // 背景（階段）
            foreach (Actor a in lane.actors.Where(x => x.backdrop)) DrawActor(g, lane, a, anims[a], motions[a], S);

            // 影
            using (Brush b = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                foreach (Actor a in lane.actors)
                {
                    if (a.backdrop || motions[a].hidden || a.e == null) continue;
                    float hw = a.fig.HalfWidth * S * (1 + anims[a].fall * 0.8f) * motions[a].scale;
                    g.FillEllipse(b, a.x + offs[a] - hw, GroundOf(lane, a) - 3 * lane.scale - 1, hw * 2, 6 * lane.scale + 2);
                }

            // テイレシアスの金色の光・宝石が揃ったときの光（キャラより奥）
            foreach (Sched s in lane.evs)
            {
                if (s.ev.kind == CombatKind.Meet) DrawGlow(g, lane, s, t, Color.FromArgb(255, 215, 100), 70, s.dur);
                if (s.ev.kind == CombatKind.GemsComplete) DrawGlow(g, lane, s, t, Color.FromArgb(255, 220, 120), 90, s.dur * 0.3f);
            }

            // キャラ（奥から手前へ。攻撃中のキャラを最前面に）→ 手前の地面の物
            List<Actor> drawOrder = lane.actors.Where(a => !a.backdrop && !a.frontRow)
                                               .OrderBy(a => anims[a].strike > 0 ? 1 : 0).ToList();
            drawOrder.AddRange(lane.actors.Where(a => a.frontRow));
            foreach (Actor a in drawOrder) DrawActor(g, lane, a, anims[a], motions[a], S);

            foreach (Sched s in lane.evs)
            {
                if (CombatLog.IsCombat(s.ev.kind)) DrawEffect(g, lane, s, t, offs);
                else DrawSoloEffect(g, lane, s, t, time, anims, motions);
            }
            LayoutFrontLabels(g, lane);
            foreach (Actor a in lane.actors)
                if (!motions[a].hidden) DrawLabel(g, lane, a, t, offs[a], anims[a]);
            DrawTexts(g, lane, t);
        }

        void DrawActor(Graphics g, Lane lane, Actor a, Anim anim, Motion m, float S)
        {
            if (m.hidden || m.scale <= 0.01f) return;
            GraphicsState st = g.Save();
            g.TranslateTransform(a.x + m.off, GroundOf(lane, a) + m.yoff);
            g.ScaleTransform(a.dir * S * m.scale, S * m.scale);
            if (m.rot != 0)
            {
                // 体の中心を軸に回す
                g.TranslateTransform(0, a.fig.CenterY);
                g.RotateTransform(m.rot);
                g.TranslateTransform(0, -a.fig.CenterY);
            }
            anim.alpha *= m.alpha;
            a.fig.Render(g, anim);
            g.Restore(st);
        }

        // 絵（物）を指定の位置に描く。p は足元の位置
        static void DrawFigureAt(Graphics g, Figure f, PointF p, float scale, int dir, float rot, float alpha, float time)
        {
            if (f == null || alpha <= 0.01f) return;
            GraphicsState st = g.Save();
            g.TranslateTransform(p.X, p.Y);
            g.ScaleTransform(dir * scale, scale);
            if (rot != 0)
            {
                g.TranslateTransform(0, f.CenterY);
                g.RotateTransform(rot);
                g.TranslateTransform(0, -f.CenterY);
            }
            f.Render(g, new Anim { time = time, dir = dir, alpha = alpha });
            g.Restore(st);
        }

        PointF MouthOf(Actor a, Lane lane, float off)
        {
            float S = BaseScale * lane.scale;
            PointF m = a.fig.Mouth;
            return new PointF(a.x + off + a.dir * m.X * S, lane.groundY + m.Y * S);
        }

        PointF CenterOf(Actor a, Lane lane, float off)
        {
            return new PointF(a.x + off, GroundOf(lane, a) + a.fig.CenterY * BaseScale * lane.scale);
        }

        PointF HeadOf(Actor a, Lane lane, float off)
        {
            return new PointF(a.x + off, GroundOf(lane, a) + a.fig.Top * BaseScale * lane.scale);
        }

        // 手前に構えた手のおおよその位置
        PointF HandOf(Actor a, Lane lane, float forward, float up)
        {
            float S = BaseScale * lane.scale, h = a.fig.height;
            return new PointF(a.x + a.dir * forward * S * h, lane.groundY - up * S * h);
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
                    using (Font f = new Font("Meiryo UI", 10 + 6 * lane.scale, FontStyle.Bold))
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

        //
        // 戦闘以外の場面の演出（手に持つ物・飛ぶ物・光・吹き出し・文字）
        //
        void DrawSoloEffect(Graphics g, Lane lane, Sched s, float t, float time, Dictionary<Actor, Anim> anims, Dictionary<Actor, Motion> motions)
        {
            float lt = t - s.start, D = s.dur;
            if (lt < 0) return;
            float S = BaseScale * lane.scale;
            float fs = 9f + 4f * lane.scale;
            CombatEvent ev = s.ev;
            Actor who = s.atk;
            PointF head = HeadOf(who, lane, motions[who].off);

            switch (ev.kind)
            {
                case CombatKind.Pickup:
                {
                    string text; Color c;
                    if (ev.item is Gold) { text = "+$" + ev.damage; c = Color.Gold; }
                    else if (ItemDesigns.IsCorpse(ev.item)) { text = "食べた"; c = Color.LightGreen; }
                    else { text = ev.item.name; c = Color.White; }
                    DrawRising(g, text, c, head, lt - 0.5f * D, fs - 1);
                    break;
                }

                case CombatKind.Use:
                    DrawUse(g, lane, s, lt, time, head, fs);
                    break;

                case CombatKind.Fall:
                {
                    PointF pit = CenterOf(s.def, lane, 0);
                    DrawRising(g, "落ちた！", Color.FromArgb(255, 120, 90), new PointF(pit.X, pit.Y - 70 * S), lt - 0.3f * D, fs);
                    if (ev.charybdis && lt > 0.7f * D && lt < 0.7f * D + 400)
                    {
                        float k = (lt - 0.7f * D) / 400f;
                        float r = (10 + 30 * k) * S;
                        using (Pen p = new Pen(Color.FromArgb((int)(220 * (1 - k)), 230, 245, 255), 2))
                            g.DrawEllipse(p, pit.X - r, pit.Y - r * 0.25f, r * 2, r * 0.5f);
                    }
                    break;
                }

                case CombatKind.Dig:
                    DrawDig(g, lane, s, lt, fs);
                    break;

                case CombatKind.Talk:
                    DrawBubble(g, ev.text, head, Hold(lt, 0.05f * D, 0.15f * D, D, D + 800), lane.scale);
                    break;

                case CombatKind.Give:
                {
                    Actor rc = s.def;
                    PointF hand = HandOf(who, lane, 28, 62), rhand = HandOf(rc, lane, 26, 60);
                    if (lt >= 0.1f * D && lt < 0.55f * D)
                    {
                        // Sting が弧を描いて Bilbo の手へ
                        float u = Clamp01((lt - 0.1f * D) / (0.4f * D));
                        PointF p = LerpP(hand, rhand, u);
                        p.Y -= (float)Math.Sin(u * Math.PI) * 30 * S;
                        DrawFigureAt(g, s.itemFig, p, S * 1.4f, 1, u * 360, 1, time);
                    }
                    if (lt >= 0.6f * D && lt < 0.95f * D)
                    {
                        // お礼の金貨が返ってくる
                        float u = Clamp01((lt - 0.6f * D) / (0.3f * D));
                        for (int i = 0; i < 3; i++)
                        {
                            float ui = Clamp01(u * 1.3f - i * 0.15f);
                            PointF p = LerpP(rhand, hand, ui);
                            p.Y -= (float)Math.Sin(ui * Math.PI) * (20 + i * 6) * S;
                            using (Brush b = new SolidBrush(Color.Gold)) g.FillEllipse(b, p.X - 3 * S, p.Y - 1.5f * S, 6 * S, 3 * S);
                        }
                    }
                    DrawBubble(g, "ありがとう！", HeadOf(rc, lane, 0), Hold(lt, 0.5f * D, 0.6f * D, D, D + 800), lane.scale);
                    DrawRising(g, "+" + ev.damage + " Gold", Color.Gold, head, lt - 0.9f * D, fs);
                    break;
                }

                case CombatKind.Embed:
                    DrawEmbed(g, lane, s, lt, time, head, fs);
                    break;

                case CombatKind.GemsComplete:
                {
                    // 4つの宝石が Hero の頭上をまわる
                    Color[] gems = { Color.FromArgb(255, 183, 197), Color.FromArgb(15, 82, 186), Color.FromArgb(255, 191, 0), Color.FromArgb(127, 255, 212) };
                    float al = Hold(lt, 0, 0.15f * D, D, D + 600);
                    PointF c = new PointF(head.X, head.Y - 12 * S);
                    for (int i = 0; i < 4; i++)
                    {
                        double th = time * 2.5 + i * Math.PI / 2;
                        PointF p = new PointF(c.X + (float)Math.Cos(th) * 26 * S, c.Y + (float)Math.Sin(th) * 8 * S);
                        DrawFigureAt(g, new GemFig(gems[i], false), new PointF(p.X, p.Y + 8 * S), S, 1, 0, al, time);
                    }
                    DrawRising(g, "伝説の宝石が揃った！", Color.Gold, new PointF(head.X, head.Y - 20 * S), lt - 0.2f * D, fs + 2, 1600);
                    break;
                }
            }
        }

        void DrawUse(Graphics g, Lane lane, Sched s, float lt, float time, PointF head, float fs)
        {
            float D = s.dur, S = BaseScale * lane.scale;
            CombatEvent ev = s.ev;
            Actor who = s.atk;
            bool potion = ev.item is Potion;
            float hold = Hold(lt, 0.05f * D, 0.3f * D, 0.55f * D, 0.75f * D);

            // 手に持った瓶・巻物
            if (hold > 0.02f && s.itemFig != null)
            {
                if (potion)
                {
                    PointF low = HandOf(who, lane, 20, 55), mouth = HandOf(who, lane, 10, 82);
                    PointF p = LerpP(low, mouth, hold);
                    DrawFigureAt(g, s.itemFig, new PointF(p.X, p.Y + 10 * S), S * 0.9f, who.dir, -who.dir * 70 * hold, 1, time);
                }
                else
                {
                    PointF p = HandOf(who, lane, 24, 58);
                    DrawFigureAt(g, s.itemFig, new PointF(p.X, p.Y + 6 * S), S * 1.3f * hold, who.dir, 0, hold, time);
                }
            }

            float after = lt - 0.55f * D;
            if (after < 0) return;
            PointF body = CenterOf(who, lane, 0);
            float glowAl = Hold(after, 0, 150, 500, 900);
            string text; Color tc;
            if (!ev.success)
            {
                // 効かなかった: 紙（瓶）が煙になって消える
                PointF hand = HandOf(who, lane, 24, 58);
                for (int i = 0; i < 5; i++)
                {
                    float k = Clamp01(after / 500f);
                    float r = (4 + k * 10) * S;
                    PointF c = new PointF(hand.X + (float)Math.Cos(i * 1.3) * r, hand.Y - k * 14 * S + (float)Math.Sin(i * 1.3) * r * 0.5f);
                    using (Brush b = new SolidBrush(Color.FromArgb((int)(150 * (1 - k)), 190, 190, 195))) g.FillEllipse(b, c.X - r * 0.5f, c.Y - r * 0.5f, r, r);
                }
                DrawRising(g, "損した気がする", Color.Silver, head, after, fs - 1);
                return;
            }
            switch (ev.effect)
            {
                case UseEffect.Healing:
                    DrawAura(g, body, 34 * S, Color.FromArgb(90, 255, 120), glowAl);
                    DrawSparkles(g, body, 30 * S, Color.FromArgb(160, 255, 160), after, "+");
                    text = "HP 全回復"; tc = Color.LightGreen; break;
                case UseEffect.Poison:
                    for (int i = 0; i < 6; i++)
                    {
                        float k = ((after / 700f) + i / 6f) % 1f;
                        float r = (2 + 3 * k) * S;
                        PointF c = new PointF(head.X + (float)Math.Sin(i * 2.1) * 12 * S, head.Y - k * 26 * S);
                        using (Pen p = new Pen(Color.FromArgb((int)(220 * (1 - k) * glowAl), 190, 110, 240), 1.5f)) g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2);
                    }
                    text = "-1"; tc = Color.FromArgb(210, 140, 255); break;
                case UseEffect.GainStrength:
                    DrawAura(g, body, 36 * S * (1 + 0.1f * (float)Math.Sin(after * 0.03f)), Color.FromArgb(255, 80, 50), glowAl);
                    text = "力がみなぎる！"; tc = Color.OrangeRed; break;
                case UseEffect.LoseStrength:
                    DrawAura(g, body, 32 * S, Color.FromArgb(90, 90, 110), glowAl);
                    text = "力が抜けた…"; tc = Color.Silver; break;
                case UseEffect.Amnesia:
                    using (Font f = new Font("Meiryo UI", 8 + 4 * lane.scale, FontStyle.Bold))
                        for (int i = 0; i < 3; i++)
                        {
                            double th = after * 0.006 + i * 2.1;
                            PointF p = new PointF(head.X + (float)Math.Cos(th) * 16 * S, head.Y - 4 * S + (float)Math.Sin(th) * 5 * S);
                            using (Brush b = new SolidBrush(Color.FromArgb((int)(255 * glowAl), 220, 190, 255))) g.DrawString("？", f, b, p.X - 6, p.Y - 8);
                        }
                    text = "あれ？"; tc = Color.Plum; break;
                case UseEffect.Identify:
                    DrawSparkles(g, HandOf(who, lane, 24, 58), 18 * S, Color.FromArgb(255, 245, 170), after, null);
                    text = "何かがわかった"; tc = Color.Khaki; break;
                case UseEffect.EnchantWeapon:
                    DrawAura(g, HandOf(who, lane, 26, 52), 18 * S, Color.FromArgb(140, 200, 255), glowAl);
                    text = "武器が青白く輝いた"; tc = Color.LightSkyBlue; break;
                case UseEffect.EnchantArmor:
                    DrawAura(g, body, 26 * S, Color.FromArgb(140, 200, 255), glowAl);
                    text = "鎧が青白く輝いた"; tc = Color.LightSkyBlue; break;
                case UseEffect.ProtectWeapon:
                    DrawAura(g, HandOf(who, lane, 26, 52), 18 * S, Color.FromArgb(255, 210, 90), glowAl);
                    text = "武器が金色に輝いた"; tc = Color.Gold; break;
                case UseEffect.ProtectArmor:
                    DrawAura(g, body, 26 * S, Color.FromArgb(255, 210, 90), glowAl);
                    text = "鎧が金色に輝いた"; tc = Color.Gold; break;
                case UseEffect.Sleep:
                    using (Font f = new Font("Meiryo UI", 7 + 4 * lane.scale, FontStyle.Bold))
                        for (int i = 0; i < 3; i++)
                        {
                            float k = ((after / 900f) + i / 3f) % 1f;
                            PointF p = new PointF(head.X + (6 + k * 18) * S, head.Y + 14 * S - k * 26 * S);
                            using (Brush b = new SolidBrush(Color.FromArgb((int)(230 * (1 - k)), 220, 230, 255))) g.DrawString("Z", f, b, p);
                        }
                    text = "眠ってしまった"; tc = Color.LightSteelBlue; break;
                default:
                    text = null; tc = Color.White; break;
            }
            if (text != null) DrawRising(g, text, tc, head, after, fs - 1);
        }

        void DrawDig(Graphics g, Lane lane, Sched s, float lt, float fs)
        {
            float D = s.dur, S = BaseScale * lane.scale, it = lt - 0.5f * D;
            if (it < 0) return;
            Actor wall = s.def;
            float face = wall.x + wall.dir * wall.fig.HalfWidth * S;     // 叩かれる面
            PointF hit = new PointF(face, lane.groundY - 45 * S);
            PointF top = HeadOf(wall, lane, 0);
            switch (s.ev.dig)
            {
                case DigStage.Knock:
                    // 岩の破片が Dwarf の側へ飛ぶ
                    if (it < 450)
                        for (int i = 0; i < 6; i++)
                        {
                            float k = it / 450f;
                            float vx = -(20 + i * 7) * S, vy = -(25 + (i % 3) * 12) * S;
                            PointF p = new PointF(hit.X + vx * k, hit.Y + vy * k + 60 * S * k * k);
                            using (Brush b = new SolidBrush(Color.FromArgb((int)(230 * (1 - k)), 130, 120, 110))) g.FillRectangle(b, p.X - 1.5f * S, p.Y - 1.5f * S, 3 * S, 3 * S);
                        }
                    DrawRising(g, "がんがんがん", Color.FromArgb(230, 210, 170), new PointF(top.X, top.Y - 6), it, fs - 1, 700);
                    break;
                case DigStage.Break:
                    for (int i = 0; i < 7; i++)
                    {
                        float k = Clamp01(it / 700f);
                        float r = (8 + k * 22) * S;
                        PointF c = new PointF(wall.x + (float)Math.Cos(i * 0.9) * r, lane.groundY - 12 * S - (float)Math.Abs(Math.Sin(i * 0.9)) * r * 0.6f);
                        using (Brush b = new SolidBrush(Color.FromArgb((int)(120 * (1 - k)), 170, 160, 150))) g.FillEllipse(b, c.X - r * 0.5f, c.Y - r * 0.5f, r, r);
                    }
                    DrawRising(g, "壁が崩れた！", Color.FromArgb(230, 210, 170), new PointF(top.X, top.Y + 20), it, fs);
                    break;
                case DigStage.Gold:
                    for (int i = 0; i < 7; i++)
                    {
                        float k = Clamp01(it / 500f);
                        float vx = ((i % 4) - 1.5f) * 16 * S, vy = -(30 + (i % 3) * 10) * S;
                        PointF p = new PointF(wall.x + vx * k, lane.groundY - 10 * S + vy * k + 70 * S * k * k);
                        if (p.Y > lane.groundY - 2) p.Y = lane.groundY - 2;
                        using (Brush b = new SolidBrush(Color.Gold)) g.FillEllipse(b, p.X - 3 * S, p.Y - 1.5f * S, 6 * S, 3 * S);
                    }
                    DrawRising(g, "金貨が出た！", Color.Gold, new PointF(top.X, top.Y + 40), it, fs);
                    break;
            }
        }

        void DrawEmbed(Graphics g, Lane lane, Sched s, float lt, float time, PointF head, float fs)
        {
            float D = s.dur, S = BaseScale * lane.scale;
            Actor altar = s.def;
            PointF hand = HandOf(s.atk, lane, 24, 58);
            PointF sock = new PointF(altar.x, lane.groundY - 18 * S * 1.0f);
            Color gc = (s.ev.item is Gem gem) ? gem.DisplayColor : Color.White;
            if (lt < 0.5f * D)
            {
                // 宝石が浮かんで台座のくぼみへ
                float u = Clamp01((lt - 0.12f * D) / (0.38f * D));
                PointF p = LerpP(hand, sock, Ease(u));
                p.Y -= (float)Math.Sin(u * Math.PI) * 26 * S;
                DrawFigureAt(g, s.itemFig, new PointF(p.X, p.Y + 8 * S), S * 0.9f, 1, 0, 1, time);
            }
            else if (s.ev.success)
            {
                // 宝石の色の光の柱
                float al = Hold(lt - 0.5f * D, 0, 150, 600, 1100);
                RectangleF col = new RectangleF(sock.X - 9 * S, lane.groundY - 150 * S, 18 * S, 150 * S - 18 * S);
                if (al > 0 && col.Height > 1)
                    using (LinearGradientBrush b = new LinearGradientBrush(col, Color.FromArgb(0, gc), Color.FromArgb((int)(170 * al), gc), 90f))
                        g.FillRectangle(b, col);
                DrawRising(g, "嵌め込んだ！", ItemDesigns.Lighten(gc, 0.3f), new PointF(sock.X, sock.Y - 50 * S), lt - 0.5f * D, fs);
            }
            else
            {
                // 季節が合わない: 跳ね返されて手元へ戻る
                float u = Clamp01((lt - 0.5f * D) / (0.3f * D));
                if (u < 1)
                {
                    PointF p = LerpP(sock, hand, Ease(u));
                    p.Y -= (float)Math.Sin(u * Math.PI) * 20 * S;
                    DrawFigureAt(g, s.itemFig, new PointF(p.X, p.Y + 8 * S), S * 0.9f, 1, 0, 1, time);
                }
                DrawRising(g, "何も起きなかった", Color.Silver, new PointF(sock.X, sock.Y - 50 * S), lt - 0.5f * D, fs - 1);
            }
        }

        // 下から上へ浮かんで消える文字。it は表示を始めてからの経過（負なら出さない）
        static void DrawRising(Graphics g, string text, Color c, PointF p, float it, float size, float life = 1100)
        {
            if (text == null || it < 0 || it > life) return;
            float al = it < life - 350 ? 1 : 1 - (it - (life - 350)) / 350f;
            DrawOutlined(g, text, size, Color.FromArgb((int)(255 * al), c), new PointF(p.X, p.Y - 16 - it * 0.02f), true);
        }

        static void DrawAura(Graphics g, PointF c, float r, Color col, float al)
        {
            if (al <= 0.01f) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(c.X - r, c.Y - r * 1.3f, r * 2, r * 2.6f);
                using (PathGradientBrush b = new PathGradientBrush(path))
                {
                    b.CenterColor = Color.FromArgb((int)(150 * al), col);
                    b.SurroundColors = new[] { Color.FromArgb(0, col) };
                    g.FillPath(b, path);
                }
            }
        }

        static void DrawSparkles(Graphics g, PointF c, float r, Color col, float after, string mark)
        {
            for (int i = 0; i < 6; i++)
            {
                float k = ((after / 800f) + i / 6f) % 1f;
                PointF p = new PointF(c.X + (float)Math.Sin(i * 2.4) * r, c.Y + r * 0.6f - k * r * 1.8f);
                int al = (int)(230 * (1 - k));
                if (mark != null)
                {
                    using (Font f = new Font("Meiryo UI", 8, FontStyle.Bold))
                    using (Brush b = new SolidBrush(Color.FromArgb(al, col))) g.DrawString(mark, f, b, p.X - 4, p.Y - 6);
                }
                else
                {
                    using (Pen pen = new Pen(Color.FromArgb(al, col), 1.2f))
                    {
                        g.DrawLine(pen, p.X - 3, p.Y, p.X + 3, p.Y);
                        g.DrawLine(pen, p.X, p.Y - 3, p.X, p.Y + 3);
                    }
                }
            }
        }

        // 吹き出し（Hobbit の台詞など）。anchor は話す人の頭上
        void DrawBubble(Graphics g, string text, PointF anchor, float al, float scale)
        {
            if (string.IsNullOrEmpty(text) || al <= 0.01f) return;
            using (Font f = new Font("Meiryo UI", scale >= 0.9f ? 8.5f : 7f, FontStyle.Bold))
            {
                float maxW = 150;
                SizeF sz = g.MeasureString(text, f, (int)maxW);
                float x = Math.Max(4, Math.Min(pic.Width - sz.Width - 12, anchor.X - sz.Width / 2 - 5));
                RectangleF box = new RectangleF(x, anchor.Y - sz.Height - 22, sz.Width + 10, sz.Height + 8);
                using (GraphicsPath path = new GraphicsPath())
                {
                    float r = 6;
                    path.AddArc(box.X, box.Y, r * 2, r * 2, 180, 90);
                    path.AddArc(box.Right - r * 2, box.Y, r * 2, r * 2, 270, 90);
                    path.AddArc(box.Right - r * 2, box.Bottom - r * 2, r * 2, r * 2, 0, 90);
                    path.AddArc(box.X, box.Bottom - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    using (Brush b = new SolidBrush(Color.FromArgb((int)(235 * al), 255, 252, 240))) g.FillPath(b, path);
                    using (Pen p = new Pen(Color.FromArgb((int)(255 * al), 90, 80, 60), 1.2f)) g.DrawPath(p, path);
                }
                PointF[] tail = { new PointF(anchor.X - 5, box.Bottom - 1), new PointF(anchor.X + 5, box.Bottom - 1), new PointF(anchor.X, box.Bottom + 9) };
                using (Brush b = new SolidBrush(Color.FromArgb((int)(235 * al), 255, 252, 240))) g.FillPolygon(b, tail);
                using (Brush b = new SolidBrush(Color.FromArgb((int)(255 * al), 50, 40, 30)))
                    g.DrawString(text, f, b, new RectangleF(box.X + 5, box.Y + 4, sz.Width + 2, sz.Height + 2));
            }
        }

        void DrawGlow(Graphics g, Lane lane, Sched s, float t, Color col, float radius, float rampMs)
        {
            float lt = t - s.start;
            if (lt < 0) return;
            float k = Clamp01(lt / rampMs);
            float S = BaseScale * lane.scale;
            PointF c = CenterOf(s.atk, lane, 0);
            float r = radius * S * (0.9f + 0.1f * (float)Math.Sin(lt * 0.004f));
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(c.X - r, c.Y - r, r * 2, r * 2);
                using (PathGradientBrush b = new PathGradientBrush(path))
                {
                    b.CenterColor = Color.FromArgb((int)(150 * k), col);
                    b.SurroundColors = new[] { Color.FromArgb(0, col) };
                    g.FillPath(b, path);
                }
            }
        }

        const float FrontLabelSize = 9f;   // 手前の地面の物の名前（左上の階名タイトルと同じ大きさ）

        // 手前の地面の物の名前を、隣と重ならないよう上下2段に振り分け、必要なら右へずらす（パネルからははみ出さない）
        void LayoutFrontLabels(Graphics g, Lane lane)
        {
            List<Actor> items = lane.actors.Where(a => a.frontRow && a.label != null).OrderBy(a => a.x).ToList();
            if (items.Count == 0) return;
            const float gap = 6f;
            float W = pic.Width;
            float[] rowRight = { float.MinValue, float.MinValue };
            using (Font f = new Font("Meiryo UI", FrontLabelSize, FontStyle.Bold))
                foreach (Actor a in items)
                {
                    float half = g.MeasureString(a.label, f).Width / 2 + 2;
                    int bestRow = 0;
                    float bestX = a.x, bestCost = float.MaxValue;
                    for (int row = 0; row < 2; row++)
                    {
                        float cx = Math.Max(half, Math.Max(a.x, rowRight[row] + gap + half));
                        float over = Math.Max(0, cx + half - W);                   // 右端からはみ出す量
                        float cost = Math.Abs(cx - a.x) + over * 10 + row * 0.5f;   // ずれが少ない段を選ぶ（同じなら上の段）
                        if (cost < bestCost) { bestCost = cost; bestRow = row; bestX = Math.Min(cx, W - half); }
                    }
                    rowRight[bestRow] = bestX + half;
                    a.labelRow = bestRow;
                    a.labelX = bestX;
                }
        }

        void DrawLabel(Graphics g, Lane lane, Actor a, float t, float off, Anim anim)
        {
            if (a.label == null) return;
            float x = a.x + off;
            if (!a.hpBar)
            {
                // 物・穴・階段: 名前だけ（手前の列は小さく、上下にずらす）
                float S = BaseScale * lane.scale;
                if (a.labelTop)
                    DrawOutlined(g, a.label, 7.5f, Color.FromArgb(220, 230, 225, 210), new PointF(x, lane.groundY + a.fig.Top * S - 16), true);
                else if (a.frontRow)
                    DrawOutlined(g, a.label, FrontLabelSize, Color.FromArgb(230, 240, 235, 215), new PointF(a.labelX, GroundOf(lane, a) + 2 + a.labelRow * 13), true);
                else
                    DrawOutlined(g, a.label, lane.scale >= 0.9f ? 8f : 7f, Color.FromArgb(225, 230, 225, 210), new PointF(x, lane.groundY + 4 * lane.scale + 1), true);
                return;
            }

            // 着弾前のダメージは HP に反映しない
            int hp = a.e.hit;
            foreach (Sched s in lane.evs)
                if (s.def == a && CombatLog.IsCombat(s.ev.kind) && t - s.start < 0.5f * s.dur) hp += s.ev.damage;
            hp = Math.Max(0, Math.Min(hp, a.e.hitmax));

            float y = lane.groundY + 4 * lane.scale + 1;
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
                if (!CombatLog.IsCombat(s.ev.kind)) continue;
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
                case CombatKind.Magic: text = ev.damage > 0 ? "-" + ev.damage : "効かない"; c = Color.Cyan; break;
                case CombatKind.Breath:
                    if (ev.damage > 0) { text = "-" + ev.damage; c = Color.Orange; }
                    else { text = "かわした"; c = Color.Silver; }
                    break;
                case CombatKind.Freeze: text = "凍結！"; c = Color.PaleTurquoise; break;
                case CombatKind.Charm: text = "魅了！"; c = Color.HotPink; break;
                case CombatKind.Polymorph: text = "豚化！"; c = Color.Pink; break;
                case CombatKind.Frostbite: text = "凍傷 -" + ev.damage; c = Color.LightSkyBlue; break;
                case CombatKind.Meet: text = "予言者テイレシアス"; c = Color.Gold; break;
                default: text = null; c = Color.White; break;
            }
        }

        static void DrawOutlined(Graphics g, string s, float size, Color c, PointF p, bool center)
        {
            using (Font f = new Font("Meiryo UI", size, FontStyle.Bold))
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
                    if (!CombatLog.IsCombat(s.ev.kind)) continue;
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
        void EnsureBackground(int floor)
        {
            if (bg != null && bgFloor == floor && bgLaneCount == lanes.Count) return;
            bgFloor = floor;
            bgLaneCount = lanes.Count;
            if (bg != null) bg.Dispose();
            int W = Math.Max(1, pic.Width), H = Math.Max(1, pic.Height);
            bg = new Bitmap(W, H);
            using (Graphics g = Graphics.FromImage(bg))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color top, bottom, ground;
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
                    // 波の線は動かすので、ここでは描かず DrawAnimatedBackground() で毎フレーム描く
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
                    float gy = GroundY(n, i), sc = Scales[n - 1];
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

        //
        // 背景のうち動くもの（キャッシュした背景の上に毎フレーム描く）。ほかの階の動きもここに足す
        //
        PointF[] waves;     // 6階の波の線の元の位置（背景と同じ乱数で決める）
        int wavesW, wavesH;

        void DrawAnimatedBackground(Graphics g, int floor, float time)
        {
            if (floor != 6) return;
            int W = pic.Width, H = pic.Height;
            float hz = H * 0.3f;   // 水平線（EnsureBackground と同じ）
            if (waves == null || wavesW != W || wavesH != H)
            {
                Random rnd = new Random(6 * 7919);
                waves = new PointF[40];
                for (int i = 0; i < waves.Length; i++)
                    waves[i] = new PointF(rnd.Next(W), hz + 6 + rnd.Next((int)(H - hz)));
                wavesW = W; wavesH = H;
            }
            // ゆっくり横に流し（手前＝画面の下ほど速い）、1本ずつ位相をずらして上下にわずかに揺らす
            using (Pen p = new Pen(Color.FromArgb(50, 170, 210, 255), 1))
                for (int i = 0; i < waves.Length; i++)
                {
                    float depth = (waves[i].Y - hz) / (H - hz);          // 0=奥 1=手前
                    float speed = 4 + 6 * depth;                          // 毎秒 4〜10px
                    float span = W + 28;
                    float x = ((waves[i].X + time * speed) % span + span) % span - 14;
                    float y = waves[i].Y + (float)Math.Sin(time * 0.8f + i * 1.3f) * 1.5f;
                    g.DrawArc(p, x, y, 14, 5, 200, 140);
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
