using CachedInventoryClient;

var productId = 1;
var resultTasks = new List<Task<RunResult>>();
for (var operationCount = 5; operationCount < 9; operationCount++)
{
  foreach (var isParallel in new[] { true, false })
  {
    resultTasks.Add(new StockTester(50, isParallel, operationCount, productId).Run());
    Interlocked.Increment(ref productId);
  }
}

var results = await Task.WhenAll(resultTasks);
Console.WriteLine("Todas las operaciones se ejecutaron, mostrando los resultados:\n\n");

if (results.All(r => r.WasSuccessful))
{
  Console.WriteLine("¡Todas las operaciones se completaron con éxito!");
  return;
}

foreach (var result in results)
{
  Console.WriteLine(result);
}
