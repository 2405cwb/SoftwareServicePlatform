import { KeyRound } from "lucide-react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";

/**
 * 登录后的通用账户入口。
 *
 * 当前项目的 MainLayout 顶部已经比较拥挤，
 * 为了不大范围改动原布局，这里提供一个统一的浮动“修改密码”入口。
 * Admin / Support / Developer / Sales / Customer 登录后都能看到。
 */
function AccountShell() {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <>
      <Outlet />

      {location.pathname !== "/change-password" && (
        <button
          type="button"
          title="修改当前登录账号密码"
          onClick={() => navigate("/change-password")}
          style={{
            position: "fixed",
            right: 24,
            bottom: 24,
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
