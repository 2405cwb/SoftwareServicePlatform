import { useState } from "react";
import { useNavigate } from "react-router-dom";

/*
 * 后端登录成功返回的数据格式
 */
interface LoginResponse {
  message: string;

  token: string;

  user: {
    id: number;
    username: string;
    displayName: string;
    role: string;
    customerId: number | null;
    email: string;
    phone: string;
  };
}

function LoginPage() {
  /*
   * 用户名
   */
  const [username, setUsername] = useState("");

  /*
   * 密码
   */
  const [password, setPassword] = useState("");

  /*
   * 登录错误提示
   */
  const [errorMessage, setErrorMessage] = useState("");

  /*
   * 是否正在登录
   */
  const [isLoggingIn, setIsLoggingIn] = useState(false);

  /*
   * React Router 页面跳转
   */
  const navigate = useNavigate();

  /*
   * 登录
   */
  async function handleLogin() {
    setErrorMessage("");

    /*
     * 基础输入检查
     */
    if (!username.trim()) {
      setErrorMessage("请输入用户名");
      return;
    }

    if (!password) {
      setErrorMessage("请输入密码");
      return;
    }

    try {
      setIsLoggingIn(true);

      /*
       * 调用后端登录接口
       *
       * Vite 已经配置 /api 代理，
       * 所以不用写 localhost:5108。
       */
      const response = await fetch("/api/auth/login", {
        method: "POST",

        headers: {
          "Content-Type": "application/json",
        },

        body: JSON.stringify({
          username: username.trim(),
          password: password,
        }),
      });

      /*
       * 获取后端 JSON
       */
      const data = await response.json();

      /*
       * 登录失败
       */
      if (!response.ok) {
        setErrorMessage(data.message || data.title || "登录失败");

        return;
      }

      /*
       * 转成登录返回类型
       */
      const loginResult = data as LoginResponse;

      /*
       * 保存 JWT
       */
      sessionStorage.setItem("access_token", loginResult.token);

      /*
       * 保存当前登录用户
       */
      sessionStorage.setItem("current_user", JSON.stringify(loginResult.user));

      /*
       * 登录成功进入后台
       */
      navigate("/customers");
    } catch (error) {
      console.error("登录请求失败：", error);

      setErrorMessage("无法连接服务器，请确认后端是否已经启动");
    } finally {
      setIsLoggingIn(false);
    }
  }

  return (
    <div className="login-page">
      <div className="login-container">
        {/* 左侧品牌区域 */}
        <section className="login-brand">
          <div className="login-brand-logo">S</div>

          <div className="login-brand-name">SOFTWARE SERVICE PLATFORM</div>

          <h1>软件服务管理平台</h1>

          <p className="login-brand-description">
            统一管理客户、软件版本、安装包与服务流程，
            让软件交付和售后支持更加清晰、高效。
          </p>

          <div className="login-feature-list">
            <div className="login-feature-item">
              <span>01</span>
              软件版本统一管理
            </div>

            <div className="login-feature-item">
              <span>02</span>
              客户软件授权管理
            </div>

            <div className="login-feature-item">
              <span>03</span>
              安装包集中发布
            </div>

            <div className="login-feature-item">
              <span>04</span>
              售后问题闭环跟踪
            </div>
          </div>
        </section>

        {/* 右侧登录区域 */}
        <section className="login-panel">
          <div className="login-form-wrapper">
            <div className="login-welcome">欢迎回来</div>

            <h2>登录您的账号</h2>

            <p className="login-tip">请输入账号信息进入软件服务管理平台</p>

            {/* 用户名 */}
            <div className="login-field">
              <label>用户名</label>

              <input
                type="text"
                value={username}
                placeholder="请输入用户名"
                autoComplete="username"
                onChange={(e) => setUsername(e.target.value)}
              />
            </div>

            {/* 密码 */}
            <div className="login-field">
              <label>密码</label>

              <input
                type="password"
                value={password}
                placeholder="请输入密码"
                autoComplete="current-password"
                onChange={(e) => setPassword(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    handleLogin();
                  }
                }}
              />
            </div>

            {/* 错误提示 */}
            {errorMessage && <div className="login-error">{errorMessage}</div>}

            {/* 登录按钮 */}
            <button
              type="button"
              className="login-submit"
              disabled={isLoggingIn}
              onClick={handleLogin}
            >
              {isLoggingIn ? "正在登录..." : "登录系统"}
            </button>

            <div className="login-footer">Software Service Platform</div>
          </div>
        </section>
      </div>
    </div>
  );
}

export default LoginPage;
