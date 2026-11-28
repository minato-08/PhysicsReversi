using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using PhysicsReversi.Walk;

namespace PhysicsReversi.Editor
{
    // Keeps Resources/GameSounds.asset in step with the files in Assets/PhysicsReversi/Audio/: a file
    // dropped in, renamed or deleted there is picked up on its own. grab.ogg is "grab"; hit_1.ogg,
    // hit_2.ogg are both "hit". Volumes and the other values set in the asset are kept.
    public sealed class GameSoundsImport : AssetPostprocessor
    {
        const string AudioDir = "Assets/PhysicsReversi/Audio";
        const string AssetPath = "Assets/PhysicsReversi/Resources/GameSounds.asset";
        static bool queued;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (queued) return;
            foreach (var list in new[] { imported, deleted, moved, movedFrom })
                foreach (var path in list)
                    if (path.StartsWith(AudioDir + "/")) { queued = true; EditorApplication.delayCall += Refresh; return; }
        }

        [MenuItem("Physics Reversi/Audio/Refresh Game Sounds")]
        public static void Refresh()
        {
            queued = false;
            var asset = AssetDatabase.LoadAssetAtPath<GameSounds>(AssetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                asset = ScriptableObject.CreateInstance<GameSounds>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var groups = new SortedDictionary<string, List<AudioClip>>();
            if (AssetDatabase.IsValidFolder(AudioDir))
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) continue;
                    string name = Regex.Replace(Path.GetFileNameWithoutExtension(path), @"_\d+$", "");
                    if (!groups.TryGetValue(name, out var clips)) groups[name] = clips = new List<AudioClip>();
                    clips.Add(clip);
                }
            var kept = new List<GameSounds.Sound>();
            var summary = new List<string>();
            foreach (var pair in groups)
            {
                pair.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                var sound = asset.Find(pair.Key) ?? new GameSounds.Sound { name = pair.Key };
                sound.clips = pair.Value.ToArray();
                kept.Add(sound);
                summary.Add(pair.Key + " x" + pair.Value.Count);
            }
            // A sound borrowing another's files stays, though it has none of its own.
            foreach (var sound in asset.sounds)
                if (sound != null && !groups.ContainsKey(sound.name) && !string.IsNullOrEmpty(sound.sameAs))
                { sound.clips = new AudioClip[0]; kept.Add(sound); summary.Add(sound.name + " = " + sound.sameAs); }
            asset.sounds = kept;
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
            Debug.Log("Game sounds: " + (summary.Count > 0 ? string.Join(", ", summary) : "none") + " (" + AssetPath + ")");
        }
    }
}
