using Tutorial9.Model;

namespace Tutorial9.Services;

public interface IWarehouseManager
{
    Task<int> AddProduct(ProductWarehouseDTO product);
    Task<bool> DoesProductExist(int id);
    Task<bool> DoesWarehouseExist(int id);
    Task<OrderDTO?> GetOrder(int idProduct, int amount, DateTime createdAt);
    Task<bool> IsOrderFulfilled(int idOrder);
    Task UpdateOrder(int idOrder, DateTime fulfilledAt);
    Task<ProductDTO?> GetProduct(int idProduct);
    
    Task<int> InsetProductInWarehouse(int idWarehouse, int idProduct, int idOrder, int amount, decimal price, DateTime createdAt);
}