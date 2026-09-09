import { Navigate, Outlet } from "react-router-dom";

interface RequireRoleProps {
  /*
   * 允许访问当前页面的角色。
   *
   * 例如：
   *
   * ["Admin", "Support", "Sales"]
   */
  allowedRoles: string[];
}

interface CurrentUser {
  id: number;
  username: string;
  displayName: string;
  role: string;
  customerId: number | null;
}

/*
 * 页面角色权限组件。
 *
 * RequireAuth：
 * 解决“有没有登录”
 *
 * RequireRole：
 * 解决“登录以后有没有权限”
 */
function RequireRole({ allowedRoles }: RequireRoleProps) {
  /*
   * 从 sessionStorage
   * 读取当前登录用户。
   */
  const userJson = sessionStorage.getItem("current_user");

  /*
   * 没有用户信息，
   * 回到登录页。
   */
  if (!userJson) {
    return <Navigate to="/login" replace />;
  }

  try {
    const currentUser = JSON.parse(userJson) as CurrentUser;

    const hasPermission = allowedRoles.includes(currentUser.role);

    /*
     * 没权限。
     */
    if (!hasPermission) {
      return <Navigate to="/forbidden" replace />;
    }

    /*
     * 有权限：
     *
     * 放行真正页面。
     */
    return <Outlet />;
  } catch (error) {
    console.error("读取用户权限失败：", error);

    return <Navigate to="/login" replace />;
  }
}

export default RequireRole;
