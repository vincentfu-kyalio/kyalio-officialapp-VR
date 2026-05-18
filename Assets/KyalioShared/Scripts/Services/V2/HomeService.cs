using System.Threading;
using Cysharp.Threading.Tasks;
using Kyalio.Models.V2;

namespace Kyalio.Services.V2
{
    /// <summary>
    /// Home feed: layout + normalized specialty / program catalog, scoped to granted projects.
    /// Body contains projectId references only — hydrate from the local Project cache.
    /// </summary>
    public class HomeService
    {
        private readonly ApiClient _client;

        public HomeService(ApiClient client)
        {
            _client = client;
        }

        public UniTask<HomeResponse> GetHomeAsync(CancellationToken ct = default)
            => _client.GetAsync<HomeResponse>("/api/me/home", ct);
    }
}
