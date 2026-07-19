using System.Collections.Generic;
using Pizzala.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pizzala.Audio
{
    /// <summary>
    /// Owns global BGM and game-flow sounds without requiring scene references.
    /// The Resources prefab is created automatically before the first scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudioController : MonoBehaviour
    {
        const string ResourcePath = "Audio/PZ_GameAudio";

        static GameAudioController instance;

        [Header("Clips")]
        [SerializeField] AudioClip bgmClip;
        [SerializeField] AudioClip countdownStartClip;
        [SerializeField] AudioClip countdownEndClip;
        [SerializeField] AudioClip hitClip;
        [SerializeField] AudioClip uiClickClip;
        [SerializeField] AudioClip throwingFrisbeeClip;
        [SerializeField] AudioClip[] angryClips;
        [SerializeField] AudioClip[] happyClips;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] float bgmVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float countdownVolume = 1f;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] float voiceVolume = 1f;
        [SerializeField, Range(0f, 1f)] float uiVolume = 0.8f;

        AudioSource bgmSource;
        AudioSource countdownSource;
        AudioSource sfxSource;
        AudioSource uiSource;
        GameFlowController flow;
        GameManager gameManager;
        GameFlowState previousFlowState;
        bool hasPreviousFlowState;
        bool previousRoundActive;
        bool endCountdownPlayed;
        float nextButtonScanTime;
        readonly HashSet<Button> hookedButtons = new HashSet<Button>();

        public static void PlayCustomerHit() => instance?.PlayOneShot(instance.sfxSource, instance.hitClip);
        public static void PlayPizzaThrow() => instance?.PlayOneShot(instance.sfxSource, instance.throwingFrisbeeClip);
        public static void PlayAngry(Vector3 worldPosition) => instance?.PlayRandom3D(instance.angryClips, worldPosition);
        public static void PlayHappy(Vector3 worldPosition) => instance?.PlayRandom3D(instance.happyClips, worldPosition);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateGlobalAudio()
        {
            if (instance != null) return;

            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[GameAudio] Missing Resources/{ResourcePath}.prefab.");
                return;
            }

            Instantiate(prefab);
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            bgmSource = CreateSource("BGM", bgmVolume, true);
            countdownSource = CreateSource("Countdown", countdownVolume, false);
            sfxSource = CreateSource("SFX", sfxVolume, false);
            uiSource = CreateSource("UI", uiVolume, false);

            if (bgmClip != null)
            {
                bgmSource.clip = bgmClip;
                bgmSource.Play();
            }
            else
            {
                Debug.LogWarning("[GameAudio] BGM clip is not assigned.");
            }
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshSceneReferences();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            if (flow == null)
            {
                FindGameFlow();
            }
            else
            {
                GameFlowState current = flow.State;
                if (hasPreviousFlowState && current != previousFlowState)
                {
                    if (current == GameFlowState.Starting || current == GameFlowState.Resuming)
                        PlayCountdownStart();

                    previousFlowState = current;
                }
            }

            TickRoundEndCountdown();

            // Results and other panels can create buttons after sceneLoaded. A cheap,
            // unscaled once-per-second scan keeps those buttons covered without scene setup.
            if (Time.unscaledTime >= nextButtonScanTime)
            {
                nextButtonScanTime = Time.unscaledTime + 1f;
                HookSceneButtons();
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshSceneReferences();
        }

        void RefreshSceneReferences()
        {
            FindGameFlow();
            gameManager = FindFirstObjectByType<GameManager>();
            previousRoundActive = gameManager != null && gameManager.RoundActive;
            endCountdownPlayed = false;
            HookSceneButtons();
        }

        void FindGameFlow()
        {
            flow = FindFirstObjectByType<GameFlowController>();
            hasPreviousFlowState = flow != null;

            if (!hasPreviousFlowState) return;

            previousFlowState = flow.State;

            // Handles BackBone opened directly with skipTutorialInEditor enabled. In that
            // path GameFlowController may already be in Starting before this object finds it.
            if (previousFlowState == GameFlowState.Starting || previousFlowState == GameFlowState.Resuming)
                PlayCountdownStart();
        }

        void TickRoundEndCountdown()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
                if (gameManager == null) return;
            }

            bool roundActive = gameManager.RoundActive;
            if (roundActive && !previousRoundActive)
                endCountdownPlayed = false;

            if (roundActive && !endCountdownPlayed && gameManager.TimeRemaining <= 4f)
            {
                endCountdownPlayed = true;
                PlayCountdownEnd();
            }

            previousRoundActive = roundActive;
        }

        void PlayCountdownStart()
        {
            if (countdownStartClip == null)
            {
                Debug.LogWarning("[GameAudio] Countdown-start clip is not assigned.");
                return;
            }

            countdownSource.Stop();
            countdownSource.clip = countdownStartClip;
            countdownSource.Play();
        }

        void PlayCountdownEnd()
        {
            if (countdownEndClip == null)
            {
                Debug.LogWarning("[GameAudio] Countdown-end clip is not assigned.");
                return;
            }

            countdownSource.Stop();
            countdownSource.clip = countdownEndClip;
            countdownSource.Play();
        }

        void HookSceneButtons()
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button == null || !hookedButtons.Add(button)) continue;
                button.onClick.AddListener(PlayUiClick);
            }
        }

        void PlayUiClick()
        {
            PlayOneShot(uiSource, uiClickClip);
        }

        void PlayRandom3D(AudioClip[] clips, Vector3 worldPosition)
        {
            if (clips == null || clips.Length == 0) return;

            int start = Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[(start + i) % clips.Length];
                if (clip == null) continue;
                PlayVoiceAt(clip, worldPosition);
                return;
            }
        }

        void PlayVoiceAt(AudioClip clip, Vector3 worldPosition)
        {
            var voiceObject = new GameObject($"Voice_{clip.name}");
            voiceObject.transform.position = worldPosition;

            var source = voiceObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = false;
            source.volume = voiceVolume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.5f;
            source.maxDistance = 12f;
            source.dopplerLevel = 0f;
            source.Play();

            Destroy(voiceObject, clip.length + 0.1f);
        }

        void PlayOneShot(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
                source.PlayOneShot(clip);
        }

        AudioSource CreateSource(string childName, float volume, bool loop)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
