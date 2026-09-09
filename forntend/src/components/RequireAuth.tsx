import { useEffect, useState } from "react";
import { Navigate, Outlet } from "react-router-dom";

/*
 * 登录保护组件
 *
 * 它本身不显示真正的业务页面，
 * 只负责判断：
 *
 * 当前用户是否已经登录。
 */
function RequireAuth() {
  /*
   * 是否已经完成登录状态检查。
   */
  const [checked, setChecked] = useState(false);

  /*
   * 当前是否已经通过身份认证。
   */
  const [authenticated, setAuthenticated] = useState(false);

  useEffect(() => {
    async function checkLogin() {
      /*
       * 从浏览器 Session Storage
       * 读取登录时保存的 JWT。
       */
      const token = sessionStorage.getItem("access_token");

      /*
       * 连 Token 都没有：
       *
       * 肯定没有登录。
       */
      if (!token) {
        setAuthenticated(false);
        setChecked(true);

        return;
      }

      try {
        /*
         * 不能只看 Token 是否存在。
         *
         * 还要让后端真正验证：
         *
         * - Token 是否正确
         * - 是否过期
         * - 签名是否正确
         */
        const response = await fetch("/api/auth/me", {
          headers: {
            Authorization: `Bearer ${token}`,
          },
        });

        if (response.ok) {
          /*
           * 后端认可这个 Token。
           */
          setAuthenticated(true);
        } else {
          /*
           * Token 无效或过期。
           *
           * 删除浏览器里的旧登录信息。
           */
          sessionStorage.removeItem("access_token");

          sessionStorage.removeItem("current_user");

          setAuthenticated(false);
        }
      } catch (error) {
        console.error("检查登录状态失败：", error);

        setAuthenticated(false);
      } finally {
        /*
         * 登录检查结束。
         */
        setChecked(true);
      }
    }

    checkLogin();
  }, []);

  /*
   * 正在询问后端：
   *
   * “这个 Token 到底还有效吗？”
   *
   * 此时先不要显示后台页面。
   */
  if (!checked) {
    return (
      <div
        style={{
          padding: "40px",
          textAlign: "center",
        }}
      >
        正在验证登录状态...
      </div>
    );
  }

  /*
   * 没登录：
   *
   * 强制跳转登录页。
   */
  if (!authenticated) {
    return <Navigate to="/login" replace />;
  }

  /*
   * 登录成功。
   *
   * Outlet 就是：
   *
   * 放行下面真正的页面。
   */
  return <Outlet />;
}

export default RequireAuth;
