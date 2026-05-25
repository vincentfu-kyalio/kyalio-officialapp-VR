using System.Collections.Generic;
using Kyalio.Models.V2;
using Kyalio.Repositories.V2;
using Kyalio.State.V2;

namespace Kyalio.Dev
{
    /// <summary>
    /// Populates the V2 cache + AppState.LastHome with a consistent fake dataset at app
    /// startup, so dev-mode pages render off exactly the same structures real mode does
    /// (no network). Called once by the bootstrapper before navigating to any page.
    /// </summary>
    public static class FakeDataSeeder
    {
        public static List<FavoriteItem> FakeFavorites { get; private set; }

        public static void Seed()
        {
            var specialties = new List<IdNameRef>
            {
                new IdNameRef { Id = "spc_cardio",  Name = "Cardiology" },
                new IdNameRef { Id = "spc_surgery", Name = "Surgery" },
                new IdNameRef { Id = "spc_neuro",   Name = "Neurology" },
                new IdNameRef { Id = "spc_gp",      Name = "General Practice" },
                new IdNameRef { Id = "spc_ortho",   Name = "Orthopedics" },
                new IdNameRef { Id = "spc_peds",    Name = "Pediatrics" },
            };

            var programs = new List<ProgramSummary>
            {
                new ProgramSummary { Id = "pkg_basic",    Name = "KyalioMed Basic" },
                new ProgramSummary { Id = "pkg_advanced", Name = "Advanced Surgical Series" },
                new ProgramSummary { Id = "pkg_clinical", Name = "Clinical Skills" },
                new ProgramSummary { Id = "pkg_xr",       Name = "XR Anatomy Atlas" },
            };

            var surgeons = new[]
            {
                "Chen Wei", "Sarah Kim", "Marcus Tan", "Emily Lau", "James Roth",
                "Alicia Wong", "David Huang", "Priya Nair", "Noah Lin", "Hannah Su"
            };

            var topics = new[]
            {
                "Anatomy Essentials", "Clinical Assessment", "Diagnostic Reasoning",
                "Procedure Fundamentals", "Patient Communication", "Acute Care Basics",
                "Interpretation Workshop", "Simulation Challenge", "Emergency Protocol",
                "Advanced Concepts", "Case Review", "Hands-on Techniques"
            };

            var modes = new[] { PlaybackMode.Flat, PlaybackMode.Vr180Sbs, PlaybackMode.Vr360Mono };

            var projects = new List<Project>();
            for (int i = 1; i <= 30; i++)
            {
                var spc     = specialties[(i - 1) % specialties.Count];
                var prog    = programs[(i - 1) % programs.Count];
                int epCount = 1 + (i - 1) % 5;

                var playlist = new List<PlaylistItem>();
                long totalBytes = 0;
                int  totalDurationSec = 0;
                for (int k = 1; k <= epCount; k++)
                {
                    int durMs = (300 + (k * 120)) * 1000;
                    long size = 1_500_000_000L + k * 200_000_000L;
                    totalBytes += size;
                    totalDurationSec += durMs / 1000;
                    playlist.Add(new PlaylistItem
                    {
                        Title        = $"Episode {k}",
                        Description  = $"{spc.Name} module {k}",
                        VideoId      = $"vid_{i:00}_{k:00}",
                        DurationMs   = durMs,
                        SizeBytes    = size,
                        PlaybackMode = modes[(i + k) % modes.Length],
                    });
                }

                projects.Add(new Project
                {
                    ProjectId               = $"prj_{i:00}",
                    ProjectVersion          = 1,
                    ThumbnailVersion        = 1,
                    ProjectName             = $"{spc.Name} {topics[(i - 1) % topics.Length]}",
                    Description             = $"A fake {spc.Name.ToLower()} project for UI testing.",
                    Surgeons                = new List<string> { surgeons[(i - 1) % surgeons.Length] },
                    Institution             = "Kyalio Medical",
                    SpecialtyId             = spc.Id,
                    RoleId                  = "rol_surgeon",
                    ProgramIds              = new List<string> { prog.Id },
                    PlaylistCount           = epCount,
                    PlaylistDurationSeconds = totalDurationSec,
                    TotalSizeBytes          = totalBytes,
                    Playlist                = playlist,
                });
            }

            // 1. Projects into the cache.
            ProjectCacheRepository.Instance.ApplyBatch(new ProjectsBatchResponse { Items = projects });

            // 2. Home layout (ids only) + filters → also sets AppState.LastHome.
            var latest      = new List<string>();
            var recommended = new List<string>();
            for (int i = 0; i < projects.Count; i++)
            {
                if (i % 3 == 0) latest.Add(projects[i].ProjectId);
                else if (i % 3 == 1) recommended.Add(projects[i].ProjectId);
            }

            var roleItems = new List<HomeRoleItem>();
            foreach (var spc in specialties)
            {
                var ids = projects
                    .FindAll(p => p.SpecialtyId == spc.Id)
                    .ConvertAll(p => p.ProjectId);
                roleItems.Add(new HomeRoleItem { Name = spc.Name, ProjectIds = ids });
            }

            AppState.Instance.SetHome(new HomeResponse
            {
                Latest      = latest,
                Recommended = recommended,
                Roles       = new HomeRoles { DisplayMode = HomeRolesDisplayMode.Projects, Items = roleItems },
                Filters     = new HomeFilters { Specialties = specialties, Programs = programs },
            });

            // 3. Some fake watch progress.
            var progress = new List<ProgressItem>();
            foreach (var p in new[] { projects[0], projects[2] })
            {
                if (p.Playlist.Count == 0) continue;
                var v = p.Playlist[0];
                progress.Add(new ProgressItem
                {
                    ProjectId         = p.ProjectId,
                    VideoId           = v.VideoId,
                    ProgressMs        = v.DurationMs / 2,
                    ProgressUpdatedAt = "2026-01-01T00:00:00Z",
                });
            }
            ProjectCacheRepository.Instance.ApplyProgress(
                new ProgressResponse { Items = progress }, merge: false);

            // 4. Favorites (ids only, hydrated for display by the page).
            FakeFavorites = new List<FavoriteItem>
            {
                new FavoriteItem { ProjectId = projects[0].ProjectId, FavoritedAt = "2026-01-01T00:00:00Z" },
                new FavoriteItem { ProjectId = projects[1].ProjectId, FavoritedAt = "2026-01-02T00:00:00Z" },
                new FavoriteItem { ProjectId = projects[2].ProjectId, FavoritedAt = "2026-01-03T00:00:00Z" },
            };
        }
    }
}
