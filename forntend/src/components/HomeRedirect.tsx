import { Navigate } from "react-router-dom";
import { getSessionUser } from "../utils/session";

/**
 * 所有已登录角色统一先进入自己的工作台。
 * DashboardPage 会根据角色组合不同模块。
 */
function HomeRedirect() {
  const user = getSessionUser();

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  return <Navigate to="/dashboard" replace />;
}

export default HomeRedirect;
