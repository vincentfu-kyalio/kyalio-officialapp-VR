using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models;

namespace Kyalio.Services
{
    /// <summary>
    /// Server-side favorites.
    /// GET /api/favorites               — list all favorites
    /// POST /api/favorites/{projectId}  — add (idempotent, 204)
    /// DELETE /api/favorites/{projectId}— remove (idempotent, 204)
    /// </summary>
    public class FavoriteService
    {
        private readonly ApiClient _client;

        public FavoriteService(ApiClient client)
        {
            _client = client;
        }

        public UniTask<FavoritesResponse> GetFavoritesAsync(CancellationToken ct = default)
            => _client.GetAsync<FavoritesResponse>("/api/favorites", ct);

        public UniTask AddFavoriteAsync(string projectId, CancellationToken ct = default)
            => _client.PostAsync($"/api/favorites/{projectId}", ct);

        public UniTask RemoveFavoriteAsync(string projectId, CancellationToken ct = default)
            => _client.DeleteAsync($"/api/favorites/{projectId}", ct);
    }
}
