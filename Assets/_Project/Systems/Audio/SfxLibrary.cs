using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastPatrol.Systems.Audio
{
    /// <summary>
    /// 사운드 이벤트 → AudioClip 매핑 ScriptableObject.
    /// 인스펙터에서 클립 드래그. 클립 비어 있으면 PlaySfx 호출은 silent (디버그 로그만).
    ///
    /// 사용:
    ///   1. Project 창 우클릭 → Create → LastPatrol → Audio → Sfx Library
    ///   2. 자동으로 모든 SfxKey 엔트리 생성됨 (Sync Entries 버튼 또는 ContextMenu)
    ///   3. 각 엔트리에 AudioClip 드래그 + 볼륨/피치 조정
    ///   4. AudioManager 컴포넌트에 이 asset 드래그
    /// </summary>
    [CreateAssetMenu(menuName = "LastPatrol/Audio/Sfx Library", fileName = "SFX_Library")]
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxKey key;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("피치 임의 변동(±). 0이면 고정. 0.05f = ±5% 변동.")]
            [Range(0f, 0.5f)] public float pitchVariance = 0.03f;
            [Tooltip("기본 피치. 1.0 = 원본.")]
            [Range(0.5f, 2f)] public float basePitch = 1f;
        }

        [SerializeField] private List<Entry> entries = new();

        [Tooltip("클립 없을 때 콘솔 경고 출력 (디자인 단계에선 false 권장 — 잡소리 줄임).")]
        [SerializeField] private bool warnOnMissingClip = false;

        private Dictionary<SfxKey, Entry> _index;

        public void EnsureIndex()
        {
            if (_index != null && _index.Count == entries.Count) return;
            _index = new Dictionary<SfxKey, Entry>(entries.Count);
            foreach (var e in entries)
            {
                if (e == null) continue;
                _index[e.key] = e;
            }
        }

        public Entry Get(SfxKey key)
        {
            EnsureIndex();
            if (_index.TryGetValue(key, out var e)) return e;
            return null;
        }

        public bool TryGetClip(SfxKey key, out AudioClip clip, out float volume, out float pitch)
        {
            var e = Get(key);
            if (e == null || e.clip == null)
            {
                clip = null; volume = 1f; pitch = 1f;
                if (warnOnMissingClip) Debug.LogWarning($"[SfxLibrary] {key} 클립 없음 — silent.", this);
                return false;
            }
            clip = e.clip;
            volume = e.volume;
            pitch = e.basePitch + UnityEngine.Random.Range(-e.pitchVariance, e.pitchVariance);
            return true;
        }

        // --- Editor helper ---

        [ContextMenu("Sync Entries with SfxKey enum")]
        private void SyncEntries()
        {
            var existing = new Dictionary<SfxKey, Entry>();
            foreach (var e in entries) if (e != null) existing[e.key] = e;

            entries.Clear();
            foreach (SfxKey k in Enum.GetValues(typeof(SfxKey)))
            {
                if (existing.TryGetValue(k, out var found)) entries.Add(found);
                else entries.Add(new Entry { key = k });
            }
            _index = null;
            Debug.Log($"[SfxLibrary] entries synced — {entries.Count}개", this);
        }
    }
}
