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
    connection.on("NotificationCreated", (notification: NotificationItem) => {
      /*
       * 实时增加未读数量。
       */
      setUnreadCount((count) => count + 1);

      /*
       * 如果通知面板已经加载过，
       * 直接把新通知插到最前面。
       */
      setNotifications((items) => {
        if (items.some((item) => item.id === notification.id)) {
          return items;
        }

        return [notification, ...items].slice(0, 8);
      });
    });

    connection
      .start()
      .then(() => {
        console.log("SignalR 通知连接成功");
      })
      .catch((error) => {
        console.error("SignalR 通知连接失败：", error);
      });

    return () => {
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
      navigate(notification.targetUrl);
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
                  await refreshNotifications();
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
