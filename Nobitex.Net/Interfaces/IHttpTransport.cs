using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.Net;
public interface IHttpTransport
{
    Task<T?> SendAsync<T>(HttpRequestMessage req, CancellationToken ct = default) where T : class;
}
