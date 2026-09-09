import { useEffect, useState } from "react";
import { apiFetch } from "../services/api";

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

  /*
   * 页面第一次打开时查询。
   */
  useEffect(() => {
    loadUsers();
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
      <div className="title-row">
        <div>
          <h2>用户管理</h2>

          <div className="page-subtitle">管理平台账号、角色及客户归属</div>
        </div>
      </div>

      {/* 搜索区域 */}
      <div className="search-row">
        <input
          type="text"
          value={keyword}
          placeholder="搜索用户名、姓名、客户或邮箱"
          onChange={(e) => setKeyword(e.target.value)}
        />
      </div>

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
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default UserPage;
