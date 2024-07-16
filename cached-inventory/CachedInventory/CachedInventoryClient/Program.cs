using CachedInventoryClient;

var results = new List<RunResult>();
for (var parallelism = 1; parallelism < 4; parallelism++)
{
  foreach (var isParallel in new[] { true, false })
  {
    results.Add(await new StockClient(50, isParallel, parallelism, 4).Run());
  }
}

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
