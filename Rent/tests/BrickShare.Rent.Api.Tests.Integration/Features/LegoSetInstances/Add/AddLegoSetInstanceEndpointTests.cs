using System.Net;
using System.Net.Http.Json;

using BrickShare.Rent.Api.Features.LegoSetInstances;
using BrickShare.Rent.Api.Features.LegoSetInstances.Add;
using BrickShare.Rent.Api.Models;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace BrickShare.Rent.Api.Tests.Integration.Features.LegoSetInstances.Add;

public sealed class AddLegoSetInstanceEndpointTests(WebApplicationFactory<Program> factory)
  : IClassFixture<WebApplicationFactory<Program>> {
  private readonly HttpClient _client = factory.CreateClient();

  [Fact]
  public async Task AddLegoSetInstance_WithValidRequest_ReturnsCreated() {
    // Arrange
    var request = new AddLegoSetInstanceRequest(Guid.NewGuid(), 10.00m, 5, 95);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetInstancesEndpoints.RoutePrefix + AddLegoSetInstanceEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.Created);

    LegoInstanceDto? dto = await response.Content.ReadFromJsonAsync<LegoInstanceDto>();
    dto.ShouldNotBeNull();
    dto.Id.ShouldNotBe(Guid.Empty);
    dto.SetId.ShouldBe(request.SetId);
    dto.PricePerDay.ShouldBe(request.PricePerDay);
    dto.MinimalRentalDays.ShouldBe(request.MinimalRentalDays);
    dto.ConditionScore.ShouldBe(request.ConditionScore);
    dto.RentalStatus.ShouldBe(RentalStatus.Available);

    response.Headers.Location.ShouldNotBeNull();
    response.Headers.Location.ToString().ShouldBe($"{LegoSetInstancesEndpoints.RoutePrefix}/{dto.Id}");
  }

  [Fact]
  public async Task AddLegoSetInstance_WithEmptySetId_ReturnsBadRequest() {
    // Arrange
    var request = new AddLegoSetInstanceRequest(Guid.Empty, 10.00m, 5, 95);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetInstancesEndpoints.RoutePrefix + AddLegoSetInstanceEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task AddLegoSetInstance_WithPriceBelowMinimum_ReturnsBadRequest() {
    // Arrange
    var request = new AddLegoSetInstanceRequest(Guid.NewGuid(), 4.99m, 5, 95);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetInstancesEndpoints.RoutePrefix + AddLegoSetInstanceEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task AddLegoSetInstance_WithMinimalRentalDaysBelowMinimum_ReturnsBadRequest() {
    // Arrange
    var request = new AddLegoSetInstanceRequest(Guid.NewGuid(), 10.00m, 4, 95);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetInstancesEndpoints.RoutePrefix + AddLegoSetInstanceEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task AddLegoSetInstance_WithConditionScoreBelowMinimum_ReturnsBadRequest() {
    // Arrange
    var request = new AddLegoSetInstanceRequest(Guid.NewGuid(), 10.00m, 5, 79);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetInstancesEndpoints.RoutePrefix + AddLegoSetInstanceEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task AddLegoSetInstance_WithConditionScoreAboveMaximum_ReturnsBadRequest() {
    // Arrange
    var request = new AddLegoSetInstanceRequest(Guid.NewGuid(), 10.00m, 5, 101);

    // Act
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      LegoSetInstancesEndpoints.RoutePrefix + AddLegoSetInstanceEndpoint.Route, request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}

