import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { getPlatformInfo } from "../services/platform";
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
   * ======================================
   * 平台品牌配置
   * ======================================
   */

  /*
   * 平台标题。
   *
   * 后端配置还没有读取成功以前，
   * 先显示默认标题。
   */
  const [platformTitle, setPlatformTitle] = useState("软件服务管理平台");

  /*
   * 公司名称。
   *
   * 如果没有配置，
   * 就保持空字符串。
   */
  const [platformCompanyName, setPlatformCompanyName] = useState("");
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
   * 用于显示：
   *
   * 注册成功，请登录
   */
  const [successMessage, setSuccessMessage] = useState("");

  /*
   * 是否正在登录
   */
  const [isLoggingIn, setIsLoggingIn] = useState(false);

  /*
   * React Router 页面跳转
   */
  const navigate = useNavigate();

  /*
   * 获取上一个页面传过来的路由状态。
   *
   * 注册成功以后 RegisterPage 会传：
   *
   * {
   *   registered: true,
   *   username: "cwb"
   * }
   */
  const location = useLocation();

  const registerState = location.state as {
    registered?: boolean;
    username?: string;
  } | null;
  /*
   * ======================================
   * 加载平台配置
   * ======================================
   *
   * 登录页面没有登录，
   * 但 /api/platform/info
   * 是 AllowAnonymous，
   * 所以可以直接获取。
   */
  useEffect(() => {
    async function loadPlatformInfo() {
      try {
        const info = await getPlatformInfo();

        /*
         * 平台标题。
         */
        setPlatformTitle(info.title);

        /*
         * 公司名称。
         */
        setPlatformCompanyName(info.companyName);

        /*
         * 同步修改浏览器标签页标题。
         */
        document.title = info.title;
      } catch (error) {
        console.error("加载平台配置失败：", error);
      }
    }

    loadPlatformInfo();
  }, []);

  /*
   * ======================================
   * 处理注册成功后的跳转
   * ======================================
   */
  useEffect(() => {
    if (!registerState?.registered) {
      return;
    }

    /*
     * 自动填入刚注册的用户名。
     */
    if (registerState.username) {
      setUsername(registerState.username);
    }

    setSuccessMessage("注册成功，请输入密码登录");

    /*
     * 清掉 history.state。
     *
     * 否则用户刷新登录页时，
     * 可能再次看到“注册成功”提示。
     */
    window.history.replaceState({}, document.title);
  }, []);
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
       * ======================================
       * 登录失败
       * ======================================
       *
       * 后端当前登录失败返回的是普通字符串：
       *
       * "用户名或密码错误"
       * "当前用户已停用"
       * "所属客户已停用"
       *
       * 所以失败时不能直接 response.json()，
       * 否则会因为 JSON 解析失败进入 catch，
       * 最终被误认为“服务器连接失败”。
       */
      if (!response.ok) {
        const errorText = await response.text();

        setErrorMessage(errorText || `登录失败：${response.status}`);

        return;
      }

      /*
       * 登录成功时后端返回的是 JSON：
       *
       * {
       *   message,
       *   token,
       *   user
       * }
       */
      const data = await response.json();

      /*
       * 转成登录返回类型。
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
      navigate("/", {
        replace: true,
      });
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

          <h1>{platformTitle}</h1>
          {platformCompanyName && (
            <div className="login-company-name">{platformCompanyName}</div>
          )}

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

            <p className="login-tip">
              请输入账号信息进入
              {platformTitle}
            </p>

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
            {/* 注册成功提示 */}
            {successMessage && (
              <div className="login-success">{successMessage}</div>
            )}
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
            <div className="register-login-link">
              还没有账号？
              <Link to="/register">注册客户账号</Link>
            </div>
            <div className="login-footer">
              {platformCompanyName ? platformCompanyName : platformTitle}
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}

export default LoginPage;
