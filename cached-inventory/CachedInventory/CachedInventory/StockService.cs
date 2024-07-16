namespace CachedInventory;

public class StockService
{
  private static readonly Dictionary<int, int> StockCache =
    new()
    {
      { 1, 2 },
      { 2, 3 },
      { 3, 5 },
      { 4, 7 },
      { 5, 11 }
    };

  public async Task<int> GetStock(int productId)
  {
    // La base de datos es un sistema antiguo que utilizan en el almacén, este delay simula esa latencia.
    await Task.Delay(250);
    return StockCache.GetValueOrDefault(productId, 0);
  }
}
