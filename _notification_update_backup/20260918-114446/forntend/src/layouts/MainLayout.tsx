import { useEffect, useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import {
  Boxes,
  Download,
  GitBranch,
  LayoutDashboard,
  LogOut,
  PackageOpen,
  SlidersHorizontal,
  TicketCheck,
  UserCog,
  Users,
  Bell,
  CheckCheck,
} from "lucide-react";
import { getPlatformInfo } from "../services/platform";
import { getRoleName, getSessionUser, hasRole } from "../utils/session";
import { useWorkProfile } from "../hooks/useWorkProfile";
import {
  getNotifications,
  getUnreadCount,
  markAllNotificationsAsRead,
  markNotificationAsRead,
  type NotificationItem,
} from "../services/notifications";
import { createNotificationConnection } from "../services/signalr";
function MainLayout() {
  const navigate = useNavigate();
  const currentUser = getSessionUser();
  const { profile } = useWorkProfile();

  const [platformTitle, setPlatformTitle] = useState("软件服务管理平台");
  const [platformCompanyName, setPlatformCompanyName] = useState("");
  const [notificationOpen, setNotificationOpen] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  useEffect(() => {
    getPlatformInfo()
      .then((info) => {
        setPlatformTitle(info.title);
        setPlatformCompanyName(info.companyName);
        document.title = info.title;
      })
      .catch((error) => console.error("加载平台配置失败：", error));
  }, []);
  useEffect(() => {
    void refreshUnreadCount();

    const timer = window.setInterval(() => {
      void refreshUnreadCount();
    }, 120000);

    return () => {
      window.clearInterval(timer);
    };
  }, []);

  useEffect(() => {
    const connection = createNotificationConnection();

    connection.onreconnected(() => {
      void refreshUnreadCount();

      if (notificationOpen) {
        void refreshNotifications();
      }
    });

    connection.onreconnecting(() => {
      console.warn("SignalR 通知连接正在重新连接...");
    });

    connection.onreconnected(() => {
      console.log("SignalR 通知连接已恢复");

      /*
       * 重连期间可能已经产生了通知，
       * 所以必须重新向服务器同步。
       */
      void refreshUnreadCount();

      if (notificationOpen) {
        void refreshNotifications();
      }
    });

    connection.onclose((error) => {
      console.error("SignalR 通知连接已关闭：", error);
    });
    connection.on("NotificationCreated", (notification: NotificationItem) => {
      /*
       * 调试阶段先保留。
       *
       * 新工单创建后，
       * 售后浏览器控制台应该立刻看到这一行。
       */
      console.log("收到实时通知：", notification);

      /*
       * ==========================================
       * 同步真实未读数量
       * ==========================================
       *
       * 原来只是：
       *
       * setUnreadCount(count => count + 1)
       *
       * 理论上可以工作，
       * 但存在几个问题：
       *
       * 1. SignalR 重连期间可能漏通知
       * 2. 多标签页可能导致本地计数不同步
       * 3. 之前状态已经错误时继续 +1 会越来越偏
       *
       * 所以收到实时事件以后，
       * 直接向后端查询真实未读数更稳。
       */
      void refreshUnreadCount();

      /*
       * 如果通知列表当前已经存在于内存中，
       * 直接把新通知放到最前面。
       */
      setNotifications((items) => {
        if (items.some((item) => item.id === notification.id)) {
          return items;
        }

        return [notification, ...items].slice(0, 8);
      });
    });

    /*
     * ==========================================
     * 启动 SignalR
     * ==========================================
     *
     * 开发环境经常会出现：
     *
     * 前端已经启动
     * ↓
     * 后端正在重启
     * ↓
     * 第一次 connection.start() 失败
     *
     * 如果只 catch，
     * 这个页面后面就不会再有实时通知。
     *
     * 所以第一次启动失败以后，
     * 每 5 秒重新尝试一次。
     */
    let stopped = false;

    async function startConnection() {
      while (!stopped && connection.state === "Disconnected") {
        try {
          await connection.start();

          console.log("SignalR 通知连接成功");

          /*
           * SignalR 刚连上以后，
           * 立即从服务器同步一次未读数。
           *
           * 防止连接之前已经产生过通知。
           */
          await refreshUnreadCount();

          break;
        } catch (error) {
          console.error("SignalR 通知连接失败，5秒后重试：", error);

          await new Promise<void>((resolve) => {
            window.setTimeout(resolve, 5000);
          });
        }
      }
    }

    void startConnection();

    return () => {
      /*
       * 告诉启动重试循环：
       * 页面已经卸载，不要再尝试重连。
       */
      stopped = true;

      void connection.stop();
    };
  }, []);
  function logout() {
    sessionStorage.removeItem("access_token");
    sessionStorage.removeItem("current_user");
    navigate("/login", { replace: true });
  }
  async function refreshUnreadCount() {
    const data = await getUnreadCount();
    setUnreadCount(data.count);
  }

  async function refreshNotifications() {
    const data = await getNotifications();
    setNotifications(data.items);
  }

  async function handleNotificationClick(notification: NotificationItem) {
    if (!notification.isRead) {
      await markNotificationAsRead(notification.id);
      await refreshUnreadCount();
    }

    setNotificationOpen(false);

    if (notification.targetUrl) {
      /*
       * ==========================================
       * 通知跳转
       * ==========================================
       *
       * 不能只调用：
       *
       * navigate(notification.targetUrl)
       *
       * 因为用户可能本来就已经停留在目标页面。
       *
       * 例如：
       *
       * 当前页面：
       * /tickets
       *
       * 新通知：
       * /tickets?ticketId=35
       *
       * TicketPage 组件不会重新挂载，
       * 原来加载的 tickets 数据也还是旧的。
       *
       * 所以通过 state 携带一个每次都不同的刷新标记。
       *
       * 目标页面如果关心这个标记，
       * 就主动重新请求最新数据。
       */
      navigate(notification.targetUrl, {
        state: {
          notificationRefreshKey: Date.now(),
        },
      });
    }
  }

  async function handleReadAll() {
    await markAllNotificationsAsRead();

    setNotifications((items) =>
      items.map((item) => ({
        ...item,
        isRead: true,
      })),
    );

    setUnreadCount(0);
  }
  const canViewTickets = hasRole(
    currentUser,
    "Admin",
    "Support",
    "Developer",
    "Customer",
  );

  return (
    <div className="page u-shell">
      <header className="header u-header">
        <div className="header-left u-brand">
          <div className="u-brand-mark">S</div>
          <div>
            <h1>{platformTitle}</h1>
            {platformCompanyName && <p>{platformCompanyName}</p>}
          </div>
        </div>

        <div className="header-user u-header-user">
          {canViewTickets && (profile?.openTicketCount ?? 0) > 0 && (
            <button
              type="button"
              className="u-header-todo"
              onClick={() => navigate("/tickets?scope=my&status=open")}
            >
              <TicketCheck size={16} />
              我的待办 {profile?.openTicketCount}
            </button>
          )}
          <div className="u-notification">
            <button
              type="button"
              className="u-notification-button"
              onClick={async () => {
                const open = !notificationOpen;

                setNotificationOpen(open);

                if (open) {
                  /*
                   * 打开通知面板时，
                   * 不仅刷新通知列表，
                   * 还要重新同步未读数量。
                   *
                   * 这样即使之前 SignalR 某一次实时消息漏掉了，
                   * 用户打开铃铛时也可以自动纠正状态。
                   */
                  await Promise.all([
                    refreshNotifications(),
                    refreshUnreadCount(),
                  ]);
                }
              }}
            >
              <Bell size={19} />

              {unreadCount > 0 && (
                <span className="u-notification-badge">
                  {Math.min(unreadCount, 99)}
                </span>
              )}
            </button>

            {notificationOpen && (
              <div className="u-notification-panel">
                <div className="u-notification-header">
                  <strong>通知</strong>

                  {unreadCount > 0 && (
                    <button type="button" onClick={handleReadAll}>
                      <CheckCheck size={15} />
                      全部已读
                    </button>
                  )}
                </div>

                <div className="u-notification-list">
                  {notifications.length === 0 ? (
                    <div className="u-notification-empty">暂无通知</div>
                  ) : (
                    notifications.map((item) => (
                      <button
                        key={item.id}
                        type="button"
                        className={`u-notification-item ${
                          item.isRead ? "" : "is-unread"
                        }`}
                        onClick={() => void handleNotificationClick(item)}
                      >
                        <strong>{item.title}</strong>
                        <span>{item.content}</span>
                      </button>
                    ))
                  )}
                </div>
              </div>
            )}
          </div>
          <div className="header-user-avatar">
            {currentUser?.displayName?.charAt(0) ||
              currentUser?.username?.charAt(0) ||
              "U"}
          </div>

          <div className="header-user-info">
            <div className="header-user-name">
              {currentUser?.displayName || currentUser?.username || "当前用户"}
            </div>
            <div className="header-user-role">
              {currentUser ? getRoleName(currentUser.role) : ""}
              {profile?.customerName && (
                <span className="u-user-company">
                  {" "}
                  · {profile.customerName}
                </span>
              )}
            </div>
          </div>

          <button
            type="button"
            className="logout-button u-logout"
            onClick={logout}
            title="退出登录"
          >
            <LogOut size={16} />
            <span>退出</span>
          </button>
        </div>
      </header>

      <div className="main-layout u-main-layout">
        <aside className="sidebar u-sidebar">
          <NavLink to="/dashboard" className="sidebar-menu-item">
            <LayoutDashboard size={18} />
            <span>工作台</span>
          </NavLink>

          {hasRole(currentUser, "Admin", "Support", "Sales") && (
            <NavLink to="/customers" className="sidebar-menu-item">
              <Users size={18} />
              <span>客户管理</span>
            </NavLink>
          )}

          {hasRole(currentUser, "Admin", "Support", "Developer") && (
            <NavLink to="/software" className="sidebar-menu-item">
              <Boxes size={18} />
              <span>软件管理</span>
            </NavLink>
          )}

          {hasRole(currentUser, "Admin", "Support", "Developer") && (
            <NavLink to="/versions" className="sidebar-menu-item">
              <GitBranch size={18} />
              <span>版本管理</span>
            </NavLink>
          )}

          {hasRole(currentUser, "Admin", "Support") && (
            <NavLink to="/download-records" className="sidebar-menu-item">
              <Download size={18} />
              <span>下载记录</span>
            </NavLink>
          )}

          {canViewTickets && (
            <NavLink
              to="/tickets"
              className="sidebar-menu-item u-menu-with-badge"
            >
              <TicketCheck size={18} />
              <span>工单管理</span>
              {(profile?.openTicketCount ?? 0) > 0 && (
                <em
                  className={
                    (profile?.urgentTicketCount ?? 0) > 0 ? "is-danger" : ""
                  }
                >
                  {Math.min(profile?.openTicketCount ?? 0, 99)}
                </em>
              )}
            </NavLink>
          )}

          {hasRole(currentUser, "Customer") && (
            <NavLink to="/my-software" className="sidebar-menu-item">
              <PackageOpen size={18} />
              <span>我的软件</span>
            </NavLink>
          )}

          <div className="u-sidebar-spacer" />

          {hasRole(currentUser, "Admin") && (
            <NavLink to="/users" className="sidebar-menu-item">
              <UserCog size={18} />
              <span>用户管理</span>
            </NavLink>
          )}

          {hasRole(currentUser, "Admin") && (
            <NavLink to="/sla-settings" className="sidebar-menu-item">
              <SlidersHorizontal size={18} />
              <span>SLA 设置</span>
            </NavLink>
          )}
        </aside>

        <main className="main-content u-main-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

export default MainLayout;
