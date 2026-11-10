using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Components
{
    /// <summary>
    /// Shows a Slider's progress by clipping a full-length fill instead of shrinking it.
    ///
    /// A Slider resizes its fill rect to value × width. Our progress bars use a 9-sliced
    /// pill sprite whose end caps are ~36 units wide together, so below roughly 9% the
    /// rect is narrower than the caps: Unity squeezes them and the fill turns into a thin
    /// line or a blob poking past the bar. Here the fill keeps its full size and a
    /// RectMask2D cuts it at the value, so the left cap always matches the background and
    /// any value, however small, is a clean slice.
    ///
    /// Install from code (<see cref="Install"/>) on display-only, left-to-right sliders;
    /// the prefabs need no change. Do not use on a slider with a handle.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class SliderFillClip : MonoBehaviour
    {
        private Slider _slider;
        private RectTransform _clip;
        private RectMask2D _mask;

        /// <summary>Adds the clip to a slider once. Null and repeated calls are ignored.</summary>
        public static void Install(Slider slider)
        {
            if (slider != null && !slider.TryGetComponent<SliderFillClip>(out _))
                slider.gameObject.AddComponent<SliderFillClip>();
        }

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            var fill = _slider.fillRect;
            if (fill == null || _slider.direction != Slider.Direction.LeftToRight)
            {
                enabled = false;
                return;
            }

            // The clip covers the area a full fill would: the fill's parent, inset by the
            // fill's offsets (the Slider sets its anchors to 0..value and keeps offsets).
            var go = new GameObject("Fill Clip", typeof(RectTransform), typeof(RectMask2D));
            go.layer = fill.gameObject.layer;
            _clip = (RectTransform)go.transform;
            _clip.SetParent(fill.parent, false);
            _clip.SetSiblingIndex(fill.GetSiblingIndex());
            _clip.anchorMin = Vector2.zero;
            _clip.anchorMax = Vector2.one;
            _clip.pivot     = fill.pivot;
            _clip.offsetMin = fill.offsetMin;
            _clip.offsetMax = fill.offsetMax;
            _mask = go.GetComponent<RectMask2D>();

            // Stop the Slider resizing the fill, then stretch it over the clip.
            _slider.fillRect = null;
            fill.SetParent(_clip, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            _slider.onValueChanged.AddListener(_ => Apply());
            Apply();
        }

        // Width changes (layout, resolution) move the cut point.
        private void OnRectTransformDimensionsChange()
        {
            if (_mask != null) Apply();
        }

        private void Apply()
        {
            float range = _slider.maxValue - _slider.minValue;
            float t = range > 0f ? Mathf.Clamp01((_slider.value - _slider.minValue) / range) : 0f;
            _mask.padding = new Vector4(0f, 0f, (1f - t) * _clip.rect.width, 0f);
        }
    }
}
