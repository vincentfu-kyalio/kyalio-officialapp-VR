using System.Collections.Generic;

namespace Kyalio.Models
{
    /// <summary>
    /// Search/filter selection for the V2 search flow. CategoryIds were renamed to
    /// SpecialtyIds to match the new contract's specialty / program filter vocabulary.
    /// </summary>
    public class FilterOptions
    {
        public string Query;
        public HashSet<string> SpecialtyIds = new();  // specialty
        public HashSet<string> ProgramIds   = new();   // program

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Query) &&
            SpecialtyIds.Count == 0 &&
            ProgramIds.Count == 0;
    }
}
