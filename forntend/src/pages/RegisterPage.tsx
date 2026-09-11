import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";

function RegisterPage() {
  const navigate = useNavigate();

 

  const [customerCode, setCustomerCode] = useState("");

  const [username, setUsername] = useState("");

  const [displayName, setDisplayName] = useState("");

  const [password, setPassword] = useState("");

  const [confirmPassword, setConfirmPassword] = useState("");

  const [email, setEmail] = useState("");

  const [phone, setPhone] = useState("");

  const [errorMessage, setErrorMessage] = useState("");

  

  const [isRegistering, setIsRegistering] = useState(false);

  /*
   * ==========================================
   * 注册
   * ==========================================
   */
  async function handleRegister() {
    setErrorMessage("");

    /*
     * 客户编码。
     */
    if (!customerCode.trim()) {
      setErrorMessage("请输入客户编码");

      return;
    }

    /*
     * 用户名。
     */
    if (!username.trim()) {
      setErrorMessage("请输入用户名");

      return;
    }

    /*
     * 用户姓名。
     */
    if (!displayName.trim()) {
      setErrorMessage("请输入姓名");

      return;
    }

    /*
     * 密码至少8位。
     *
     * 后端也会再次验证，
     * 前端这里只是方便用户。
     */
    if (password.length < 8) {
      setErrorMessage("密码不能少于8位");

      return;
    }

    /*
     * 两次密码必须一致。
     *
     * confirmPassword 不需要发送给后端。
     */
    if (password !== confirmPassword) {
      setErrorMessage("两次输入的密码不一致");

      return;
    }

    try {
      setIsRegistering(true);

      /*
       * 注册时还没有 JWT，
       * 所以这里直接使用 fetch，
       * 不使用需要登录身份的业务接口。
       */
      const response = await fetch("/api/auth/register", {
        method: "POST",

        headers: {
          "Content-Type": "application/json",
        },

        body: JSON.stringify({
          customerCode: customerCode.trim(),

          username: username.trim(),

          displayName: displayName.trim(),

          password,

          email: email.trim(),

          phone: phone.trim(),
        }),
      });

      /*
       * 后端错误目前可能直接返回字符串，
       * 所以这里用 text() 比 json() 更稳。
       */
      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `注册失败：${response.status}`);
      }

      /*
       * 注册成功后跳回登录页。
       *
       * 通过 React Router 的 state
       * 把刚注册的用户名带过去。
       *
       * 不需要放到 localStorage，
       * 也不会显示在 URL 中。
       */
      navigate("/login", {
        replace: true,

        state: {
          registered: true,

          username: username.trim(),
        },
      });
    } catch (error) {
      console.error("注册失败：", error);

      setErrorMessage(error instanceof Error ? error.message : "注册失败");
    } finally {
      setIsRegistering(false);
    }
  }

  return (
    <div className="login-page">
      <div className="login-container">
        {/* 左侧说明 */}
        <section className="login-brand">
          <div className="login-brand-logo">S</div>

          <div className="login-brand-name">SOFTWARE SERVICE PLATFORM</div>

          <h1>软件服务管理平台</h1>

          <div className="login-feature-list">
            <div className="login-feature-item">
              <span>01</span>
              注册客户账号
            </div>

            <div className="login-feature-item">
              <span>02</span>
              查看已授权软件
            </div>

            <div className="login-feature-item">
              <span>03</span>
              下载软件版本
            </div>

            <div className="login-feature-item">
              <span>04</span>
              提交售后工单
            </div>
          </div>
        </section>

        {/* 注册表单 */}
        <section className="login-panel">
          <div className="login-form-wrapper register-form-wrapper">
            <div className="login-welcome">创建账号</div>

            <h2>客户用户注册</h2>

            <p className="login-tip">客户编码请向软件服务管理员获取</p>

            <div className="register-form-grid">
              <div className="login-field">
                <label>客户编码</label>

                <input
                  type="text"
                  value={customerCode}
                  placeholder="例如 WH001"
                  onChange={(e) => setCustomerCode(e.target.value)}
                />
              </div>

              <div className="login-field">
                <label>用户名</label>

                <input
                  type="text"
                  value={username}
                  placeholder="用于登录"
                  autoComplete="username"
                  onChange={(e) => setUsername(e.target.value)}
                />
              </div>

              <div className="login-field">
                <label>姓名</label>

                <input
                  type="text"
                  value={displayName}
                  placeholder="请输入姓名"
                  onChange={(e) => setDisplayName(e.target.value)}
                />
              </div>

              <div className="login-field">
                <label>手机号</label>

                <input
                  type="text"
                  value={phone}
                  placeholder="选填"
                  onChange={(e) => setPhone(e.target.value)}
                />
              </div>

              <div className="login-field">
                <label>邮箱</label>

                <input
                  type="email"
                  value={email}
                  placeholder="选填"
                  onChange={(e) => setEmail(e.target.value)}
                />
              </div>

              <div className="login-field">
                <label>密码</label>

                <input
                  type="password"
                  value={password}
                  placeholder="至少8位"
                  autoComplete="new-password"
                  onChange={(e) => setPassword(e.target.value)}
                />
              </div>

              <div className="login-field">
                <label>确认密码</label>

                <input
                  type="password"
                  value={confirmPassword}
                  placeholder="再次输入密码"
                  autoComplete="new-password"
                  onChange={(e) => setConfirmPassword(e.target.value)}
                />
              </div>
            </div>

            {errorMessage && <div className="login-error">{errorMessage}</div>}

            <button
              type="button"
              className="login-submit"
              disabled={isRegistering}
              onClick={handleRegister}
            >
              {isRegistering ? "正在注册..." : "注册账号"}
            </button>

            <div className="register-login-link">
              已有账号？
              <Link to="/login">返回登录</Link>
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}

export default RegisterPage;
