using System.Collections.Generic;
using Kyalio.Models;
using Kyalio.Repositories;

namespace Kyalio.Dev
{
    /// <summary>
    /// Populates ProjectCacheRepository with a consistent fake dataset at app startup.
    /// Called once by DevBootstrapper before navigating to any page, so every page
    /// has access to the same data regardless of which page is opened first.
    /// </summary>
    public static class FakeDataSeeder
    {
        public static void Seed()
        {
            var categories = new List<Category>
            {
                new Category { Id = "cat001", Name = "Cardiology" },
                new Category { Id = "cat002", Name = "Surgery" },
                new Category { Id = "cat003", Name = "General Practice" },
                new Category { Id = "cat004", Name = "Neurology" },
                new Category { Id = "cat005", Name = "Emergency Medicine" },
                new Category { Id = "cat006", Name = "Pediatrics" },
                new Category { Id = "cat007", Name = "Oncology" },
                new Category { Id = "cat008", Name = "Orthopedics" },
                new Category { Id = "cat009", Name = "Radiology" },
                new Category { Id = "cat010", Name = "Dermatology" },
                new Category { Id = "cat011", Name = "ENT" },
                new Category { Id = "cat012", Name = "Ophthalmology" },
                new Category { Id = "cat013", Name = "Psychiatry" },
                new Category { Id = "cat014", Name = "Anesthesiology" },
                new Category { Id = "cat015", Name = "Rehabilitation" },
            };

            // Programs are embedded in projects and derived by ProjectCacheRepository.Build()
            var programIds   = new[] { "prog001", "prog002", "prog003", "prog004", "prog005", "prog006", "prog007", "prog008", "prog009", "prog010", "prog011", "prog012", "prog013", "prog014", "prog015" };
            var programNames = new[] { "KyalioMed Basic", "Advanced Surgical Series", "Clinical Skills", "Emergency Response Track", "Residency Prep", "Diagnostic Mastery", "Procedure Lab", "Patient Safety Essentials", "Specialist Deep Dive", "XR Anatomy Atlas", "Clinical Communication", "Case Challenge Series", "Evidence in Practice", "Interdisciplinary Bootcamp", "Board Review Sprint" };

            var drNames = new[]
            {
                "Chen Wei", "Sarah Kim", "Marcus Tan", "Emily Lau", "James Roth",
                "Alicia Wong", "David Huang", "Priya Nair", "Noah Lin", "Hannah Su"
            };

            var topics = new[]
            {
                "Anatomy Essentials", "Clinical Assessment", "Diagnostic Reasoning", "Procedure Fundamentals",
                "Patient Communication", "Acute Care Basics", "Interpretation Workshop", "Simulation Challenge",
                "Emergency Protocol", "Advanced Concepts", "Case Review", "Hands-on Techniques"
            };

            var projects = new List<SubscribedProject>();
            for (int i = 1; i <= 75; i++)
            {
                var cat     = categories[(i - 1) % categories.Count];
                int progIdx = ((i - 1) * 2) % programIds.Length;
                projects.Add(new SubscribedProject
                {
                    Id                      = $"p{i:000}",
                    Name                    = $"{cat.Name} {topics[(i - 1) % topics.Length]} {(i - 1) / categories.Count + 1}",
                    DrName                  = drNames[(i - 1) % drNames.Length],
                    CategoryId              = cat.Id,
                    CategoryName            = cat.Name,
                    ProgramId               = programIds[progIdx],
                    ProgramName             = programNames[progIdx],
                    PlaylistDurationSeconds = 900 + (i - 1) % 8 * 300,
                    PlaylistCount           = 1 + (i - 1) % 6,
                });
            }

            ProjectCacheRepository.Instance.Build(new List<SubscriptionItem>
            {
                new SubscriptionItem { Projects = projects, Categories = categories }
            });
        }
    }
}
