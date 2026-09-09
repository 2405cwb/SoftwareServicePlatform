namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 软件产品信息
    /// </summary>
    public class Software
    {
        /// <summary>
        /// 软件ID，数据库主键
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 软件名称
        /// 例如：路面检测数据处理软件
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 软件唯一编码
        /// 建议创建以后尽量不要修改
        /// 例如：ROAD_PROCESS
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 软件简称
        /// 例如：道路处理平台
        /// </summary>
        public string ShortName { get; set; } = string.Empty;

        /// <summary>
        /// 软件分类
        /// 例如：道路检测、隧道检测、图像处理、数据转换
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 软件描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 开发负责人
        /// 当前阶段先存姓名，
        /// 后续有用户系统后可以改为关联 User
        /// </summary>
        public string Developer { get; set; } = string.Empty;

        /// <summary>
        /// 售后负责人
        /// </summary>
        public string SupportOwner { get; set; } = string.Empty;

        /// <summary>
        /// 软件所属部门
        /// 例如：研发部、公路事业部
        /// </summary>
        public string Department { get; set; } = string.Empty;

        /// <summary>
        /// 软件运行平台
        /// 例如：Windows、Linux、Web
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// 软件技术栈
        /// 例如：C++ / Qt、C# / WinForms
        /// </summary>
        public string TechnologyStack { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用
        /// false 表示该软件已经停用，不再对外发布
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 是否允许客户下载
        /// </summary>
        public bool AllowDownload { get; set; } = true;

        /// <summary>
        /// 软件备注
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 该软件拥有的全部版本
        /// </summary>
        public List<SoftwareVersion> Versions { get; set; } = new();

        /// <summary>
        /// 当前软件与客户之间的绑定关系
        /// </summary>
        public List<CustomerSoftware> CustomerSoftwares { get; set; } = new();
    }
}
