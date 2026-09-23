import { useEffect, useMemo, useState } from "react";
import {
  Boxes,
  Building2,
  Monitor,
  Pencil,
  RefreshCw,
  Search,
  Settings2,
  ShieldCheck,
  ShieldOff,
} from "lucide-react";
import { apiFetch } from "../services/api";

interface DeviceBindingSummary {
  customerSoftwareId: number;
  customerId: number;
  customerName: string;
  customerCode: string;
  customerEnabled: boolean;
  softwareId: number;
  softwareName: string;
  softwareCode: string;
  softwareEnabled: boolean;
  bindingEnabled: boolean;
  maxDeviceCount: number;
  deviceCount: number;
  enabledDeviceCount: number;
  disabledDeviceCount: number;
  lastUsedAt: string | null;
}

interface DeviceItem {
  id: number;
  installationId: string;
  deviceName: string;
  remark: string;
  tokenPrefix: string;
  isEnabled: boolean;
  activatedAt: string;
  lastUsedAt: string | null;
  updatedAt: string;
}

interface DeviceDetailResult {
  customerSoftwareId: number;
  customerId: number;
  customerName: string;
  customerCode: string;
  softwareId: number;
  softwareName: string;
  softwareCode: string;
  bindingEnabled: boolean;
  maxDeviceCount: number;
  enabledDeviceCount: number;
  devices: DeviceItem[];
}

