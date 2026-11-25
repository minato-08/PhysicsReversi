using System.Collections.Generic;
using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Short per-renderer feedback on the toon shader: a squash-and-stretch pop and a colour flash.
    // Both go through a MaterialPropertyBlock, so shared materials stay untouched and colliders
    // never change size. One hidden driver updates only the renderers that are animating.
    public sealed class ToonFx : MonoBehaviour
    {
        static readonly int PopId = Shader.PropertyToID("_Pop");
        static readonly int FlashId = Shader.PropertyToID("_FlashColor");
        static ToonFx driver;

        sealed class Effect
        {
            public Vector3 peak; public float popTime, popDuration;
            public Color flash; public float flashTime, flashDuration;
        }
        readonly Dictionary<Renderer, Effect> active = new Dictionary<Renderer, Effect>();
        readonly List<Renderer> finished = new List<Renderer>();
        MaterialPropertyBlock block;

        static ToonFx Driver
        {
            get
            {
                if (driver == null) driver = new GameObject("Toon Fx") { hideFlags = HideFlags.HideInHierarchy }.AddComponent<ToonFx>();
                return driver;
            }
        }

        // Scales the drawn shape toward peak (per object axis) and back, overshooting slightly.
        public static void Pop(Renderer renderer, Vector3 peak, float duration = .18f)
        {
            if (renderer == null || duration <= 0) return;
            var effect = Driver.Get(renderer);
            effect.peak = peak; effect.popTime = 0; effect.popDuration = duration;
        }

        // Tints toward colour (alpha = strength) and fades out.
        public static void Flash(Renderer renderer, Color color, float duration = .12f)
        {
            if (renderer == null || duration <= 0) return;
            var effect = Driver.Get(renderer);
            effect.flash = color; effect.flashTime = 0; effect.flashDuration = duration;
        }

        Effect Get(Renderer renderer)
        {
            if (!active.TryGetValue(renderer, out var effect)) active[renderer] = effect = new Effect { popDuration = -1, flashDuration = -1 };
            return effect;
        }

        void LateUpdate()
        {
            if (active.Count == 0) return;
            if (block == null) block = new MaterialPropertyBlock();
            float dt = Time.deltaTime;
            foreach (var pair in active)
            {
                var renderer = pair.Key; var effect = pair.Value;
                if (renderer == null) { finished.Add(renderer); continue; }
                Vector3 scale = Vector3.one; Color flash = Color.clear;
                bool running = false;
                if (effect.popDuration > 0)
                {
                    effect.popTime += dt;
                    float t = effect.popTime / effect.popDuration;
                    if (t < 1) { scale = Vector3.LerpUnclamped(Vector3.one, effect.peak, PopCurve(t)); running = true; }
                    else effect.popDuration = -1;
                }
                if (effect.flashDuration > 0)
                {
                    effect.flashTime += dt;
                    float t = effect.flashTime / effect.flashDuration;
                    if (t < 1) { flash = effect.flash; flash.a *= 1 - t * t; running = true; }
                    else effect.flashDuration = -1;
                }
                renderer.GetPropertyBlock(block);
                block.SetVector(PopId, new Vector4(scale.x, scale.y, scale.z, 0));
                block.SetColor(FlashId, flash);
                renderer.SetPropertyBlock(block);
                if (!running) finished.Add(renderer);
            }
            foreach (var renderer in finished)
            {
                // Hand the renderer back to the SRP Batcher once it is at rest.
                if (renderer != null) renderer.SetPropertyBlock(null);
                active.Remove(renderer);
            }
            finished.Clear();
        }

        // Up to the peak in the first third, then a small overshoot past rest and settle.
        static float PopCurve(float t)
        {
            if (t < .33f) return Mathf.Sin(t / .33f * Mathf.PI * .5f);
            float u = (t - .33f) / .67f;
            return Mathf.Cos(u * Mathf.PI * 1.5f) * (1 - u);
        }

        void OnDestroy() { if (driver == this) driver = null; }
    }
}
