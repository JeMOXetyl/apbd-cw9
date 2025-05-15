using Microsoft.AspNetCore.Mvc;
using Tutorial9.Model;
using Tutorial9.Services;

namespace Tutorial9.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseManager _warehouseManager;

    public WarehouseController(IWarehouseManager warehouseManager)
    {
        _warehouseManager = warehouseManager;
    }
    
    [HttpPost]
    public async Task<IActionResult> AddProductToWarehouse(ProductWarehouseDTO warehouseProduct)
    {
        try
        {
            if (warehouseProduct == null)
            {
                return BadRequest("Invalid request");
            }

            if (warehouseProduct.Amount <= 0)
            {
                return BadRequest("Amount must be greater than 0");
            }

            var warehouseProductId = await _warehouseManager.AddProduct(warehouseProduct);

            return Ok(warehouseProductId);
        }
        catch (Exception e)
        {
            return NotFound(e.Message);
        }
    }
}