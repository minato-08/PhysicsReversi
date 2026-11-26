using UnityEngine;

namespace PhysicsReversi.Walk
{
    // Trial (2026-11-27, SPEC.md 試遊で出た課題 3): a stone on the board that is not confirmed yet
    // wears a faint overlay on its caps (plain, stripes or dots, steady or blinking), so the
    // confirmed ones, which look plain, can be told from the loose ones at a glance. The overlay is
    // drawn by the toon shader (_LooseColor); a loose stone simply swaps each face's material for a
    // copy with the overlay turned on. Shared materials, so the SRP Batcher keeps drawing them
    // together, and nothing is written per renderer that ToonFx's effects could wipe.
    public sealed class LooseStoneLook : MonoBehaviour
    {
        public WalkBoardRecognition recognition;
        [Tooltip("A stone's own materials, and in the same order, the copies it wears while loose on the board.")]
        public Material[] plain, loose;

        bool[] shown;

        void LateUpdate()
        {
            if (recognition == null || recognition.stones == null || plain == null || loose == null) return;
            var stones = recognition.stones;
            if (shown == null || shown.Length != stones.Length) shown = new bool[stones.Length];
            bool reading = recognition.isActiveAndEnabled;
            for (int i = 0; i < stones.Length; i++)
            {
                var stone = stones[i]; if (stone == null) continue;
                // Once the board is no longer read (the result is being stacked), every stone looks plain.
                bool on = reading && stone.status == StoneStatus.OnBoard && !stone.Confirmed;
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
            var from = on ? plain : loose; var to = on ? loose : plain;
            int index = System.Array.IndexOf(from, material);
            return index >= 0 && index < to.Length && to[index] != null ? to[index] : material;
        }
    }
}
