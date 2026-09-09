import { Navigate, Route, Routes } from "react-router-dom";

import MainLayout from "./layouts/MainLayout";

import LoginPage from "./pages/LoginPage";
import CustomerPage from "./pages/CustomerPage";
import SoftwarePage from "./pages/SoftwarePage";
import VersionPage from "./pages/VersionPage";

function App() {
  return (
    <Routes>
      {/* 登录页面：独立于后台主界面 */}
      <Route path="/login" element={<LoginPage />} />

      {/* 后台管理页面 */}
      <Route element={<MainLayout />}>
        <Route path="/customers" element={<CustomerPage />} />

        <Route path="/software" element={<SoftwarePage />} />

        <Route path="/versions" element={<VersionPage />} />
      </Route>

      {/* 默认进入客户管理 */}
      <Route path="/" element={<Navigate to="/customers" replace />} />
    </Routes>
  );
}

export default App;
