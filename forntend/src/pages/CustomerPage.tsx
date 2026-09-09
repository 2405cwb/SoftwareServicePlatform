import { useEffect, useState } from "react";
import StatusBadge from "../components/StatusBadge";
import ConfirmDeleteButton from "../components/ConfirmDeleteButton";
import PageHeader from "../components/PageHeader";
import SearchBar from "../components/SearchBar";

interface Customer {
  id: number;

  name: string;
  code: string;

  customerType: string;
  industry: string;

  province: string;
  city: string;
  address: string;

  contactName: string;
  contactPhone: string;
  contactEmail: string;

  salesOwner: string;
  supportOwner: string;

  isEnabled: boolean;

  remark: string;

  createdAt: string;
  updatedAt: string;
}

/**
 * 软件基本信息
 *
 * 客户软件管理页面只需要这些字段。
 */
interface Software {
  id: number;
  name: string;
  code: string;
  isEnabled: boolean;
  allowDownload: boolean;
}

/**
 * 客户与软件的绑定关系
 *
 * 对应后端：
 * GET /api/customersoftwares?customerId=xxx
 */
interface CustomerSoftwareBinding {
  id: number;
  customerId: number;
  softwareId: number;
  isEnabled: boolean;
  boundAt: string;
  remark: string;

  softwareName: string;
  softwareCode: string;
  softwareEnabled: boolean;
  softwareAllowDownload: boolean;
}

