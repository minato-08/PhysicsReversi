using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Trial (2026-11-27, SPEC.md 試遊で出た課題 3): a confirmed stone wears a thin ring on its
    // caps, so it can be told from a loose one at a glance. The ring is drawn by the toon shader
    // (_MarkColor); a confirmed stone simply swaps each face's material for a copy with the ring
    // turned on. Shared materials, so the SRP Batcher keeps drawing them together, and nothing is
    // written per renderer that ToonFx's effects could wipe.
    public sealed class ConfirmedLook : MonoBehaviour
    {
        public WalkBoardRecognition recognition;
        [Tooltip("A stone's own materials, and in the same order, the copies it wears while confirmed.")]
        public Material[] loose, confirmed;

        bool[] shown;

        void LateUpdate()
        {
            if (recognition == null || recognition.stones == null || loose == null || confirmed == null) return;
            var stones = recognition.stones;
            if (shown == null || shown.Length != stones.Length) shown = new bool[stones.Length];
            for (int i = 0; i < stones.Length; i++)
            {
                var stone = stones[i]; if (stone == null) continue;
                bool on = stone.Confirmed && stone.status == StoneStatus.OnBoard;
                if (on == shown[i]) continue;
                shown[i] = on;
                var renderer = stone.GetComponent<Renderer>(); if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++) materials[m] = Swap(materials[m], on);
                renderer.sharedMaterials = materials;
            }
        }

        Material Swap(Material material, bool on)
        {
            var from = on ? loose : confirmed; var to = on ? confirmed : loose;
            int index = System.Array.IndexOf(from, material);
            return index >= 0 && index < to.Length && to[index] != null ? to[index] : material;
        }
    }
}
