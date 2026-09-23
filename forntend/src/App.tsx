import { Route, Routes } from "react-router-dom";
import "./App.css";
import "./styles/upgrade.css";

import MainLayout from "./layouts/MainLayout";
import RequireAuth from "./components/RequireAuth";
import RequireRole from "./components/RequireRole";
import HomeRedirect from "./components/HomeRedirect";
import AccountShell from "./components/AccountShell";

import LoginPage from "./pages/LoginPage";
import RegisterPage from "./pages/RegisterPage";
import CustomerPage from "./pages/CustomerPage";
import SoftwarePage from "./pages/SoftwarePage";
import VersionPage from "./pages/VersionPage";
import ForbiddenPage from "./pages/ForbiddenPage";
import MySoftwarePage from "./pages/MySoftwarePage";
import UserPage from "./pages/UserPage";
import TicketPage from "./pages/TicketPage";
import DashboardPage from "./pages/DashboardPage";
import DownloadRecordPage from "./pages/DownloadRecordPage";
import SlaSettingsPage from "./pages/SlaSettingsPage";
import NotificationSettingsPage from "./pages/NotificationSettingsPage";
import ClientUpdatePage from "./pages/ClientUpdatePage";
import ChangePasswordPage from "./pages/ChangePasswordPage";
import ClientDeviceAdminPage from "./pages/ClientDeviceAdminPage";

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route element={<RequireAuth />}>
        {/*
         * 所有已登录角色都经过 AccountShell，
         * 因此 Admin / Support / Developer / Sales / Customer
         * 都能看到统一的“修改密码”入口。
         */}
        <Route element={<AccountShell />}>
          <Route element={<MainLayout />}>
            <Route path="/" element={<HomeRedirect />} />
            <Route path="/forbidden" element={<ForbiddenPage />} />

            <Route
              element={
                <RequireRole
                  allowedRoles={["Admin", "Support", "Developer", "Sales", "Customer"]}
                />
              }
            >
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/change-password" element={<ChangePasswordPage />} />
            </Route>

            <Route element={<RequireRole allowedRoles={["Admin"]} />}>
              <Route path="/users" element={<UserPage />} />
              <Route path="/client-devices" element={<ClientDeviceAdminPage />} />
              <Route path="/sla-settings" element={<SlaSettingsPage />} />
              <Route
                path="/notification-settings"
                element={<NotificationSettingsPage />}
              />
            </Route>

            <Route element={<RequireRole allowedRoles={["Admin", "Support"]} />}>
              <Route path="/download-records" element={<DownloadRecordPage />} />
            </Route>

            <Route
              element={<RequireRole allowedRoles={["Admin", "Support", "Sales"]} />}
            >
              <Route path="/customers" element={<CustomerPage />} />
            </Route>

            <Route
              element={
                <RequireRole allowedRoles={["Admin", "Support", "Developer"]} />
              }
            >
              <Route path="/software" element={<SoftwarePage />} />
              <Route path="/versions" element={<VersionPage />} />
            </Route>

            <Route
              element={<RequireRole allowedRoles={["Admin", "Developer"]} />}
            >
              <Route path="/client-updates" element={<ClientUpdatePage />} />
            </Route>

            <Route
              element={
                <RequireRole
                  allowedRoles={["Admin", "Support", "Developer", "Customer"]}
                />
              }
            >
              <Route path="/tickets" element={<TicketPage />} />
            </Route>

            <Route element={<RequireRole allowedRoles={["Customer"]} />}>
              <Route path="/my-software" element={<MySoftwarePage />} />
            </Route>
          </Route>
        </Route>
      </Route>
    </Routes>
  );
}

export default App;
