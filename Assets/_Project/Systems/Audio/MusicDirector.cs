using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using LastPatrol.Systems.World;

namespace LastPatrol.Systems.Audio
{
    /// <summary>
    /// 게임 상태 기반 BGM 자동 전환. WAV/MP3 파일을 인스펙터 슬롯에 드래그하면 작동.
    ///
    /// 트랙 우선순위 (높음 → 낮음):
    ///   1. CombatStatus.InCombat → combatBGM (AMBUSH/드론 wave 등)
    ///   2. 활성 씬 이름이 indoorSceneNamePrefix로 시작 → indoorBGM (S03 실내)
    ///   3. 그 외 → drivingBGM (S01 외부 운전 기본)
    ///
    /// 두 AudioSource 사이 crossfade — 트랙 전환 시 부드러움.
    /// persistAcrossScenes=true 면 DontDestroyOnLoad — S01→S03→S01 흐름에서도 유지.
    ///
    /// 사용법:
    ///   1. Project → Assets/_Project/Audio/Music/ 폴더에 WAV/MP3 드래그 (자동 AudioClip 변환)
    ///   2. 빈 GameObject + MusicDirector 컴포넌트 → 씬 root에 둠
    ///   3. drivingBGM / indoorBGM / combatBGM 슬롯에 클립 드래그
    ///   4. 슬롯 비워둬도 됨 — 해당 상태에선 무음 (상태 진입해도 트랙만 멈춤)
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicDirector : MonoBehaviour
    {
        [Header("Tracks (드래그)")]
        [Tooltip("S01 외부 운전 기본 BGM. 인카운터·실내 외 모든 시점.")]
        [SerializeField] private AudioClip drivingBGM;
        [Tooltip("실내 수사 BGM. indoorSceneNamePrefix 로 시작하는 씬에서 재생.")]
        [SerializeField] private AudioClip indoorBGM;
        [Tooltip("전투 BGM. CombatStatus.InCombat 동안 우선 재생.")]
        [SerializeField] private AudioClip combatBGM;

        [Header("Behavior")]
        [Tooltip("트랙 전환 시 crossfade 시간(초).")]
        [SerializeField, Range(0f, 5f)] private float crossfadeDuration = 1.5f;
        [Tooltip("BGM 마스터 볼륨 (0..1). AudioManager.bgmVolume과 별도.")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
        [Tooltip("이 prefix로 시작하는 씬은 실내로 간주. 'S03', 'Scene_Indoor' 등.")]
        [SerializeField] private string indoorSceneNamePrefix = "S03";
        [Tooltip("씬 전환 시 살아남게 할지. 보통 true.")]
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private bool logEvents = true;

        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _activeIsA = true; // 현재 재생 중인 소스
        private AudioClip _currentClip;
        private Coroutine _crossfadeRoutine;

        void Awake()
        {
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
            BuildSources();
        }

        void Update()
        {
            AudioClip target = ResolveTrack();
            if (target != _currentClip) SwitchTo(target);
        }

        private AudioClip ResolveTrack()
        {
            if (CombatStatus.InCombat) return combatBGM != null ? combatBGM : drivingBGM;

            if (!string.IsNullOrEmpty(indoorSceneNamePrefix))
            {
                string sceneName = SceneManager.GetActiveScene().name;
                if (sceneName.StartsWith(indoorSceneNamePrefix) && indoorBGM != null) return indoorBGM;
            }

            return drivingBGM;
        }

        private void SwitchTo(AudioClip newClip)
        {
            _currentClip = newClip;
            if (logEvents) Debug.Log($"[MusicDirector] switch → {(newClip != null ? newClip.name : "null/silent")}", this);

            if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
            _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(newClip));
        }

        private IEnumerator CrossfadeRoutine(AudioClip newClip)
        {
            AudioSource fromSrc = _activeIsA ? _sourceA : _sourceB;
            AudioSource toSrc   = _activeIsA ? _sourceB : _sourceA;

            // 새 트랙 준비
            if (newClip != null)
            {
                toSrc.clip = newClip;
                toSrc.volume = 0f;
                toSrc.Play();
            }

            // 페이드
            float t = 0f;
            float fadeDur = Mathf.Max(0.05f, crossfadeDuration);
            float fromStart = fromSrc.volume;
            while (t < fadeDur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeDur);
                fromSrc.volume = Mathf.Lerp(fromStart, 0f, k);
                if (newClip != null) toSrc.volume = Mathf.Lerp(0f, volume, k);
                yield return null;
            }

            // 정리
            fromSrc.Stop();
            fromSrc.clip = null;
            fromSrc.volume = 0f;

            if (newClip != null)
            {
                toSrc.volume = volume;
                _activeIsA = !_activeIsA;
            }
            _crossfadeRoutine = null;
        }

        private void BuildSources()
        {
            _sourceA = CreateSource("MusicSource_A");
            _sourceB = CreateSource("MusicSource_B");
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D
            src.volume = 0f;
            return src;
        }

        // --- API ---

        /// <summary>볼륨 즉시 적용 (현재 재생 중인 소스에).</summary>
        public void SetVolume(float v)
        {
            volume = Mathf.Clamp01(v);
            var active = _activeIsA ? _sourceA : _sourceB;
            if (active != null && active.isPlaying) active.volume = volume;
        }

        /// <summary>강제 트랙 변경 — 외부 cutscene 같은 곳에서 사용.</summary>
        public void ForceTrack(AudioClip clip)
        {
            SwitchTo(clip);
        }

        /// <summary>모든 BGM 멈춤 — Game Over UI 등.</summary>
        public void StopAll()
        {
            if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
            if (_sourceA != null) _sourceA.Stop();
            if (_sourceB != null) _sourceB.Stop();
            _currentClip = null;
        }
    }
}
