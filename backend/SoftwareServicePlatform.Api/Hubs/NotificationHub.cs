using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SoftwareServicePlatform.Api.Hubs;

/// <summary>
/// 站内通知 SignalR Hub。
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
}