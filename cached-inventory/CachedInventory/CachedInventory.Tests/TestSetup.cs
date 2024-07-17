namespace CachedInventory.Tests;

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;

public record TestSetup(string Url) : IAsyncDisposable
{
  private static readonly SemaphoreSlim PortSemaphore = new(1);
  private static readonly Random Random = new();
  private static readonly HttpClient client = new();
  private readonly List<long> requestDurations = [];

  public long AverageRequestDuration => Convert.ToInt64(requestDurations.Average());

  public async ValueTask DisposeAsync()
  {
    await WebAppTracker.TryDispose();
    GC.SuppressFinalize(this);
  }

  public async Task VerifyStockFromFile(int productId, int expectedStock)
  {
    // Tiempo de latencia permitido para actualizar el fichero.
    await Task.Delay(3_000);
    var response = await client.GetAsync($"{Url}stock/verify-from-file/{productId}");
    var content = await response.Content.ReadAsStringAsync();
    var fileStock = int.Parse(content);
    Assert.True(
      fileStock == expectedStock,
      $"El fichero no se actualizó correctamente. Stock en el fichero: {fileStock}. Stock esperado: {expectedStock}.");
  }

  public async Task<int> GetStock(int productId)
  {
    var stopwatch = Stopwatch.StartNew();
    var response = await client.GetAsync($"{Url}stock/{productId}");
    var content = await response.Content.ReadAsStringAsync();
    stopwatch.Stop();
    requestDurations.Add(stopwatch.ElapsedMilliseconds);
    Assert.True(response.IsSuccessStatusCode, $"Error al obtener el stock del producto {productId}.");
    return int.Parse(content);
  }

  public async Task Restock(int productId, int totalToRetrieve)
  {
    var currentStock = await GetStock(productId);
    var missingStock = totalToRetrieve - currentStock;
    switch (missingStock)
    {
      case > 0:
      {
        var restockRequest = new RestockRequest(productId, missingStock);
        var restockRequestJson = JsonSerializer.Serialize(restockRequest);
        var restockRequestContent = new StringContent(restockRequestJson);
        restockRequestContent.Headers.ContentType = new("application/json");
        var stopwatch = Stopwatch.StartNew();
        var response = await client.PostAsync($"{Url}stock/restock", restockRequestContent);
        stopwatch.Stop();
        requestDurations.Add(stopwatch.ElapsedMilliseconds);
        Assert.True(response.IsSuccessStatusCode, $"Error al reponer el stock del producto {productId}.");
        return;
      }
      case < 0:
        await Retrieve(productId, -missingStock);
        break;
    }
  }

  public async Task Retrieve(int productId, int amount)
  {
    var retrieveRequest = new { productId, amount };
    var retrieveRequestJson = JsonSerializer.Serialize(retrieveRequest);
    var retrieveRequestContent = new StringContent(retrieveRequestJson);
    retrieveRequestContent.Headers.ContentType = new("application/json");
    var stopwatch = Stopwatch.StartNew();
    var response = await client.PostAsync($"{Url}stock/retrieve", retrieveRequestContent);
    stopwatch.Stop();
    requestDurations.Add(stopwatch.ElapsedMilliseconds);
    Assert.True(response.IsSuccessStatusCode, $"Error al retirar el stock del producto {productId}.");
  }

  private static int GetRandomPort() => Random.Next(30000) + 10000;

  private static async Task<int> GetFreePort()
  {
    await PortSemaphore.WaitAsync();

    try
    {
      return Go(GetRandomPort());

      int Go(int pn) => GetConnectionInfo().Any(ci => ci.LocalEndPoint.Port == pn) ? Go(GetRandomPort()) : pn;
    }
    finally
    {
      PortSemaphore.Release();
    }
  }

  // ReSharper disable once ReturnTypeCanBeEnumerable.Local
  private static TcpConnectionInformation[] GetConnectionInfo() =>
    IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();


  public static async Task<TestSetup> Initialize()
  {
    var baseUrl = await WebAppTracker.Get();
    return new(baseUrl);
  }

  private static class WebAppTracker
  {
    private static int handleCount;
    private static (WebApplication app, string url)? setup;
    private static readonly SemaphoreSlim TrackerSemaphore = new(1);

    internal static async Task<string> Get()
    {
      await TrackerSemaphore.WaitAsync();
      try
      {
        Interlocked.Increment(ref handleCount);
        if (setup.HasValue)
        {
          return setup.Value.url;
        }

        var sitePort = await GetFreePort();
        var baseUrl = $"http://localhost:{sitePort}/";
        var app = CachedInventoryApiBuilder.Build(["--urls", baseUrl]);
        await app.StartAsync();
        setup = (app, baseUrl);

        return setup.Value.url;
      }
      finally { TrackerSemaphore.Release(); }
    }

    internal static async Task TryDispose()
    {
      await TrackerSemaphore.WaitAsync();
      try
      {
        Interlocked.Decrement(ref handleCount);
        if (handleCount == 0 && setup.HasValue)
        {
          await setup.Value.app.DisposeAsync();
          setup = null;
        }
      }
      finally { TrackerSemaphore.Release(); }
    }
  }
}
