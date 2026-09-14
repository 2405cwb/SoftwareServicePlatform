namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 工单 SLA 规则。
    ///
    /// SLA = Service Level Agreement
    /// 服务级别目标。
    ///
    /// 当前按照工单 Priority 配置不同目标：
    ///
    /// Low
    /// Normal
    /// High
    /// Urgent
    ///
    /// 注意：
    /// 这里保存的是“规则”，
    /// 不是某张工单的统计结果。
    /// </summary>
    public class TicketSlaRule
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }


        /// <summary>
        /// 工单优先级。
        ///
        /// Low
        /// Normal
        /// High
        /// Urgent
        ///
        /// 数据库中同一种优先级只允许一条规则。
        /// </summary>
        public string Priority { get; set; } = string.Empty;


        /// <summary>
        /// 首次响应目标，单位：分钟。
        ///
        /// 例如：
        ///
        /// 30  = 30分钟
        /// 120 = 2小时
        /// 240 = 4小时
        ///
        /// 后续判断：
        ///
        /// FirstResponseAt - CreatedAt
        ///
        /// 是否超过这个时间。
        /// </summary>
        public int FirstResponseTargetMinutes { get; set; }


        /// <summary>
        /// 解决目标，单位：分钟。
        ///
        /// 例如：
        ///
        /// 480  = 8小时
        /// 1440 = 24小时
        /// 2880 = 48小时
        ///
        /// 后续判断：
        ///
        /// ResolvedAt - CreatedAt
        ///
        /// 是否达到 SLA。
        /// </summary>
        public int ResolutionTargetMinutes { get; set; }
        /// <summary>
        /// SLA 即将超时预警时间。
        ///
        /// 单位：分钟。
        ///
        /// 例如：
        ///
        /// Urgent：30
        ///     距离 SLA 截止还有30分钟时开始预警
        ///
        /// High：120
        ///     提前2小时预警
        ///
        /// Normal：240
        ///     提前4小时预警
        ///
        /// 0：
        ///     不启用“即将超时”预警。
        /// </summary>
        public int WarningBeforeMinutes { get; set; }

        /// <summary>
        /// 当前规则是否启用。
        ///
        /// false：
        /// 此优先级暂时不参与 SLA 判断。
        /// </summary>
        public bool IsEnabled { get; set; } = true;


        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        /// <summary>
        /// 最后修改时间。
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}