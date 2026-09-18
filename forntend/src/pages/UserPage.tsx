import { useEffect, useState } from "react";
import { apiFetch } from "../services/api";
import SearchBar from "../components/SearchBar";
import PageHeader from "../components/PageHeader";

interface CustomerOption {
  id: number;

  name: string;

  code: string;

  isEnabled: boolean;
}
/*
 * ======================================
 * 外部通知账号绑定
 * ======================================
 *
 * 当前第一版只使用 DingTalk。
 *
 * 后端接口：
 *
 * GET
 * /api/external-user-bindings/user/{userId}
 */
interface ExternalUserBindingItem {
  id: number;

  userId: number;

  channel: string;

  externalUserId: string | null;

  mobile: string | null;

  isEnabled: boolean;

  createdAt: string;

  updatedAt: string;
}
/*
 * 后端 GET /api/users 返回的用户结构。
 */
interface UserItem {
  id: number;

  username: string;

  displayName: string;

  role: string;

  email: string;

  phone: string;

  isEnabled: boolean;

  customerId: number | null;

  customerName: string;

  lastLoginAt: string | null;

  createdAt: string;

  updatedAt: string;
}

function UserPage() {
  /*
   * 用户列表。
   */
  const [users, setUsers] = useState<UserItem[]>([]);

  /*
   * 页面加载状态。
   */
  const [loading, setLoading] = useState(true);

  /*
   * 错误信息。
   */
  const [errorMessage, setErrorMessage] = useState("");

  /*
   * 搜索关键字。
   *
   * 这一版先在前端过滤，
   * 后面用户数量大了再改成后端分页搜索。
   */
  const [keyword, setKeyword] = useState("");
  /*
   * 是否显示新增用户区域。
   */
  const [showCreateUser, setShowCreateUser] = useState(false);

  /*
   * 新增用户表单。
   */
  const [newUsername, setNewUsername] = useState("");

  const [newPassword, setNewPassword] = useState("");

  const [newDisplayName, setNewDisplayName] = useState("");

  const [newRole, setNewRole] = useState("Developer");

  const [newEmail, setNewEmail] = useState("");

  const [newPhone, setNewPhone] = useState("");

  /*
   * 当前正在编辑哪个用户。
   *
   * null：
   * 当前是“新增用户”
   *
   * 有数字：
   * 当前是“编辑用户”
   */
  const [editingUserId, setEditingUserId] = useState<number | null>(null);

  /*
   * 编辑用户时使用。
   *
   * 新增账号默认启用。
   */
  const [newIsEnabled, setNewIsEnabled] = useState(true);

  /*
   * 是否正在保存。
   *
   * 防止用户连续点击两次“保存”。
   */
  const [savingUser, setSavingUser] = useState(false);
  /*
   * ======================================
   * 重置密码相关状态
   * ======================================
   */

  /*
   * 当前准备重置密码的用户。
   *
   * null：
   * 当前没有打开重置密码区域。
   */
  const [resetPasswordUser, setResetPasswordUser] = useState<UserItem | null>(
    null,
  );

  /*
   * 管理员输入的新密码。
   */
  const [resetPassword, setResetPassword] = useState("");

  /*
   * 防止重复提交。
   */
  const [resetPasswordSaving, setResetPasswordSaving] = useState(false);

  /*
   * ======================================
   * 钉钉账号绑定相关状态
   * ======================================
   */

  /*
   * 当前正在配置钉钉账号的系统用户。
   *
   * null：
   * 没有打开钉钉绑定编辑区域。
   */
  const [dingTalkBindingUser, setDingTalkBindingUser] =
    useState<UserItem | null>(null);

  /*
   * 钉钉绑定手机号。
   *
   * 注意：
   *
   * 这里不是 Users.Phone。
   *
   * Users.Phone：
   * 系统中的联系电话。
   *
   * DingTalk Mobile：
   * 钉钉账号实际绑定的手机号。
   */
  const [dingTalkMobile, setDingTalkMobile] = useState("");

  /*
   * 钉钉外部用户ID。
   *
   * 当前群机器人第一版主要使用手机号 @。
   *
   * 这个字段先保留，
   * 后面如果接入钉钉通讯录 API 可以直接使用。
   */
  const [dingTalkExternalUserId, setDingTalkExternalUserId] = useState("");

  /*
   * 当前绑定是否启用。
   */
  const [dingTalkBindingEnabled, setDingTalkBindingEnabled] = useState(true);

  /*
   * 正在读取绑定。
   */
  const [dingTalkBindingLoading, setDingTalkBindingLoading] = useState(false);

  /*
   * 正在保存绑定。
   */
  const [dingTalkBindingSaving, setDingTalkBindingSaving] = useState(false);
  /*
   * 系统客户列表。
   *
   * 编辑 Customer 用户时选择所属客户。
   */
  const [customers, setCustomers] = useState<CustomerOption[]>([]);

  /*
   * 当前表单选择的客户ID。
   *
   * 空字符串代表没有选择。
   */
  const [newCustomerId, setNewCustomerId] = useState<number | "">("");

  /*
   * ======================================
   * 查询用户
   * ======================================
   */
  async function loadUsers() {
    try {
      setLoading(true);

      setErrorMessage("");

      /*
       * apiFetch 会自动携带：
       *
       * Authorization: Bearer xxx
       */
      const response = await apiFetch("/api/users");

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取用户失败：${response.status}`);
      }

      const data = (await response.json()) as UserItem[];

      setUsers(data);
    } catch (error) {
      console.error("加载用户列表失败：", error);

      setErrorMessage("加载用户列表失败");
    } finally {
      setLoading(false);
    }
  }
  function clearCreateUserForm() {
    setNewUsername("");

    setNewPassword("");

    setNewDisplayName("");

    setNewRole("Developer");

    setNewEmail("");

    setNewPhone("");
    setNewIsEnabled(true);
    setNewCustomerId("");
    setEditingUserId(null);
  }
  /*
   * ======================================
   * 查询客户列表
   * ======================================
   *
   * 用途：
   *
   * 当编辑 Customer 用户时，
   * “所属客户”下拉框需要显示系统中的客户。
   */
  async function loadCustomers() {
    try {
      /*
       * 调用现有客户接口。
       *
       * 当前 UserPage 只有 Admin 可以访问，
       * Admin 本身也有 /api/customers 的访问权限。
       */
      const response = await apiFetch("/api/customers");

      /*
       * 请求失败。
       */
      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取客户列表失败：${response.status}`);
      }

      /*
       * 把后端返回的 JSON
       * 转成 CustomerOption[]。
       */
      const data = (await response.json()) as CustomerOption[];

      /*
       * 保存到 React 状态。
       *
       * 后面的“所属客户”下拉框
       * 就会使用 customers。
       */
      setCustomers(data);
    } catch (error) {
      console.error("加载客户列表失败：", error);
    }
  }
  async function saveUser() {
    /*
     * 前端先做最基础检查。
     *
     * 真正的安全校验仍然必须以后端为准。
     */
    if (!newUsername.trim()) {
      alert("请输入用户名");

      return;
    }

    if (!newDisplayName.trim()) {
      alert("请输入姓名");

      return;
    }

    /*
     * 只有新增用户时才必须输入密码。
     *
     * 编辑用户不修改密码。
     */
    if (editingUserId === null && newPassword.length < 8) {
      alert("密码不能少于8位");

      return;
    }

    try {
      setSavingUser(true);

      let response: Response;

      /*
       * ======================================
       * 新增用户
       * ======================================
       */
      if (editingUserId === null) {
        response = await apiFetch("/api/users", {
          method: "POST",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            username: newUsername.trim(),

            password: newPassword,

            displayName: newDisplayName.trim(),

            role: newRole,

            email: newEmail.trim(),

            phone: newPhone.trim(),
          }),
        });
      } else {
        /*
         * ======================================
         * 编辑用户
         * ======================================
         */
        response = await apiFetch(`/api/users/${editingUserId}`, {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            displayName: newDisplayName.trim(),

            role: newRole,

            email: newEmail.trim(),

            phone: newPhone.trim(),

            isEnabled: newIsEnabled,

            customerId: newRole === "Customer" ? newCustomerId : null,
          }),
        });
      }

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `创建用户失败：${response.status}`);
      }

      /*
       * 创建成功以后：
       *
       * 1. 清空表单
       * 2. 关闭新增区域
       * 3. 重新加载用户列表
       */
      clearCreateUserForm();

      setShowCreateUser(false);

      await loadUsers();
    } catch (error) {
      console.error("创建用户失败：", error);

      alert("创建用户失败：" + String(error));
    } finally {
      setSavingUser(false);
    }
  }
  /*
   * ======================================
   * 管理员重置用户密码
   * ======================================
   */
  async function saveResetPassword() {
    /*
     * 没有选择用户时不执行。
     */
    if (resetPasswordUser === null) {
      return;
    }

    /*
     * 当前后端规则：
     *
     * 密码至少8位。
     */
    if (resetPassword.length < 8) {
      alert("新密码不能少于8位");

      return;
    }

    try {
      setResetPasswordSaving(true);

      /*
       * 调用后端：
       *
       * POST
       * /api/users/{id}/reset-password
       */
      const response = await apiFetch(
        `/api/users/${resetPasswordUser.id}/reset-password`,
        {
          method: "POST",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            newPassword: resetPassword,
          }),
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `重置密码失败：${response.status}`);
      }

      /*
       * 重置成功。
       */
      alert("密码重置成功");

      /*
       * 清空状态并关闭区域。
       */
      setResetPassword("");

      setResetPasswordUser(null);
    } catch (error) {
      console.error("重置密码失败：", error);

      alert("重置密码失败：" + String(error));
    } finally {
      setResetPasswordSaving(false);
    }
  }
  /*
   * ======================================
   * 打开钉钉账号绑定区域
   * ======================================
   */
  async function openDingTalkBinding(user: UserItem) {
    /*
     * 先记录当前用户。
     *
     * 页面会显示：
     *
     * 当前用户：张三
     */
    setDingTalkBindingUser(user);

    /*
     * 每次打开先清空旧状态。
     *
     * 防止：
     *
     * 上一次编辑张三
     * ↓
     * 再打开李四
     * ↓
     * 短暂显示张三手机号
     */
    setDingTalkMobile("");

    setDingTalkExternalUserId("");

    setDingTalkBindingEnabled(true);

    try {
      setDingTalkBindingLoading(true);

      /*
       * 查询当前用户所有外部平台绑定。
       */
      const response = await apiFetch(
        `/api/external-user-bindings/user/${user.id}`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `读取钉钉绑定失败：${response.status}`);
      }

      const bindings = (await response.json()) as ExternalUserBindingItem[];

      /*
       * 当前只取 DingTalk。
       *
       * 后面即使这个接口同时返回：
       *
       * WeCom
       * Feishu
       *
       * 这里也不会互相干扰。
       */
      const dingTalkBinding = bindings.find(
        (item) => item.channel === "DingTalk",
      );

      /*
       * 没有绑定属于正常情况。
       *
       * 说明管理员是第一次配置。
       */
      if (!dingTalkBinding) {
        return;
      }

      setDingTalkMobile(dingTalkBinding.mobile ?? "");

      setDingTalkExternalUserId(dingTalkBinding.externalUserId ?? "");

      setDingTalkBindingEnabled(dingTalkBinding.isEnabled);
    } catch (error) {
      console.error("加载钉钉绑定失败：", error);

      alert(error instanceof Error ? error.message : "加载钉钉绑定失败");
    } finally {
      setDingTalkBindingLoading(false);
    }
  } /*
   * ======================================
   * 保存钉钉账号绑定
   * ======================================
   */
  async function saveDingTalkBinding() {
    /*
     * 没有选中用户时不执行。
     */
    if (dingTalkBindingUser === null) {
      return;
    }

    /*
     * 当前第一版主要依赖手机号
     * 做钉钉群机器人 @。
     *
     * 如果绑定启用，
     * 至少需要填写手机号
     * 或 ExternalUserId。
     *
     * 后端还有第二层校验。
     */
    if (
      dingTalkBindingEnabled &&
      !dingTalkMobile.trim() &&
      !dingTalkExternalUserId.trim()
    ) {
      alert("启用钉钉绑定时，请至少填写钉钉手机号或外部用户ID");

      return;
    }

    try {
      setDingTalkBindingSaving(true);

      /*
       * 调用后端 Upsert 接口。
       *
       * 第一次：
       * 创建绑定。
       *
       * 已经存在：
       * 更新绑定。
       */
      const response = await apiFetch(
        `/api/external-user-bindings/${dingTalkBindingUser.id}/DingTalk`,
        {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            externalUserId: dingTalkExternalUserId.trim() || null,

            mobile: dingTalkMobile.trim() || null,

            isEnabled: dingTalkBindingEnabled,
          }),
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `保存钉钉绑定失败：${response.status}`);
      }

      alert("钉钉账号绑定保存成功");

      /*
       * 保存后关闭编辑区域。
       */
      setDingTalkBindingUser(null);

      setDingTalkMobile("");

      setDingTalkExternalUserId("");
    } catch (error) {
      console.error("保存钉钉绑定失败：", error);

      alert(error instanceof Error ? error.message : "保存钉钉绑定失败");
    } finally {
      setDingTalkBindingSaving(false);
    }
  }
  function editUser(user: UserItem) {
    setEditingUserId(user.id);

    setNewUsername(user.username);

    setNewDisplayName(user.displayName);

    setNewRole(user.role);

    setNewEmail(user.email);

    setNewPhone(user.phone);

    setNewIsEnabled(user.isEnabled);
    setNewCustomerId(user.customerId ?? "");
    /*
     * 编辑时不需要填写密码。
     *
     * 密码重置我们后面单独做。
     */
    setNewPassword("");

    /*
     * 打开表单。
     */
    setShowCreateUser(true);
  }

  /*
   * 页面第一次打开时查询。
   */
  useEffect(() => {
    loadUsers();

    loadCustomers();
  }, []);

  /*
   * ======================================
   * 角色英文 → 中文
   * ======================================
   */
  function getRoleName(role: string) {
    switch (role) {
      case "Admin":
        return "管理员";

      case "Support":
        return "售后";

      case "Developer":
        return "开发";

      case "Sales":
        return "销售";

      case "Customer":
        return "客户";

      default:
        return role;
    }
  }

  /*
   * ======================================
   * 日期格式化
   * ======================================
   */
  function formatDate(dateText: string | null) {
    if (!dateText) {
      return "-";
    }

    return new Date(dateText).toLocaleString();
  }

  /*
   * ======================================
   * 搜索过滤
   * ======================================
   */
  const filteredUsers = users.filter((user) => {
    const searchText = keyword.trim().toLowerCase();

    if (!searchText) {
      return true;
    }

    return (
      user.username.toLowerCase().includes(searchText) ||
      user.displayName.toLowerCase().includes(searchText) ||
      user.customerName.toLowerCase().includes(searchText) ||
      user.email.toLowerCase().includes(searchText)
    );
  });

  return (
    <div className="content">
      {/* 页面标题 */}
      <PageHeader
        title="用户管理"
        buttonText="+ 新增用户"
        onButtonClick={() => {
          clearCreateUserForm();

          setShowCreateUser(true);
        }}
      />
      {/* 搜索区域 */}

      {showCreateUser && (
        <div className="form-box">
          <h3>{editingUserId === null ? "新增内部用户" : "编辑内部用户"}</h3>

          <div className="form-section">
            <h4>账号信息</h4>

            <div className="form-grid">
              {/* 用户名 */}
              <div className="form-item">
                <label>用户名：</label>

                <input
                  type="text"
                  placeholder="例如：zhangsan"
                  value={newUsername}
                  disabled={editingUserId !== null}
                  onChange={(e) => setNewUsername(e.target.value)}
                />
              </div>

              {/* 姓名 */}
              <div className="form-item">
                <label>姓名：</label>

                <input
                  type="text"
                  placeholder="例如：张三"
                  value={newDisplayName}
                  onChange={(e) => setNewDisplayName(e.target.value)}
                />
              </div>

              {/* 初始密码 */}
              {editingUserId === null && (
                <div className="form-item">
                  <label>初始密码：</label>

                  <input
                    type="password"
                    placeholder="至少8位"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                  />
                </div>
              )}

              {/* 角色 */}
              <div className="form-item">
                <label>角色：</label>

                <select
                  value={newRole}
                  onChange={(e) => setNewRole(e.target.value)}
                >
                  <option value="Admin">管理员</option>

                  <option value="Support">售后人员</option>

                  <option value="Developer">开发人员</option>

                  <option value="Sales">销售人员</option>

                  {editingUserId !== null && (
                    <option value="Customer">客户用户</option>
                  )}
                </select>
              </div>
              {newRole === "Customer" && (
                <div className="form-item">
                  <label>所属客户：</label>

                  <select
                    value={newCustomerId}
                    onChange={(e) => {
                      const value = e.target.value;

                      setNewCustomerId(value === "" ? "" : Number(value));
                    }}
                  >
                    <option value="">请选择客户</option>

                    {customers
                      .filter((customer) => customer.isEnabled)
                      .map((customer) => (
                        <option key={customer.id} value={customer.id}>
                          {customer.name}
                          {customer.code ? ` (${customer.code})` : ""}
                        </option>
                      ))}
                  </select>
                </div>
              )}
              {editingUserId !== null && (
                <div className="form-item">
                  <label>账号状态：</label>

                  <select
                    value={newIsEnabled ? "enabled" : "disabled"}
                    onChange={(e) =>
                      setNewIsEnabled(e.target.value === "enabled")
                    }
                  >
                    <option value="enabled">启用</option>

                    <option value="disabled">停用</option>
                  </select>
                </div>
              )}
              {/* 手机 */}
              <div className="form-item">
                <label>手机：</label>

                <input
                  type="text"
                  placeholder="请输入手机号"
                  value={newPhone}
                  onChange={(e) => setNewPhone(e.target.value)}
                />
              </div>

              {/* 邮箱 */}
              <div className="form-item">
                <label>邮箱：</label>

                <input
                  type="email"
                  placeholder="请输入邮箱"
                  value={newEmail}
                  onChange={(e) => setNewEmail(e.target.value)}
                />
              </div>
            </div>
          </div>

          <div className="form-buttons">
            <button
              type="button"
              className="primary-button"
              disabled={savingUser}
              onClick={saveUser}
            >
              {savingUser
                ? "正在保存..."
                : editingUserId === null
                  ? "创建用户"
                  : "保存修改"}
            </button>

            <button
              type="button"
              className="normal-button"
              disabled={savingUser}
              onClick={() => {
                clearCreateUserForm();

                setShowCreateUser(false);
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}
      {/* ======================================
    重置密码区域
    ====================================== */}
      {resetPasswordUser !== null && (
        <div className="form-box">
          <h3>重置用户密码</h3>

          <div className="form-section">
            <h4>
              当前用户：
              {resetPasswordUser.displayName || resetPasswordUser.username}
            </h4>

            <div className="form-grid">
              {/* 用户名 */}
              <div className="form-item">
                <label>用户名：</label>

                <input
                  type="text"
                  value={resetPasswordUser.username}
                  disabled
                />
              </div>

              {/* 新密码 */}
              <div className="form-item">
                <label>新密码：</label>

                <input
                  type="password"
                  placeholder="请输入至少8位的新密码"
                  value={resetPassword}
                  onChange={(e) => setResetPassword(e.target.value)}
                />
              </div>
            </div>
          </div>

          <div className="form-buttons">
            {/* 确认重置 */}
            <button
              type="button"
              className="primary-button"
              disabled={resetPasswordSaving}
              onClick={saveResetPassword}
            >
              {resetPasswordSaving ? "正在重置..." : "确认重置"}
            </button>

            {/* 取消 */}
            <button
              type="button"
              className="normal-button"
              disabled={resetPasswordSaving}
              onClick={() => {
                setResetPassword("");

                setResetPasswordUser(null);
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}
      {/* ======================================
    钉钉账号绑定
    ====================================== */}
      {dingTalkBindingUser !== null && (
        <div className="form-box">
          <h3>钉钉通知账号绑定</h3>

          <div className="form-section">
            <h4>
              当前用户：
              {dingTalkBindingUser.displayName || dingTalkBindingUser.username}
            </h4>

            {dingTalkBindingLoading ? (
              <div className="empty-state">正在读取钉钉绑定...</div>
            ) : (
              <div className="form-grid">
                {/* 系统账号 */}
                <div className="form-item">
                  <label>系统账号：</label>

                  <input
                    type="text"
                    value={dingTalkBindingUser.username}
                    disabled
                  />
                </div>

                {/* 用户角色 */}
                <div className="form-item">
                  <label>用户角色：</label>

                  <input
                    type="text"
                    value={getRoleName(dingTalkBindingUser.role)}
                    disabled
                  />
                </div>

                {/* 钉钉手机号 */}
                <div className="form-item">
                  <label>钉钉手机号：</label>

                  <input
                    type="text"
                    placeholder="请输入该用户钉钉绑定手机号"
                    value={dingTalkMobile}
                    onChange={(event) => setDingTalkMobile(event.target.value)}
                  />

                  <div className="table-secondary-text">
                    用于钉钉群机器人 @ 对应用户
                  </div>
                </div>

                {/* 钉钉 UserId */}
                <div className="form-item">
                  <label>钉钉 UserId：</label>

                  <input
                    type="text"
                    placeholder="当前可以暂不填写"
                    value={dingTalkExternalUserId}
                    onChange={(event) =>
                      setDingTalkExternalUserId(event.target.value)
                    }
                  />

                  <div className="table-secondary-text">
                    为后续接入钉钉通讯录能力预留
                  </div>
                </div>

                {/* 绑定状态 */}
                <div className="form-item">
                  <label>绑定状态：</label>

                  <select
                    value={dingTalkBindingEnabled ? "enabled" : "disabled"}
                    onChange={(event) =>
                      setDingTalkBindingEnabled(
                        event.target.value === "enabled",
                      )
                    }
                  >
                    <option value="enabled">启用</option>

                    <option value="disabled">停用</option>
                  </select>
                </div>
              </div>
            )}
          </div>

          <div className="form-buttons">
            <button
              type="button"
              className="primary-button"
              disabled={dingTalkBindingLoading || dingTalkBindingSaving}
              onClick={() => void saveDingTalkBinding()}
            >
              {dingTalkBindingSaving ? "正在保存..." : "保存钉钉绑定"}
            </button>

            <button
              type="button"
              className="normal-button"
              disabled={dingTalkBindingSaving}
              onClick={() => {
                setDingTalkBindingUser(null);

                setDingTalkMobile("");

                setDingTalkExternalUserId("");
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}
      <SearchBar
        value={keyword}
        placeholder="搜索用户名、姓名、客户或邮箱"
        onChange={setKeyword}
        onClear={() => setKeyword("")}
      />

      {/* 加载中 */}
      {loading && <div className="empty-state">正在加载用户...</div>}

      {/* 加载失败 */}
      {!loading && errorMessage && (
        <div className="empty-state">
          <div>{errorMessage}</div>

          <button type="button" className="normal-button" onClick={loadUsers}>
            重新加载
          </button>
        </div>
      )}

      {/* 用户列表 */}
      {!loading && !errorMessage && (
        <div className="user-table">
          {/* 表头 */}
          <div className="user-table-header">
            <div>用户名</div>

            <div>姓名</div>

            <div>角色</div>

            <div>所属客户</div>

            <div>联系方式</div>

            <div>状态</div>

            <div>最后登录</div>

            <div>创建时间</div>

            <div>操作</div>
          </div>

          {/* 没有数据 */}
          {filteredUsers.length === 0 && (
            <div className="empty-state">暂无符合条件的用户</div>
          )}

          {/* 数据 */}
          {filteredUsers.map((user) => (
            <div className="user-table-row" key={user.id}>
              {/* 用户名 */}
              <div>
                <strong>{user.username}</strong>
              </div>

              {/* 姓名 */}
              <div>{user.displayName || "-"}</div>

              {/* 角色 */}
              <div>
                <span className="role-badge">{getRoleName(user.role)}</span>
              </div>
              {newRole === "Customer" && (
                <div className="form-item">
                  <label>所属客户：</label>

                  <select
                    value={newCustomerId}
                    onChange={(e) => {
                      const value = e.target.value;

                      setNewCustomerId(value === "" ? "" : Number(value));
                    }}
                  >
                    <option value="">请选择客户</option>

                    {customers
                      .filter((customer) => customer.isEnabled)
                      .map((customer) => (
                        <option key={customer.id} value={customer.id}>
                          {customer.name}
                          {customer.code ? ` (${customer.code})` : ""}
                        </option>
                      ))}
                  </select>
                </div>
              )}
              {/* 客户 */}
              <div>{user.customerName || "-"}</div>

              {/* 联系方式 */}
              <div>
                <div>{user.phone || "-"}</div>

                {user.email && (
                  <div className="table-secondary-text">{user.email}</div>
                )}
              </div>

              {/* 状态 */}
              <div>
                <span
                  className={
                    user.isEnabled ? "status-enabled" : "status-disabled"
                  }
                >
                  {user.isEnabled ? "启用" : "停用"}
                </span>
              </div>

              {/* 最后登录 */}
              <div>{formatDate(user.lastLoginAt)}</div>

              {/* 创建时间 */}
              <div>{formatDate(user.createdAt)}</div>

              <div className="table-actions">
                {/* 编辑用户 */}
                <button
                  type="button"
                  className="normal-button"
                  onClick={() => editUser(user)}
                >
                  编辑
                </button>

                {/* 重置密码 */}
                <button
                  type="button"
                  className="normal-button"
                  onClick={() => {
                    /*
                     * 记录当前准备重置密码的用户。
                     */
                    setResetPasswordUser(user);

                    /*
                     * 每次打开时清空上一次输入。
                     */
                    setResetPassword("");
                  }}
                >
                  重置密码
                </button>
                {/* ======================================
    钉钉通知绑定
    ======================================

    Customer 用户当前主要使用站内通知，
    不需要绑定公司内部钉钉。

    所以第一版只给内部用户显示。
*/}
                {user.role !== "Customer" && (
                  <button
                    type="button"
                    className="normal-button"
                    onClick={() => void openDingTalkBinding(user)}
                  >
                    钉钉绑定
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default UserPage;
