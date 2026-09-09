import { NavLink, Outlet } from "react-router-dom";

function MainLayout() {
  return (
    <div className="page">
      {/* 顶部标题 */}
      <div className="header">
        <h1>软件服务管理平台</h1>

        <p>客户、软件版本与服务统一管理</p>
      </div>

      {/* 后台主体 */}
      <div className="main-layout">
        {/* 左侧菜单 */}
        <div className="sidebar">
          <NavLink to="/customers">客户管理</NavLink>

          <NavLink to="/software">软件管理</NavLink>

          <NavLink to="/versions">版本管理</NavLink>
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
