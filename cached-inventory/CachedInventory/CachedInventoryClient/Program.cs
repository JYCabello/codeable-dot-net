using CachedInventoryClient;

const string CachedInventory_HostAddress = "http://localhost:5250";

var stockClient = new StockClient(50, true, 1, 4);
await stockClient.Run();
