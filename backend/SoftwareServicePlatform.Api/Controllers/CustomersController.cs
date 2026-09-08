using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[Controller]")]
    public class CustomersController:ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public CustomersController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        } 

        private static int _nextId = 1;

        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _dbContext.Customers.ToListAsync();
            return Ok(customers);
        }

        [HttpPost] //新增
        public async Task<IActionResult> CreateCustomer(Customer customer)
        {
           
           _dbContext.Customers.Add(customer);
         await   _dbContext.SaveChangesAsync();
            return Ok(customer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id,Customer customer)
        {
            var exisintCustomer = await _dbContext.Customers.FindAsync(id);
            if (exisintCustomer== null)
            {
                return NotFound();

            }
            exisintCustomer.Name = customer.Name;
            await _dbContext.SaveChangesAsync();
            return Ok(exisintCustomer);

        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _dbContext.Customers.FindAsync(id);
            if (customer== null)
            {
                return NotFound();
            }
            _dbContext.Customers.Remove(customer);
            await _dbContext.SaveChangesAsync();
            return NoContent();
        }
    }

   
}
