import { Route, Routes } from "react-router-dom";

import "./App.css";

import MainLayout from "./layouts/MainLayout";

import RequireAuth from "./components/RequireAuth";
import RequireRole from "./components/RequireRole";
import HomeRedirect from "./components/HomeRedirect";

import LoginPage from "./pages/LoginPage";
import CustomerPage from "./pages/CustomerPage";
import SoftwarePage from "./pages/SoftwarePage";
import VersionPage from "./pages/VersionPage";
import ForbiddenPage from "./pages/ForbiddenPage";
import MySoftwarePage from "./pages/MySoftwarePage";
import UserPage from "./pages/UserPage";
import TicketPage from "./pages/TicketPage";
function App() {
  return (
    <Routes>
      {/* =====================================================
          登录页面

          登录页面不需要后台布局，
          也不需要 RequireAuth。
          ===================================================== */}
      <Route path="/login" element={<LoginPage />} />

      {/* =====================================================
          以下所有页面都要求先登录
          ===================================================== */}
      <Route element={<RequireAuth />}>
        {/* ===================================================
            所有登录后的页面统一使用后台布局

            MainLayout 包含：

            顶部 Header
            左侧菜单
            右侧 Outlet
            =================================================== */}
        <Route element={<MainLayout />}>
          {/* =================================================
              默认首页

              根据当前用户角色自动跳转：

              Admin / Support / Sales
              → /customers

              Developer
              → /software

              Customer
              → /my-software
              ================================================= */}
          <Route path="/" element={<HomeRedirect />} />

          {/* =================================================
              403 没有权限页面

              这里只要求已经登录，
              不限制角色。

              否则可能出现：
              没权限 → /forbidden
              /forbidden 又没权限
              → 无限跳转
              ================================================= */}
          <Route path="/forbidden" element={<ForbiddenPage />} />

          {/* =================================================
              用户管理

              仅 Admin 可以访问
              ================================================= */}
          <Route element={<RequireRole allowedRoles={["Admin"]} />}>
            <Route path="/users" element={<UserPage />} />
          </Route>

          {/* =================================================
              客户管理

              Admin
              Support
              Sales
              ================================================= */}
          <Route
            element={
              <RequireRole allowedRoles={["Admin", "Support", "Sales"]} />
            }
          >
            <Route path="/customers" element={<CustomerPage />} />
          </Route>

          {/* =================================================
              软件管理

              Admin
              Support
              Developer
              ================================================= */}
          <Route
            element={
              <RequireRole allowedRoles={["Admin", "Support", "Developer"]} />
            }
          >
            <Route path="/software" element={<SoftwarePage />} />
          </Route>

          {/* =================================================
              版本管理

              Admin
              Support
              Developer
              ================================================= */}
          <Route
            element={
              <RequireRole allowedRoles={["Admin", "Support", "Developer"]} />
            }
          >
            <Route path="/versions" element={<VersionPage />} />
          </Route>
          {/* =================================================
    工单管理

    Admin
    Support
    Developer
    Customer

    Sales 当前暂时没有工单权限。
    ================================================= */}
          <Route
            element={
              <RequireRole
                allowedRoles={["Admin", "Support", "Developer", "Customer"]}
              />
            }
          >
            <Route path="/tickets" element={<TicketPage />} />
          </Route>
          {/* =================================================
              客户门户 - 我的软件

              仅 Customer 可以访问
              ================================================= */}
          <Route element={<RequireRole allowedRoles={["Customer"]} />}>
            <Route path="/my-software" element={<MySoftwarePage />} />
          </Route>
        </Route>
      </Route>
    </Routes>
  );
}

export default App;
