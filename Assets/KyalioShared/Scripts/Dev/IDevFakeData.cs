namespace Kyalio.Dev
{
    /// <summary>
    /// Implemented by pages that support fake data injection for UI testing.
    /// Called automatically when DevFlags.UseFakeData is true.
    /// </summary>
    public interface IDevFakeData
    {
        void LoadFakeData();
    }
}
