using System.Collections.Generic;

namespace Kyalio.Models
{
    public class FilterOptions
    {
        public string Query;
        public HashSet<string> CategoryIds = new();  // specialty
        public HashSet<string> ProgramIds = new();   // program

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Query) &&
            CategoryIds.Count == 0 &&
            ProgramIds.Count == 0;
    }
}
