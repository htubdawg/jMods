using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
    internal sealed class TurnSignalAudio
    {
        private readonly AudioSource source;
        private readonly AudioClip tick, tock;
        private readonly SignalRelayTracker tracker = new SignalRelayTracker();
        private bool wasAudible;

        internal TurnSignalAudio(Transform parent, GameObject cabin, List<Object> owned)
        {
            var go = new GameObject("SlapPack.TurnSignalRelay");
            owned.Add(go); go.transform.SetParent(parent, false);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false;
            source.spatialBlend = 0; source.dopplerLevel = 0;
            source.outputAudioMixerGroup = AudioGroupZone.For(cabin);
            tick = Clip(true, owned); tock = Clip(false, owned);
        }

        private static AudioClip Clip(bool on, List<Object> owned)
        {
            float[] samples = SignalRelaySamples.Generate(on);
            AudioClip clip = AudioClip.Create(on ? "SlapPack relay tick" : "SlapPack relay tock", samples.Length, 1, SignalRelaySamples.SampleRate, false);
            owned.Add(clip);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Could not initialize turn-signal relay audio.");
            return clip;
        }

        internal void Step(int direction, bool lit, bool audible, float volume)
        {
            RelayCue cue = tracker.Step(direction, lit, audible);
            if (!source) return;
            if (!audible)
            {
                // Stop even if the game's audio listener has already paused the voice.
                if (wasAudible) source.Stop();
                wasAudible = false;
                return;
            }
            wasAudible = true;
            source.volume = Mathf.Clamp01(volume);
            if (cue != RelayCue.None) source.PlayOneShot(cue == RelayCue.Tick ? tick : tock);
        }

        internal void Click(float volume)
        {
            if (!source) return;
            source.volume = Mathf.Clamp01(volume);
            source.PlayOneShot(tick);
        }

        internal void Stop() { if (source) source.Stop(); }
    }
}
