namespace CachedInventoryClient;

using System.Diagnostics;
using System.Text.Json;

public class StockClient(int totalToRetrieve, bool isParallel, int operationCount, int productId)
{
  private const string Url = "http://localhost:5250";
  private readonly HttpClient client = new();
  private int requestCount;
  private Stopwatch stopwatch = new();

  private string SettingsSummary =>
    $"""

     - Total para retirar: {totalToRetrieve}.
     - En paralelo: {isParallel}.
     - Total de operaciones: {operationCount}.
     - ID del producto: {productId} 
     - Tiempo transcurrido: {stopwatch.ElapsedMilliseconds} ms.
     - Tiempo medio por solicitud: {TimeElapsedPerRequest} ms.
     """;

  private string TimeElapsedPerRequest => requestCount == 0
    ? "[no disponible]"
    : (stopwatch.ElapsedMilliseconds / requestCount).ToString();

  private void OutputResult(string message) =>
    Console.WriteLine($"Mensaje: {message}\nConfiguración: {SettingsSummary}");

  public async Task Run()
  {
    stopwatch = Stopwatch.StartNew();
    requestCount = 0;
    OutputResult("Inicio de la operación.");
    await Restock();
    var amountPerOperation = totalToRetrieve / operationCount;
    var remaining = totalToRetrieve % operationCount;
    var amounts = Enumerable.Range(0, operationCount + 1)
      .Select(_ => amountPerOperation)
      .Concat(remaining > 0 ? [remaining] : []);
    var tasks = new List<Task<bool>>();
    foreach (var amount in amounts)
    {
      var task = Retrieve(amount);
      if (!isParallel)
      {
        await task;
      }

      tasks.Add(task);
    }

    var results = await Task.WhenAll(tasks);
    if (!results.All(r => r))
    {
      OutputResult("Error al retirar el stock.");
      return;
    }

    var finalStock = await GetStock();
    if (finalStock != 0)
    {
      OutputResult($"El stock final es {finalStock} en lugar de 0.");
      return;
    }

    OutputResult("Completado con éxito.");
  }

  private async Task<bool> Retrieve(int amount)
  {
    var retrieveRequest = new { productId, amount };
    var retrieveRequestJson = JsonSerializer.Serialize(retrieveRequest);
    var retrieveRequestContent = new StringContent(retrieveRequestJson);
    retrieveRequestContent.Headers.ContentType = new("application/json");
    var response = await client.PostAsync($"{Url}/stock/retrieve", retrieveRequestContent);
    requestCount++;
    return response.IsSuccessStatusCode;
  }

  private async Task<int> GetStock()
  {
    var response = await client.GetAsync($"{Url}/stock/{productId}");
    var content = await response.Content.ReadAsStringAsync();
    requestCount++;
    return int.Parse(content);
  }

  private async Task Restock()
  {
    Console.WriteLine("Setting up initial stock");
    var currentStock = await GetStock();
    var missingStock = totalToRetrieve - currentStock;
    if (missingStock > 0)
    {
      Console.WriteLine($"Missing stock: {missingStock}. Restocking...");
      var restockRequest = new { productId, amount = missingStock };
      var restockRequestJson = JsonSerializer.Serialize(restockRequest);
      var restockRequestContent = new StringContent(restockRequestJson);
      restockRequestContent.Headers.ContentType = new("application/json");
      var response = await client.PostAsync($"{Url}/stock/restock", restockRequestContent);
      requestCount++;
      if (!response.IsSuccessStatusCode)
      {
        OutputResult("Error al reponer el stock.");
      }

      return;
    }

    if (missingStock < 0)
    {
      Console.WriteLine($"Excess stock: {missingStock}. Removing...");
      if (!await Retrieve(-missingStock))
      {
        OutputResult("Error al reponer el stock inicial.");
      }

      requestCount++;

      return;
    }

    OutputResult("El stock ya está en el nivel deseado.");
  }
}
