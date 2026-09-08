using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SoftwaresController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public SoftwaresController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 获取软件列表
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSoftwares()
        {
            var softwares = await _dbContext.Softwares
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(softwares);
        }

        /// <summary>
        /// 新增软件
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSoftware(Software software)
        {
            if (string.IsNullOrWhiteSpace(software.Name))
            {
                return BadRequest("软件名称不能为空");
            }

            if (string.IsNullOrWhiteSpace(software.Code))
            {
                return BadRequest("软件编码不能为空");
            }

            var codeExists = await _dbContext.Softwares
    .AnyAsync(x => x.Code == software.Code);

            if (codeExists)
            {
                return BadRequest("软件编码已存在");
            }

            software.CreatedAt = DateTime.UtcNow;
            software.UpdatedAt = DateTime.UtcNow;

            _dbContext.Softwares.Add(software);

            await _dbContext.SaveChangesAsync();

            return Ok(software);
        }

        /// <summary>
        /// 修改软件
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSoftware(
            int id,
            Software software)
        {
            var existingSoftware =
                await _dbContext.Softwares.FindAsync(id);

            if (existingSoftware == null)
            {
                return NotFound("软件不存在");
            }

            if (string.IsNullOrWhiteSpace(software.Name))
            {
                return BadRequest("软件名称不能为空");
            }

            if (string.IsNullOrWhiteSpace(software.Code))
            {
                return BadRequest("软件编码不能为空");
            }

            var codeExists = await _dbContext.Softwares
                .AnyAsync(x =>
                    x.Code == software.Code &&
                    x.Id != id);

            if (codeExists)
            {
                return BadRequest("软件编码已存在");
            }

            existingSoftware.Name = software.Name;
            existingSoftware.Code = software.Code;
            existingSoftware.ShortName = software.ShortName;
            existingSoftware.Category = software.Category;
            existingSoftware.Description = software.Description;

            existingSoftware.Developer = software.Developer;
            existingSoftware.SupportOwner = software.SupportOwner;
            existingSoftware.Department = software.Department;

            existingSoftware.Platform = software.Platform;
            existingSoftware.TechnologyStack = software.TechnologyStack;

            existingSoftware.IsEnabled = software.IsEnabled;
            existingSoftware.AllowDownload = software.AllowDownload;

            existingSoftware.Remark = software.Remark;

            existingSoftware.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(existingSoftware);
        }
        /// <summary>
        /// 删除软件
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSoftware(int id)
        {
            var software =
                await _dbContext.Softwares.FindAsync(id);

            if (software == null)
            {
                return NotFound("软件不存在");
            }

            _dbContext.Softwares.Remove(software);

            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }
}