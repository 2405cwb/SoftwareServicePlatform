import { NavLink, Outlet, useNavigate } from "react-router-dom";

/*
 * 当前登录用户的数据结构。
 *
 * 对应登录成功以后保存到：
 *
 * sessionStorage["current_user"]
 */
interface CurrentUser {
  id: number;

  username: string;

  displayName: string;

  role: string;

  customerId: number | null;

  email: string;

  phone: string;
}

function MainLayout() {
  /*
   * 用于代码中主动跳转页面。
   */
  const navigate = useNavigate();

  /*
   * ============================
   * 读取当前登录用户
   * ============================
   *
   * LoginPage 登录成功时已经执行：
   *
   * sessionStorage.setItem(
   *   "current_user",
   *   JSON.stringify(loginResult.user)
   * );
   */
  let currentUser: CurrentUser | null = null;

  const userJson = sessionStorage.getItem("current_user");

  if (userJson) {
    try {
      /*
       * sessionStorage 保存的是字符串，
       * JSON.parse 把字符串重新还原为对象。
       */
      currentUser = JSON.parse(userJson) as CurrentUser;
    } catch (error) {
      console.error("解析当前登录用户失败：", error);
    }
  }

  /*
   * ============================
   * 退出登录
   * ============================
   */
  function logout() {
    /*
     * 删除 JWT。
     */
    sessionStorage.removeItem("access_token");

    /*
     * 删除当前用户信息。
     */
    sessionStorage.removeItem("current_user");

    /*
     * 回到登录页面。
     *
     * replace:true
     *
     * 表示：
     * 不希望用户退出以后，
     * 再按浏览器“后退”回到后台。
     */
    navigate("/login", {
      replace: true,
    });
  }

  /*
   * 把英文角色转换为中文，
   * 只是为了界面显示。
   */
  function getRoleName(role: string) {
    switch (role) {
      case "Admin":
        return "系统管理员";

      case "Support":
        return "售后人员";

      case "Developer":
        return "开发人员";

      case "Sales":
        return "销售人员";

      case "Customer":
        return "客户用户";

      default:
        return role;
    }
  }
  /*
   * 判断当前用户是否属于某些角色。
   */
  function hasRole(...roles: string[]) {
    if (!currentUser) {
      return false;
    }

    return roles.includes(currentUser.role);
  }
  return (
    <div className="page">
      {/* =============================
          顶部 Header
          ============================= */}
      <div className="header">
        {/* 左侧平台名称 */}
        <div className="header-left">
          <h1>软件服务管理平台</h1>

          <p>客户、软件版本与服务统一管理</p>
        </div>

        {/* 右侧当前用户 */}
        <div className="header-user">
          <div className="header-user-avatar">
            {/*
             * 优先显示 DisplayName 第一个字。
             *
             * 例如：
             *
             * 张三 → 张
             *
             * 如果没有 DisplayName，
             * 就使用 Username 第一个字。
             */}
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
            </div>
          </div>

          <button type="button" className="logout-button" onClick={logout}>
            退出登录
          </button>
        </div>
      </div>

      {/* =============================
          后台主体
          ============================= */}
      <div className="main-layout">
        {/* 左侧菜单 */}
        <div className="sidebar">
          {hasRole("Admin", "Support", "Sales") && (
            <NavLink to="/customers">客户管理</NavLink>
          )}

          {hasRole("Admin", "Support", "Developer") && (
            <NavLink to="/software">软件管理</NavLink>
          )}

          {hasRole("Admin", "Support", "Developer") && (
            <NavLink to="/versions">版本管理</NavLink>
          )}
          {hasRole("Admin") && <NavLink to="/users">用户管理</NavLink>}
          {hasRole("Customer") && <NavLink to="/my-software">我的软件</NavLink>}
        </div>

        {/* 右侧页面 */}
        <div className="main-content">
          <Outlet />
        </div>
      </div>
    </div>
  );
}

export default MainLayout;
