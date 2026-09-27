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
using System.IO;
using System.Media;

namespace Maze
{
    // 効果音の種類
    enum Sfx
    {
        SwingLight, SwingHeavy,                                 // 武器を振る風切り音（軽い・重い）
        HitDagger, HitSword, HitLongSword, HitVorpal,           // 武器ごとの当たった音
        HitBlunt, HitPick, HitCutlass, HitFist, Scratch,
        MagicCast, MagicHit,                                    // Companion の魔法
        CritRing, Deflect, Barrier, PassWhoosh,                 // クリティカル・はじかれた・結界・すり抜け
        Bite, Breath, AcidSpit, IceFreeze, SirenSong, CirceWarp, GiantSmash, ShadeWhisper,   // 敵の攻撃
        Coin, Potion, Scroll,                                   // 拾う（金貨・ポーション・巻物）
    }

    //
    // 効果音。音源ファイルは使わず、すべてプログラムで波形を合成する（44100Hz・16bit・モノラル）。
    // 1ターン分の音を時刻付きで受け取り、1本の音に混ぜてから鳴らす（重なっても途切れず、絵とも揃う）。
    // 新しい場面の音が来たら前の音は止めて切り替える。常に ON。
    //
    static class SoundFx
    {
        public const int Rate = 44100;
        static readonly Dictionary<Sfx, float[]> cache = new Dictionary<Sfx, float[]>();
        static SoundPlayer player;
        static MemoryStream playing;    // 再生中の WAV（再生が終わるまで保持する）
        internal static bool silentForTest = false;   // 画面を使わないテスト用プログラムでスピーカーから鳴らさないため（ゲームでは常に false）

        // 1ターン分の音を鳴らす。cues は（音, 開始時刻ms）の並び
        public static void PlayScene(List<KeyValuePair<Sfx, float>> cues)
        {
            if (cues == null || cues.Count == 0 || silentForTest) return;
            try
            {
                byte[] wav = ToWav(Mix(cues));
                if (player != null) player.Stop();
                playing = new MemoryStream(wav);
                player = new SoundPlayer(playing);
                player.Play();   // 非同期再生（入力はブロックしない）
            }
            catch (Exception)
            {
                // 音声デバイスがない等で鳴らせなくてもゲームは続ける
            }
        }

        public static float[] Get(Sfx s)
        {
            float[] w;
            if (!cache.TryGetValue(s, out w))
            {
                w = Synth(s);
                cache[s] = w;
            }
            return w;
        }

        // 時刻付きの音を1本に混ぜる（重なりは加算し、音割れしないよう柔らかく圧縮する）
        public static float[] Mix(List<KeyValuePair<Sfx, float>> cues)
        {
            int len = 0;
            foreach (var c in cues) len = Math.Max(len, (int)(c.Value * Rate / 1000f) + Get(c.Key).Length);
            float[] mix = new float[len];
            foreach (var c in cues)
            {
                float[] w = Get(c.Key);
                int off = (int)(c.Value * Rate / 1000f);
                for (int i = 0; i < w.Length; i++) mix[off + i] += w[i];
            }
            for (int i = 0; i < len; i++) mix[i] = (float)Math.Tanh(mix[i] * 1.1f) * 0.85f;
            return mix;
        }

