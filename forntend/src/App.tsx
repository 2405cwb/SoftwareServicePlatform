import { Navigate, Route, Routes } from "react-router-dom";

import "./App.css";

import MainLayout from "./layouts/MainLayout";

import RequireAuth from "./components/RequireAuth";
import RequireRole from "./components/RequireRole";

import LoginPage from "./pages/LoginPage";
import CustomerPage from "./pages/CustomerPage";
import SoftwarePage from "./pages/SoftwarePage";
import VersionPage from "./pages/VersionPage";
import ForbiddenPage from "./pages/ForbiddenPage";
import HomeRedirect from "./components/HomeRedirect";
import MySoftwarePage from "./pages/MySoftwarePage";
function App() {
  return (
    <Routes>
      {/* 登录页面 */}
      <Route path="/login" element={<LoginPage />} />

      {/*
       * ===============================
       * 以下页面全部要求先登录
       * ===============================
       */}
      <Route element={<RequireAuth />}>
        <Route element={<MainLayout />}>
          {
            <Route element={<RequireRole allowedRoles={["Customer"]} />}>
              <Route path="/my-software" element={<MySoftwarePage />} />
            </Route>

            /*
             * 403 页面
             *
             * 只要求登录，
             * 不要求特定角色。
             */
          }
          <Route path="/forbidden" element={<ForbiddenPage />} />

          {/*
           * ===========================
           * 客户管理
           *
           * Admin
           * Support
           * Sales
           * ===========================
           */}
          <Route
            element={
              <RequireRole allowedRoles={["Admin", "Support", "Sales"]} />
            }
          >
            <Route path="/customers" element={<CustomerPage />} />
          </Route>

          {/*
           * ===========================
           * 软件管理
           *
           * Admin
           * Support
           * Developer
           * ===========================
           */}
          <Route
            element={
              <RequireRole allowedRoles={["Admin", "Support", "Developer"]} />
            }
          >
            <Route path="/software" element={<SoftwarePage />} />
          </Route>

          {/*
           * ===========================
           * 版本管理
           *
           * Admin
           * Support
           * Developer
           * ===========================
           */}
          <Route
            element={
              <RequireRole allowedRoles={["Admin", "Support", "Developer"]} />
            }
          >
            <Route path="/versions" element={<VersionPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="/" element={<HomeRedirect />} />
    </Routes>
  );
}

export default App;
