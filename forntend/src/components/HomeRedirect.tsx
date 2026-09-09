import { Navigate } from "react-router-dom";

/*
 * 当前用户结构。
 */
interface CurrentUser {
  id: number;
  username: string;
  displayName: string;
  role: string;
  customerId: number | null;
}

/*
 * 根据当前登录用户角色，
 * 决定默认进入哪个页面。
 */
function HomeRedirect() {
  /*
   * 登录成功以后，
   * LoginPage 已经把用户信息保存在：
   *
   * sessionStorage["current_user"]
   */
  const userJson = sessionStorage.getItem("current_user");

  /*
   * 连用户信息都没有，
   * 直接回登录页。
   */
  if (!userJson) {
    return <Navigate to="/login" replace />;
  }

  try {
    /*
     * JSON 字符串重新转换成用户对象。
     *
     * 注意：
     * as CurrentUser 不要拆行。
     */
    const currentUser = JSON.parse(userJson) as CurrentUser;

    /*
     * 根据角色决定默认页面。
     */
    switch (currentUser.role) {
      /*
       * 管理员：
       * 默认进入客户管理。
       */
      case "Admin":
        return <Navigate to="/customers" replace />;

      /*
       * 售后：
       * 暂时默认客户管理。
       */
      case "Support":
        return <Navigate to="/customers" replace />;

      /*
       * 销售：
       * 主要工作对象是客户。
       */
      case "Sales":
        return <Navigate to="/customers" replace />;

      /*
       * 开发：
       * 默认进入软件管理。
       */
      case "Developer":
        return <Navigate to="/software" replace />;

      /*
       * 客户：
       *
       * 后面我们马上创建：
       *
       * /my-software
       */
      case "Customer":
        return <Navigate to="/my-software" replace />;

      /*
       * 未知角色。
       */
      default:
        return <Navigate to="/forbidden" replace />;
    }
  } catch (error) {
    console.error("读取当前用户失败：", error);

    return <Navigate to="/login" replace />;
  }
}

export default HomeRedirect;
