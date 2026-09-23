import { KeyRound, Monitor } from "lucide-react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { getSessionUser } from "../utils/session";

/**
 * 登录后的通用账户入口。
 *
 * 1. 所有登录用户：可以进入“修改密码”；
 * 2. Admin：额外提供“更新设备授权”入口。
 *
 * 这里继续采用浮动入口，避免为了本次功能大范围改动 MainLayout。
 * 后续如果你希望把它放进左侧菜单，再单独调整布局即可。
 */
function AccountShell() {
  const navigate = useNavigate();
  const location = useLocation();
  const currentUser = getSessionUser();

  const isAdmin = currentUser?.role === "Admin";

  const commonStyle = {
    position: "fixed" as const,
    right: 24,
    zIndex: 2000,
    display: "flex",
    alignItems: "center",
    gap: 8,
    border: "1px solid #d7dde8",
    borderRadius: 999,
    padding: "10px 16px",
    background: "#ffffff",
    boxShadow: "0 8px 24px rgba(15, 23, 42, 0.12)",
    cursor: "pointer",
    fontSize: 14,
  };

  return (
    <>
      <Outlet />

      {isAdmin && location.pathname !== "/client-devices" && (
        <button
          type="button"
          title="查看和管理各客户已经授权自动更新的设备"
          onClick={() => navigate("/client-devices")}
          style={{
            ...commonStyle,
            bottom: 76,
          }}
        >
          <Monitor size={16} />
          更新设备授权
        </button>
      )}

      {location.pathname !== "/change-password" && (
        <button
          type="button"
          title="修改当前登录账号密码"
          onClick={() => navigate("/change-password")}
          style={{
            ...commonStyle,
            bottom: 24,
          }}
        >
          <KeyRound size={16} />
          修改密码
        </button>
      )}
    </>
  );
}

export default AccountShell;
