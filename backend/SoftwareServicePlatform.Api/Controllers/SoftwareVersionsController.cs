using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 软件版本管理接口
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SoftwareVersionsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        /// <summary>
        /// 通过依赖注入获取数据库上下文
        /// </summary>
        public SoftwareVersionsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 获取版本列表
        ///
        /// 调用方式：
        ///
        /// 获取所有版本：
        /// GET /api/softwareversions
        ///
        /// 获取某个软件的版本：
        /// GET /api/softwareversions?softwareId=1
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSoftwareVersions(
            [FromQuery] int? softwareId)
        {
            // 先得到 SoftwareVersions 的查询对象
            var query = _dbContext.SoftwareVersions.AsQueryable();

            // 如果前端传了 softwareId，
            // 就只查询这个软件下面的版本
            if (softwareId.HasValue)
            {
                query = query.Where(
                    x => x.SoftwareId == softwareId.Value
                );
            }

            // 最新创建的版本显示在最上面
            var versions = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(versions);
        }

        /// <summary>
        /// 新增软件版本
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSoftwareVersion(
            SoftwareVersion softwareVersion)
        {
            // 1. 必须选择一个软件
            if (softwareVersion.SoftwareId <= 0)
            {
                return BadRequest("请选择所属软件");
            }

            // 2. 版本号不能为空
            if (string.IsNullOrWhiteSpace(
                softwareVersion.Version))
            {
                return BadRequest("版本号不能为空");
            }

            // 3. 检查软件是否真的存在
            var softwareExists =
                await _dbContext.Softwares.AnyAsync(
                    x => x.Id == softwareVersion.SoftwareId
                );

            if (!softwareExists)
            {
                return BadRequest("所属软件不存在");
            }

            // 4. 同一个软件不能存在两个相同版本号
            //
            // 例如：
            // 路面检测软件 1.0.0
            // 路面检测软件 1.0.0
            //
            // 这种情况不允许
            var versionExists =
                await _dbContext.SoftwareVersions.AnyAsync(
                    x =>
                        x.SoftwareId
                            == softwareVersion.SoftwareId
                        &&
                        x.Version
                            == softwareVersion.Version
                );

            if (versionExists)
            {
                return BadRequest(
                    "该软件已经存在相同版本号"
                );
            }

            // 5. 如果用户勾选“已发布”，
            // 自动记录发布时间
            if (softwareVersion.IsPublished)
            {
                softwareVersion.PublishedAt =
                    DateTime.UtcNow;
            }
            else
            {
                softwareVersion.PublishedAt = null;
            }

            // 6. 创建时间和修改时间由服务器负责
            softwareVersion.CreatedAt =
                DateTime.UtcNow;

            softwareVersion.UpdatedAt =
                DateTime.UtcNow;

            // 7. 添加到数据库
            _dbContext.SoftwareVersions.Add(
                softwareVersion
            );

            await _dbContext.SaveChangesAsync();

            return Ok(softwareVersion);
        }

        /// <summary>
        /// 修改软件版本
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSoftwareVersion(
            int id,
            SoftwareVersion softwareVersion)
        {
            // 1. 根据 ID 查找原始版本记录
            var existingVersion =
                await _dbContext.SoftwareVersions
                    .FindAsync(id);

            if (existingVersion == null)
            {
                return NotFound("软件版本不存在");
            }

            // 2. 检查软件ID
            if (softwareVersion.SoftwareId <= 0)
            {
                return BadRequest("请选择所属软件");
            }

            // 3. 检查版本号
            if (string.IsNullOrWhiteSpace(
                softwareVersion.Version))
            {
                return BadRequest("版本号不能为空");
            }

            // 4. 确认所属软件存在
            var softwareExists =
                await _dbContext.Softwares.AnyAsync(
                    x => x.Id == softwareVersion.SoftwareId
                );

            if (!softwareExists)
            {
                return BadRequest("所属软件不存在");
            }

            // 5. 检查版本号重复
            //
            // x.Id != id 很重要：
            // 修改自己时不能把自己判断成重复数据
            var versionExists =
                await _dbContext.SoftwareVersions.AnyAsync(
                    x =>
                        x.SoftwareId
                            == softwareVersion.SoftwareId
                        &&
                        x.Version
                            == softwareVersion.Version
                        &&
                        x.Id != id
                );

            if (versionExists)
            {
                return BadRequest(
                    "该软件已经存在相同版本号"
                );
            }

            // 6. 更新允许修改的字段
            existingVersion.SoftwareId =
                softwareVersion.SoftwareId;

            existingVersion.Version =
                softwareVersion.Version;

            existingVersion.VersionType =
                softwareVersion.VersionType;

            existingVersion.Title =
                softwareVersion.Title;

            existingVersion.ReleaseNotes =
                softwareVersion.ReleaseNotes;

            existingVersion.ForceUpdate =
                softwareVersion.ForceUpdate;

            existingVersion.AllowDownload =
                softwareVersion.AllowDownload;

            /*
             * 处理发布状态。
             *
             * 情况1：
             * 原来未发布，现在改成发布
             * -> 自动记录发布时间
             *
             * 情况2：
             * 原来已经发布
             * -> 保留原发布时间
             *
             * 情况3：
             * 改成未发布
             * -> 清空发布时间
             */
            if (softwareVersion.IsPublished)
            {
                if (!existingVersion.IsPublished)
                {
                    existingVersion.PublishedAt =
                        DateTime.UtcNow;
                }
            }
            else
            {
                existingVersion.PublishedAt = null;
            }

            existingVersion.IsPublished =
                softwareVersion.IsPublished;

            // 修改时间重新记录
            existingVersion.UpdatedAt =
                DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(existingVersion);
        }

        /// <summary>
        /// 删除软件版本
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSoftwareVersion(
            int id)
        {
            var softwareVersion =
                await _dbContext.SoftwareVersions
                    .FindAsync(id);

            if (softwareVersion == null)
            {
                return NotFound("软件版本不存在");
            }

            _dbContext.SoftwareVersions.Remove(
                softwareVersion
            );

            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }
}