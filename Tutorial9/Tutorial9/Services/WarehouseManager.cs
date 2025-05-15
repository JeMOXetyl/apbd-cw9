using Microsoft.Data.SqlClient;
using Tutorial9.Model;

namespace Tutorial9.Services;

public class WarehouseManager : IWarehouseManager
{
    private readonly IConfiguration _configuration;

    public WarehouseManager(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task<int> AddProduct(ProductWarehouseDTO warehouseProduct)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            if (!await DoesProductExist(warehouseProduct.IdProduct)) throw new Exception("Product does not exist");

            if (!await DoesWarehouseExist(warehouseProduct.IdWarehouse))
                throw new Exception("Warehouse does not exist");

            var order = await GetOrder(warehouseProduct.IdOrder, warehouseProduct.Amount, warehouseProduct.CreatedAt);

            if (order == null) throw new Exception($"No matching order found for product {warehouseProduct.IdProduct} with amount {warehouseProduct.Amount}");

            if (await IsOrderFulfilled(order.IdOrder)) throw new Exception("Order is already fulfilled");

            await UpdateOrder(warehouseProduct.IdOrder, DateTime.Now);

            var product = await GetProduct(warehouseProduct.IdProduct);
            decimal price = product.Price * warehouseProduct.Amount;

            var id = await InsetProductInWarehouse(
                warehouseProduct.IdWarehouse,
                warehouseProduct.IdProduct,
                warehouseProduct.IdOrder,
                warehouseProduct.Amount,
                price,
                warehouseProduct.CreatedAt);

            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DoesProductExist(int id)
    {
        var query = "SELECT * FROM Product WHERE IdProduct = @id";
        
        await using SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand comm = new SqlCommand();
        comm.Connection = conn;
        comm.CommandText = query;
        comm.Parameters.AddWithValue("@id", id);
        
        await conn.OpenAsync();
        var result = await comm.ExecuteReaderAsync();
        return result != null;
    }

    public async Task<bool> DoesWarehouseExist(int id)
    {
        var query = "SELECT * FROM Warehouse WHERE IdWarehouse = @id";
        
        await using SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand comm = new SqlCommand();
        comm.Connection = conn;
        comm.CommandText = query;
        comm.Parameters.AddWithValue("@id", id);
        
        await conn.OpenAsync();
        var result = await comm.ExecuteReaderAsync();
        return result != null;
    }

    public async Task<OrderDTO?> GetOrder(int idProduct, int amount, DateTime createdAt)
    {
        var query = "SELECT * FROM [Order] WHERE IdProduct = @idProduct AND Amount = @amount AND CreatedAt <= @createdAt AND (FulfilledAt IS NULL OR FulfilledAt > @createdAt)";
        await using SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand comm = new SqlCommand();
        comm.Connection = conn;
        comm.CommandText = query;
        comm.Parameters.AddWithValue("@idProduct", idProduct);
        comm.Parameters.AddWithValue("@amount", amount);
        comm.Parameters.AddWithValue("@createdAt", createdAt);
        
        await conn.OpenAsync();
        var result = await comm.ExecuteReaderAsync();
        
        if (await result.ReadAsync())
        {
            try
            {
                var idOrderOrdinal = result.GetOrdinal("IdOrder");
                var idProductOrdinal = result.GetOrdinal("IdProduct");
                var amountOrdinal = result.GetOrdinal("Amount");
                var createdAtOrdinal = result.GetOrdinal("CreatedAt");
                var fulfilledAtOrdinal = result.GetOrdinal("FulfilledAt");

                return new OrderDTO()
                {
                    IdOrder = result.GetInt32(idOrderOrdinal),
                    IdProduct = result.GetInt32(idProductOrdinal),
                    Amount = result.GetInt32(amountOrdinal),
                    CreatedAt = result.GetDateTime(createdAtOrdinal),
                    FulfilledAt = result.IsDBNull(fulfilledAtOrdinal) ? 
                        null : result.GetDateTime(fulfilledAtOrdinal)
                };
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error reading order data: {e.Message}");
                throw;
            }
        }
        return null;
    }

    public async Task<bool> IsOrderFulfilled(int idOrder)
    {
        var query = "SELECT * FROM Product_Warehouse WHERE IdOrder = @idOrder";
        
        await using SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand comm = new SqlCommand();
        comm.Connection = conn;
        comm.CommandText = query;
        comm.Parameters.AddWithValue("@idOrder", idOrder);
        
        await conn.OpenAsync();
        var result = await comm.ExecuteScalarAsync();
        return result != null;
    }

    public async Task UpdateOrder(int idOrder, DateTime fulfilledAt)
    {
        var query = "UPDATE [Order] SET FulfilledAt = @FulfilledAt WHERE IdOrder = @idOrder";
        
        await using SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand comm = new SqlCommand();
        comm.Connection = conn;
        comm.CommandText = query;
        comm.Parameters.AddWithValue("@idOrder", idOrder);
        comm.Parameters.AddWithValue("@FulfilledAt", fulfilledAt);
        await conn.OpenAsync();
        await comm.ExecuteNonQueryAsync();
    }

    public async Task<ProductDTO?> GetProduct(int idProduct)
    {
        var query = "SELECT * FROM Product WHERE Idproduct = @idProduct";

        await using SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand comm = new SqlCommand();
        comm.Connection = conn;
        comm.CommandText = query;
        comm.Parameters.AddWithValue("@idProduct", idProduct);
        await conn.OpenAsync();
        
        var result = await comm.ExecuteReaderAsync();
        var idProductQ = result.GetOrdinal("IdProduct");
        var nameQ = result.GetOrdinal("Name");
        var descriptionQ = result.GetOrdinal("Description");
        var priceQ = result.GetOrdinal("Price");
        ProductDTO product = null;

        while (await result.ReadAsync())
        {
            product = new ProductDTO()
            {
                IdProduct = result.GetInt32(idProductQ),
                Name = result.GetString(nameQ),
                Description = result.GetString(descriptionQ),
                Price = result.GetDecimal(priceQ)
            };
        }
        
        if (product == null) throw new Exception("No product found");
        return product;
    }

    public async Task<int> InsetProductInWarehouse(int idWarehouse, int idProduct, int idOrder, int amount, decimal price,
        DateTime createdAt)
    {
        var query = @"
                INSERT INTO Product_Warehouse
                (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                OUTPUT INSERTED.IdProductWarehouse
                VALUES 
                (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt)";
        await using SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand comm = new SqlCommand();
        comm.Connection = conn;
        comm.CommandText = query;
        comm.Parameters.AddWithValue("@IdWarehouse", idWarehouse);
        comm.Parameters.AddWithValue("@IdProduct", idProduct);
        comm.Parameters.AddWithValue("@IdOrder", idOrder);
        comm.Parameters.AddWithValue("@Amount", amount);
        comm.Parameters.AddWithValue("@Price", price);
        comm.Parameters.AddWithValue("@CreatedAt", createdAt);
        await conn.OpenAsync();
        var result = await comm.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

}