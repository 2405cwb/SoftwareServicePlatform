import { useEffect, useState } from "react";

interface Customer {
  id: number;
  name: string;
}

function CustomerPage() {
  const [message, setMessage] = useState("正在连接后端...");

  const [count, setCount] = useState(0);

  const [showCreateCustomer, setShowCreateCustomer] = useState(false);

  const [customerName, setCustomerName] = useState("");

  const [customers, setCustomers] = useState<Customer[]>([]);

  const [editingCustomerId, setEditingCustomerId] = useState<number | null>(
    null,
  );

  async function saveCustomer() {
    if (customerName.trim() === "") {
      alert("客户名称不能为空");
      return;
    }
    try {
      let response: Response;
      if (editingCustomerId === null) {
        //新增
        response = await fetch("/api/customers", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ name: customerName }),
        });
      } else {
        response = await fetch(`/api/customers/${editingCustomerId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            id: editingCustomerId,
            name: customerName,
          }),
        });
      }

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      setCustomerName("");
      setEditingCustomerId(null);
      setShowCreateCustomer(false);
      await loadCustomers();
    } catch (error) {
      console.error("保存客户失败:", error);
    }
  }

  async function loadCustomers() {
    try {
      const response = await fetch("/api/customers");
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      const data: Customer[] = await response.json();
      setCustomers(data);
    } catch (error) {
      console.error("加载客户列表失败:", error);
    }
  }
  async function deleteCustomer(id: number) {
    const confirmed = window.confirm(`确定要删除这个客户吗？`);
    if (!confirmed) {
      return;
    }
    try {
      const response = await fetch(`/api/customers/${id}`, {
        method: "DELETE",
      });
      if (!response.ok) {
        throw new Error(`删除失败: ${response.status}`);
      }
      console.log("删除客户成功:", id);
      await loadCustomers();
    } catch (error) {
      console.error("删除客户失败:", error);
    }
  }

  function editCustomer(customer: Customer) {
    setEditingCustomerId(customer.id);
    setCustomerName(customer.name);
    setShowCreateCustomer(true);
  }

  useEffect(() => {
    loadCustomers();
  }, []);

  return (
    <div className="content">
      <div className="title-row">
        <h2>客户管理</h2>

        <button
          className="primary-button"
          onClick={() => {
            setEditingCustomerId(null);
            setCustomerName("");
            setShowCreateCustomer(true);
          }}
        >
          + 新增客户
        </button>
      </div>

      {showCreateCustomer && (
        <div className="form-box">
          <h3>{editingCustomerId !== null ? "编辑客户" : "新增客户"}</h3>

          <div className="form-row">
            <label>客户名称：</label>

            <input
              placeholder="请输入客户名称"
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
            />
          </div>

          <div className="form-buttons">
            <button className="primary-button" onClick={saveCustomer}>
              {editingCustomerId !== null ? "保存修改" : "保存"}
            </button>

            <button
              className="normal-button"
              onClick={() => {
                setShowCreateCustomer(false);
                setEditingCustomerId(null);
                setCustomerName("");
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}

      <div className="customer-list">
        <div className="table-header">
          <span>客户名称</span>
          <span>操作</span>
        </div>

        {customers.length === 0 ? (
          <div className="empty">暂无客户</div>
        ) : (
          customers.map((customer) => (
            <div className="customer-row" key={customer.id}>
              <span>{customer.name}</span>

              <div>
                <button
                  className="edit-button"
                  onClick={() => editCustomer(customer)}
                >
                  编辑
                </button>

                <button
                  className="delete-button"
                  onClick={() => deleteCustomer(customer.id)}
                >
                  删除
                </button>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

export default CustomerPage;
