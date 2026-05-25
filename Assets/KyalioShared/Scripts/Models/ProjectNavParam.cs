namespace Kyalio.Models
{
    /// <summary>
    /// Navigation parameter passed to ProjectInfoPage via UIManager.GoTo.
    /// Carries the entry source so the page can report project-page-session analytics.
    /// </summary>
    public class ProjectNavParam
    {
        public string ProjectId;

        /// <summary>One of Kyalio.Models.V2.ProjectPageSource constants.</summary>
        public string Source;

        /// <summary>Non-null only when Source == "search". The searchEventId from the search response.</summary>
        public string SearchEventId;
    }
}
