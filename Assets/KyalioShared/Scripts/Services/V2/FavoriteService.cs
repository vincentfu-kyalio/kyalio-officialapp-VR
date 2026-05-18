using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// Server-side favorites. Response contains only { projectId, favoritedAt };
    /// hydrate display metadata from the local Project cache.
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
