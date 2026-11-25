using System.Collections.Generic;
using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Visual feedback for what stones do: grabs, landings, knocks, flips and confirmations each get
    // an answer within one frame. Strength follows rarity (2026-11-25 research): confirmations,
    // which happen all the time, only glint; flips, the payoff, get the most. No time scaling during
    // play, since a hit-stop would freeze the other player too. Never changes a rule or a rigidbody.
    public sealed class StoneFeel : MonoBehaviour
    {
        public CarryAuthority authority;
        [Tooltip("Unlit, transparent material for the square particles.")]
        public Material particleMaterial;
        [Tooltip("Scales every particle's size; tuned against the game camera, where 1 was too small to read.")]
        public float particleScale = 2.2f;

        [Header("Grab")]
        public Vector3 grabPop = new Vector3(1.08f, 1.5f, 1.08f);
        [Header("Landing and knocks")]
        [Tooltip("Impacts slower than this (relative speed) make no feedback.")]
        public float minImpactSpeed = 2.5f;
        [Tooltip("Impact speed at which the feedback is at full strength.")]
        public float fullImpactSpeed = 12;
        public Vector3 landPop = new Vector3(1.1f, .55f, 1.1f);
        [Range(0, 12)] public int dustCount = 5;
        [Tooltip("A stone gives at most one knock effect in this time.")]
        public float impactCooldown = .12f;
        [Header("Flip")]
        [Tooltip("A small dip as the stone starts to turn: the anticipation.")]
        public Vector3 flipDip = new Vector3(1.06f, .7f, 1.06f);
        [Tooltip("The pop as it lands on its new face.")]
        public Vector3 flipPop = new Vector3(1.15f, 1.6f, 1.15f);
        [Range(0, 1)] public float flipFlash = .55f;
        [Range(0, 20)] public int sparkleCount = 8;
        [Tooltip("Sparkles of one capture start this much later per stone, so a line reads as a ripple.")]
        public float rippleDelay = .05f;
        [Header("Confirm")]
        [Tooltip("Confirmations are frequent, so they only glint.")]
        [Range(0, 1)] public float confirmFlash = .2f;

        ParticleSystem particles;
        readonly Dictionary<CarryStone, float> lastImpact = new Dictionary<CarryStone, float>();
        readonly List<(float time, CarryStone stone)> pendingSparkles = new List<(float, CarryStone)>();
        float lastFlipTime = -1; int flipsThisFrame;

        void OnEnable()
        {
            if (authority != null) { authority.StoneGrabbed += OnGrabbed; authority.StoneThrown += OnThrown; }
            CarryStone.Impacted += OnImpact; CarryStone.FlipStarted += OnFlip; CarryStone.FlipEnded += OnFlipEnded; CarryStone.BecameConfirmed += OnConfirmed;
        }
        void OnDisable()
        {
            if (authority != null) { authority.StoneGrabbed -= OnGrabbed; authority.StoneThrown -= OnThrown; }
            CarryStone.Impacted -= OnImpact; CarryStone.FlipStarted -= OnFlip; CarryStone.FlipEnded -= OnFlipEnded; CarryStone.BecameConfirmed -= OnConfirmed;
        }

        void OnGrabbed(WalkPlayer player, CarryStone stone)
        {
            var r = Renderer(stone); ToonFx.Pop(r, grabPop, .2f); ToonFx.Flash(r, new Color(1, 1, 1, .35f), .1f);
            Burst(stone.transform.position, 4, Palette.Player(player.playerId), 2.5f, .25f, .3f);
        }

        void OnThrown(WalkPlayer player, CarryStone stone, float speed)
        {
            ToonFx.Flash(Renderer(stone), WithAlpha(Palette.Accent, .5f), .15f);
            Burst(stone.transform.position, 6, Palette.Accent, 4, .2f, .35f);
        }

        void OnImpact(CarryStone stone, Collision collision)
        {
            if (stone.status == StoneStatus.Reserve) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;
            if (lastImpact.TryGetValue(stone, out float last) && Time.time - last < impactCooldown) return;
            lastImpact[stone] = Time.time;
            float strength = Mathf.InverseLerp(minImpactSpeed, fullImpactSpeed, speed);
            ToonFx.Pop(Renderer(stone), Vector3.LerpUnclamped(Vector3.one, landPop, .4f + .6f * strength), .16f);
            Vector3 at = collision.contactCount > 0 ? collision.GetContact(0).point : stone.transform.position;
            Burst(at, Mathf.RoundToInt(dustCount * (.4f + .6f * strength)), Palette.Table, 2 + 3 * strength, .2f, .4f);
        }

        void OnFlip(CarryStone stone) => ToonFx.Pop(Renderer(stone), flipDip, .12f);

        void OnFlipEnded(CarryStone stone)
        {
            var r = Renderer(stone); ToonFx.Pop(r, flipPop, .25f); ToonFx.Flash(r, WithAlpha(Palette.StoneWhite, flipFlash), .2f);
            // Stones of one capture land in the same frame; space their sparkles out into a ripple.
            if (!Mathf.Approximately(lastFlipTime, Time.time)) { lastFlipTime = Time.time; flipsThisFrame = 0; }
            pendingSparkles.Add((Time.time + rippleDelay * flipsThisFrame++, stone));
        }

        void OnConfirmed(CarryStone stone)
        {
            ToonFx.Flash(Renderer(stone), WithAlpha(Palette.Accent, confirmFlash), .2f);
        }

        void Update()
        {
            for (int i = pendingSparkles.Count - 1; i >= 0; i--)
            {
                if (Time.time < pendingSparkles[i].time) continue;
                var stone = pendingSparkles[i].stone; pendingSparkles.RemoveAt(i);
                if (stone == null) continue;
                Vector3 at = stone.transform.position + Vector3.up * .4f;
                Burst(at, sparkleCount / 2, Palette.Accent, 6, .3f, .5f);
                Burst(at, sparkleCount - sparkleCount / 2, Palette.StoneWhite, 5, .25f, .45f);
            }
        }

        static Renderer Renderer(CarryStone stone) => stone != null ? stone.GetComponent<Renderer>() : null;
        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        // Square bits flung up and out from a point, shrinking away. One shared system, no allocations per event.
        void Burst(Vector3 at, int count, Color color, float speed, float size, float life)
        {
            if (count <= 0) return;
            var system = Particles();
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, startColor = color };
            for (int i = 0; i < count; i++)
            {
                Vector2 around = Random.insideUnitCircle.normalized;
                emit.position = at + new Vector3(around.x, 0, around.y) * .4f;
                emit.velocity = (new Vector3(around.x, 1.2f, around.y)).normalized * speed * Random.Range(.7f, 1.1f);
                emit.startSize = size * particleScale * Random.Range(.8f, 1.2f);
                emit.startLifetime = life * Random.Range(.8f, 1.2f);
                emit.rotation3D = new Vector3(0, 0, Random.Range(0f, 90f));
                system.Emit(emit, 1);
            }
        }

        ParticleSystem Particles()
        {
            if (particles != null) return particles;
            var obj = new GameObject("Stone Feel Particles");
            obj.transform.SetParent(transform, false);
            particles = obj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.2f; main.maxParticles = 400;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var sizeOverLife = particles.sizeOverLifetime; sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play();
            return particles;
        }
    }
}
