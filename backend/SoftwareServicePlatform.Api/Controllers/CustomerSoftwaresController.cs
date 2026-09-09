using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using Microsoft.AspNetCore.Authorization;
namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 客户与软件绑定关系管理
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerSoftwaresController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public CustomerSoftwaresController(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 查询某个客户绑定的所有软件关系
        ///
        /// GET:
        /// /api/customersoftwares?customerId=1
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomerSoftwares(
            [FromQuery] int customerId)
        {
            /*
             * customerId 必须有效
             */
            if (customerId <= 0)
            {
                return BadRequest(
                    "客户ID不能为空"
                );
            }

            /*
             * 这里使用 Include(x => x.Software)
             *
             * 因为我们不仅想知道：
             *
             * SoftwareId = 2
             *
             * 还希望知道对应软件的：
             *
             * Name
             * Code
             * IsEnabled
             * AllowDownload
             */
            var bindings =
                await _dbContext.CustomerSoftwares
                    .Include(x => x.Software)
                    .Where(
                        x => x.CustomerId == customerId
                    )
                    .OrderByDescending(
                        x => x.BoundAt
                    )
                    .ToListAsync();

            /*
             * 这里不要直接 return Ok(bindings)
             *
             * 因为 CustomerSoftware 里有导航属性：
             *
             * Customer
             * Software
             *
             * 后面容易出现循环引用问题。
             *
             * 所以这里先手工整理成一个简单对象。
             */
            var result = bindings.Select(
                x => new
                {
                    x.Id,

                    x.CustomerId,

                    x.SoftwareId,

                    x.IsEnabled,

                    x.BoundAt,

                    x.Remark,

                    SoftwareName =
                        x.Software != null
                            ? x.Software.Name
                            : string.Empty,

                    SoftwareCode =
                        x.Software != null
                            ? x.Software.Code
                            : string.Empty,

                    SoftwareEnabled =
                        x.Software != null
                        && x.Software.IsEnabled,

                    SoftwareAllowDownload =
                        x.Software != null
                        && x.Software.AllowDownload
                }
            );

            return Ok(result);
        }

        /// <summary>
        /// 给客户绑定一个软件
        ///
        /// POST:
        /// /api/customersoftwares
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> BindSoftware(
            CustomerSoftware customerSoftware)
        {
            /*
             * 1. 检查客户ID
             */
            if (customerSoftware.CustomerId <= 0)
            {
                return BadRequest(
                    "请选择客户"
                );
            }

            /*
             * 2. 检查软件ID
             */
            if (customerSoftware.SoftwareId <= 0)
            {
                return BadRequest(
                    "请选择软件"
                );
            }

            /*
             * 3. 确认客户真的存在
             */
            var customerExists =
                await _dbContext.Customers.AnyAsync(
                    x =>
                        x.Id ==
                        customerSoftware.CustomerId
                );

            if (!customerExists)
            {
                return BadRequest(
                    "客户不存在"
                );
            }

            /*
             * 4. 确认软件真的存在
             */
            var softwareExists =
                await _dbContext.Softwares.AnyAsync(
                    x =>
                        x.Id ==
                        customerSoftware.SoftwareId
                );

            if (!softwareExists)
            {
                return BadRequest(
                    "软件不存在"
                );
            }

            /*
             * 5. 查询这个客户是否已经绑定过这个软件
             */
            var existingBinding =
                await _dbContext.CustomerSoftwares
                    .FirstOrDefaultAsync(
                        x =>
                            x.CustomerId ==
                            customerSoftware.CustomerId
                            &&
                            x.SoftwareId ==
                            customerSoftware.SoftwareId
                    );

            /*
             * 已经存在绑定关系
             */
            if (existingBinding != null)
            {
                /*
                 * 如果只是之前被停用了，
                 * 我们不再插入一条新的重复记录。
                 *
                 * 而是直接重新启用。
                 */
                if (!existingBinding.IsEnabled)
                {
                    existingBinding.IsEnabled = true;

                    existingBinding.Remark =
                        customerSoftware.Remark;

                    /*
                     * 重新启用时，可以把 BoundAt
                     * 理解为“最近一次重新绑定时间”
                     */
                    existingBinding.BoundAt =
                        DateTime.UtcNow;

                    await _dbContext
                        .SaveChangesAsync();

                    return Ok(
                        existingBinding
                    );
                }

                return BadRequest(
                    "该客户已经绑定此软件"
                );
            }

            /*
             * 6. 新绑定关系
             */
            customerSoftware.IsEnabled = true;

            customerSoftware.BoundAt =
                DateTime.UtcNow;

            /*
             * 防止前端把整个 Customer / Software
             * 导航对象传回来。
             */
            customerSoftware.Customer = null;

            customerSoftware.Software = null;

            _dbContext.CustomerSoftwares.Add(
                customerSoftware
            );

            await _dbContext.SaveChangesAsync();

            return Ok(customerSoftware);
        }

        /// <summary>
        /// 修改绑定关系
        ///
        /// 目前主要用于：
        /// 修改备注
        /// 启用 / 停用
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBinding(
            int id,
            CustomerSoftware customerSoftware)
        {
            var existingBinding =
                await _dbContext.CustomerSoftwares
                    .FindAsync(id);

            if (existingBinding == null)
            {
                return NotFound(
                    "客户软件绑定关系不存在"
                );
            }

            /*
             * 这里暂时不允许通过 PUT
             * 修改 CustomerId 和 SoftwareId。
             *
             * 因为：
             *
             * “客户A绑定软件1”
             *
             * 如果直接修改成：
             *
             * “客户B绑定软件2”
             *
             * 实际上已经是另一条关系了。
             *
             * 这种情况应该删除原关系再新增。
             */

            existingBinding.IsEnabled =
                customerSoftware.IsEnabled;

            existingBinding.Remark =
                customerSoftware.Remark;

            await _dbContext.SaveChangesAsync();

            return Ok(existingBinding);
        }

        /// <summary>
        /// 取消客户的软件绑定
        ///
        /// 注意：
        /// 这里暂时不真正 DELETE 数据。
        ///
        /// 而是把 IsEnabled 改成 false。
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DisableBinding(
            int id)
        {
            var existingBinding =
                await _dbContext.CustomerSoftwares
                    .FindAsync(id);

            if (existingBinding == null)
            {
                return NotFound(
                    "客户软件绑定关系不存在"
                );
            }

            /*
             * 不物理删除，
             * 只停用。
             */
            existingBinding.IsEnabled = false;

            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }
}