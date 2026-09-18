import { Route, Routes } from "react-router-dom";
import "./App.css";
import "./styles/upgrade.css";

import MainLayout from "./layouts/MainLayout";
import RequireAuth from "./components/RequireAuth";
import RequireRole from "./components/RequireRole";
import HomeRedirect from "./components/HomeRedirect";

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

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<MainLayout />}>
          <Route path="/" element={<HomeRedirect />} />
          <Route path="/forbidden" element={<ForbiddenPage />} />

          {/* 五种角色都有自己的工作台，页面内部按角色取不同数据。 */}
          <Route
            element={
              <RequireRole
                allowedRoles={["Admin", "Support", "Developer", "Sales", "Customer"]}
              />
            }
          >
            <Route path="/dashboard" element={<DashboardPage />} />
          </Route>

          <Route element={<RequireRole allowedRoles={["Admin"]} />}>
            <Route path="/users" element={<UserPage />} />
            <Route path="/sla-settings" element={<SlaSettingsPage />} />
            <Route
              path="/notification-settings"
              element={<NotificationSettingsPage />}
            />
          </Route>

          <Route element={<RequireRole allowedRoles={["Admin", "Support"]} />}>
            <Route path="/download-records" element={<DownloadRecordPage />} />
          </Route>

          <Route element={<RequireRole allowedRoles={["Admin", "Support", "Sales"]} />}>
            <Route path="/customers" element={<CustomerPage />} />
          </Route>

          <Route element={<RequireRole allowedRoles={["Admin", "Support", "Developer"]} />}>
            <Route path="/software" element={<SoftwarePage />} />
            <Route path="/versions" element={<VersionPage />} />
          </Route>

          {/* 客户端自动更新：管理员和开发人员使用。 */}
          <Route element={<RequireRole allowedRoles={["Admin", "Developer"]} />}>
            <Route path="/client-updates" element={<ClientUpdatePage />} />
          </Route>

          <Route
            element={
              <RequireRole allowedRoles={["Admin", "Support", "Developer", "Customer"]} />
            }
          >
            <Route path="/tickets" element={<TicketPage />} />
          </Route>

          <Route element={<RequireRole allowedRoles={["Customer"]} />}>
            <Route path="/my-software" element={<MySoftwarePage />} />
          </Route>
        </Route>
      </Route>
    </Routes>
  );
}

export default App;
