import { useState } from "react";
import type { FormEvent } from "react";
import { KeyRound } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { apiFetch } from "../services/api";
import { getSessionUser } from "../utils/session";

/**
 * 当前登录用户修改自己的密码。
 *
 * 所有已登录角色都可以使用。
 * 管理员给用户重置密码仍然走“用户管理”页面，
 * 这里专门处理“我修改我自己的密码”。
 */
function ChangePasswordPage() {
  const navigate = useNavigate();
  const currentUser = getSessionUser();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage("");

    if (!currentPassword) {
      setMessage("请输入当前密码");
      return;
    }

    if (newPassword.length < 8) {
      setMessage("新密码不能少于8位");
      return;
    }

    if (newPassword !== confirmPassword) {
      setMessage("两次输入的新密码不一致");
      return;
    }

    try {
      setSaving(true);

      const response = await apiFetch("/api/account/change-password", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          currentPassword,
          newPassword,
          confirmPassword,
        }),
      });

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `修改密码失败：${response.status}`);
      }

      /*
       * 密码修改成功后主动退出当前登录。
       * 即使旧 JWT 还没有自然过期，也不继续复用旧会话。
       */
      sessionStorage.removeItem("access_token");
      sessionStorage.removeItem("current_user");

      alert("密码修改成功，请使用新密码重新登录");
      navigate("/login", { replace: true });
    } catch (error) {
      console.error("修改密码失败：", error);
      setMessage(error instanceof Error ? error.message : "修改密码失败");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="content u-page">
      <header className="u-page-header">
        <div>
          <span className="u-eyebrow">ACCOUNT SECURITY</span>
          <h2>修改密码</h2>
          <p>
            当前账号：
            {currentUser?.displayName || currentUser?.username || "当前用户"}
          </p>
        </div>
      </header>

      <section
        className="u-panel"
        style={{ maxWidth: 560, margin: "0 auto" }}
      >
        <div className="u-panel-head">
          <div>
            <h3 style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <KeyRound size={18} />
              账户密码
            </h3>
            <p>修改成功后会退出当前登录，请使用新密码重新登录。</p>
          </div>
        </div>

        <form onSubmit={submit}>
          <div className="form-section">
            <label>当前密码</label>
            <input
              type="password"
              autoComplete="current-password"
              value={currentPassword}
              onChange={(event) => setCurrentPassword(event.target.value)}
              placeholder="请输入当前密码"
            />
          </div>

          <div className="form-section">
            <label>新密码</label>
            <input
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              placeholder="至少8位"
            />
          </div>

          <div className="form-section">
            <label>确认新密码</label>
            <input
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              placeholder="请再次输入新密码"
            />
          </div>

          {message && (
            <div className="download-record-error" style={{ marginTop: 14 }}>
              {message}
            </div>
          )}

          <div className="form-buttons" style={{ marginTop: 20 }}>
            <button type="submit" className="primary-button" disabled={saving}>
              {saving ? "正在修改..." : "确认修改"}
            </button>

            <button
              type="button"
              className="normal-button"
              disabled={saving}
              onClick={() => navigate(-1)}
            >
              返回
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}

export default ChangePasswordPage;
