using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using Microsoft.AspNetCore.Authorization;
namespace SoftwareServicePlatform.Api.Controllers
{
    [Authorize(
      Roles = "Admin,Support,Sales"
  )]
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
            customer.CreatedAt = DateTime.UtcNow;
            customer.UpdatedAt = DateTime.UtcNow;

            _dbContext.Customers.Add(customer);
         await   _dbContext.SaveChangesAsync();
            return Ok(customer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(
     int id,
     Customer customer)
        {
            var existingCustomer =
                await _dbContext.Customers.FindAsync(id);

            if (existingCustomer == null)
            {
                return NotFound();
            }

            existingCustomer.Name = customer.Name;
            existingCustomer.Code = customer.Code;
            existingCustomer.CustomerType = customer.CustomerType;
            existingCustomer.Industry = customer.Industry;

            existingCustomer.Province = customer.Province;
            existingCustomer.City = customer.City;
            existingCustomer.Address = customer.Address;

            existingCustomer.ContactName = customer.ContactName;
            existingCustomer.ContactPhone = customer.ContactPhone;
            existingCustomer.ContactEmail = customer.ContactEmail;

            existingCustomer.SalesOwner = customer.SalesOwner;
            existingCustomer.SupportOwner = customer.SupportOwner;

            existingCustomer.IsEnabled = customer.IsEnabled;
            existingCustomer.Remark = customer.Remark;

            existingCustomer.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(existingCustomer);
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
