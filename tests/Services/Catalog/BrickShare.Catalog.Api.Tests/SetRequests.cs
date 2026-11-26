using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Xunit;

namespace BrickShare.Catalog.Api.Tests;

public static class SetRequests
{
    public static object ValidBody(string legoSetNumber = "10294", string name = "Millennium Falcon") => new
    {
        legoSetNumber,
        name,
        minRentalDays = 3,
        dailyPrice = 1500,
        deposit = 15000,
        replacementValue = 30000,
        lateFeePerDay = 500
    };

    public static async Task<HttpResponseMessage> SendWithTokenAsync(
        this HttpClient client, HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}