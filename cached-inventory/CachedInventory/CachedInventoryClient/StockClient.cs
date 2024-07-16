namespace CachedInventoryClient;

using System.Text.Json;

public class StockClient(int totalToRetrieve, bool isParallel, int operationCount, int productId)
{
  private const string Url = "http://localhost:5250";
  private readonly HttpClient client = new();

  private string SettingsSummary =>
    $"\n- Total para retirar: {totalToRetrieve}.\n- En paralelo: {isParallel}.\n- Total de operaciones: {operationCount}.\n- ID del producto: {productId}";

  private void OutputResult(string message) =>
    Console.WriteLine($"Mensaje: {message}\nConfiguración: {SettingsSummary}");

  public async Task Run()
  {
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
    return response.IsSuccessStatusCode;
  }

  private async Task<int> GetStock()
  {
    var response = await client.GetAsync($"{Url}/stock/{productId}");
    var content = await response.Content.ReadAsStringAsync();
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
      if (!response.IsSuccessStatusCode)
      {
        OutputResult("Error al reponer el stock.");
      }

      return;
    }

    if (missingStock < 0)
    {
      Console.WriteLine($"Excess stock: {missingStock}. Removing...");
      var retrieveStockRequest = new { productId, amount = -missingStock };
      var retrieveStockRequestJson = JsonSerializer.Serialize(retrieveStockRequest);
      var retrieveStockRequestContent = new StringContent(retrieveStockRequestJson);
      retrieveStockRequestContent.Headers.ContentType = new("application/json");
      var response = await client.PostAsync($"{Url}/stock/retrieve", retrieveStockRequestContent);
      if (!response.IsSuccessStatusCode)
      {
        OutputResult("Error al reponer el stock inicial.");
      }

      return;
    }

    OutputResult("El stock ya está en el nivel deseado.");
  }
}
