// ReSharper disable ClassNeverInstantiated.Global

namespace CachedInventory.Tests;

public class SingleRetrieval
{
  [Fact(DisplayName = "retrieve a single product")]
  public static async Task Test() => await TestApiPerformance.Test(1, [3], false, 2_000);
}

public class FourRetrievalsInParallel
{
  [Fact(DisplayName = "retrieve four products in parallel")]
  public static async Task Test() => await TestApiPerformance.Test(2, [1, 2, 3, 4], true, 1_000);
}

public class FourRetrievalsSequentially
{
  [Fact(DisplayName = "retrieve four products sequentially")]
  public static async Task Test() => await TestApiPerformance.Test(3, [1, 2, 3, 4], false, 1_000);
}

public class SevenRetrievalsInParallel
{
  [Fact(DisplayName = "retrieve seven products in parallel")]
  public static async Task Test() => await TestApiPerformance.Test(4, [1, 2, 3, 4, 5, 6, 7], true, 500);
}

public class SevenRetrievalsSequentially
{
  [Fact(DisplayName = "retrieve seven products sequentially")]
  public static async Task Test() => await TestApiPerformance.Test(5, [1, 2, 3, 4, 5, 6, 7], false, 500);
}

internal static class TestApiPerformance
{
  internal static async Task Test(int productId, int[] retrievals, bool isParallel, long expectedPerformance)
  {
    await using var setup = await TestSetup.Initialize();
    await setup.Restock(productId, retrievals.Sum());
    await setup.VerifyStockFromFile(productId, retrievals.Sum());
    var tasks = new List<Task>();
    foreach (var retrieval in retrievals)
    {
      var task = setup.Retrieve(productId, retrieval);
      if (isParallel)
      {
        await task;
      }

      tasks.Add(task);
    }

    await Task.WhenAll(tasks);
    var finalStock = await setup.GetStock(productId);
    Assert.True(finalStock == 0, $"El stock final no es 0, sino {finalStock}.");
    await setup.VerifyStockFromFile(productId, 0);
    Assert.True(
      setup.AverageRequestDuration < expectedPerformance,
      $"Duración promedio: {setup.AverageRequestDuration}ms, se esperaba un máximo de {expectedPerformance}ms.");
  }
}
