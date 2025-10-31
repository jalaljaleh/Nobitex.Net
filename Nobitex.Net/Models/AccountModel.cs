using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Nobitex.Net;



    /// <summary>
    /// Response for GET /margin/delegation-limit
    /// </summary>
    public record DelegationLimitResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("limit")] string? Limit,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("message")] string? Message
    );

