using System.Collections.Generic;
using UnityEngine;

namespace PhysicsReversi.Walk
{
    // The game's sounds, by the names in AUDIO.md. The list is filled from Assets/PhysicsReversi/Audio/
    // by the editor (GameSoundsImport): grab.ogg becomes "grab", hit_1.ogg and hit_2.ogg both "hit".
    // Volumes and the other values here are kept when the list is filled again.
    public sealed class GameSounds : ScriptableObject
    {
        [System.Serializable]
        public sealed class Sound
        {
            public string name;
            [Range(0, 1)] public float volume = .8f;
            [Tooltip("Plays the files of this other sound instead of its own (\"place\" uses \"hit\"). Its own volume still applies.")]
            public string sameAs = "";
            [Tooltip("Variants; one is picked at random, never the same twice in a row when there are several.")]
            public AudioClip[] clips = new AudioClip[0];
        }

        public List<Sound> sounds = new List<Sound>();
        [Header("Every sound")]
        [Tooltip("Each play is shifted up or down in pitch by up to this much, so repeats do not sound mechanical.")]
        [Range(0, .3f)] public float pitchJitter = .06f;
        [Tooltip("Each play is quieter by up to this much, for the same reason.")]
        [Range(0, .5f)] public float volumeJitter = .1f;
        [Tooltip("Sounds lean to the side of the screen they come from: black plays on the left, white on the right. 0 keeps everything in the middle.")]
        [Range(0, 1)] public float pan = .4f;
        [Tooltip("At most this many sounds at once; the oldest gives way.")]
        [Range(1, 32)] public int voices = 12;
        [Header("Hits (stone on stone, stone on player)")]
        [Tooltip("Hits slower than this (relative speed) are silent.")]
        [Min(0)] public float hitMinSpeed = 2.5f;
        [Tooltip("Hits this fast or faster are at full volume; slower ones are quieter.")]
        [Min(0)] public float hitFullSpeed = 12;
        [Tooltip("Volume of the softest hit that still sounds, against a full one.")]
        [Range(0, 1)] public float hitSoftVolume = .3f;
        [Tooltip("A stone makes at most one hit or place sound in this time.")]
        [Min(0)] public float hitCooldown = .08f;
        [Header("Places (a stone landing on the table or the floor)")]
        [Tooltip("Landings slower than this are silent.")]
        [Min(0)] public float placeMinSpeed = 1;
        [Tooltip("Landings this fast or faster are at full volume.")]
        [Min(0)] public float placeFullSpeed = 8;
        [Range(0, 1)] public float placeSoftVolume = .3f;

        public Sound Find(string soundName)
        {
            foreach (var s in sounds) if (s != null && s.name == soundName) return s;
            return null;
        }
        // The files a sound plays: its own, or those of the sound it is the same as.
        public AudioClip[] Clips(Sound sound)
        {
            if (sound == null) return null;
            if (!string.IsNullOrEmpty(sound.sameAs) && sound.sameAs != sound.name) return Find(sound.sameAs)?.clips;
            return sound.clips;
        }
    }
}
