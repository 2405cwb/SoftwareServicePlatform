import { useEffect, useState } from "react";
import "./App.css";
import CustomerPage from "./pages/CustomerPage";

function App() {
  return (
    <div className="page">
      <div className="header">
        <h1>软件服务管理平台</h1>
      </div>

      <CustomerPage />
    </div>
  );
}

export default App;
