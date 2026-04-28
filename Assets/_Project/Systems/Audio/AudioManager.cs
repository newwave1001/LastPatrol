using UnityEngine;

namespace LastPatrol.Systems.Audio
{
    /// <summary>
    /// 게임 전역 오디오 관리자.
    ///   - 1 BGM AudioSource (loop)
    ///   - N SFX AudioSource pool (One-shot, round-robin)
    ///   - 카테고리별 볼륨 (Master/BGM/SFX/UI)
    ///
    /// 정적 API:
    ///   AudioManager.PlaySfx(SfxKey.BulletFireRobot);          // 2D
    ///   AudioManager.PlaySfx(SfxKey.BulletHit, hitPosition);   // 3D positioned
    ///   AudioManager.PlayBGM(bgmClip);
    ///   AudioManager.SetVolume(AudioCategory.SFX, 0.7f);
    ///
    /// 인스턴스: 씬 시작 시 AudioManager 컴포넌트 가진 GO를 한 번 두면 자동으로 DontDestroyOnLoad.
    /// 두 번 이상 두면 첫 번째만 살아남고 나머지 destroy (Singleton 패턴).
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Library")]
        [SerializeField] private SfxLibrary library;

        [Header("Pool")]
        [SerializeField, Range(2, 32)] private int sfxSourcePoolSize = 8;

        [Header("Volume (0..1)")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume    = 0.6f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume    = 1f;
        [SerializeField, Range(0f, 1f)] private float uiVolume     = 0.9f;

        [Header("Behavior")]
        [Tooltip("씬 전환 시 살아남게 할지. 보통 true.")]
        [SerializeField] private bool persistAcrossScenes = true;
        [Tooltip("3D 위치 사운드의 fall-off 거리.")]
        [SerializeField] private float spatial3dMaxDistance = 30f;

        private AudioSource _bgmSource;
        private AudioSource[] _sfxPool;
        private int _sfxRotateIdx;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

            BuildSources();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildSources()
        {
            // BGM
            var bgmGo = new GameObject("BGM");
            bgmGo.transform.SetParent(transform, false);
            _bgmSource = bgmGo.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f; // 2D

            // SFX pool
            _sfxPool = new AudioSource[sfxSourcePoolSize];
            for (int i = 0; i < sfxSourcePoolSize; i++)
            {
                var go = new GameObject($"SFX_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                src.maxDistance = spatial3dMaxDistance;
                src.rolloffMode = AudioRolloffMode.Linear;
                _sfxPool[i] = src;
            }
        }

        // --- Static API ---

        public static void PlaySfx(SfxKey key)
        {
            if (Instance == null) return;
            Instance.PlaySfxInternal(key, null);
        }

        public static void PlaySfx(SfxKey key, Vector3 worldPosition)
        {
            if (Instance == null) return;
            Instance.PlaySfxInternal(key, worldPosition);
        }

        public static void PlayBGM(AudioClip clip)
        {
            if (Instance == null || Instance._bgmSource == null) return;
            if (clip == null) { Instance._bgmSource.Stop(); return; }
            Instance._bgmSource.clip = clip;
            Instance._bgmSource.volume = Instance.masterVolume * Instance.bgmVolume;
            Instance._bgmSource.Play();
        }

        public static void StopBGM()
        {
            if (Instance == null) return;
            Instance._bgmSource.Stop();
        }

        public static void SetVolume(AudioCategory cat, float value)
        {
            if (Instance == null) return;
            value = Mathf.Clamp01(value);
            switch (cat)
            {
                case AudioCategory.Master: Instance.masterVolume = value; break;
                case AudioCategory.BGM:    Instance.bgmVolume    = value; break;
                case AudioCategory.SFX:    Instance.sfxVolume    = value; break;
                case AudioCategory.UI:     Instance.uiVolume     = value; break;
            }
            // BGM volume 즉시 반영
            if (Instance._bgmSource != null)
                Instance._bgmSource.volume = Instance.masterVolume * Instance.bgmVolume;
        }

        // --- Internal ---

        private void PlaySfxInternal(SfxKey key, Vector3? worldPos)
        {
            if (library == null)
            {
                // 라이브러리 없을 때 silent
                return;
            }
            if (!library.TryGetClip(key, out var clip, out float vol, out float pitch))
                return;

            var src = NextSfxSource();
            src.transform.position = worldPos ?? Vector3.zero;
            src.spatialBlend = worldPos.HasValue ? 1f : 0f; // 3D vs 2D
            src.clip = clip;
            src.pitch = pitch;
            src.volume = vol * masterVolume * sfxVolume;
            src.Play();
        }

        private AudioSource NextSfxSource()
        {
            // round-robin — 가장 오래된(또는 정지된) source 우선
            for (int i = 0; i < _sfxPool.Length; i++)
            {
                int idx = (_sfxRotateIdx + i) % _sfxPool.Length;
                if (!_sfxPool[idx].isPlaying)
                {
                    _sfxRotateIdx = (idx + 1) % _sfxPool.Length;
                    return _sfxPool[idx];
                }
            }
            // 모두 재생 중 — 다음 인덱스 강제 재사용
            var s = _sfxPool[_sfxRotateIdx];
            _sfxRotateIdx = (_sfxRotateIdx + 1) % _sfxPool.Length;
            return s;
        }
    }

    public enum AudioCategory { Master, BGM, SFX, UI }
}
