using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Plays / switches between the decoded game idle clips on the character's legacy Animation
    /// component. The builder fills <see cref="clipNames"/> with every clip it imported.
    ///
    /// Usage in Play mode:
    ///   • autoCycle on  → cross-fades through all idles, <see cref="secondsPerClip"/> each.
    ///   • autoCycle off → plays the clip at <see cref="clipIndex"/>; change the index in the
    ///     Inspector (or press N) to try another idle.
    /// </summary>
    [RequireComponent(typeof(Animation))]
    public class Sims4IdleSwitcher : MonoBehaviour
    {
        [Tooltip("All idle clips imported for this character (set by the builder).")]
        public string[] clipNames = new string[0];

        [Tooltip("Cross-fade through every idle automatically.")]
        public bool autoCycle = true;

        [Tooltip("Seconds to hold each idle when autoCycle is on.")]
        public float secondsPerClip = 6f;

        [Tooltip("Which idle to play when autoCycle is off. Press N to advance.")]
        public int clipIndex = 0;

        private Animation _anim;
        private float _timer;
        private int _playing = -1;

        private void OnEnable()
        {
            _anim = GetComponent<Animation>();
            Play(clipIndex);
        }

        private void Update()
        {
            if (clipNames == null || clipNames.Length == 0) return;

            if (autoCycle)
            {
                _timer += Time.deltaTime;
                if (_timer >= Mathf.Max(0.5f, secondsPerClip))
                {
                    _timer = 0f;
                    Play(clipIndex + 1);
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.N)) Play(clipIndex + 1);
                else if (clipIndex != _playing) Play(clipIndex);
            }
        }

        /// <summary>Cross-fade to the idle at index <paramref name="i"/> (wraps around).</summary>
        public void Play(int i)
        {
            if (_anim == null) _anim = GetComponent<Animation>();
            if (clipNames == null || clipNames.Length == 0) return;

            clipIndex = ((i % clipNames.Length) + clipNames.Length) % clipNames.Length;
            var name = clipNames[clipIndex];
            if (!string.IsNullOrEmpty(name) && _anim.GetClip(name) != null)
            {
                _anim.wrapMode = WrapMode.Loop;
                _anim.CrossFade(name, 0.35f);
                _playing = clipIndex;
            }
        }
    }
}
