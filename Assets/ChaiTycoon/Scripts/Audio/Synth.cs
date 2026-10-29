using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaiTycoon
{
    /// <summary>
    /// Every sound is synthesised at startup: pops, clinks, coins, horns, a happy "wah!", and a
    /// looping tabla + rabab tune. Keeps the build tiny and needs no audio files.
    /// </summary>
    public class Synth : MonoBehaviour
    {
        public static Synth I;
        const int SR = 22050;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource music, musicDrums;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        public bool SfxOn = true, MusicOn = true;
        float rushLayer;

        public static void Create()
        {
            if (I) return;
            var go = new GameObject("Synth");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Synth>();
            I.Build();
        }

        void Build()
        {
            for (int i = 0; i < 10; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                pool.Add(s);
            }
            Make("pop", 0.09f, Pop);
            Make("clink", 0.22f, t => Bell(t, 2100, new[] { 1f, 1.51f, 2.3f }, 18f) * 0.45f);
            Make("coin", 0.26f, Coin);
            Make("tick", 0.03f, t => Noise() * Env(t, 0.002f, 0.025f) * 0.35f);
            Make("thud", 0.16f, t => (Mathf.Sin(2 * Mathf.PI * 90 * t * (1 - t)) * 0.8f + Noise() * 0.15f) * Env(t, 0.003f, 0.14f));
            Make("tada", 0.75f, Tada);
            Make("whistle", 0.5f, t => Mathf.Sin(2 * Mathf.PI * Phase(t, 1400, 1900, 0.5f)) * Env(t, 0.03f, 0.47f) * 0.35f);
            Make("chime", 0.6f, t => (Bell(t, 1046, new[] { 1f, 2.0f, 3.0f }, 6f) + (t > 0.09f ? Bell(t - 0.09f, 1318, new[] { 1f, 2f }, 6f) : 0) + (t > 0.18f ? Bell(t - 0.18f, 1568, new[] { 1f, 2f }, 5f) : 0)) * 0.3f);
            Make("bell", 0.9f, t => Bell(t, 1318, new[] { 1f, 2.76f, 5.4f }, 4f) * 0.4f);
            Make("ding", 0.45f, t => Bell(t, 1568, new[] { 1f, 2f, 3f }, 8f) * 0.4f);
            Make("horn", 0.55f, RickshawHorn);
            Make("truckhorn", 0.9f, TruckHorn);
            Make("wah", 0.42f, Wah);
            Make("hmm", 0.4f, Hmm);
            Make("sizzle", 0.7f, t => HiNoise() * Env(t, 0.02f, 0.65f) * (0.6f + 0.4f * (UnityEngine.Random.value > 0.97f ? 1 : 0)) * 0.25f);
            Make("swoosh", 0.25f, t => HiNoise() * Mathf.Sin(Mathf.PI * t / 0.25f) * 0.25f);
            Make("click", 0.05f, t => Mathf.Sin(2 * Mathf.PI * 1800 * t) * Env(t, 0.001f, 0.045f) * 0.3f);
            Make("buy", 0.4f, t => (Tone(t, 784, 0, 0.12f) + Tone(t, 988, 0.08f, 0.14f) + Tone(t, 1318, 0.16f, 0.24f)) * 0.35f);
            Make("flourish", 1.0f, Flourish);
            Make("cheer", 0.9f, Cheer);
            Make("step", 0.04f, t => HiNoise() * Env(t, 0.002f, 0.035f) * 0.08f);

            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.volume = 0.32f;
            music.clip = MusicLoop(false);
            musicDrums = gameObject.AddComponent<AudioSource>();
            musicDrums.loop = true;
            musicDrums.volume = 0f;
            musicDrums.clip = MusicLoop(true);
        }

        public void StartMusic()
        {
            if (!music.isPlaying) { music.Play(); musicDrums.Play(); musicDrums.timeSamples = music.timeSamples; }
        }

        public void SetRush(bool on) => rushLayer = on ? 1 : 0;

        void Update()
        {
            if (music == null) return;
            float target = MusicOn ? 0.2f : 0f;      // quiet under the effects: the SFX carry the feel
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime);
            musicDrums.volume = Mathf.MoveTowards(musicDrums.volume, MusicOn ? rushLayer * 0.22f : 0, Time.unscaledDeltaTime * 0.6f);
            if (musicDrums.isPlaying && Mathf.Abs(musicDrums.timeSamples - music.timeSamples) > 2000) musicDrums.timeSamples = music.timeSamples;
        }

        public static void Play(string name, float pitch = 1f, float volume = 1f, float minGap = 0.03f)
        {
            if (!I || !I.SfxOn || Time.captureDeltaTime > 0) return;
            if (!I.clips.TryGetValue(name, out var clip)) return;
            float now = Time.unscaledTime;
            if (I.lastPlayed.TryGetValue(name, out var last) && now - last < minGap) return;
            I.lastPlayed[name] = now;
            AudioSource src = null;
            foreach (var s in I.pool) if (!s.isPlaying) { src = s; break; }
            if (src == null) src = I.pool[0];
            src.pitch = pitch;
            src.volume = volume;
            src.PlayOneShot(clip);
        }

        // ------------- building blocks -------------

        void Make(string name, float dur, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(dur * SR);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)SR), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }

        static readonly System.Random rng = new System.Random(7);
        static float Noise() => (float)(rng.NextDouble() * 2 - 1);
        static float lastN;
        static float HiNoise() { float n = Noise(); float h = n - lastN; lastN = n; return h * 0.7f; }

        static float Env(float t, float a, float d)
        {
            if (t < a) return t / a;
            float k = (t - a) / d;
            return Mathf.Clamp01(1 - k) * Mathf.Exp(-k * 3f);
        }

        static float Phase(float t, float f0, float f1, float dur)
        {
            // integral of linear sweep
            float k = (f1 - f0) / dur;
            return f0 * t + 0.5f * k * t * t;
        }

        static float Bell(float t, float f, float[] partials, float decay)
        {
            if (t < 0) return 0;
            float s = 0;
            for (int i = 0; i < partials.Length; i++)
                s += Mathf.Sin(2 * Mathf.PI * f * partials[i] * t) * Mathf.Exp(-t * decay * (1 + i * 0.7f)) / (1 + i);
            return s * Mathf.Min(1, t * 400);
        }

        static float Tone(float t, float f, float start, float len)
        {
            float u = t - start;
            if (u < 0 || u > len + 0.2f) return 0;
            float e = Env(u, 0.005f, len);
            return (Mathf.Sin(2 * Mathf.PI * f * u) + 0.3f * Mathf.Sin(2 * Mathf.PI * f * 2 * u)) * e;
        }

        static float Pop(float t)
        {
            float f = Phase(t, 420, 980, 0.09f);
            return Mathf.Sin(2 * Mathf.PI * f) * Env(t, 0.004f, 0.08f) * 0.55f;
        }

        static float Coin(float t)
        {
            float a = t < 0.07f ? Mathf.Sin(2 * Mathf.PI * 1976 * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 3952 * t) : 0;
            float u = t - 0.06f;
            float b = u > 0 ? (Mathf.Sin(2 * Mathf.PI * 2637 * u) + 0.3f * Mathf.Sin(2 * Mathf.PI * 5274 * u)) * Mathf.Exp(-u * 14) : 0;
            return (a * Env(t, 0.002f, 0.07f) + b) * 0.28f;
        }

        static float Tada(float t)
        {
            float[] notes = { 523, 659, 784, 1046 };
            float s = 0;
            for (int i = 0; i < 4; i++)
            {
                float st = i * 0.08f;
                float len = i == 3 ? 0.55f : 0.12f;
                float u = t - st;
                if (u < 0) continue;
                float tri = Mathf.PingPong(notes[i] * u * 4, 2) - 1; // triangle
                s += (tri * 0.6f + Mathf.Sin(2 * Mathf.PI * notes[i] * u) * 0.4f) * Env(u, 0.005f, len);
            }
            return s * 0.3f;
        }

        static float RickshawHorn(float t)
        {
            float on = (t < 0.18f || (t > 0.26f && t < 0.5f)) ? 1 : 0;
            float u = t < 0.22f ? t : t - 0.26f;
            float sq1 = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 410 * t));
            float sq2 = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 515 * t));
            float e = Mathf.Clamp01(u * 60) * Mathf.Clamp01((0.2f - (u % 0.24f)) * 30 + 1);
            return (sq1 + sq2) * 0.12f * on * e;
        }

        static float TruckHorn(float t)
        {
            float e = Mathf.Clamp01(t * 30) * Mathf.Clamp01((0.85f - t) * 8);
            float s = 0;
            float[] f = { 196, 247, 294 };
            foreach (var fr in f) s += Mathf.Asin(Mathf.Sin(2 * Mathf.PI * fr * t)) / 1.57f;
            return s * 0.14f * e;
        }

        static float Wah(float t)
        {
            // glottal pulse train shaped by a moving vowel formant ("w" -> "aa")
            float f0 = Mathf.Lerp(230, 300, Mathf.Clamp01(t / 0.12f)) - Mathf.Clamp01((t - 0.2f) / 0.2f) * 60;
            float k = Mathf.Clamp01(t / 0.15f);
            float F1 = Mathf.Lerp(320, 780, k), F2 = Mathf.Lerp(800, 1250, k);
            float s = 0;
            for (int h = 1; h < 16; h++)
            {
                float fh = f0 * h;
                float g1 = Mathf.Exp(-Mathf.Pow((fh - F1) / 140f, 2));
                float g2 = 0.6f * Mathf.Exp(-Mathf.Pow((fh - F2) / 180f, 2));
                s += Mathf.Sin(2 * Mathf.PI * fh * t) * (g1 + g2 + 0.04f / h);
            }
            return s * Env(t, 0.02f, 0.38f) * 0.32f;
        }

        static float Hmm(float t)
        {
            float f0 = Mathf.Lerp(190, 140, t / 0.4f);
            float s = 0;
            for (int h = 1; h < 8; h++)
            {
                float fh = f0 * h;
                float g = Mathf.Exp(-Mathf.Pow((fh - 300) / 120f, 2)) + 0.3f / h;
                s += Mathf.Sin(2 * Mathf.PI * fh * t) * g;
            }
            return s * Env(t, 0.04f, 0.34f) * 0.25f;
        }

        static float Cheer(float t)
        {
            float s = 0;
            for (int v = 0; v < 5; v++)
            {
                float f0 = 220 + v * 47;
                float u = t - v * 0.03f;
                if (u < 0) continue;
                s += Mathf.Sin(2 * Mathf.PI * (f0 + Mathf.Sin(u * 30 + v) * 12) * u) * Mathf.Exp(-Mathf.Pow((f0 * 3 - 900) / 600f, 2));
            }
            return (s * 0.12f + HiNoise() * 0.2f) * Env(t, 0.05f, 0.8f);
        }

        static float Flourish(float t)
        {
            float s = 0;
            float[] hits = { 0, 0.07f, 0.14f, 0.21f, 0.28f, 0.42f };
            for (int i = 0; i < hits.Length; i++) s += TablaNa(t - hits[i], 700 + i * 30) * (i == hits.Length - 1 ? 1.2f : 0.8f);
            s += TablaGe(t - 0.42f) * 1.2f;
            return s * 0.45f;
        }

        // ------------- tabla voices -------------
        static float TablaNa(float u, float f = 720)
        {
            if (u < 0 || u > 0.4f) return 0;
            return (Mathf.Sin(2 * Mathf.PI * f * u) + 0.5f * Mathf.Sin(2 * Mathf.PI * f * 2.02f * u) + 0.25f * Mathf.Sin(2 * Mathf.PI * f * 3.1f * u)) * Mathf.Exp(-u * 16) * Mathf.Min(1, u * 800) * 0.5f;
        }

        static float TablaTin(float u)
        {
            if (u < 0 || u > 0.3f) return 0;
            return (Mathf.Sin(2 * Mathf.PI * 1050 * u) * 0.6f + Noise() * 0.2f * Mathf.Exp(-u * 80)) * Mathf.Exp(-u * 24) * Mathf.Min(1, u * 800) * 0.4f;
        }

        static float TablaGe(float u)
        {
            if (u < 0 || u > 0.6f) return 0;
            float f = 80 + 60 * Mathf.Clamp01(u / 0.12f); // the bayan's upward glide
            float ph = 80 * u + 60 * (u < 0.12f ? u * u / 0.24f : 0.06f + (u - 0.12f));
            return Mathf.Sin(2 * Mathf.PI * ph) * Mathf.Exp(-u * 6) * Mathf.Min(1, u * 400) * 0.9f + 0 * f;
        }

        static float Dhol(float u)
        {
            if (u < 0 || u > 0.3f) return 0;
            return (Mathf.Sin(2 * Mathf.PI * 70 * u) * Mathf.Exp(-u * 12) + Noise() * Mathf.Exp(-u * 50) * 0.4f) * Mathf.Min(1, u * 500);
        }

        // ------------- music -------------

        AudioClip MusicLoop(bool drumsOnly)
        {
            const float bpm = 96;
            float beat = 60f / bpm;
            int bars = 8;
            float len = bars * 4 * beat;
            int n = Mathf.CeilToInt(len * SR);
            int tail = SR * 2;
            var buf = new float[n + tail];
            var r = new System.Random(drumsOnly ? 11 : 3);

            if (!drumsOnly)
            {
                // tabla: theka-style pattern on eighth notes ("dha dhin dhin dha | dha dhin dhin dha | na tin tin na | dha dhin dhin dha")
                string[] pat = { "D", "-", "N", "N", "N", "-", "D", "-", "D", "-", "N", "N", "N", "-", "D", "-",
                                 "T", "-", "t", "t", "t", "-", "T", "-", "D", "-", "N", "N", "N", "-", "D", "G" };
                float eighth = beat / 2f;
                int steps = Mathf.RoundToInt(len / eighth);
                for (int st = 0; st < steps; st++)
                {
                    string h = pat[st % pat.Length];
                    if (h == "-") continue;
                    int s0 = Mathf.RoundToInt(st * eighth * SR);
                    float vel = 0.85f + (float)r.NextDouble() * 0.15f;
                    for (int i = 0; i < SR * 0.6f && s0 + i < buf.Length; i++)
                    {
                        float u = i / (float)SR;
                        float v = 0;
                        switch (h)
                        {
                            case "D": v = TablaNa(u) * 0.8f + TablaGe(u) * 0.7f; break;
                            case "N": v = TablaNa(u, 780) * 0.55f; break;
                            case "T": v = TablaTin(u) * 0.8f; break;
                            case "t": v = TablaTin(u) * 0.5f; break;
                            case "G": v = TablaGe(u) * 0.9f; break;
                        }
                        buf[s0 + i] += v * vel * 0.5f;
                    }
                }

                // drone (tanpura-ish): D and A, slow swell
                float d = 73.42f, a = 110f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SR;
                    float sw = 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * t / (beat * 8));
                    float v = 0;
                    for (int hh = 1; hh <= 6; hh++)
                        v += (Mathf.Sin(2 * Mathf.PI * d * hh * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * a * hh * t + hh)) / (hh * hh * 0.8f + 1);
                    buf[i] += v * 0.035f * (0.6f + 0.4f * sw);
                }

                // rabab melody (Karplus-Strong plucks) on a D major pentatonic
                float[] scale = { 293.66f, 329.63f, 369.99f, 440f, 493.88f, 587.33f, 659.26f, 739.99f, 880f };
                int[] mel =
                {
                    5, -1, 4, 3, 2, -1, 3, -1,   2, 1, 0, -1, 1, 2, 3, -1,
                    5, -1, 6, 5, 4, 3, 4, -1,    3, -1, 2, 1, 2, -1, -1, -1,
                    3, -1, 5, -1, 6, 7, 6, 5,    4, -1, 5, 4, 3, -1, 2, -1,
                    0, 1, 2, -1, 3, 2, 1, -1,    0, -1, -1, -1, 2, -1, 4, 5
                };
                float eighthN = beat / 2f;
                for (int st = 0; st < mel.Length; st++)
                {
                    if (mel[st] < 0) continue;
                    int s0 = Mathf.RoundToInt(st * eighthN * SR);
                    Pluck(buf, s0, scale[mel[st]], 0.9f, 0.22f, r);
                    if (st % 8 == 0) Pluck(buf, s0, scale[mel[st]] / 2f, 1.2f, 0.12f, r);
                }
            }
            else
            {
                // rush layer: driving dhol on every beat with offbeat claps
                float eighth = beat / 2f;
                int steps = Mathf.RoundToInt(len / eighth);
                for (int st = 0; st < steps; st++)
                {
                    int s0 = Mathf.RoundToInt(st * eighth * SR);
                    for (int i = 0; i < SR * 0.3f && s0 + i < buf.Length; i++)
                    {
                        float u = i / (float)SR;
                        float v = st % 2 == 0 ? Dhol(u) * 0.7f : (HiNoise() * Mathf.Exp(-u * 60) * 0.5f);
                        buf[s0 + i] += v * 0.5f;
                    }
                }
            }

            // fold the tail back to the start for a seamless loop
            var outBuf = new float[n];
            for (int i = 0; i < n; i++) outBuf[i] = buf[i];
            for (int i = 0; i < tail; i++) outBuf[i % n] += buf[n + i];
            float peak = 0.001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(outBuf[i]));
            float g = 0.9f / peak;
            for (int i = 0; i < n; i++) outBuf[i] *= g;

            var clip = AudioClip.Create(drumsOnly ? "rush" : "music", n, 1, SR, false);
            clip.SetData(outBuf, 0);
            return clip;
        }

        static void Pluck(float[] buf, int start, float freq, float dur, float vol, System.Random r)
        {
            int period = Mathf.Max(2, Mathf.RoundToInt(SR / freq));
            var ring = new float[period];
            for (int i = 0; i < period; i++) ring[i] = (float)(r.NextDouble() * 2 - 1);
            int n = Mathf.RoundToInt(dur * SR);
            int idx = 0;
            float prev = 0;
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                float v = ring[idx];
                int nx = (idx + 1) % period;
                float avg = 0.5f * (ring[idx] + ring[nx]) * 0.993f;
                ring[idx] = avg;
                idx = nx;
                // gentle low-pass for a warmer, rabab-like tone
                prev = prev + (v - prev) * 0.55f;
                float env = Mathf.Min(1, i / 40f);
                buf[start + i] += prev * vol * env;
            }
        }
    }
}
