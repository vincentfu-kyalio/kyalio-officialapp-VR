namespace Kyalio.Core
{
    /// <summary>
    /// Interface implemented by all Page scripts.
    /// </summary>
    public interface IPageHandler
    {
        void OnEnter(object param);
        void OnExit();
    }

    /// <summary>
    /// Optional for a page entered through a fade: the screen stays black until
    /// <see cref="IsReadyToReveal"/> turns true (or a timeout passes), so the user never
    /// sees the page before its content is up — e.g. the video page before its first frame.
    /// </summary>
    public interface IRevealGate
    {
        bool IsReadyToReveal { get; }
    }
}