function ClientDeviceAdminPage() {
  const [items, setItems] = useState<DeviceBindingSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");
  const [keyword, setKeyword] = useState("");

  const [detail, setDetail] = useState<DeviceDetailResult | null>(null);
  const [deviceLoading, setDeviceLoading] = useState(false);
  const [savingDeviceId, setSavingDeviceId] = useState<number | null>(null);
  const [savingLimitId, setSavingLimitId] = useState<number | null>(null);

  async function loadBindings() {
    try {
      setLoading(true);
      setErrorMessage("");

      const response = await apiFetch("/api/client-device-admin/bindings");

      if (!response.ok) {
        throw new Error(
          (await response.text()) || `加载更新设备失败：${response.status}`,
        );
      }

      setItems((await response.json()) as DeviceBindingSummary[]);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "加载更新设备授权失败",
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadBindings();
  }, []);

  const filteredItems = useMemo(() => {
    const value = keyword.trim().toLowerCase();

    if (!value) {
      return items;
    }

    return items.filter(
      (item) =>
        item.customerName.toLowerCase().includes(value) ||
        item.customerCode.toLowerCase().includes(value) ||
        item.softwareName.toLowerCase().includes(value) ||
        item.softwareCode.toLowerCase().includes(value),
    );
  }, [items, keyword]);

  async function openDevices(item: DeviceBindingSummary) {
    try {
      setDeviceLoading(true);

      const response = await apiFetch(
        `/api/client-device-admin/bindings/${item.customerSoftwareId}/devices`,
      );

      if (!response.ok) {
        throw new Error(
          (await response.text()) || `加载设备列表失败：${response.status}`,
        );
      }

      setDetail((await response.json()) as DeviceDetailResult);
    } catch (error) {
      alert(error instanceof Error ? error.message : "加载设备列表失败");
    } finally {
      setDeviceLoading(false);
    }
  }

  async function refreshDetail(customerSoftwareId: number) {
    const response = await apiFetch(
      `/api/client-device-admin/bindings/${customerSoftwareId}/devices`,
    );

    if (!response.ok) {
      throw new Error((await response.text()) || "刷新设备列表失败");
    }

    setDetail((await response.json()) as DeviceDetailResult);
  }

  async function setDeviceEnabled(device: DeviceItem, enabled: boolean) {
    if (!detail) {
      return;
    }

    const actionName = enabled ? "重新启用更新" : "停用更新";

    if (
      !window.confirm(
        `确定对设备“${device.deviceName || "未命名设备"}”执行“${actionName}”吗？\n\n` +
          "只影响这台设备的在线自动更新，不影响业务软件正常使用。",
      )
    ) {
      return;
    }

    try {
      setSavingDeviceId(device.id);

      const action = enabled ? "enable" : "revoke";

      const response = await apiFetch(
        `/api/client-device-admin/devices/${device.id}/${action}`,
        { method: "POST" },
      );

      if (!response.ok) {
        throw new Error((await response.text()) || "修改设备状态失败");
      }

      await refreshDetail(detail.customerSoftwareId);
      await loadBindings();
    } catch (error) {
      alert(error instanceof Error ? error.message : "修改设备状态失败");
    } finally {
      setSavingDeviceId(null);
    }
  }

  async function editDevice(device: DeviceItem) {
    const name = window.prompt("设备显示名称：", device.deviceName);

    if (name === null) {
      return;
    }

    const remark = window.prompt("设备备注：", device.remark || "");

    if (remark === null) {
      return;
    }

    try {
      setSavingDeviceId(device.id);

      const response = await apiFetch(
        `/api/client-device-admin/devices/${device.id}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            deviceName: name,
            remark,
          }),
        },
      );

      if (!response.ok) {
        throw new Error((await response.text()) || "修改设备信息失败");
      }

      if (detail) {
        await refreshDetail(detail.customerSoftwareId);
      }
    } catch (error) {
      alert(error instanceof Error ? error.message : "修改设备信息失败");
    } finally {
      setSavingDeviceId(null);
    }
  }

  async function setDeviceLimit(item: DeviceBindingSummary) {
    const current =
      item.maxDeviceCount === 0 ? "0" : String(item.maxDeviceCount);

    const input = window.prompt(
      "请输入最大启用设备数。\n0 表示不限制。\n降低上限不会自动停用现有设备。",
      current,
    );

    if (input === null) {
      return;
    }

    const maxDeviceCount = Number(input.trim());

    if (
      !Number.isInteger(maxDeviceCount) ||
      maxDeviceCount < 0 ||
      maxDeviceCount > 9999
    ) {
      alert("请输入 0～9999 的整数");
      return;
    }

    try {
      setSavingLimitId(item.customerSoftwareId);

      const response = await apiFetch(
        `/api/client-device-admin/bindings/${item.customerSoftwareId}/device-limit`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ maxDeviceCount }),
        },
      );

      if (!response.ok) {
        throw new Error((await response.text()) || "设置设备上限失败");
      }

      await loadBindings();

      if (detail?.customerSoftwareId === item.customerSoftwareId) {
        await refreshDetail(item.customerSoftwareId);
      }
    } catch (error) {
      alert(error instanceof Error ? error.message : "设置设备上限失败");
    } finally {
      setSavingLimitId(null);
    }
  }

  function formatDate(value: string | null) {
    return value ? new Date(value).toLocaleString("zh-CN") : "-";
  }

  return (
    <div className="content u-page">
      <header className="u-page-header">
        <div>
          <span className="u-eyebrow">DEVICE UPDATE AUTHORIZATION</span>
          <h2>更新设备授权</h2>
          <p>
            查看每个客户的软件更新设备，设置设备数量上限，并可单独停用、启用、改名和备注。
          </p>
        </div>

        <button
          type="button"
          className="u-secondary-button"
          onClick={() => void loadBindings()}
        >
          <RefreshCw size={16} />
          刷新
        </button>
      </header>

      <section className="u-panel">
        <div
          style={{
            display: "flex",
            gap: 12,
            alignItems: "center",
            marginBottom: 18,
          }}
        >
          <Search size={17} />

          <input
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
            placeholder="搜索客户名称、客户编码、软件名称或软件编码"
            style={{
              flex: 1,
              minWidth: 220,
              height: 38,
              border: "1px solid #d7dde8",
              borderRadius: 8,
              padding: "0 12px",
            }}
          />
        </div>

        {loading ? (
          <div className="u-loading-card">正在加载更新设备授权...</div>
        ) : errorMessage ? (
          <div className="u-error-card">{errorMessage}</div>
        ) : filteredItems.length === 0 ? (
          <div className="u-empty-state">暂无符合条件的客户软件授权。</div>
        ) : (
          <div style={{ display: "grid", gap: 12 }}>
            {filteredItems.map((item) => (
              <div
                key={item.customerSoftwareId}
                style={{
                  border: "1px solid #e2e8f0",
                  borderRadius: 12,
                  padding: 16,
                  display: "grid",
                  gridTemplateColumns:
                    "minmax(180px,1.1fr) minmax(180px,1.1fr) minmax(240px,1.2fr) auto",
                  gap: 18,
                  alignItems: "center",
                }}
              >
                <div>
                  <strong
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                    }}
                  >
                    <Building2 size={16} />
                    {item.customerName}
                  </strong>
                  <small>{item.customerCode}</small>
                </div>

                <div>
                  <strong
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                    }}
                  >
                    <Boxes size={16} />
                    {item.softwareName}
                  </strong>
                  <small>{item.softwareCode}</small>
                </div>

                <div style={{ fontSize: 13, lineHeight: 1.8 }}>
                  <div>
                    已授权：{item.deviceCount} 台 · 正常{" "}
                    {item.enabledDeviceCount} · 已停用 {item.disabledDeviceCount}
                  </div>
                  <div>
                    设备上限：
                    {item.maxDeviceCount === 0
                      ? "不限制"
                      : `${item.maxDeviceCount} 台`}
                  </div>
                  <div>最近检查：{formatDate(item.lastUsedAt)}</div>
                </div>

                <div style={{ display: "flex", gap: 8 }}>
                  <button
                    type="button"
                    className="normal-button"
                    disabled={savingLimitId === item.customerSoftwareId}
                    onClick={() => void setDeviceLimit(item)}
                  >
                    <Settings2 size={15} />
                    设置上限
                  </button>

                  <button
                    type="button"
                    className="normal-button"
                    onClick={() => void openDevices(item)}
                  >
                    <Monitor size={15} />
                    查看设备
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {detail && (
        <div className="version-attachment-mask">
          <div
            className="version-attachment-dialog"
            style={{ maxWidth: 960, width: "94vw" }}
          >
            <div className="version-attachment-header">
              <div>
                <h3>更新设备管理</h3>
                <p>
                  {detail.customerName} · {detail.softwareName} ·{" "}
                  {detail.softwareCode}
                </p>
                <small>
                  当前启用 {detail.enabledDeviceCount} 台 · 上限{" "}
                  {detail.maxDeviceCount === 0
                    ? "不限制"
                    : `${detail.maxDeviceCount} 台`}
                </small>
              </div>

              <button
                type="button"
                className="normal-button"
                onClick={() => setDetail(null)}
              >
                关闭
              </button>
            </div>

            {deviceLoading ? (
              <div className="u-loading-card">正在加载设备...</div>
            ) : detail.devices.length === 0 ? (
              <div className="u-empty-state">暂无已授权更新设备。</div>
            ) : (
              <div style={{ display: "grid", gap: 12, marginTop: 16 }}>
                {detail.devices.map((device) => (
                  <div
                    key={device.id}
                    style={{
                      border: "1px solid #e2e8f0",
                      borderRadius: 10,
                      padding: 14,
                      display: "grid",
                      gridTemplateColumns:
                        "minmax(180px,1fr) minmax(300px,1.6fr) auto",
                      gap: 18,
                      alignItems: "center",
                    }}
                  >
                    <div>
                      <strong>{device.deviceName || "未命名设备"}</strong>
                      <div style={{ fontSize: 13, marginTop: 5 }}>
                        {device.remark || "暂无备注"}
                      </div>
                      <div
                        style={{
                          fontSize: 12,
                          color: "#64748b",
                          marginTop: 5,
                        }}
                      >
                        Token：{device.tokenPrefix || "-"}
                      </div>
                    </div>

                    <div
                      style={{
                        color: "#64748b",
                        fontSize: 13,
                        lineHeight: 1.8,
                      }}
                    >
                      <div>安装实例：{device.installationId}</div>
                      <div>授权时间：{formatDate(device.activatedAt)}</div>
                      <div>最近检查：{formatDate(device.lastUsedAt)}</div>
                    </div>

                    <div
                      style={{
                        display: "grid",
                        gap: 8,
                        justifyItems: "end",
                      }}
                    >
                      <strong>
                        {device.isEnabled ? (
                          <>
                            <ShieldCheck size={15} /> 正常
                          </>
                        ) : (
                          <>
                            <ShieldOff size={15} /> 已停用
                          </>
                        )}
                      </strong>

                      <div style={{ display: "flex", gap: 8 }}>
                        <button
                          type="button"
                          className="normal-button"
                          disabled={savingDeviceId === device.id}
                          onClick={() => void editDevice(device)}
                        >
                          <Pencil size={14} />
                          编辑
                        </button>

                        <button
                          type="button"
                          className={
                            device.isEnabled ? "delete-button" : "normal-button"
                          }
                          disabled={savingDeviceId === device.id}
                          onClick={() =>
                            void setDeviceEnabled(device, !device.isEnabled)
                          }
                        >
                          {savingDeviceId === device.id
                            ? "处理中..."
                            : device.isEnabled
                              ? "停用更新"
                              : "重新启用"}
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default ClientDeviceAdminPage;
