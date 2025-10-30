
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public interface IAccountClient
{
    Task<Profile?> GetProfileAsync(CancellationToken ct = default);
}
