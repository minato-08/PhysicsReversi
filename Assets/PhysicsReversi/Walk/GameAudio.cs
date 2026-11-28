using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicsReversi.Walk
{
    // Plays the game's sounds (AUDIO.md) for what happens in a Walk scene. Made on its own when Play
    // starts, in any scene with a CarryAuthority, so no scene needs editing; it outlives a restart
    // (the scene being loaded again). Sound only: it never changes a rule or a rigidbody.
    // A sound with no file yet is simply silent.
    public sealed class GameAudio : MonoBehaviour
    {
        static GameAudio instance;
        GameSounds sounds;
        CarryAuthority authority;
        AudioSource[] voices;
        int nextVoice;
        readonly Dictionary<string, int> lastClip = new Dictionary<string, int>();
        readonly Dictionary<CarryStone, float> lastHit = new Dictionary<CarryStone, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var loaded = Resources.Load<GameSounds>("GameSounds");
            if (loaded == null) return;
            instance = new GameObject("Game Audio").AddComponent<GameAudio>();
            DontDestroyOnLoad(instance.gameObject);
            instance.sounds = loaded;
        }

        void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            CarryStone.Impacted += OnImpact;
        }
        void Start() => Attach();
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CarryStone.Impacted -= OnImpact;
            Detach();
            if (instance == this) instance = null;
        }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Detach(); lastHit.Clear(); Attach(); }

        void Attach()
        {
            authority = FindAnyObjectByType<CarryAuthority>();
            if (authority != null) authority.StoneGrabbed += OnGrabbed;
        }
        void Detach()
        {
            if (authority != null) authority.StoneGrabbed -= OnGrabbed;
            authority = null;
        }

        void OnGrabbed(WalkPlayer player, CarryStone stone) => Play("grab", stone.transform.position, 1);

        void OnImpact(CarryStone stone, Collision collision)
        {
            if (sounds == null || stone == null) return;
            var otherStone = collision.collider.GetComponentInParent<CarryStone>();
            var otherPlayer = otherStone == null ? collision.collider.GetComponentInParent<WalkPlayer>() : null;
            if (otherStone == null && otherPlayer == null) { OnLanding(stone, collision); return; }
            if (otherStone != null)
            {
                // Both stones report the same knock; let one of them speak.
                if (otherStone.GetHashCode() < stone.GetHashCode()) return;
                if (stone.status == StoneStatus.Reserve && otherStone.status == StoneStatus.Reserve) return;
            }
            float speed = collision.relativeVelocity.magnitude;
            if (speed < sounds.hitMinSpeed) return;
            if (lastHit.TryGetValue(stone, out float last) && Time.time - last < sounds.hitCooldown) return;
            if (otherStone != null && lastHit.TryGetValue(otherStone, out last) && Time.time - last < sounds.hitCooldown) return;
            lastHit[stone] = Time.time; if (otherStone != null) lastHit[otherStone] = Time.time;
            float strength = Mathf.InverseLerp(sounds.hitMinSpeed, Mathf.Max(sounds.hitFullSpeed, sounds.hitMinSpeed + .01f), speed);
            Vector3 at = collision.contactCount > 0 ? collision.GetContact(0).point : stone.transform.position;
            Play("hit", at, Mathf.Lerp(sounds.hitSoftVolume, 1, strength));
        }

        // A stone on the table or the floor: "place". Reserve stones settling in their slots stay quiet.
        void OnLanding(CarryStone stone, Collision collision)
        {
            if (stone.status != StoneStatus.OnBoard) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < sounds.placeMinSpeed) return;
            if (lastHit.TryGetValue(stone, out float last) && Time.time - last < sounds.hitCooldown) return;
            lastHit[stone] = Time.time;
            float strength = Mathf.InverseLerp(sounds.placeMinSpeed, Mathf.Max(sounds.placeFullSpeed, sounds.placeMinSpeed + .01f), speed);
            Vector3 at = collision.contactCount > 0 ? collision.GetContact(0).point : stone.transform.position;
            Play("place", at, Mathf.Lerp(sounds.placeSoftVolume, 1, strength));
        }

        // loudness scales the sound's own volume, 0 to 1.
        void Play(string soundName, Vector3 at, float loudness)
        {
            if (sounds == null) return;
            var sound = sounds.Find(soundName);
            var clips = sounds.Clips(sound);
            if (clips == null || clips.Length == 0) return;
            int pick = Random.Range(0, clips.Length);
            if (clips.Length > 1 && lastClip.TryGetValue(soundName, out int previous) && pick == previous)
                pick = (pick + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            lastClip[soundName] = pick;
            var clip = clips[pick]; if (clip == null) return;
            var voice = Voice();
            voice.clip = clip;
            voice.volume = sound.volume * loudness * (1 - Random.Range(0, sounds.volumeJitter));
            voice.pitch = 1 + Random.Range(-sounds.pitchJitter, sounds.pitchJitter);
            voice.panStereo = Pan(at);
            voice.Play();
        }

        float Pan(Vector3 at)
        {
            var cam = Camera.main;
            if (cam == null || sounds.pan <= 0) return 0;
            float x = cam.WorldToViewportPoint(at).x;
            return Mathf.Clamp(x * 2 - 1, -1, 1) * sounds.pan;
        }

        // A free voice, or else the one that started longest ago.
        AudioSource Voice()
        {
            int count = Mathf.Max(1, sounds.voices);
            if (voices == null || voices.Length != count)
            {
                if (voices != null) foreach (var v in voices) if (v != null) Destroy(v);
                voices = new AudioSource[count];
                for (int i = 0; i < count; i++)
                {
                    var v = gameObject.AddComponent<AudioSource>();
                    v.playOnAwake = false; v.spatialBlend = 0; voices[i] = v;
                }
                nextVoice = 0;
            }
            for (int i = 0; i < count; i++)
            {
                var v = voices[(nextVoice + i) % count];
                if (!v.isPlaying) { nextVoice = (nextVoice + i + 1) % count; return v; }
            }
            var oldest = voices[nextVoice]; nextVoice = (nextVoice + 1) % count;
            return oldest;
        }
    }
}
