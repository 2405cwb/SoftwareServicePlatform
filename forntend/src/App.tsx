import { Link, Navigate, Route, Routes } from "react-router-dom";

import "./App.css";
import CustomerPage from "./pages/CustomerPage";
import SoftwarePage from "./pages/SoftwarePage";

function App() {
  return (
    <div className="page">
      <div className="header">
        <h1>软件服务管理平台</h1>
      </div>
      <div className="main-layout">
        <div className="sidebar">
          <Link to="/customers">客户管理</Link>
          <Link to="/software">软件管理</Link>
        </div>
        <div className="main-content">
          <Routes>
            <Route path="/" element={<Navigate to="/customers" replace />} />

            <Route path="/customers" element={<CustomerPage />} />

            <Route path="/software" element={<SoftwarePage />} />
          </Routes>
        </div>
      </div>
    </div>
  );
}

export default App;