        public static byte[] ToWav(float[] samples)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16);
                w.Write((short)1); w.Write((short)1);            // PCM・モノラル
                w.Write(Rate); w.Write(Rate * 2);
                w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataBytes);
                foreach (float s in samples)
                    w.Write((short)(Math.Max(-1f, Math.Min(1f, s)) * 32000));
                w.Flush();
                return ms.ToArray();
            }
        }

        //
        // 合成
        //
        static float[] Synth(Sfx s)
        {
            Random r = new Random((int)s * 7919 + 13);   // 毎回同じ音になるよう種を固定
            switch (s)
            {
                case Sfx.SwingLight: return Whoosh(r, 0.12f, 2500, 5500, 0.30f);
                case Sfx.SwingHeavy: return Whoosh(r, 0.18f, 600, 1900, 0.40f);
                case Sfx.HitDagger: return Add(Metal(0.18f, new[] { 2900f, 4100f, 5300f }, 0.06f, 0.5f), Click(r, 0.18f, 0.005f, 0.5f));
                case Sfx.HitSword:
                    return Add(Metal(0.40f, new[] { 1800f, 2700f, 3950f, 5100f }, 0.12f, 0.45f),
                               Thump(0.40f, 150, 150, 0.04f, 0.35f), Click(r, 0.40f, 0.006f, 0.45f));
                case Sfx.HitLongSword:
                    return Add(Metal(0.55f, new[] { 1300f, 2050f, 3000f, 4200f }, 0.20f, 0.45f),
                               Thump(0.55f, 110, 110, 0.05f, 0.45f), Click(r, 0.55f, 0.006f, 0.45f));
                case Sfx.HitVorpal:
                {
                    float[] m = Metal(0.75f, new[] { 1500f, 2250f, 3380f }, 0.35f, 0.45f);
                    for (int i = 0; i < m.Length; i++) m[i] *= 0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * 18 * i / Rate);   // きらめく余韻
                    return Add(m, Chirp(0.75f, 2000, 5000, 0.25f, 0.4f, 0.12f), Click(r, 0.75f, 0.006f, 0.4f));
                }
                case Sfx.HitBlunt: return Add(Thump(0.28f, 140, 55, 0.12f, 0.8f), NoiseBurst(r, 0.28f, 0.05f, 900, 0.5f));
                case Sfx.HitPick: return Add(Metal(0.22f, new[] { 1200f, 3300f }, 0.08f, 0.55f), Click(r, 0.22f, 0.005f, 0.5f));
                case Sfx.HitCutlass: return Add(Metal(0.2f, new[] { 2200f, 3300f, 4400f }, 0.08f, 0.45f), Whoosh(r, 0.2f, 3000, 4500, 0.15f));
                case Sfx.HitFist: return Add(Thump(0.14f, 220, 110, 0.06f, 0.7f), NoiseBurst(r, 0.14f, 0.02f, 1200, 0.3f));
                case Sfx.Scratch: return Scratchy(r, 0.18f);
                // 魔法は頻繁に鳴るので、大きすぎるとの指摘を受けて約半分（約 −6dB）にしている
                case Sfx.MagicCast: return Gain(Add(Chirp(0.35f, 600, 1800, 0.25f, 0.35f, 0.1f), Sparkle(r, 0.35f, 6, 3000, 6000, 0.18f)), 0.5f);
                case Sfx.MagicHit: return Gain(Add(Sparkle(r, 0.3f, 8, 2500, 5500, 0.25f), Thump(0.3f, 300, 200, 0.05f, 0.3f)), 0.5f);
                case Sfx.CritRing: return Metal(0.45f, new[] { 3500f, 5250f, 7000f }, 0.25f, 0.35f);
                case Sfx.Deflect:
                    return Add(Metal(0.2f, new[] { 2500f, 3800f }, 0.05f, 0.45f),
                               Delay(Metal(0.16f, new[] { 2700f, 4000f }, 0.04f, 0.3f), 0.04f, 0.2f));
                case Sfx.Barrier: return Hum(0.32f);
                case Sfx.PassWhoosh: return Whoosh(r, 0.35f, 900, 300, 0.25f);
                case Sfx.Bite:
                    return Add(Click(r, 0.14f, 0.008f, 0.6f), Thump(0.14f, 320, 200, 0.03f, 0.5f),
                               Delay(Add(Click(r, 0.09f, 0.008f, 0.6f), Thump(0.09f, 300, 180, 0.03f, 0.5f)), 0.05f, 0.14f));
                case Sfx.Breath: return Roar(r, 0.65f);
                case Sfx.AcidSpit: return Bubbles(r, 0.32f);
                case Sfx.IceFreeze: return Add(Sparkle(r, 0.45f, 9, 3000, 6500, 0.3f), NoiseBurst(r, 0.45f, 0.2f, 6000, 0.08f));
                case Sfx.SirenSong: return Melody(new[] { 659.3f, 784f, 987.8f, 1318.5f }, 0.11f);
                case Sfx.CirceWarp: return Warp(0.45f);
                case Sfx.GiantSmash: return Add(Thump(0.6f, 90, 35, 0.35f, 0.95f), NoiseBurst(r, 0.6f, 0.15f, 500, 0.6f), Thump(0.6f, 50, 45, 0.3f, 0.4f));
                case Sfx.ShadeWhisper: return Whisper(r, 0.4f);
                case Sfx.Coin: return Coins(r);
                case Sfx.Potion: return WaterSplash(r);
                case Sfx.Scroll: return Rustle(r, 0.36f);
            }
            return new float[1];
        }

        static int N(float sec) { return Math.Max(1, (int)(sec * Rate)); }

        static float[] Gain(float[] w, float g)
        {
            float[] o = new float[w.Length];
            for (int i = 0; i < w.Length; i++) o[i] = w[i] * g;
            return o;
        }

        // 金貨の「チャリンチャリン」: 硬貨を落としたときの音（ユーザー提示の参考音を解析して写した）。
        // 最初の約0.2秒に強い打音が密集し、そのあと跳ねるように間隔を空けて弱まりながら約1秒で消える。
        // 1打ごとに 8300Hz・10400Hz を中心とする非常に高い金属音を鳴らす
        static float[] Coins(Random r)
        {
            // 参考音で強かった周波数（4940・6280・7620・8320・10420Hz）を、ユーザーの希望で約15%高くしたもの
            float[] freqs = { 5680f, 7220f, 8760f, 9570f, 11980f };
            float[] amps  = { 0.35f, 0.40f, 0.55f, 0.90f, 1.00f };
            // （打つ時刻 秒, 強さ, 響きの長さ 秒）
            float[,] strikes = {
                { 0.000f, 0.85f, 0.30f }, { 0.025f, 0.80f, 0.30f }, { 0.050f, 0.60f, 0.20f }, { 0.095f, 1.00f, 0.32f },
                { 0.125f, 0.60f, 0.18f }, { 0.165f, 0.55f, 0.22f }, { 0.230f, 0.30f, 0.25f }, { 0.260f, 0.25f, 0.35f },
                { 0.300f, 0.60f, 0.20f }, { 0.375f, 0.30f, 0.20f }, { 0.460f, 0.20f, 0.30f }, { 0.520f, 0.15f, 0.20f },
                { 0.560f, 0.22f, 0.20f }, { 0.600f, 0.12f, 0.20f }, { 0.650f, 0.12f, 0.18f }, { 0.705f, 0.08f, 0.15f },
                { 0.760f, 0.06f, 0.15f }, { 0.850f, 0.06f, 0.18f },
            };
            float[] o = new float[N(1.15f)];
            for (int k = 0; k < strikes.GetLength(0); k++)
            {
                float at = strikes[k, 0], amp = strikes[k, 1] * 0.13f, decay = strikes[k, 2] / 2.3f;   // −20dB で指定の長さ
                float detune = 1 + ((float)r.NextDouble() - 0.5f) * 0.03f;                              // 1打ごとに少し音程を変える
                int off = N(at);
                int len = Math.Min(o.Length - off, N(strikes[k, 2] * 1.6f));
                for (int i = 0; i < len; i++)
                {
                    float t = (float)i / Rate;
                    float env = (float)Math.Exp(-t / decay);
                    float v = 0;
                    for (int p = 0; p < freqs.Length; p++)
                        v += amps[p] * (float)Math.Sin(2 * Math.PI * Math.Min(freqs[p] * detune, Rate * 0.47f) * t) * (p < 3 ? env * env : env);   // 低い方の倍音は早く消える
                    o[off + i] += amp * env * v;
                }
                // 打った瞬間のごく短い「チッ」
                for (int i = 0; i < N(0.0015f) && off + i < o.Length; i++)
                    o[off + i] += amp * 1.5f * (float)(r.NextDouble() * 2 - 1);
            }
            return o;
        }

        // ポーションを拾う音: 水の「ちゃぷん」（ユーザー提示の参考音を解析して写した）。
        // 最初に水がはねる柔らかいノイズ（「ちゃ」）、続いて小さな泡の「ぽ」「ぷ」がいくつか鳴り、
        // いちばん大きな「ぷん」が2回続いて消える。泡の音は鳴り始めてから音程が上がるのが水らしさ
        static float[] WaterSplash(Random r)
        {
            float[] o = new float[N(0.45f)];
            Splash(o, r, 0.000f, 0.07f, 0.35f);                   // はねる「ちゃ」
            // （時刻 秒, 鳴り始めの高さ Hz, 強さ, 響き −20dB まで 秒）。強さは参考音の比（いちばん大きな「ぷん」を 1.0）
            Bubble(o, 0.000f, 620f, 0.38f, 0.06f);
            Bubble(o, 0.020f, 560f, 0.54f, 0.06f);
            Bubble(o, 0.090f, 2400f, 0.20f, 0.04f);
            Bubble(o, 0.125f, 2800f, 0.38f, 0.04f);
            Bubble(o, 0.145f, 2780f, 0.45f, 0.04f);
            Bubble(o, 0.185f, 1040f, 1.00f, 0.06f);               // 「ぷん」は2回続けて鳴る
            Bubble(o, 0.205f, 1080f, 0.80f, 0.05f);
            Bubble(o, 0.235f, 1060f, 0.34f, 0.05f);
            Bubble(o, 0.280f, 680f, 0.11f, 0.06f);
            Bubble(o, 0.300f, 680f, 0.09f, 0.05f);
            return o;
        }

        // 水中の泡が鳴る音: f0 から約1.6倍まで音程がすっと上がりながら、ring 秒で −20dB まで消える
        static void Bubble(float[] o, float at, float f0, float amp, float ring)
        {
            int off = N(at);
            float decay = ring / 2.3f, a = amp * 0.45f;
            int len = Math.Min(o.Length - off, N(ring * 1.6f));
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                double f = f0 * (1 + 0.6 * (1 - Math.Exp(-t / (ring * 0.4f))));   // 音程が上がる
                ph += 2 * Math.PI * f / Rate;
                float env = Math.Min(1f, t / 0.0015f) * (float)Math.Exp(-t / decay);
                o[off + i] += a * env * (float)Math.Sin(ph);
            }
        }

        // 水がはねる柔らかいノイズ（800〜2500Hz 付近）
        static void Splash(float[] o, Random r, float at, float dur, float amp)
        {
            int off = N(at), len = Math.Min(o.Length - off, N(dur));
            Biquad bp = new Biquad();
            bp.SetBandpass(1500, 0.7f);
            for (int i = 0; i < len; i++)
            {
                float u = (float)i / len;
                float env = Math.Min(1f, u * 12) * (1 - u) * (1 - u);
                o[off + i] += amp * env * bp.Process((float)(r.NextDouble() * 2 - 1));
            }
        }

        static float[] Add(params float[][] parts)
        {
            int len = 0;
            foreach (float[] p in parts) len = Math.Max(len, p.Length);
            float[] o = new float[len];
            foreach (float[] p in parts)
                for (int i = 0; i < p.Length; i++) o[i] += p[i];
            return o;
        }

        // at 秒だけ遅らせる（全体の長さは total 秒）
        static float[] Delay(float[] w, float at, float total)
        {
            float[] o = new float[Math.Max(N(total), N(at) + w.Length)];
            int off = N(at);
            for (int i = 0; i < w.Length; i++) o[off + i] += w[i];
            return o;
        }

        // 倍音がずれた金属音（剣・クリティカルなど）。partials の各周波数が decay 秒で減衰する
        static float[] Metal(float dur, float[] partials, float decay, float amp)
        {
            float[] o = new float[N(dur)];
            for (int k = 0; k < partials.Length; k++)
            {
                float a = amp / (1 + k * 0.6f);
                float d = decay * (1 - k * 0.12f);
                for (int i = 0; i < o.Length; i++)
                {
                    float t = (float)i / Rate;
                    o[i] += a * (float)Math.Exp(-t / d) * (float)Math.Sin(2 * Math.PI * partials[k] * t);
                }
            }
            return o;
        }

        // 周波数が from→to へ下がる低い打撃音
        static float[] Thump(float dur, float from, float to, float decay, float amp)
        {
            float[] o = new float[N(dur)];
            double ph = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / Rate;
                float f = to + (from - to) * (float)Math.Exp(-t / (decay * 0.6f));
                ph += 2 * Math.PI * f / Rate;
                o[i] = amp * (float)Math.Exp(-t / decay) * (float)Math.Sin(ph);
            }
            return o;
        }

        // ごく短いノイズの「カチッ」
        static float[] Click(Random r, float dur, float len, float amp)
        {
            float[] o = new float[N(dur)];
            for (int i = 0; i < N(len) && i < o.Length; i++)
                o[i] = amp * (float)(r.NextDouble() * 2 - 1) * (1 - (float)i / N(len));
            return o;
        }

        // 低域通過したノイズの破裂（cutoff Hz）
        static float[] NoiseBurst(Random r, float dur, float decay, float cutoff, float amp)
        {
            float[] o = new float[N(dur)];
            float k = (float)(1 - Math.Exp(-2 * Math.PI * cutoff / Rate)), y = 0;
            for (int i = 0; i < o.Length; i++)
            {
                y += k * ((float)(r.NextDouble() * 2 - 1) - y);
                o[i] = amp * 2.5f * y * (float)Math.Exp(-(float)i / Rate / decay);
            }
            return o;
        }

        // 帯域を from→to Hz へ動かしたノイズ（風切り音）
        static float[] Whoosh(Random r, float dur, float from, float to, float amp)
        {
            float[] o = new float[N(dur)];
            Biquad bp = new Biquad();
            for (int i = 0; i < o.Length; i++)
            {
                float u = (float)i / o.Length;
                if (i % 32 == 0) bp.SetBandpass(from + (to - from) * u, 1.2f);
                float env = (float)Math.Sin(Math.PI * u);
                o[i] = amp * 2.2f * env * env * bp.Process((float)(r.NextDouble() * 2 - 1));
            }
            return o;
        }

        // 上昇（下降）するきらめき音
        static float[] Chirp(float dur, float from, float to, float sweep, float amp, float decay)
        {
            float[] o = new float[N(dur)];
            double ph = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / Rate;
                float u = Math.Min(1f, t / sweep);
                ph += 2 * Math.PI * (from + (to - from) * u) / Rate;
                float env = Math.Min(1f, t / 0.02f) * (float)Math.Exp(-Math.Max(0, t - sweep) / decay);
                o[i] = amp * env * (float)(Math.Sin(ph) + 0.3 * Math.Sin(2 * ph));
            }
            return o;
        }

        // 高い澄んだ粒（氷・魔法のきらめき）を count 個ばらまく
        static float[] Sparkle(Random r, float dur, int count, float lo, float hi, float amp)
        {
            float[] o = new float[N(dur)];
            for (int c = 0; c < count; c++)
            {
                float f = lo + (float)r.NextDouble() * (hi - lo);
                int start = (int)(r.NextDouble() * o.Length * 0.7);
                for (int i = start; i < o.Length; i++)
                {
                    float t = (float)(i - start) / Rate;
                    o[i] += amp * (float)Math.Exp(-t / 0.04f) * (float)Math.Sin(2 * Math.PI * f * t);
                }
            }
            return o;
        }

        // 結界の「ヴン」: 低い奇数倍音のうなり＋高い音
        static float[] Hum(float dur)
        {
            float[] o = new float[N(dur)];
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Math.Min(1f, t / 0.02f) * (float)Math.Exp(-t / 0.12f);
                double b = 0;
                for (int h = 1; h <= 7; h += 2) b += Math.Sin(2 * Math.PI * 110 * h * t) / h;
                o[i] = env * (float)(0.4 * b * (0.7 + 0.3 * Math.Sin(2 * Math.PI * 30 * t)) + 0.15 * Math.Sin(2 * Math.PI * 880 * t));
            }
            return o;
        }

        // 爪の引っかき: 高域のノイズを3本の筋で
        static float[] Scratchy(Random r, float dur)
        {
            float[] o = new float[N(dur)];
            Biquad bp = new Biquad();
            bp.SetBandpass(4000, 2f);
            for (int i = 0; i < o.Length; i++)
            {
                float u = (float)i / o.Length;
                float env = (float)Math.Max(0, Math.Sin(3 * Math.PI * u)) * (1 - u);
                o[i] = 0.9f * env * bp.Process((float)(r.NextDouble() * 2 - 1));
            }
            return o;
        }

        // Dragon の炎: 低いノイズの轟音＋パチパチ
        static float[] Roar(Random r, float dur)
        {
            float[] o = new float[N(dur)];
            float k = (float)(1 - Math.Exp(-2 * Math.PI * 700 / Rate)), y = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / Rate, u = (float)i / o.Length;
                y += k * ((float)(r.NextDouble() * 2 - 1) - y);
                float env = Math.Min(1f, t / 0.08f) * (1 - u * u);
                o[i] = env * (2.2f * y + 0.25f * (float)Math.Sin(2 * Math.PI * 60 * t));
                if (r.NextDouble() < 0.004) o[i] += 0.5f * (float)(r.NextDouble() * 2 - 1);   // パチパチ
            }
            return o;
        }

        // Acid の酸: 音程が上がる泡を6つ
        static float[] Bubbles(Random r, float dur)
        {
            float[] o = new float[N(dur)];
            for (int b = 0; b < 6; b++)
            {
                int start = N(0.04f * b + (float)r.NextDouble() * 0.02f);
                float f0 = 300 + (float)r.NextDouble() * 400;
                double ph = 0;
                for (int i = start; i < Math.Min(o.Length, start + N(0.06f)); i++)
                {
                    float t = (float)(i - start) / Rate;
                    ph += 2 * Math.PI * f0 * (1 + t * 12) / Rate;
                    o[i] += 0.4f * (float)Math.Exp(-t / 0.02f) * (float)Math.Sin(ph);
                }
            }
            return o;
        }

        // Siren の歌: 短いメロディ（ビブラート付き）
        static float[] Melody(float[] notes, float noteDur)
        {
            float[] o = new float[N(noteDur * notes.Length + 0.15f)];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = N(noteDur * n);
                for (int i = start; i < Math.Min(o.Length, start + N(noteDur + 0.15f)); i++)
                {
                    float t = (float)(i - start) / Rate;
                    double f = notes[n] * (1 + 0.006 * Math.Sin(2 * Math.PI * 6 * t));
                    float env = Math.Min(1f, t / 0.015f) * (float)Math.Exp(-t / 0.12f);
                    o[i] += 0.3f * env * (float)(Math.Sin(2 * Math.PI * f * t) + 0.25 * Math.Sin(4 * Math.PI * f * t));
                }
            }
            return o;
        }

        // Circe の豚化: 音程が下がりながらうねる魔法音
        static float[] Warp(float dur)
        {
            float[] o = new float[N(dur)];
            double ph = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / Rate, u = (float)i / o.Length;
                double f = (700 - 400 * u) * (1 + 0.15 * u * Math.Sin(2 * Math.PI * 14 * t));
                ph += 2 * Math.PI * f / Rate;
                float env = Math.Min(1f, t / 0.03f) * (1 - u);
                o[i] = 0.4f * env * (float)(Math.Sin(ph) + 0.4 * Math.Sin(3 * ph));
            }
            return o;
        }

        // Shade: ささやくような息の音（ゆっくり強弱）
        static float[] Whisper(Random r, float dur)
        {
            float[] o = new float[N(dur)];
            Biquad bp = new Biquad();
            bp.SetBandpass(1500, 0.8f);
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / Rate, u = (float)i / o.Length;
                float env = (float)Math.Sin(Math.PI * u) * (0.6f + 0.4f * (float)Math.Sin(2 * Math.PI * 9 * t));
                o[i] = 0.8f * env * bp.Process((float)(r.NextDouble() * 2 - 1));
            }
            return o;
        }

        // 巻物の紙がこすれる「カサカサ」: 帯域を絞ったノイズを細かい粒で強弱
        static float[] Rustle(Random r, float dur)
        {
            float[] o = new float[N(dur)];
            Biquad bp = new Biquad();
            bp.SetBandpass(2800, 0.9f);
            float grain = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / Rate;
                if (i % 180 == 0) grain = 0.3f + (float)r.NextDouble() * 0.9f;   // 約8ms ごとの粒
                float env = Bump(t, 0.08f, 0.07f) + 0.8f * Bump(t, 0.24f, 0.08f);
                o[i] = 0.9f * env * grain * bp.Process((float)(r.NextDouble() * 2 - 1));
            }
            return o;
        }

        // center 秒を頂点、幅 width 秒のなだらかな山
        static float Bump(float t, float center, float width)
        {
            float x = (t - center) / width;
            return (float)Math.Exp(-x * x * 2);
        }

        // 2次の帯域通過フィルタ（RBJ）
        class Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public void SetBandpass(float freq, float q)
            {
                double w = 2 * Math.PI * Math.Min(freq, Rate * 0.45) / Rate;
                double alpha = Math.Sin(w) / (2 * q), a0 = 1 + alpha;
                b0 = (float)(alpha / a0); b1 = 0; b2 = (float)(-alpha / a0);
                a1 = (float)(-2 * Math.Cos(w) / a0); a2 = (float)((1 - alpha) / a0);
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }
    }
}
