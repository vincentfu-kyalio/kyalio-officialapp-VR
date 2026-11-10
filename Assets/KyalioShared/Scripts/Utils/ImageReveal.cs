using UnityEngine;
using UnityEngine.UI;

namespace Kyalio.Utils
{
    /// <summary>
    /// Puts a loaded thumbnail or logo on an Image, fading it in when it had to be
    /// downloaded instead of popping in.
    ///
    /// Pass the frame the load started on: a sprite that came back in the same frame was
    /// already cached and is shown at once, so cards rebinding while a list scrolls stay
    /// crisp. The fade runs on the CanvasRenderer color (the same channel a Button color
    /// transition uses), so it must not be used on an Image that is a Button's target graphic.
    /// </summary>
    public static class ImageReveal
    {
        public const float Duration = 0.35f;

        // Thumbnails start from their dark placeholder tone, so the swap never flashes.
        private static readonly Color PlaceholderTone = new Color32(43, 43, 43, 255);

        /// <summary>Thumbnail with a dark placeholder: brightens from the placeholder tone.</summary>
        public static void ShowThumbnail(Image image, Sprite sprite, int loadStartFrame)
        {
            image.sprite = sprite;
            image.color  = Color.white;

            if (Time.frameCount == loadStartFrame)
            {
                Reset(image);
                return;
            }
            image.canvasRenderer.SetColor(PlaceholderTone);
            image.CrossFadeColor(Color.white, Duration, true, false);
        }

        /// <summary>Image with no placeholder (e.g. a program logo): fades in from transparent.</summary>
        public static void ShowFadeIn(Image image, Sprite sprite, int loadStartFrame)
        {
            image.sprite = sprite;

            if (Time.frameCount == loadStartFrame)
            {
                Reset(image);
                return;
            }
            image.canvasRenderer.SetAlpha(0f);
            image.CrossFadeAlpha(1f, Duration, true);
        }

        /// <summary>
        /// Hides an image with no placeholder (a logo) until <see cref="ShowFadeIn"/> brings
        /// it up, so neither the previous item's logo nor an empty white quad shows meanwhile.
        /// </summary>
        public static void HideUntilLoaded(Image image)
        {
            if (image == null) return;
            Reset(image);
            image.canvasRenderer.SetAlpha(0f);
        }

        /// <summary>
        /// Ends any running fade and restores full color. Call when a recycled card is
        /// rebound, so a half-finished fade from its previous item does not carry over.
        /// </summary>
        public static void Reset(Image image)
        {
            if (image == null) return;
            // Zero duration stops the running tween and applies the color at once.
            image.CrossFadeColor(Color.white, 0f, true, true);
        }
    }
}
