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


        /*
 * ==========================================
 * 下载记录去重
 * ==========================================
 *
 * 浏览器下载大文件时可能发送多个 Range 请求。
 *
 * 例如同一个：
 *
 * /api/download/file?ticket=abc
 *
 * 可能实际请求多次。
 *
 * 如果每次请求都写 DownloadRecord，
 * 一次下载就会被统计成多次。
 *
 * 所以一个 ticket 生命周期内，
 * 只能成功创建一次下载记录。
 */
        private int _downloadRecordCreated = 0;


        /*
         * 尝试取得“创建下载记录”的资格。
         *
         * Interlocked.CompareExchange 是线程安全的。
         *
         * 第一个请求：
         *
         * 0 -> 1
         * 返回 true
         *
         * 后面的 Range 请求看到已经是1：
         *
         * 返回 false
         */
        public bool TryMarkDownloadRecordCreated()
        {
            return Interlocked.CompareExchange(
                ref _downloadRecordCreated,
                1,
                0
            ) == 0;
        }


        /*
         * 如果写数据库失败，
         * 恢复状态。
         *
         * 这样后面的 Range 请求还有机会
         * 再次尝试记录。
         */
        public void ResetDownloadRecordCreated()
        {
            Interlocked.Exchange(
                ref _downloadRecordCreated,
                0
            );
        }
    }
}