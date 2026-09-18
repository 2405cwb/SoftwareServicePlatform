using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services.ExternalNotifications;

namespace SoftwareServicePlatform.Api.Services
{
    /// <summary>
    /// 站内通知服务。
    ///
    /// 业务层只需要告诉它：
    ///
    /// 通知谁
    /// 什么类型
    /// 什么内容
    ///
    /// 至于通知如何保存、如何去重、
    /// 后续是否通过 SignalR 推送，
    /// 都由 NotificationService 负责。
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// 向指定用户添加一条通知。
        ///
        /// 注意：
        /// 这里只把 Notification 加入当前 DbContext，
        /// 不主动调用 SaveChangesAsync。
        ///
        /// 最终由业务代码统一 SaveChanges，
        /// 从而让业务数据和通知处于同一次数据库事务中。
        /// </summary>
        Task<Notification?> AddAsync(
      int userId,
      string type,
      string title,
      string content,
      string level = "Info",
      string? targetUrl = null,
      string? dedupKey = null,

      /// <summary>
      /// 可选的通知投递策略。
      ///
      /// null：
      /// 只走原来的站内通知 + SignalR。
      ///
      /// 非 null：
      /// 可以额外发送钉钉、企业微信等。
      ///
      /// 放在最后并且有默认值，
      /// 所以以前所有调用 AddAsync() 的代码都不用修改。
      /// </summary>
      NotificationDeliveryOptions? deliveryOptions = null
  );

        Task PushPendingAsync(
    CancellationToken cancellationToken = default);
    }
}