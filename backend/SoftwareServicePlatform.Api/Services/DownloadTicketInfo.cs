namespace SoftwareServicePlatform.Api.Services
{
    /// <summary>
    /// 短时下载票据在服务器内存中保存的信息。
    /// </summary>
    public class DownloadTicketInfo
    {
        /// <summary>
        /// 谁申请的下载。
        /// </summary>
        public int UserId { get; set; }


        /// <summary>
        /// 要下载哪个软件版本。
        /// </summary>
        public int SoftwareVersionId { get; set; }
    }
}