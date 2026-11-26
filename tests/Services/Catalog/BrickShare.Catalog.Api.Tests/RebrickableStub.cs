using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BrickShare.Catalog.Api.Tests;

public sealed class RebrickableStub : HttpMessageHandler
{
    private const string TitanicSet = """
        {
          "set_num": "10294-1",
          "name": "Titanic",
          "year": 2021,
          "theme_id": 1008,
          "num_parts": 9092,
          "set_img_url": "https://cdn.rebrickable.com/media/sets/10294-1.jpg",
          "set_url": "https://rebrickable.com/sets/10294-1/titanic/"
        }
        """;

    private const string TitanicMinifigs = """
        {
          "count": 3,
          "next": null,
          "previous": null,
          "results": []
        }
        """;

    private const string SubTheme = """
        {
          "id": 1008,
          "name": "Sub Theme",
          "parent_id": 158
        }
        """;

    private const string TopTheme = """
        {
          "id": 158,
          "name": "Top Theme",
          "parent_id": null
        }
        """;

    private readonly Dictionary<string, string> _responses = [];

    private bool _unavailable;

    public List<string> RequestedPaths { get; } = [];

    public static RebrickableStub WithTitanic() => new RebrickableStub()
        .Respond("/api/v3/lego/sets/10294-1/", TitanicSet)
        .Respond("/api/v3/lego/sets/10294-1/minifigs/", TitanicMinifigs)
        .Respond("/api/v3/lego/themes/1008/", SubTheme)
        .Respond("/api/v3/lego/themes/158/", TopTheme);

    public RebrickableStub Respond(string path, string json)
    {
        _responses[path] = json;
        return this;
    }

    public RebrickableStub Unavailable()
    {
        _unavailable = true;
        return this;
    }

    public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("https://rebrickable.com/") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_unavailable)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        var path = request.RequestUri!.AbsolutePath;
        RequestedPaths.Add(path);

        var response = _responses.TryGetValue(path, out var json)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        return Task.FromResult(response);
    }
}