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
}