function CustomerPage() {
  const [showCreateCustomer, setShowCreateCustomer] = useState(false);
  const [searchKeyword, setSearchKeyword] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [customerCode, setCustomerCode] = useState("");

  const [customerType, setCustomerType] = useState("");
  const [industry, setIndustry] = useState("");

  const [province, setProvince] = useState("");
  const [city, setCity] = useState("");
  const [address, setAddress] = useState("");

  const [contactName, setContactName] = useState("");
  const [contactPhone, setContactPhone] = useState("");
  const [contactEmail, setContactEmail] = useState("");

  const [salesOwner, setSalesOwner] = useState("");
  const [supportOwner, setSupportOwner] = useState("");

  const [isEnabled, setIsEnabled] = useState(true);

  const [remark, setRemark] = useState("");

  const [customers, setCustomers] = useState<Customer[]>([]);

  const [editingCustomerId, setEditingCustomerId] = useState<number | null>(
    null,
  );

  /**
   * 当前正在管理软件的客户
   *
   * null：
   * 当前没有打开“管理软件”
   */
  const [softwareManageCustomer, setSoftwareManageCustomer] =
    useState<Customer | null>(null);

  /**
   * 系统中的全部软件
   */
  const [softwareList, setSoftwareList] = useState<Software[]>([]);

  /**
   * 当前客户已有的绑定记录
   */
  const [customerSoftwareBindings, setCustomerSoftwareBindings] = useState<
    CustomerSoftwareBinding[]
  >([]);

  /**
   * 当前界面中被勾选的软件ID
   *
   * 例如：
   *
   * [1, 3, 5]
   *
   * 代表当前客户选择了：
   * 软件1、软件3、软件5
   */
  const [selectedSoftwareIds, setSelectedSoftwareIds] = useState<number[]>([]);

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
          body: JSON.stringify({
            name: customerName,
            code: customerCode,

            customerType: customerType,
            industry: industry,

            province: province,
            city: city,
            address: address,

            contactName: contactName,
            contactPhone: contactPhone,
            contactEmail: contactEmail,

            salesOwner: salesOwner,
            supportOwner: supportOwner,

            isEnabled: isEnabled,

            remark: remark,
          }),
        });
      } else {
        response = await fetch(`/api/customers/${editingCustomerId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            id: editingCustomerId,

            name: customerName,
            code: customerCode,

            customerType: customerType,
            industry: industry,

            province: province,
            city: city,
            address: address,

            contactName: contactName,
            contactPhone: contactPhone,
            contactEmail: contactEmail,

            salesOwner: salesOwner,
            supportOwner: supportOwner,

            isEnabled: isEnabled,

            remark: remark,
          }),
        });
      }

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      clearCustomerForm();
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
    setCustomerCode(customer.code);

    setCustomerType(customer.customerType);
    setIndustry(customer.industry);

    setProvince(customer.province);
    setCity(customer.city);
    setAddress(customer.address);

    setContactName(customer.contactName);
    setContactPhone(customer.contactPhone);
    setContactEmail(customer.contactEmail);

    setSalesOwner(customer.salesOwner);
    setSupportOwner(customer.supportOwner);

    setIsEnabled(customer.isEnabled);

    setRemark(customer.remark);

    setShowCreateCustomer(true);
  }

  useEffect(() => {
    loadCustomers();
  }, []);

  function clearCustomerForm() {
    setCustomerName("");
    setCustomerCode("");

    setCustomerType("");
    setIndustry("");

    setProvince("");
    setCity("");
    setAddress("");

    setContactName("");
    setContactPhone("");
    setContactEmail("");

    setSalesOwner("");
    setSupportOwner("");

    setIsEnabled(true);

    setRemark("");
  }
  const filteredCustomers = customers.filter((customer) => {
    const keyword = searchKeyword.trim().toLowerCase();

    if (keyword === "") {
      return true;
    }

    return (
      customer.name.toLowerCase().includes(keyword) ||
      customer.code.toLowerCase().includes(keyword) ||
      customer.contactName.toLowerCase().includes(keyword) ||
      customer.contactPhone.toLowerCase().includes(keyword)
    );
  });
  /**
   * 打开某个客户的软件管理界面
   */
  async function manageCustomerSoftware(customer: Customer) {
    try {
      /*
       * 记录现在正在管理哪个客户。
       */
      setSoftwareManageCustomer(customer);

      /*
       * 1. 查询系统全部软件
       */
      const softwareResponse = await fetch("/api/softwares");

      if (!softwareResponse.ok) {
        throw new Error(`获取软件列表失败：${softwareResponse.status}`);
      }

      const softwares: Software[] = await softwareResponse.json();

      setSoftwareList(softwares);

      /*
       * 2. 查询当前客户已经绑定的软件
       */
      const bindingResponse = await fetch(
        `/api/customersoftwares?customerId=${customer.id}`,
      );

      if (!bindingResponse.ok) {
        throw new Error(`获取客户软件失败：${bindingResponse.status}`);
      }

      const bindings: CustomerSoftwareBinding[] = await bindingResponse.json();

      setCustomerSoftwareBindings(bindings);

      /*
       * 3. 找到当前仍然有效的绑定。
       *
       * 例如：
       *
       * 软件1 true
       * 软件2 false
       * 软件3 true
       *
       * 最终得到：
       *
       * [1, 3]
       */
      const enabledSoftwareIds = bindings
        .filter((binding) => binding.isEnabled)
        .map((binding) => binding.softwareId);

      setSelectedSoftwareIds(enabledSoftwareIds);
    } catch (error) {
      console.error("加载客户软件信息失败：", error);

      alert("加载客户软件信息失败：" + String(error));
    }
  } /**
   * 修改某个软件的勾选状态
   */
  function toggleSoftware(softwareId: number, checked: boolean) {
    if (checked) {
      /*
       * 勾选：
       * 把 SoftwareId 加进去。
       */
      setSelectedSoftwareIds((currentIds) => [...currentIds, softwareId]);
    } else {
      /*
       * 取消勾选：
       * 把这个 SoftwareId 过滤掉。
       */
      setSelectedSoftwareIds((currentIds) =>
        currentIds.filter((id) => id !== softwareId),
      );
    }
  }
  /**
   * 保存客户的软件绑定关系
   */
  async function saveCustomerSoftwares() {
    if (softwareManageCustomer === null) {
      return;
    }

    try {
      /*
       * 遍历系统中的所有软件，
       * 判断每一个软件应该：
       *
       * 绑定
       * 取消绑定
       * 什么都不做
       */
      for (const software of softwareList) {
        /*
         * 当前界面是否勾选这个软件
         */
        const shouldEnabled = selectedSoftwareIds.includes(software.id);

        /*
         * 数据库中是否已经存在
         * 这个客户 + 这个软件的绑定记录
         */
        const existingBinding = customerSoftwareBindings.find(
          (binding) => binding.softwareId === software.id,
        );

        /*
         * ==================================
         * 情况1：
         *
         * 用户现在勾选了
         *
         * 但是数据库：
         * - 没绑定过
         * 或
         * - 以前绑定过但已经停用
         *
         * → POST绑定
         * ==================================
         */
        if (
          shouldEnabled &&
          (existingBinding === undefined || !existingBinding.isEnabled)
        ) {
          const response = await fetch("/api/customersoftwares", {
            method: "POST",

            headers: {
              "Content-Type": "application/json",
            },

            body: JSON.stringify({
              customerId: softwareManageCustomer.id,

              softwareId: software.id,

              remark: "",
            }),
          });

          if (!response.ok) {
            const errorText = await response.text();

            throw new Error(errorText || `绑定软件失败：${software.name}`);
          }
        }

        /*
         * ==================================
         * 情况2：
         *
         * 用户取消了勾选
         *
         * 并且数据库当前是启用状态
         *
         * → DELETE / 停用绑定
         * ==================================
         */
        if (
          !shouldEnabled &&
          existingBinding !== undefined &&
          existingBinding.isEnabled
        ) {
          const response = await fetch(
            `/api/customersoftwares/${existingBinding.id}`,
            {
              method: "DELETE",
            },
          );

          if (!response.ok) {
            const errorText = await response.text();

            throw new Error(errorText || `取消软件绑定失败：${software.name}`);
          }
        }
      }

      alert("客户软件保存成功");

      /*
       * 保存完成关闭管理区域。
       */
      setSoftwareManageCustomer(null);

      setSoftwareList([]);

      setCustomerSoftwareBindings([]);

      setSelectedSoftwareIds([]);
    } catch (error) {
      console.error("保存客户软件失败：", error);

      alert("保存客户软件失败：" + String(error));
    }
  }
  return (
    <div className="content">
      <PageHeader
        title="客户管理"
        buttonText="+ 新增客户"
        onButtonClick={() => {
          setEditingCustomerId(null);
          clearCustomerForm();
          setShowCreateCustomer(true);
        }}
      />
      {/* =========================
    客户软件管理
    ========================= */}
      {softwareManageCustomer !== null && (
        <div className="form-box">
          <h3>管理客户软件</h3>

          <div className="form-section">
            <h4>
              当前客户：
              {softwareManageCustomer.name}
            </h4>

            <div className="software-binding-list">
              {softwareList.map((software) => (
                <label className="software-binding-item" key={software.id}>
                  <input
                    type="checkbox"
                    checked={selectedSoftwareIds.includes(software.id)}
                    onChange={(e) =>
                      toggleSoftware(software.id, e.target.checked)
                    }
                  />

                  <div>
                    <div>
                      <strong>{software.name}</strong>{" "}
                      <span>({software.code})</span>
                    </div>

                    <div className="software-binding-status">
                      软件状态：
                      {software.isEnabled ? "启用" : "停用"}
                      {" ｜ "}
                      下载：
                      {software.allowDownload ? "允许" : "禁止"}
                    </div>
                  </div>
                </label>
              ))}
            </div>
          </div>

          <div className="form-buttons">
            <button className="primary-button" onClick={saveCustomerSoftwares}>
              保存
            </button>

            <button
              className="normal-button"
              onClick={() => {
                setSoftwareManageCustomer(null);

                setSoftwareList([]);

                setCustomerSoftwareBindings([]);

                setSelectedSoftwareIds([]);
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}

      <SearchBar
        value={searchKeyword}
        placeholder="搜索客户名称、编码、联系人或电话"
        onChange={setSearchKeyword}
        onClear={() => setSearchKeyword("")}
      />

      {showCreateCustomer && (
        <div className="form-box">
          <h3>{editingCustomerId !== null ? "编辑客户" : "新增客户"}</h3>

          {/* 基本信息 */}
          <div className="form-section">
            <h4>基本信息</h4>

            <div className="form-grid">
              <div className="form-item">
                <label>客户编码：</label>
                <input
                  placeholder="例如：WH001"
                  value={customerCode}
                  onChange={(e) => setCustomerCode(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>客户名称：</label>
                <input
                  placeholder="请输入客户名称"
                  value={customerName}
                  onChange={(e) => setCustomerName(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>客户类型：</label>
                <input
                  placeholder="例如：企业"
                  value={customerType}
                  onChange={(e) => setCustomerType(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>所属行业：</label>
                <input
                  placeholder="例如：公路检测"
                  value={industry}
                  onChange={(e) => setIndustry(e.target.value)}
                />
              </div>
            </div>
          </div>

          {/* 联系信息 */}
          <div className="form-section">
            <h4>联系信息</h4>

            <div className="form-grid">
              <div className="form-item">
                <label>省份：</label>
                <input
                  placeholder="例如：湖北省"
                  value={province}
                  onChange={(e) => setProvince(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>城市：</label>
                <input
                  placeholder="例如：武汉市"
                  value={city}
                  onChange={(e) => setCity(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>联系人：</label>
                <input
                  placeholder="请输入联系人"
                  value={contactName}
                  onChange={(e) => setContactName(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>联系电话：</label>
                <input
                  placeholder="请输入联系电话"
                  value={contactPhone}
                  onChange={(e) => setContactPhone(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>联系邮箱：</label>
                <input
                  placeholder="请输入邮箱"
                  value={contactEmail}
                  onChange={(e) => setContactEmail(e.target.value)}
                />
              </div>
            </div>

            <div className="form-item form-item-full">
              <label>详细地址：</label>
              <input
                placeholder="请输入详细地址"
                value={address}
                onChange={(e) => setAddress(e.target.value)}
              />
            </div>
          </div>

          {/* 负责人 */}
          <div className="form-section">
            <h4>负责人</h4>

            <div className="form-grid">
              <div className="form-item">
                <label>商务负责人：</label>
                <input
                  placeholder="请输入商务负责人"
                  value={salesOwner}
                  onChange={(e) => setSalesOwner(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>售后负责人：</label>
                <input
                  placeholder="请输入售后负责人"
                  value={supportOwner}
                  onChange={(e) => setSupportOwner(e.target.value)}
                />
              </div>
            </div>
          </div>

          {/* 状态 */}
          <div className="form-section">
            <h4>状态</h4>

            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={isEnabled}
                onChange={(e) => setIsEnabled(e.target.checked)}
              />
              客户启用
            </label>
          </div>

          {/* 备注 */}
          <div className="form-section">
            <h4>备注</h4>

            <textarea
              className="remark-input"
              placeholder="请输入客户备注"
              value={remark}
              onChange={(e) => setRemark(e.target.value)}
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
                clearCustomerForm();
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}
      <div className="customer-table">
        <div className="customer-table-header">
          <div>客户编码</div>
          <div>客户名称</div>
          <div>客户类型</div>
          <div>地区</div>
          <div>联系人</div>
          <div>联系电话</div>
          <div>售后负责人</div>
          <div>状态</div>
          <div>操作</div>
        </div>

        {filteredCustomers.length === 0 ? (
          <div className="empty">暂无客户</div>
        ) : (
          filteredCustomers.map((customer) => (
            <div className="customer-table-row" key={customer.id}>
              <div>{customer.code || "-"}</div>

              <div>{customer.name}</div>

              <div>{customer.customerType || "-"}</div>

              <div>
                {customer.province || customer.city
                  ? `${customer.province}${customer.city}`
                  : "-"}
              </div>

              <div>{customer.contactName || "-"}</div>

              <div>{customer.contactPhone || "-"}</div>

              <div>{customer.supportOwner || "-"}</div>

              <div>
                <StatusBadge enabled={customer.isEnabled} />
              </div>

              <div className="table-actions">
                <button
                  className="edit-button"
                  onClick={() => editCustomer(customer)}
                >
                  编辑
                </button>
                <button
                  className="normal-button"
                  onClick={() => manageCustomerSoftware(customer)}
                >
                  管理软件
                </button>
                <ConfirmDeleteButton
                  message="确定要删除这个客户吗？"
                  onConfirm={() => deleteCustomer(customer.id)}
                />
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

export default CustomerPage;
